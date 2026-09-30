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

        [Header("효과음 설정")]
        [SerializeField] private AudioClip coinSoundClip;
        [SerializeField] private AudioSource audioSource;

        [Header("애니메이션 설정")]
        [SerializeField] private Animator animator;

        private Seat _assignedSeat;
        private CustomerState _state = CustomerState.WalkingToSeat;
        private MenuData _orderedMenu;
        private GameObject _spawnedFoodVisual;

        private readonly List<MenuData> _orderCandidates = new();
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
        public float WaitingSince { get; private set; }

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
        }

        private void OnSeatPathFailed()
        {
            Debug.LogWarning("[CustomerController] 좌석 경로 탐색 실패로 강제 착석 처리합니다.");
            OnArrivedAtSeat();
        }

        private void DecideOrder()
        {
            if (availableMenus == null || availableMenus.Count == 0) return;

            _orderCandidates.Clear();
            for (int i = 0; i < availableMenus.Count; i++)
            {
                MenuData menu = availableMenus[i];
                if (menu != null && menu.IsActive) _orderCandidates.Add(menu);
            }

            if (_orderCandidates.Count == 0)
            {
                Debug.LogError($"{name}: availableMenus에 주문할 수 있는 메뉴가 없다. 비어 있거나 IsActive가 전부 꺼져 있다.", this);
                return;
            }

            int randomIndex = Random.Range(0, _orderCandidates.Count);
            _orderedMenu = _orderCandidates[randomIndex];

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

        private IEnumerator RejectAndLeaveRoutine()
        {
            if (imgOrderIcon != null) imgOrderIcon.gameObject.SetActive(false);
            if (imgHappyFeedback != null) imgHappyFeedback.gameObject.SetActive(false);
            if (imgCoinFeedback != null) imgCoinFeedback.gameObject.SetActive(false);
            if (imgAngryFeedback != null) imgAngryFeedback.gameObject.SetActive(true);

            Debug.LogWarning("[Customer] 잘못된 음식을 받았습니다. 불만을 품고 즉시 퇴장합니다.");

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
