using System.Collections;
using System.Collections.Generic;
using Marea.Cooking;
using Marea.Core;
using Marea.Data;
using UnityEngine;
using UnityEngine.AI;
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
        [SerializeField] private float sitAlignDuration = 1.0f;

        [Header("음식 서빙 비주얼 설정 (2안 오프셋)")]
        [SerializeField] private float foodForwardOffset = 0.65f;
        [SerializeField] private float foodHeightOffset = 0.8f;
        [SerializeField] private float foodScale = 0.7f;

        [Header("주문 설정 및 UI")]
        [SerializeField] private List<MenuData> availableMenus;
        [SerializeField] private GameObject orderBubble;
        [SerializeField] private Image imgOrderIcon;
        [SerializeField] private Image imgAngryFeedback;
        [SerializeField] private Image imgHappyFeedback;
        [SerializeField] private Image imgCoinFeedback;

        // (+10/8, A) 오래 기다리면 말풍선이 주문 아이콘과 화난 얼굴을 번갈아 보여 준다. 떠나지는 않는다(연출만).
        [Header("오래 기다리면 화남 (+10/8, A)")]
        [Tooltip("주문하고 이만큼 지나도 음식을 못 받으면 화난 얼굴을 띄우기 시작한다. 0이면 안 띄운다.")]
        [SerializeField, Min(0f)] private float angryAfterSeconds = 30f;
        [SerializeField, Min(0.1f)] private float angryShowSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float orderShowSeconds = 2.5f;
        [Tooltip("(+10/9, A) 주문하고 이만큼 지나도 음식을 못 받으면 화난 채 떠난다. 0이면 안 떠난다(화만 낸다). "
               + "손님 배는 내린 손님이 다 떠나야 출항하니, 안 떠나면 배가 그날 끝날 때까지 선다.")]
        [SerializeField, Min(0f)] private float leaveAfterSeconds = 60f;

        [Header("효과음 설정")]
        [SerializeField] private AudioClip coinSoundClip;
        [SerializeField] private AudioSource audioSource;

        [Header("애니메이션 설정")]
        [SerializeField] private Animator animator;

        private Seat _assignedSeat;
        private CustomerState _state = CustomerState.WalkingToSeat;
        private MenuData _orderedMenu;
        private GameObject _spawnedFoodVisual;

        private AgentMover _mover;
        private NavMeshAgent _navAgent;
        private Rigidbody _rigidbody;
        private Collider _collider;
        private Vector3 _exitPoint;
        private Coroutine _sitAlignCoroutine;

        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamIsSitting = Animator.StringToHash("IsSitting");
        private static readonly int ParamTriggerClap = Animator.StringToHash("Clap");

        public CustomerState State => _state;
        public MenuData OrderedMenu => _orderedMenu;

        /// <summary>(+10/10, A) 앉을 · 앉은 좌석. 특별 이벤트가 선장 뒤 줄 자리를 잡는 데 쓴다. 떠나기 시작하면 null.</summary>
        public Seat AssignedSeat => _assignedSeat;
        public float WaitingSince { get; private set; }

        /// <summary>(+10/9, A) 음식을 너무 오래 못 받아 떠났는가. 말풍선(CustomerSpeech)이 잘못된 음식으로 떠날 때와 대사를 나눈다.</summary>
        public bool LeftAfterLongWait { get; private set; }

        /// <summary>(+10/9, A) 특별 이벤트 선장처럼 먼저 챙길 손님. GetWaitingCustomers가 맨 앞에 둔다 — 완성된 요리 · 서빙 직원이 먼저 간다.</summary>
        public bool IsPriority { get; set; }

        /// <summary>(+10/9, A) 맞는 음식을 받았는가, 받았으면 그 판정(S = Perfect). 특별 이벤트가 판정에 쓴다.</summary>
        public bool HasReceivedFood { get; private set; }
        public HitGrade ReceivedGrade { get; private set; }

        /// <summary>(+10/9, A) 이 손님만 기다리는 한도를 바꾼다(초). 0이면 안 떠난다.</summary>
        public void SetPatience(float seconds) => leaveAfterSeconds = Mathf.Max(0f, seconds);

        /// <summary>
        /// (+10/9, A) 지금 하던 걸 멈추고 화난 채 떠난다 — 특별 이벤트 실패(선원들이 데크를 부수고 떠남).
        /// 주문 중이었으면 말풍선이 "오래 기다려 떠남" 대사를 한다. 이미 떠나는 중이면 아무것도 안 한다.
        /// </summary>
        public void LeaveAngry()
        {
            if (_state == CustomerState.Leaving || _state == CustomerState.Finished) return;
            StopAllCoroutines();
            _sitAlignCoroutine = null;
            LeftAfterLongWait = true;
            _state = CustomerState.Leaving;
            StartCoroutine(RejectAndLeaveRoutine(waitedTooLong: true));
        }

        private void Awake()
        {
            _mover = GetComponent<AgentMover>();
            _navAgent = GetComponent<NavMeshAgent>();
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            if (orderBubble != null) orderBubble.SetActive(false);
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
            if (imgCoinFeedback != null) imgCoinFeedback.gameObject.SetActive(false);
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
            if (_navAgent != null)
            {
                _navAgent.enabled = false;
            }

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }

            SetSittingAnimation(true);
            SetMovingAnimation(false);

            if (_sitAlignCoroutine != null)
            {
                StopCoroutine(_sitAlignCoroutine);
            }
            _sitAlignCoroutine = StartCoroutine(SmoothSitRoutine());
        }

        private IEnumerator SmoothSitRoutine()
        {
            if (_assignedSeat != null && _assignedSeat.SitPoint != null)
            {
                Vector3 startPos = transform.position;
                Quaternion startRot = transform.rotation;
                Vector3 targetPos = _assignedSeat.SitPoint.position;
                Quaternion targetRot = _assignedSeat.SitPoint.rotation;

                float elapsed = 0f;
                while (elapsed < sitAlignDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / sitAlignDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                    transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                    yield return null;
                }

                transform.position = targetPos;
                transform.rotation = targetRot;
            }

            _state = CustomerState.WaitingOrder;
            WaitingSince = Time.time;
            _sitAlignCoroutine = null;

            DecideOrder();
            if (angryAfterSeconds > 0f) StartCoroutine(ImpatientRoutine());   // (+10/8, A)
        }

        private void OnSeatPathFailed()
        {
            Debug.LogWarning("[CustomerController] 좌석 경로 탐색 실패로 강제 착석 처리합니다.");
            OnArrivedAtSeat();
        }

        private void DecideOrder()
        {
            // (+10/9, A) 오늘의 메뉴 안에서 · 같은 메뉴가 이어지지 않게 고른다(OrderPicker). 오늘의 메뉴를 안 골랐으면
            // 예전처럼 이 손님의 availableMenus에서.
            _orderedMenu = OrderPicker.Pick(availableMenus);
            if (_orderedMenu == null)
            {
                Debug.LogError($"{name}: 주문할 메뉴가 없다. 오늘의 메뉴가 비었고 availableMenus도 비었거나 IsActive가 전부 꺼져 있다.", this);
                return;
            }

            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
            if (imgCoinFeedback != null) imgCoinFeedback.gameObject.SetActive(false);

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
            HasReceivedFood = true;            // (+10/9, A)
            ReceivedGrade = food.bestGrade;
            Debug.Log("[Customer] 음식을 받았습니다. 식사를 시작합니다.");

            SpawnFoodVisual(food.menuData);

            if (animator != null)
            {
                animator.SetTrigger(ParamTriggerClap);
            }

            if (BusinessManager.Instance != null)
                BusinessManager.Instance.RecordServedDish(food.bestGrade, food.finalPrice);

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

            Vector3 spawnPos = transform.position + (transform.forward * foodForwardOffset) + (Vector3.up * foodHeightOffset);
            Quaternion spawnRot = transform.rotation;

            _spawnedFoodVisual = Instantiate(menu.ServingPrefab, spawnPos, spawnRot);
            _spawnedFoodVisual.transform.localScale = Vector3.one * foodScale;
        }

        private void ClearFoodVisual()
        {
            if (_spawnedFoodVisual != null)
            {
                Destroy(_spawnedFoodVisual);
                _spawnedFoodVisual = null;
            }
        }

        /// <summary>
        /// (+10/8, A) 오래 기다리면 주문 아이콘 ↔ 화난 얼굴을 번갈아. 음식을 받거나 떠나면 끝난다.
        /// (+10/9, A) leaveAfterSeconds가 지나면 화난 얼굴로 떠난다(잘못된 음식을 받았을 때와 같은 퇴장).
        /// </summary>
        private IEnumerator ImpatientRoutine()
        {
            // (+10/9, A) 떠날 시간이 화낼 시간보다 짧으면(특별 이벤트 선장 등) 화내기 전에도 떠난다.
            while (_state == CustomerState.WaitingOrder && Time.time - WaitingSince < angryAfterSeconds && !TimeToLeave())
                yield return null;

            while (_state == CustomerState.WaitingOrder)
            {
                if (TimeToLeave()) break;
                SetImpatient(true);
                yield return WaitWhileWaiting(angryShowSeconds);
                if (_state != CustomerState.WaitingOrder || TimeToLeave()) break;
                SetImpatient(false);
                yield return WaitWhileWaiting(orderShowSeconds);
            }

            if (_state == CustomerState.WaitingOrder && TimeToLeave())
            {
                Debug.Log($"[Customer] {leaveAfterSeconds:F0}초 기다려도 음식이 안 와서 떠납니다.");
                LeftAfterLongWait = true;
                _state = CustomerState.Leaving;   // 화난 얼굴을 띄우는 1초 사이에 음식을 받지 않게 먼저 바꾼다
                StartCoroutine(RejectAndLeaveRoutine(waitedTooLong: true));
            }
        }

        private bool TimeToLeave() => leaveAfterSeconds > 0f && Time.time - WaitingSince >= leaveAfterSeconds;

        /// <summary>기다리는 동안만 센다 — 떠날 때가 되면 화남 깜빡임 중간이라도 바로 끝낸다.</summary>
        private IEnumerator WaitWhileWaiting(float seconds)
        {
            for (float t = 0f; t < seconds && _state == CustomerState.WaitingOrder && !TimeToLeave(); t += Time.deltaTime)
                yield return null;
        }

        private void SetImpatient(bool angry)
        {
            if (_state != CustomerState.WaitingOrder) return;   // 그사이 음식을 받았으면 다른 루틴이 표시를 정한다
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(!angry);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(angry);
        }

        private IEnumerator RejectAndLeaveRoutine(bool waitedTooLong = false)
        {
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
            if (imgCoinFeedback != null) imgCoinFeedback.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(true);

            if (!waitedTooLong) Debug.LogWarning("[Customer] 잘못된 음식을 받았습니다. 불만을 품고 즉시 퇴장합니다.");

            yield return new WaitForSeconds(1.0f);

            LeaveRestaurant();
        }

        private IEnumerator EatAndLeaveRoutine()
        {
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(false);
            if (imgCoinFeedback != null) imgCoinFeedback.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(true);

            yield return new WaitForSeconds(1.5f);

            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }
            if (imgHappyFeedback != null)
            {
                imgHappyFeedback.gameObject.SetActive(false);
            }

            float duration = Mathf.Max(5.0f, eatingDuration);
            float remainingEatTime = duration - 1.5f;

            yield return new WaitForSeconds(remainingEatTime);

            if (imgCoinFeedback != null)
            {
                imgCoinFeedback.gameObject.SetActive(true);
            }

            if (orderBubble != null)
            {
                orderBubble.SetActive(true);
            }

            PlayCoinSound();

            yield return new WaitForSeconds(1.2f);

            if (imgCoinFeedback != null)
            {
                imgCoinFeedback.gameObject.SetActive(false);
            }

            Debug.Log("[Customer] 식사를 마치고 퇴장합니다.");

            LeaveRestaurant();
        }

        private void PlayCoinSound()
        {
            if (coinSoundClip == null) return;

            if (audioSource != null)
            {
                audioSource.PlayOneShot(coinSoundClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(coinSoundClip, transform.position);
            }
        }

        private void LeaveRestaurant()
        {
            if (_sitAlignCoroutine != null)
            {
                StopCoroutine(_sitAlignCoroutine);
                _sitAlignCoroutine = null;
            }

            _state = CustomerState.Leaving;

            ClearFoodVisual();

            if (orderBubble != null)
            {
                orderBubble.SetActive(false);
            }

            Seat seatToRelease = _assignedSeat;
            _assignedSeat = null;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }

            if (_collider != null)
            {
                _collider.isTrigger = false;
            }

            if (_navAgent != null)
            {
                _navAgent.enabled = true;
            }

            SetSittingAnimation(false);
            SetMovingAnimation(true);

            if (seatToRelease != null)
            {
                seatToRelease.ReleaseSeat();
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
