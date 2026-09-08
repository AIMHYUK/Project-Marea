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
