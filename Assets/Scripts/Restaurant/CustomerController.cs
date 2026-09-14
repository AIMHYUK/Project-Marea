using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Restaurant
{
    public enum CustomerState
    {
        WalkingToSeat, // 좌석으로 이동 중
        WaitingOrder,  // 음식 대기 중
        Eating,        // 식사 중
        Leaving,       // 퇴장 이동 중
        Finished       // 퇴장
    }

    [RequireComponent(typeof(AgentMover))]
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
        private CustomerState _state = CustomerState.WalkingToSeat;
        private MenuData _orderedMenu;
        private AgentMover _mover;
        private Vector3 _exitPoint;

        public CustomerState State => _state;
        public MenuData OrderedMenu => _orderedMenu;

        /// <summary>이 손님이 앉은 시각. 서빙 순서를 정하는 데 쓴다. (+9/3)</summary>
        public float WaitingSince { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();

            if (orderBubble != null) orderBubble.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
        }

        public void Initialize(Seat seat, Vector3 exitPoint = default)
        {
            _assignedSeat = seat;
            _assignedSeat.AssignCustomer(this);
            _exitPoint = exitPoint == default ? transform.position : exitPoint;

            _state = CustomerState.WalkingToSeat;

            if (_mover != null)
            {
                _mover.GoTo(seat.SitPoint.position, OnArrivedAtSeat, OnSeatPathFailed);
            }
            else
            {
                OnArrivedAtSeat();
            }
        }

        private void OnArrivedAtSeat()
        {
            // 좌석 위치 및 회전값으로 스냅 이동
            if (_assignedSeat != null)
            {
                transform.position = _assignedSeat.SitPoint.position;
                transform.rotation = _assignedSeat.SitPoint.rotation;
            }

            _state = CustomerState.WaitingOrder;
            WaitingSince = Time.time;   // (+9/3)

            DecideOrder();
        }

        private void OnSeatPathFailed()
        {
            Debug.LogWarning("[CustomerController] 좌석 경로 탐색 실패로 강제 착석 처리합니다.");
            OnArrivedAtSeat();
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
                ServeFood(food);
                return true;
            }
            else
            {
                StartCoroutine(RejectAndLeaveRoutine());
                return false;
            }
        }

        /// <summary>
        /// 손님이 음식을 받았다. 플레이어가 건네든 직원이 배달하든 두 경로가 여기로 모인다.
        ///
        /// 그래서 매출도 여기서 센다 (+9/10). 부르는 쪽에서 세면 경로마다 한 벌씩 생기고,
        /// 실제로 그랬다 — 플레이어 경로(CustomerInteractable)에만 기록이 있어서
        /// 직원이 배달한 매출은 하루 종일 0이었다.
        ///
        /// 가격을 인자로 받는 이유는 손님이 등급을 모르기 때문이다. 주문한 메뉴는 알지만
        /// Perfect/Good 판정은 조리 쪽 결과라, 그걸 들고 오는 쪽이 넘겨야 한다.
        /// </summary>
        public void ServeFood(CookingResult food)
        {
            if (_state != CustomerState.WaitingOrder) return;

            _state = CustomerState.Eating;
            Debug.Log("[Customer] 음식을 받았습니다. 식사를 시작합니다.");

            // 영업 중이 아니면 BusinessManager 쪽에서 알아서 무시한다.
            if (BusinessManager.Instance != null)
                BusinessManager.Instance.RecordServedDish(food.bestGrade, food.finalPrice);

            StartCoroutine(EatAndLeaveRoutine());
        }

        private IEnumerator RejectAndLeaveRoutine()
        {
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

            LeaveRestaurant();
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

            Debug.Log("[Customer] 식사를 마치고 퇴장합니다.");

            LeaveRestaurant();
        }

        private void LeaveRestaurant()
        {
            _state = CustomerState.Leaving;

            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }

            if (_assignedSeat != null)
            {
                _assignedSeat.ReleaseSeat();
                _assignedSeat = null;
            }

            if (_mover != null)
            {
                _mover.GoTo(_exitPoint, FinishAndDestroy, FinishAndDestroy, allowPartialPath: true);
            }
            else
            {
                FinishAndDestroy();
            }
        }

        private void FinishAndDestroy()
        {
            _state = CustomerState.Finished;
            Destroy(gameObject);
        }
    }
}
