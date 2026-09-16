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
        WalkingToSeat,
        WaitingOrder,
        Eating,
        Leaving,
        Finished
    }

    [RequireComponent(typeof(AgentMover))]
    public class CustomerController : MonoBehaviour
    {
        [Header("식사 설정")]
        [SerializeField] private float eatingDuration = 5.0f;

        [Header("주문 설정 및 UI")]
        [SerializeField] private List<MenuData> availableMenus;
        [SerializeField] private GameObject orderBubble;
        [SerializeField] private Image imgOrderIcon;
        [SerializeField] private Image imgAngryFeedback;
        [SerializeField] private Image imgHappyFeedback;

        [Header("애니메이션 설정")]
        [SerializeField] private Animator animator;

        private Seat _assignedSeat;
        private CustomerState _state = CustomerState.WalkingToSeat;
        private MenuData _orderedMenu;
        private AgentMover _mover;
        private Vector3 _exitPoint;

        // Animator Parameters
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsSitting = Animator.StringToHash("IsSitting");
        private static readonly int ParamTriggerClap = Animator.StringToHash("Clap");

        public CustomerState State => _state;
        public MenuData OrderedMenu => _orderedMenu;
        public float WaitingSince { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

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

            // 걷기 상태로 전환
            SetSittingAnimation(false);
            SetMovingAnimation(true);

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
            if (_assignedSeat != null)
            {
                transform.position = _assignedSeat.SitPoint.position;
                transform.rotation = _assignedSeat.SitPoint.rotation;
            }

            _state = CustomerState.WaitingOrder;
            WaitingSince = Time.time;

            // 이동 정지 및 착석 애니메이션 실행
            SetMovingAnimation(false);
            SetSittingAnimation(true);

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

            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);

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

        public void ServeFood(CookingResult food)
        {
            if (_state != CustomerState.WaitingOrder) return;

            _state = CustomerState.Eating;
            Debug.Log("[Customer] 음식을 받았습니다. 식사를 시작합니다.");

            // 긍정 피드백 박수 애니메이션 트리거
            if (animator != null)
            {
                animator.SetTrigger(ParamTriggerClap);
            }

            if (BusinessManager.Instance != null)
                BusinessManager.Instance.RecordServedDish(food.bestGrade, food.finalPrice);

            StartCoroutine(EatAndLeaveRoutine());
        }

        private IEnumerator RejectAndLeaveRoutine()
        {
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(true);

            Debug.LogWarning("[Customer] 잘못된 음식을 받았습니다. 불만을 품고 즉시 퇴장합니다.");

            yield return new WaitForSeconds(1.0f);

            LeaveRestaurant();
        }

        private IEnumerator EatAndLeaveRoutine()
        {
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(true);

            yield return new WaitForSeconds(1.5f);

            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }

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

            // 착석 해제 후 퇴장 걷기 모션 전환
            SetSittingAnimation(false);
            SetMovingAnimation(true);

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

        private void SetMovingAnimation(bool isMoving)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsMoving, isMoving);
            }
        }

        private void SetSittingAnimation(bool isSitting)
        {
            if (animator != null)
            {
                animator.SetBool(ParamIsSitting, isSitting);
            }
        }
    }
}
