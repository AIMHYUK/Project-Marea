using System;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Player;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class SkewerMinigameUI : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] private GameObject gameRoot;
        [SerializeField] private TimingBarController timingBar;
        [SerializeField] private TextMeshProUGUI txtFeedback;

        [Header("3D 재료 연출 컨트롤러")]
        [SerializeField] private IngredientController ingredientController;

        [Header("카메라 연동")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("미니게임 동안 꺼둘 카메라 추적. 비워두면 targetCamera에서 찾고, " +
                 "그래도 없으면 에러를 낸다. (+9/3)")]
        [SerializeField] private CameraFollow cameraFollow;

        [Header("게임 설정")]
        [SerializeField] private int totalIngredients = 4;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private int _currentIngredientIndex;
        private readonly List<HitGrade> _hitHistory = new();
        private bool _isPlaying;

        private Vector3 _originalCamPos;
        private Quaternion _originalCamRot;

        /// <summary>
        /// 미니게임 동안 꺼둔 카메라 추적. 안 껐으면 null. (+9/3)
        ///
        /// CameraFollow는 LateUpdate에서 매 프레임 카메라 위치를 다시 계산한다.
        /// 여기서 위치만 옮겨놓으면 같은 프레임 안에 덮어써져서 화면이 안 바뀐다.
        /// "껐다"는 사실을 기억해야 원래 켜져 있던 것만 되켤 수 있다.
        /// </summary>
        private CameraFollow _suspendedFollow;

        public void StartGame(MenuData menu, Action<CookingResult> onComplete)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // 미니게임 시작 시 손님 스폰 일시정지
            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _currentIngredientIndex = 0;
            _hitHistory.Clear();
            _isPlaying = true;

            SwitchToMinigameCamera();

            if (gameRoot != null) gameRoot.SetActive(true);
            gameObject.SetActive(true);

            if (timingBar != null) timingBar.Initialize();
            if (ingredientController != null) ingredientController.InitializeMinigame3D();

            if (txtFeedback != null) txtFeedback.text = "타이밍에 맞춰 스페이스바 또는 좌클릭!";
        }

        private void SwitchToMinigameCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || ingredientController == null || ingredientController.MinigameCameraViewPoint == null)
            {
                Debug.LogWarning("[SkewerMinigameUI] 카메라 전환 실패: 타깃 카메라 또는 MinigameCameraViewPoint가 없습니다.");
                return;
            }

            // 카메라를 옮기기 전에 추적을 먼저 끈다. (+9/3)
            // 위 가드를 통과한 뒤에 끄는 게 중요하다 — 전환에 실패했는데 추적만 꺼지면
            // 미니게임은 안 열리고 카메라만 플레이어를 안 따라가는 상태가 된다.
            SuspendCameraFollow();

            // 기존 월드 위치 및 회전 백업 (부모 계층을 변경하지 않아 렌더링 꺼짐 방지)
            _originalCamPos = targetCamera.transform.position;
            _originalCamRot = targetCamera.transform.rotation;

            Transform viewPoint = ingredientController.MinigameCameraViewPoint;
            viewPoint.gameObject.SetActive(true);

            targetCamera.transform.position = viewPoint.position;
            targetCamera.transform.rotation = viewPoint.rotation;
        }

        private void RestoreCamera()
        {
            if (targetCamera != null)
            {
                targetCamera.transform.position = _originalCamPos;
                targetCamera.transform.rotation = _originalCamRot;
            }

            // 위치를 되돌린 다음에 켠다. 순서가 반대면 미니게임 자리에서 보간이 시작돼
            // 카메라가 한 번 훑고 지나간다. (+9/3)
            ResumeCameraFollow();
        }

        /// <summary>
        /// 미니게임에 들어가는 동안 카메라 추적을 끈다. (+9/3)
        ///
        /// 원래 꺼져 있던 것은 기억하지 않는다 — 그래야 끝났을 때 켜지 않는다.
        /// </summary>
        private void SuspendCameraFollow()
        {
            if (_suspendedFollow != null) return;   // 이미 껐다

            CameraFollow follow = ResolveCameraFollow();
            if (follow == null)
            {
                // 못 찾았다 = 씬 설정이 잘못됐다. 조용히 넘어가면 안 된다.
                // 카메라가 미니게임 자리로 안 가는데 원인이 안 보이는 상태가 된다.
                Debug.LogError(
                    "[SkewerMinigameUI] CameraFollow를 찾지 못했습니다. " +
                    "cameraFollow 필드에 직접 넣거나, Main Camera에 CameraFollow를 붙이세요. " +
                    "이대로 두면 미니게임 카메라가 매 프레임 덮어써집니다.", this);
                return;
            }

            // 이미 꺼져 있다 = 다른 쪽이 껐다. 정상 상황이라 로그를 남기지 않는다.
            // 여기서 _suspendedFollow에 담지 않아야 끝났을 때 남의 것을 켜지 않는다.
            if (!follow.enabled) return;

            follow.enabled = false;
            _suspendedFollow = follow;
        }

        /// <summary>
        /// 끌 CameraFollow를 정한다. 인스펙터 지정이 우선이다. (+9/3)
        ///
        /// 탐색은 폴백일 뿐이다 — 의존은 인스펙터에 보이는 게 맞다.
        /// 부모까지 보는 이유는 지금 CameraFollow가 카메라 자신에 붙어 있지만
        /// 나중에 카메라 리그를 두면 부모로 올라가기 때문이다.
        /// </summary>
        private CameraFollow ResolveCameraFollow()
        {
            if (cameraFollow != null) return cameraFollow;
            if (targetCamera == null) return null;

            return targetCamera.GetComponentInParent<CameraFollow>();
        }

        /// <summary>내가 끈 것만 되켠다.</summary>
        private void ResumeCameraFollow()
        {
            if (_suspendedFollow == null) return;

            _suspendedFollow.enabled = true;
            _suspendedFollow = null;
        }

        /// <summary>
        /// 미니게임이 끝나기 전에 오브젝트가 꺼질 수 있다. (+9/3)
        /// 그때 되켜주지 않으면 카메라가 영영 플레이어를 안 따라간다.
        /// </summary>
        private void OnDisable()
        {
            ResumeCameraFollow();
        }

        private void Update()
        {
            if (!_isPlaying) return;

            bool isTriggered = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                               || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (isTriggered)
            {
                OnAttemptHit();
            }
        }

        private void OnAttemptHit()
        {
            HitGrade grade = timingBar != null ? timingBar.EvaluateHit() : HitGrade.Miss;
            Debug.Log($"[SkewerMinigame] 시도 결과: {grade} (현재 인덱스: {_currentIngredientIndex})");
            _hitHistory.Add(grade);

            if (grade != HitGrade.Miss)
            {
                if (ingredientController != null)
                {
                    ingredientController.AttachIngredient(_currentIngredientIndex);
                }

                _currentIngredientIndex++;
                ShowFeedback(grade);

                if (_currentIngredientIndex >= totalIngredients)
                {
                    FinishGame(true);
                }
            }
            else
            {
                ShowFeedback(HitGrade.Miss);
            }
        }

        private void ShowFeedback(HitGrade grade)
        {
            if (txtFeedback == null) return;
            txtFeedback.text = grade switch
            {
                HitGrade.Perfect => "<color=yellow>PERFECT!</color>",
                HitGrade.Good => "<color=green>GOOD!</color>",
                _ => "<color=red>MISS!</color>"
            };
        }

        private void FinishGame(bool isSuccess)
        {
            _isPlaying = false;

            if (timingBar != null) timingBar.StopGauge();
            if (ingredientController != null) ingredientController.StopMoving();

            RestoreCamera();

            // 미니게임 종료 시 손님 스폰 재개
            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            float multiplier = 1.0f;
            if (_hitHistory.Contains(HitGrade.Perfect)) multiplier += 0.1f;
            if (!isSuccess) multiplier -= 0.2f;

            int finalPrice = _currentMenu != null ? Mathf.RoundToInt(_currentMenu.BasePrice * multiplier) : 0;

            CookingResult result = new CookingResult
            {
                isSuccess = isSuccess,
                finalPrice = finalPrice,
                bestGrade = _hitHistory.Contains(HitGrade.Perfect) ? HitGrade.Perfect : HitGrade.Good
            };

            // 요리 성공 시 플레이어 손에 음식 지급
            if (isSuccess)
            {
                DispatchCookedFood();
            }

            if (gameRoot != null) gameRoot.SetActive(false);
            if (ingredientController != null) ingredientController.HideAll();

            _onCompleteCallback?.Invoke(result);
        }

        /// <summary>
        /// 완성된 음식을 어디로 보낼지 정한다. (+9/3)
        ///
        /// 씬에 ServeBoard와 서빙 직원이 둘 다 있으면 보드에 올린다 - 직원이 집어서
        /// 대기 중인 손님에게 간다. 하나라도 없으면 예전처럼 플레이어 손에 들린다.
        /// 그래서 직원 오브젝트를 끄면 이전 동작으로 그대로 돌아간다.
        /// </summary>
        private void DispatchCookedFood()
        {
            Sprite icon = _currentMenu != null ? _currentMenu.Icon : null;

            ServeBoard board = FindFirstObjectByType<ServeBoard>();
            ServingStaff[] staffs = FindObjectsByType<ServingStaff>(FindObjectsSortMode.None);

            if (board == null || staffs.Length == 0)
            {
                GiveFoodToPlayer();
                return;
            }

            CustomerController target = PickTarget(board, staffs);
            if (target == null)
            {
                // 여긴 진짜로 "지금 받을 손님이 없다"는 게임 상황이다. 설정 오류가 아니다.
                Debug.LogWarning("[SkewerMinigameUI] 배달할 손님을 정하지 못했습니다. 음식을 버립니다.");
                return;
            }

            // 콜백 안에서 다시 null을 보는 이유: 직원이 도착하기 전에 손님이
            // 사라질 수 있다. Unity의 == null 오버로드가 파괴된 오브젝트를 잡아준다.
            board.Post(target.transform, icon, () =>
            {
                if (target != null) target.ServeFood();
            });

            Debug.Log($"[SkewerMinigameUI] 서빙 작업 등록 - 대상 {target.name}");
        }

        /// <summary>
        /// 가장 오래 기다린 손님. 단, 이미 다른 음식이 배정된 손님은 건너뛴다. (+9/3)
        ///
        /// 손님에게 '예약됨' 플래그를 달지 않는다. 배달이 실패해 작업이 버려지면
        /// 그 플래그가 영영 안 풀려서 손님이 좌석을 물고 안 나간다.
        /// 대신 큐와 직원이 지금 들고 있는 것을 직접 본다 - 스스로 정리된다.
        /// </summary>
        private CustomerController PickTarget(ServeBoard board, ServingStaff[] staffs)
        {
            // 매니저가 없는 것과 손님이 없는 것은 전혀 다른 일이다.
            // 둘 다 null로 뭉뚱그리면 "손님이 없네" 로그만 보고 원인을 못 찾는다. (+9/3)
            CustomerManager manager = FindFirstObjectByType<CustomerManager>();
            if (manager == null)
            {
                Debug.LogError("[SkewerMinigameUI] 씬에 CustomerManager가 없습니다. " +
                               "직원 서빙이 동작하지 않습니다.", this);
                return null;
            }

            foreach (CustomerController c in manager.GetWaitingCustomers())
            {
                if (board.IsTargeted(c.transform)) continue;

                bool taken = false;
                foreach (ServingStaff s in staffs)
                {
                    if (s.DeliverTarget == c.transform) { taken = true; break; }
                }

                if (!taken) return c;
            }

            return null;
        }

        private void GiveFoodToPlayer()
        {
            PlayerServingController playerServing = FindFirstObjectByType<PlayerServingController>();

            // 혹시 비활성화된 오브젝트인지 포함해서 검색
            if (playerServing == null)
            {
                playerServing = FindFirstObjectByType<PlayerServingController>(FindObjectsInactive.Include);
            }

            if (playerServing != null)
            {
                playerServing.PickUpFood();
            }
            else
            {
                Debug.LogError("[SkewerMinigameUI] 씬에서 PlayerServingController를 찾을 수 없습니다!");
            }
        }
    }
}