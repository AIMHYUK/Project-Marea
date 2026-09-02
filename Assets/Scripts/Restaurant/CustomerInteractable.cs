using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Restaurant
{
    [RequireComponent(typeof(CustomerController))]
    public class CustomerInteractable : MonoBehaviour
    {
        [Header("상호작용 키")]
        [SerializeField] private Key interactKey = Key.E;

        private CustomerController _customer;
        private PlayerServingController _playerServing;
        private bool _isPlayerInRange;

        private void Awake()
        {
            _customer = GetComponent<CustomerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[CustomerInteractable] 충돌 감지된 오브젝트: {other.name}");

            if (other.CompareTag("Player") || (other.transform.root != null && other.transform.root.CompareTag("Player")))
            {
                _isPlayerInRange = true;

                // 부모뿐 아니라 루트 전체에서 검색하도록 보강
                _playerServing = other.GetComponentInParent<PlayerServingController>();
                if (_playerServing == null && other.transform.root != null)
                {
                    _playerServing = other.transform.root.GetComponentInChildren<PlayerServingController>();
                }

                Debug.Log($"[CustomerInteractable] 플레이어 인식 성공! PlayerServingController 찾음 여부: {_playerServing != null}");
            }
        }

        private void Update()
        {
            if (!_isPlayerInRange || _customer == null || _playerServing == null) return;

            if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
            {
                Debug.Log($"[CustomerInteractable] E키 입력 감지됨! 현재 손님 상태: {_customer.State}, 플레이어 음식 소지 여부: {_playerServing.IsHoldingFood}");

                if (_customer.State == CustomerState.WaitingOrder && _playerServing.IsHoldingFood)
                {
                    if (_playerServing.DeliverFood())
                    {
                        _customer.ServeFood();
                    }
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
    }
}