using System.Collections.Generic;
using Marea.Cooking;
using Marea.Restaurant;
using UnityEngine;
using Marea.Core;

namespace Marea.Restaurant
{
    /// <summary>
    /// (+10/9, A) 음식을 집는 건 근접 E 경로(ProximityInteractor)로 — 가까이 가면 "[E] 집기"가 뜬다.
    /// 예전엔 여기서 트리거 + E를 직접 읽어 표시가 없었다. 클릭은 안 받는다.
    /// </summary>
    public class CookingCounter : InteractableBase
    {
        [Header("음식 거치 슬롯 위치 (최대 5개)")]
        [SerializeField] private Transform[] slotPoints;

        [Header("상호작용 설정")]
        [SerializeField] private GameObject defaultFoodPrefab;

        private readonly Queue<CookingResult> _foodQueue = new();
        private readonly List<GameObject> _spawnedVisuals = new();

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

        public override bool AllowClick => false;

        public override string InteractLabel(IInteractor actor) => "집기";

        // 빈손 + 집을 음식(직원 몫 빼고)이 있을 때만 [E] 집기가 뜬다.
        public override bool CanInteract(IInteractor actor)
        {
            PlayerServingController serving = PlayerServingController.Of(actor);
            return serving != null && !serving.IsHoldingFood && HasFood && FindPlayerPickIndex() >= 0;
        }

        public override void Interact(IInteractor actor)
        {
            PlayerServingController serving = PlayerServingController.Of(actor);
            if (serving != null) TryGiveFoodToPlayer(serving);
        }

        private void TryGiveFoodToPlayer(PlayerServingController serving)
        {
            if (!HasFood) return;

            if (serving.IsHoldingFood)
            {
                Debug.LogWarning("[CookingCounter] 플레이어가 이미 음식을 들고 있어 수령할 수 없습니다.");
                return;
            }

            // (+10/7, A) 서빙 직원이 가져가기로 한 음식은 건너뛴다 — 플레이어가 집으면 손님이 두 번 받는다.
            int index = FindPlayerPickIndex();
            if (index < 0)
            {
                Debug.Log("[CookingCounter] 조리대의 음식은 모두 서빙 직원이 가져갈 몫이다.");
                return;
            }

            CookingResult food = RemoveAt(index, out GameObject removedVisual);
            if (removedVisual != null)
            {
                Destroy(removedVisual);
            }

            RearrangeVisuals();

            serving.PickUpFood(food);
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

            food = RemoveAt(index, out visual);
            RearrangeVisuals();
            return true;
        }

        /// <summary>(+10/7, A) index번째 음식을 큐 · 비주얼 목록에서 뺀다. 비주얼은 지우지 않고 돌려준다.</summary>
        private CookingResult RemoveAt(int index, out GameObject visual)
        {
            List<CookingResult> items = new(_foodQueue);
            CookingResult food = items[index];
            items.RemoveAt(index);
            _foodQueue.Clear();
            foreach (CookingResult r in items) _foodQueue.Enqueue(r);

            visual = null;
            if (index < _spawnedVisuals.Count)
            {
                visual = _spawnedVisuals[index];
                _spawnedVisuals.RemoveAt(index);
            }
            return food;
        }

        /// <summary>
        /// (+10/7, A) 플레이어가 집을 음식 — 앞에서부터, 직원 몫(게시판에 대기 중 · 직원이 가지러 가는 중인 아이콘)은
        /// 그 수만큼 건너뛴다. 다 직원 몫이면 -1. 직원이 없으면 직원 몫도 없어서 예전처럼 맨 앞.
        /// </summary>
        private int FindPlayerPickIndex()
        {
            Dictionary<Sprite, int> reserved = new();

            Marea.Core.ServeBoard board = FindAnyObjectByType<Marea.Core.ServeBoard>();
            if (board != null)
                foreach (Sprite s in board.PendingIcons) Reserve(reserved, s);
            foreach (Marea.Field.ServingStaff staff in FindObjectsByType<Marea.Field.ServingStaff>(FindObjectsSortMode.None))
                Reserve(reserved, staff.ReservedIcon);

            int i = 0;
            foreach (CookingResult r in _foodQueue)
            {
                Sprite icon = r.menuData != null ? r.menuData.Icon : null;
                if (icon != null && reserved.TryGetValue(icon, out int n) && n > 0) reserved[icon] = n - 1;
                else return i;
                i++;
            }
            return -1;
        }

        private static void Reserve(Dictionary<Sprite, int> reserved, Sprite icon)
        {
            if (icon == null) return;
            reserved[icon] = reserved.TryGetValue(icon, out int n) ? n + 1 : 1;
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
