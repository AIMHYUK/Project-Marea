using System.Collections;
using UnityEngine;

namespace Marea.Restaurant
{
    public enum CustomerState
    {
        WaitingOrder, // 음식 대기 중
        Eating,       // 식사 중
        Finished      // 퇴장
    }

    public class CustomerController : MonoBehaviour
    {
        [Header("식사 설정")]
        [SerializeField] private float eatingDuration = 5.0f; // 서빙 후 퇴장까지 걸리는 시간

        private Seat _assignedSeat;
        private CustomerState _state = CustomerState.WaitingOrder;

        public CustomerState State => _state;

        public void Initialize(Seat seat)
        {
            _assignedSeat = seat;
            _assignedSeat.AssignCustomer(this);

            // 좌석 위치 및 회전값으로 스냅 이동
            transform.position = seat.SitPoint.position;
            transform.rotation = seat.SitPoint.rotation;

            _state = CustomerState.WaitingOrder;
        }

        // 플레이어 상호작용으로 호출되는 서빙 메서드
        public void ServeFood()
        {
            if (_state != CustomerState.WaitingOrder) return;

            _state = CustomerState.Eating;
            Debug.Log("[Customer] 음식을 받았습니다. 식사를 시작합니다.");

            StartCoroutine(EatAndLeaveRoutine());
        }

        private IEnumerator EatAndLeaveRoutine()
        {
            yield return new WaitForSeconds(eatingDuration);

            _state = CustomerState.Finished;
            Debug.Log("[Customer] 식사를 마치고 퇴장합니다.");

            if (_assignedSeat != null)
            {
                _assignedSeat.ReleaseSeat();
            }

            Destroy(gameObject);
        }
    }
}