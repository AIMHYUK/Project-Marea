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

        [Header("음식 서빙 비주얼 설정")]
        [SerializeField] private float foodForwardOffset = 0.65f; // 손님 정면 기준 테이블 쪽 거리
        [SerializeField] private float foodHeightOffset = 0.8f;   // 테이블 상판 높이에 맞출 Y 오프셋

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
        private GameObject _spawnedFoodVisual; // 손님 앞에 놓인 서빙 음식 인스턴스

        // 주문할 때마다 새 List를 만들지 않으려고 들고 있는 버퍼. 손님 수만큼 쓰레기가
        // 생기는 자리라 인스턴스마다 하나씩 재사용한다. (+9/16)
        private readonly List<MenuData> _orderCandidates = new();
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

        private void OnDestroy()
        {
            ClearFoodVisual();
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

            // 기획 MenuData.IsActive가 「주문 후보 포함 여부」다 (+9/16, #47).
            // 빈 칸도 여기서 같이 걸러낸다 — 전에는 null이 뽑히면 손님이 주문 없이 앉아만
            // 있었고, 증상만 보면 원인이 인스펙터인지 코드인지 알 수 없었다.
            _orderCandidates.Clear();
            for (int i = 0; i < availableMenus.Count; i++)
            {
                MenuData menu = availableMenus[i];
                if (menu != null && menu.IsActive) _orderCandidates.Add(menu);
            }

            if (_orderCandidates.Count == 0)
            {
                Debug.LogError($"{name}: availableMenus에 주문할 수 있는 메뉴가 없다. "
                             + "비어 있거나 IsActive가 전부 꺼져 있다.", this);
                return;
            }

            int randomIndex = Random.Range(0, _orderCandidates.Count);
            _orderedMenu = _orderCandidates[randomIndex];

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

            // 1. 손님 앞 테이블 위치에 서빙된 음식 모델 스폰
            SpawnFoodVisual(food.menuData);

            // 2. 긍정 피드백 박수 애니메이션 트리거
            if (animator != null)
            {
                animator.SetTrigger(ParamTriggerClap);
            }

            if (BusinessManager.Instance != null)
                BusinessManager.Instance.RecordServedDish(food.bestGrade, food.finalPrice);

            // 3. 식사 코루틴 시작 (총 5초 유지)
            StartCoroutine(EatAndLeaveRoutine());
        }

        private void SpawnFoodVisual(MenuData menu)
        {
            ClearFoodVisual();

            if (menu == null || menu.ServingPrefab == null)
            {
                Debug.LogWarning($"[CustomerController] '{menu?.DisplayName}'의 ServingPrefab이 비어 있어 음식 비주얼 스폰을 건너뜁니다.");
                return;
            }

            // 손님이 바라보는 앞쪽 및 테이블 높이 오프셋 적용
            Vector3 spawnPos = transform.position + (transform.forward * foodForwardOffset) + (Vector3.up * foodHeightOffset);
            Quaternion spawnRot = transform.rotation;

            _spawnedFoodVisual = Instantiate(menu.ServingPrefab, spawnPos, spawnRot);
        }

        private void ClearFoodVisual()
        {
            if (_spawnedFoodVisual != null)
            {
                Destroy(_spawnedFoodVisual);
                _spawnedFoodVisual = null;
            }
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
            // 주문 아이콘 끄고 만족 피드백 표시
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(true);

            // 1.5초 동안 만족 피드백 아이콘 보여줌
            yield return new WaitForSeconds(1.5f);

            // 만족 피드백 숨김
            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }
            if (imgHappyFeedback != null)
            {
                imgHappyFeedback.gameObject.SetActive(false);
            }

            // eatingDuration이 인스펙터에서 0으로 설정되어 있어도 최소 5초는 유지되도록 방어
            float duration = Mathf.Max(5.0f, eatingDuration);
            float remainingEatTime = duration - 1.5f;

            // 남은 시간(3.5초) 동안 식사 지속
            yield return new WaitForSeconds(remainingEatTime);

            Debug.Log("[Customer] 식사를 마치고 퇴장합니다.");

            // 식사 완료 후 퇴장 (음식도 함께 제거됨)
            LeaveRestaurant();
        }

        private void LeaveRestaurant()
        {
            _state = CustomerState.Leaving;

            // 자리에서 일어날 때 테이블 위의 서빙 음식 정리
            ClearFoodVisual();

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
            ClearFoodVisual();
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
