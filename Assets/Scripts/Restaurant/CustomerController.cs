using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Data;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("주문 설정 및 UI")]
        [SerializeField] private List<MenuData> availableMenus;
        [SerializeField] private GameObject orderBubble;
        [SerializeField] private Image imgOrderIcon;
        [SerializeField] private Image imgAngryFeedback; // 분노 피드백 이미지 아이콘
        [SerializeField] private Image imgHappyFeedback; // 만족/성공 피드백 이미지 아이콘

        private Seat _assignedSeat;
        private CustomerState _state = CustomerState.WaitingOrder;
        private MenuData _orderedMenu;

        public CustomerState State => _state;
        public MenuData OrderedMenu => _orderedMenu;

        /// <summary>이 손님이 앉은 시각. 서빙 순서를 정하는 데 쓴다. (+9/3)</summary>
        public float WaitingSince { get; private set; }

        public void Initialize(Seat seat)
        {
            _assignedSeat = seat;
            _assignedSeat.AssignCustomer(this);

            // 좌석 위치 및 회전값으로 스냅 이동
            transform.position = seat.SitPoint.position;
            transform.rotation = seat.SitPoint.rotation;

            _state = CustomerState.WaitingOrder;
            WaitingSince = Time.time;   // (+9/3)

            DecideOrder();
        }

        private void DecideOrder()
        {
            if (availableMenus == null || availableMenus.Count == 0) return;

            int randomIndex = Random.Range(0, availableMenus.Count);
            _orderedMenu = availableMenus[randomIndex];

            if (imgAngryFeedback != null)
            {
                imgAngryFeedback.gameObject.SetActive(false);
            }

            if (imgHappyFeedback != null)
            {
                imgHappyFeedback.gameObject.SetActive(false);
            }

            if (imgOrderIcon != null && _orderedMenu != null && _orderedMenu.Icon != null)
            {
                imgOrderIcon.sprite = _orderedMenu.Icon;
                imgOrderIcon.gameObject.SetActive(true);
            }

            if (orderBubble != null)
            {
                orderBubble.SetActive(true);
            }
        }

        public bool TryReceiveFood(CookingResult food)
        {
            if (_state != CustomerState.WaitingOrder) return false;

            if (food.menuData != null && food.menuData == _orderedMenu)
            {
                ServeFood();
                return true;
            }
            else
            {
                StartCoroutine(RejectAndLeaveRoutine());
                return false;
            }
        }

        // 플레이어 상호작용으로 호출되는 서빙 메서드
        public void ServeFood()
        {
            if (_state != CustomerState.WaitingOrder) return;

            _state = CustomerState.Eating;
            Debug.Log("[Customer] 음식을 받았습니다. 식사를 시작합니다.");

            StartCoroutine(EatAndLeaveRoutine());
        }

        private IEnumerator RejectAndLeaveRoutine()
        {
            _state = CustomerState.Finished;

            if (imgOrderIcon != null)
            {
                imgOrderIcon.gameObject.SetActive(false);
            }

            if (imgHappyFeedback != null)
            {
                imgHappyFeedback.gameObject.SetActive(false);
            }

            if (imgAngryFeedback != null)
            {
                imgAngryFeedback.gameObject.SetActive(true);
            }

            Debug.LogWarning("[Customer] 잘못된 음식을 받았습니다. 불만을 품고 즉시 퇴장합니다.");

            yield return new WaitForSeconds(1.0f);

            if (_assignedSeat != null)
            {
                _assignedSeat.ReleaseSeat();
            }

            Destroy(gameObject);
        }

        private IEnumerator EatAndLeaveRoutine()
        {
            // 주문 아이콘 끄고 만족 피드백 아이콘 표시
            if (imgOrderIcon != null)
            {
                imgOrderIcon.gameObject.SetActive(false);
            }

            if (imgAngryFeedback != null)
            {
                imgAngryFeedback.gameObject.SetActive(false);
            }

            if (imgHappyFeedback != null)
            {
                imgHappyFeedback.gameObject.SetActive(true);
            }

            // 피드백을 잠시 보여준 뒤 말풍선 숨김
            yield return new WaitForSeconds(1.5f);

            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }

            // 남은 식사 시간 대기 (전체 식사 시간 eatingDuration 유지)
            float remainingEatTime = Mathf.Max(0f, eatingDuration - 1.5f);
            yield return new WaitForSeconds(remainingEatTime);

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
