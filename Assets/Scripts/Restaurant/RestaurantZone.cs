using Marea.UI;
using UnityEngine;

namespace Marea.Restaurant
{
    [RequireComponent(typeof(Collider))]
    public class RestaurantZone : MonoBehaviour
    {
        [SerializeField] private BusinessUIController businessUIController;

        private void Awake()
        {
            // 트리거 콜라이더 확인
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            if (businessUIController == null)
            {
                businessUIController = FindFirstObjectByType<BusinessUIController>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 플레이어 컴포넌트 또는 태그 확인
            if (other.GetComponent<PlayerServingController>() != null || other.CompareTag("Player"))
            {
                if (businessUIController != null)
                {
                    businessUIController.SetPlayerInZone(true);

                    Debug.Log($"레스토랑 입장");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // 플레이어가 영역 밖으로 나갈 때 처리
            if (other.GetComponent<PlayerServingController>() != null || other.CompareTag("Player"))
            {
                if (businessUIController != null)
                {
                    businessUIController.SetPlayerInZone(false);

                    Debug.Log($"레스토랑 탈출");
                }
            }
        }
    }
}
