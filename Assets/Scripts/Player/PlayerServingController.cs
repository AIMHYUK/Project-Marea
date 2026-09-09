using Marea.Cooking;
using UnityEngine;

namespace Marea.Restaurant
{
    public class PlayerServingController : MonoBehaviour
    {
        [Header("시각 연출")]
        [SerializeField] private GameObject heldFoodVisual; // 플레이어 손에 붙인 꼬치 3D 오브젝트

        private bool _isHoldingFood;
        private CookingResult _lastCookingResult;

        public bool IsHoldingFood => _isHoldingFood;
        public CookingResult LastCookingResult => _lastCookingResult;

        private void Awake()
        {
            SetFoodVisual(false);
        }

        // 미니게임 완료 시 호출
        public void PickUpFood(CookingResult result)
        {
            _isHoldingFood = true;
            _lastCookingResult = result;
            SetFoodVisual(true);
            Debug.Log($"[PlayerServingController] PickUpFood 호출됨! 들고 있는 상태: {_isHoldingFood}, Visual 유효 여부: {heldFoodVisual != null}");
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
            else
            {
                Debug.LogError("[PlayerServingController] heldFoodVisual 슬롯이 비어있습니다! 인스펙터를 확인하세요.");
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
