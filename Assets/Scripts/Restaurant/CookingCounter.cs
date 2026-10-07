using System.Collections.Generic;
using Marea.Cooking;
using Marea.Restaurant;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Restaurant
{
    public class CookingCounter : MonoBehaviour
    {
        [Header("음식 거치 슬롯 위치 (최대 5개)")]
        [SerializeField] private Transform[] slotPoints;

        [Header("상호작용 설정")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private GameObject defaultFoodPrefab;

        private readonly Queue<CookingResult> _foodQueue = new();
        private readonly List<GameObject> _spawnedVisuals = new();

        private bool _isPlayerInRange;
        private PlayerServingController _playerServing;

        public int MaxCapacity => slotPoints != null ? slotPoints.Length : 5;
        public int CurrentCount => _foodQueue.Count;
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

            int targetIndex = _foodQueue.Count;
            if (targetIndex >= slotPoints.Length) return false;

            Transform targetPoint = slotPoints[targetIndex];
            _foodQueue.Enqueue(result);

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

            CookingResult food = _foodQueue.Dequeue();

            if (_spawnedVisuals.Count > 0)
            {
                GameObject removedVisual = _spawnedVisuals[0];
                _spawnedVisuals.RemoveAt(0);
                if (removedVisual != null)
                {
                    Destroy(removedVisual);
                }
            }

            RearrangeVisuals();

            _playerServing.PickUpFood(food);
            Debug.Log($"[CookingCounter] 플레이어가 조리대에서 음식을 수령했습니다: {food.menuData?.DisplayName}");
        }

        /// <summary>
        /// (+10/7, A) 서빙 직원이 음식을 집어 간다. 메뉴 아이콘이 같은 음식을 앞에서부터 찾아 빼고,
        /// 그 비주얼은 지우지 않고 넘긴다(직원 손으로 옮겨 간다). 아이콘이 없거나 맞는 게 없으면 맨 앞 것.
        /// 조리대가 비었으면 false — 직원은 빈손으로라도 배달한다.
        /// </summary>
        public bool TryTakeFood(Sprite icon, out CookingResult food, out GameObject visual)
        {
            food = default;
            visual = null;
            if (!HasFood) return false;

            List<CookingResult> items = new(_foodQueue);
            int index = 0;
            if (icon != null)
            {
                int match = items.FindIndex(r => r.menuData != null && r.menuData.Icon == icon);
                if (match >= 0) index = match;
            }

            food = items[index];
            items.RemoveAt(index);
            _foodQueue.Clear();
            foreach (CookingResult r in items) _foodQueue.Enqueue(r);

            if (index < _spawnedVisuals.Count)
            {
                visual = _spawnedVisuals[index];
                _spawnedVisuals.RemoveAt(index);
            }
            RearrangeVisuals();
            return true;
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
