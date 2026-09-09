using Marea.Cooking;
using UnityEngine;

namespace Marea.Restaurant
{
    public class PlayerServingController : MonoBehaviour
    {
        [Header("시각 연출")]
        [SerializeField] private Transform holdPoint; // 음식을 쥐어줄 플레이어 손 위치 트랜스폼
        [SerializeField] private GameObject defaultHandFoodPrefab; // 메뉴 전용 프리팹이 없을 때 쓸 기본 비주얼 프리팹
        [SerializeField] private GameObject heldFoodVisual; // 기존 고정 꼬치 오브젝트 (하위 호환 유지)

        private bool _isHoldingFood;
        private CookingResult _lastCookingResult;
        private GameObject _currentHoldingVisual; // 실시간 생성된 음식 오브젝트 인스턴스

        public bool IsHoldingFood => _isHoldingFood;
        public CookingResult LastCookingResult => _lastCookingResult;

        private void Awake()
        {
            SetFoodVisual(false);
        }

        // 미니게임 완료 시 호출
        public void PickUpFood(CookingResult result)
        {
            _lastCookingResult = result;
            _isHoldingFood = true;

            // 기존에 들려있던 오브젝트가 있다면 제거
            if (_currentHoldingVisual != null)
            {
                Destroy(_currentHoldingVisual);
            }

            // 메뉴 전용 프리팹이 있으면 손에 생성
            GameObject prefabToSpawn = (result.menuData != null && result.menuData.ServingPrefab != null)
                ? result.menuData.ServingPrefab
                : defaultHandFoodPrefab;

            if (prefabToSpawn != null && holdPoint != null)
            {
                _currentHoldingVisual = Instantiate(prefabToSpawn, holdPoint.position, holdPoint.rotation, holdPoint);
                _currentHoldingVisual.SetActive(true);

                // 손에 든 오브젝트의 콜라이더 비활성화
                Collider col = _currentHoldingVisual.GetComponentInChildren<Collider>();
                if (col != null) col.enabled = false;
            }
            else
            {
                // holdPoint가 없는 경우 기존 정적 비주얼 켜기
                SetFoodVisual(true);
            }

            Debug.Log($"[PlayerServing] 음식을 들었습니다: {result.menuData?.DisplayName}");
        }

        // 손님에게 서빙 시도 (주문 일치 검증 및 결과와 무관하게 손 비우기)
        public bool TryServeToCustomer(CustomerController targetCustomer)
        {
            if (!_isHoldingFood || targetCustomer == null) return false;

            CookingResult food = _lastCookingResult;

            // 서빙을 시도하면 성공하든 실패(오배송)하든 플레이어 손의 음식은 소모/폐기
            ClearHeldFood();

            bool isMatched = targetCustomer.TryReceiveFood(food);
            if (!isMatched)
            {
                Debug.LogWarning("[PlayerServingController] 주문과 다른 요리이므로 음식이 폐기되었습니다.");
            }

            return isMatched;
        }

        // 손님에게 서빙 완료 시 호출
        public bool DeliverFood()
        {
            if (!_isHoldingFood) return false;

            _isHoldingFood = false;
            SetFoodVisual(false);
            Debug.Log("[PlayerServingController] 손님에게 요리를 전달했습니다.");
            return true;
        }

        private void SetFoodVisual(bool active)
        {
            if (heldFoodVisual != null)
            {
                heldFoodVisual.SetActive(active);
            }

            if (!active && _currentHoldingVisual != null)
            {
                Destroy(_currentHoldingVisual);
                _currentHoldingVisual = null;
            }
        }

        // 플레이어가 들고 있는 음식을 버리기
        public void ClearHeldFood()
        {
            _isHoldingFood = false;
            _lastCookingResult = default;
            SetFoodVisual(false);
            Debug.Log("[PlayerServingController] 들고 있던 음식이 초기화(폐기)되었습니다.");
        }
    }
}
