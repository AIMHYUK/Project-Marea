using System.Collections.Generic;
using Marea.Cooking;
using Marea.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Restaurant
{
    /// <summary>
    /// 완성된 음식이 놓이는 조리대.
    ///
    /// <b>음식의 실체는 여기 하나뿐이다 (+9/10).</b> 전에는 조리가 끝나면 여기에
    /// 한 접시를 올리면서 동시에 <c>ServeBoard.Post</c> 로 직원 배달도 걸었다 —
    /// 조리 한 번에 "조리대의 실물 하나 + 직원이 들고 가는 실체 없는 아이콘 하나"가
    /// 나와서, 같은 요리를 두 번 팔 수 있었다 (이슈 #33).
    /// 이제 직원도 플레이어와 같이 여기서 꺼내 간다.
    /// </summary>
    public class CookingCounter : MonoBehaviour
    {
        [Header("음식 거치 슬롯 위치 (최대 5개)")]
        [SerializeField] private Transform[] slotPoints;

        [Header("상호작용 설정")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private GameObject defaultFoodPrefab;

        // Queue가 아니라 List다 (+9/10). 직원은 "자기 손님이 주문한 메뉴"를 꺼내 가므로
        // 맨 앞이 아닌 접시를 집을 수 있어야 한다. Queue면 맨 앞만 빠져서,
        // 앞 접시를 받을 손님이 없으면 뒤에 맞는 접시가 있어도 서빙이 멈춘다.
        private readonly List<CookingResult> _foods = new();
        private readonly List<GameObject> _spawnedVisuals = new();

        private bool _isPlayerInRange;
        private PlayerServingController _playerServing;

        public int MaxCapacity => slotPoints != null ? slotPoints.Length : 5;
        public int CurrentCount => _foods.Count;
        public bool IsFull => CurrentCount >= MaxCapacity;
        public bool HasFood => CurrentCount > 0;

        public bool TryPlaceFood(CookingResult result)
        {
            if (IsFull)
            {
                Debug.LogWarning("[CookingCounter] 조리대 거치 공간이 가득 찼습니다.");
                return false;
            }

            if (slotPoints == null || slotPoints.Length == 0) return false;

            int targetIndex = _foods.Count;
            if (targetIndex >= slotPoints.Length) return false;

            Transform targetPoint = slotPoints[targetIndex];
            _foods.Add(result);

            GameObject prefabToSpawn = (result.menuData != null && result.menuData.ServingPrefab != null)
                ? result.menuData.ServingPrefab
                : defaultFoodPrefab;

            GameObject visualObj = null;
            if (prefabToSpawn != null && targetPoint != null)
            {
                // 부모를 지정하지 않고 월드에 독립 생성하여 부모의 비균등 스케일 상속 차단
                visualObj = Instantiate(prefabToSpawn, targetPoint.position, targetPoint.rotation);

                // 프리팹 원본 로컬 스케일을 그대로 월드 스케일로 적용
                visualObj.transform.localScale = prefabToSpawn.transform.localScale;

                visualObj.SetActive(true);

                // 거치된 프리팹의 콜라이더 비활성화
                Collider col = visualObj.GetComponentInChildren<Collider>();
                if (col != null) col.enabled = false;
            }
            _spawnedVisuals.Add(visualObj);

            Debug.Log($"[CookingCounter] 조리대에 음식 배치 완료 ({CurrentCount}/{MaxCapacity}): {result.menuData?.DisplayName}");
            return true;
        }

        /// <summary>
        /// 이 메뉴가 지금 몇 접시 있나. (+9/10)
        ///
        /// ServeBoard 가 배정을 정할 때 부른다 — "이 손님이 주문한 게 조리대에 있나".
        /// </summary>
        public int CountOf(MenuData menu)
        {
            if (menu == null) return 0;

            int n = 0;
            foreach (CookingResult food in _foods)
            {
                if (food.menuData == menu) n++;
            }

            return n;
        }

        /// <summary>
        /// 이 메뉴가 지금 놓여 있는 슬롯의 월드 좌표. (+9/10)
        ///
        /// 직원이 "그 접시 앞까지" 걸어가려고 ServeBoard 를 통해 묻는다.
        /// <b>이 좌표는 고정이 아니다</b> — 앞 접시가 빠지면 남은 접시가 앞 슬롯으로
        /// 당겨진다(<see cref="RearrangeVisuals"/>). 그래서 이건 "지금 기준 어림값"이고,
        /// 실제로 꺼내는 것은 도착해서 <see cref="TryTakeFood"/> 가 메뉴로 다시 찾는다.
        /// </summary>
        public bool TryGetSlotPosition(MenuData menu, out Vector3 position)
        {
            position = transform.position;
            if (menu == null || slotPoints == null) return false;

            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].menuData != menu) continue;
                if (i >= slotPoints.Length || slotPoints[i] == null) return false;

                position = slotPoints[i].position;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 이 메뉴 한 접시를 꺼낸다. (+9/10)
        ///
        /// 직원이 픽업 지점에 도착했을 때 ServeBoard 가 부른다. <b>이 함수가 실패하는 게
        /// 정상 경로다</b> — 직원이 걸어오는 동안 플레이어가 같은 접시를 집어갔을 수 있고,
        /// 그때 배정이 풀려 다시 배정된다.
        /// </summary>
        /// <param name="visual">거치돼 있던 실물 오브젝트. 파괴하지 않고 넘긴다 —
        /// 직원이 이걸 손에 달고 간다 (+9/10). 프리팹이 없던 접시면 null 이다.
        /// <b>받은 쪽이 파괴 책임을 진다.</b></param>
        /// <returns>꺼냈으면 true. 그 메뉴가 없으면 false.</returns>
        public bool TryTakeFood(MenuData menu, out CookingResult food, out GameObject visual)
        {
            food = default;
            visual = null;
            if (menu == null) return false;

            for (int i = 0; i < _foods.Count; i++)
            {
                if (_foods[i].menuData != menu) continue;

                food = _foods[i];
                visual = ReleaseAt(i);
                Debug.Log($"[CookingCounter] 직원이 조리대에서 음식을 수령했습니다: {menu.DisplayName} "
                        + $"({CurrentCount}/{MaxCapacity} 남음)");
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (!_isPlayerInRange || _playerServing == null) return;

            if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                TryGiveFoodToPlayer();
            }
        }

        private void TryGiveFoodToPlayer()
        {
            if (!HasFood) return;

            if (_playerServing.IsHoldingFood)
            {
                Debug.LogWarning("[CookingCounter] 플레이어가 이미 음식을 들고 있어 수령할 수 없습니다.");
                return;
            }

            // 플레이어는 맨 앞 접시를 집는다. 직원이 그 접시를 예약해 걸어오는 중일 수도
            // 있는데, 먼저 집은 쪽이 가져간다 — 직원은 도착해서 빈손을 확인하고 돌아간다.
            CookingResult food = _foods[0];
            RemoveAt(0);

            _playerServing.PickUpFood(food);
            Debug.Log($"[CookingCounter] 플레이어가 조리대에서 음식을 수령했습니다: {food.menuData?.DisplayName}");
        }

        /// <summary>한 접시를 목록·비주얼에서 같이 빼고 실물을 파괴한다.</summary>
        private void RemoveAt(int index)
        {
            GameObject released = ReleaseAt(index);
            if (released != null) Destroy(released);
        }

        /// <summary>
        /// 한 접시를 목록에서 빼고 실물을 <b>파괴하지 않고</b> 돌려준다. (+9/10)
        /// 남은 접시는 앞으로 당긴다.
        /// </summary>
        private GameObject ReleaseAt(int index)
        {
            _foods.RemoveAt(index);

            GameObject visual = null;
            if (index < _spawnedVisuals.Count)
            {
                visual = _spawnedVisuals[index];
                _spawnedVisuals.RemoveAt(index);
            }

            RearrangeVisuals();
            return visual;
        }

        private void RearrangeVisuals()
        {
            for (int i = 0; i < _spawnedVisuals.Count; i++)
            {
                if (_spawnedVisuals[i] != null && i < slotPoints.Length)
                {
                    // 부모 종속 없이 좌표와 회전값만 앞 슬롯으로 갱신
                    _spawnedVisuals[i].transform.position = slotPoints[i].position;
                    _spawnedVisuals[i].transform.rotation = slotPoints[i].rotation;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
            {
                _isPlayerInRange = true;
                _playerServing = other.GetComponentInParent<PlayerServingController>();
                if (_playerServing == null && other.transform.root != null)
                {
                    _playerServing = other.transform.root.GetComponentInChildren<PlayerServingController>();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
            {
                _isPlayerInRange = false;
                _playerServing = null;
            }
        }

        private void OnDestroy()
        {
            // 조리대 파괴 시 남아있는 독립 음식 비주얼 오브젝트 정리
            foreach (var visual in _spawnedVisuals)
            {
                if (visual != null)
                {
                    Destroy(visual);
                }
            }
            _spawnedVisuals.Clear();
        }
    }
}
