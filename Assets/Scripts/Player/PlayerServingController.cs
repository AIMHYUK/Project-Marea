using UnityEngine;

namespace Marea.Restaurant
{
    public class PlayerServingController : MonoBehaviour
    {
        [Header("시각 연출")]
        [SerializeField] private GameObject heldFoodVisual; // 플레이어 손에 붙인 꼬치 3D 오브젝트

        private bool _isHoldingFood;

        public bool IsHoldingFood => _isHoldingFood;

        private void Awake()
        {
            SetFoodVisual(false);
        }

        // 미니게임 완료 시 호출
        public void PickUpFood()
        {
            _isHoldingFood = true;
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
    }
}