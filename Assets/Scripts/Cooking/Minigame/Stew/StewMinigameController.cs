using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Restaurant;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public enum StirDirection
    {
        Up,
        Down,
        Left,
        Right
    }

    public class StewMinigameController : MonoBehaviour
    {
        [Header("카메라 시스템")]
        [SerializeField] private MinigameCameraController cameraController;
        [SerializeField] private Transform cameraViewPoint;

        [Header("3D 냄비 컨트롤러")]
        [SerializeField] private StewPot stewPot;

        [Header("국물 연출")]
        [Tooltip("냄비 안에서 회전할 국물 오브젝트의 트랜스폼")]
        [SerializeField] private Transform soupTransform;
        [Tooltip("드래그 시 국물이 회전하는 속도/감도")]
        [SerializeField] private float soupRotationSpeed = 80f;

        [Header("미니게임 시간 설정")]
        [SerializeField] private float gameDuration = 8f;

        [Header("게이지 물리 감도")]
        [Tooltip("드래그 상승력 (기존 1.2 -> 2.5로 상향)")]
        [SerializeField] private float dragPower = 2.5f;
        [Tooltip("자연 감쇠 속도 (기존 0.45 -> 0.22로 완화하여 마우스 재위치 시간 확보)")]
        [SerializeField] private float gaugeDecaySpeed = 0.22f;
        [SerializeField] private float dragSensitivity = 8f;

        [Header("세이프 존 범위 (0.0 ~ 1.0)")]
        [Range(0f, 1f)][SerializeField] private float safeZoneMin = 0.35f;
        [Range(0f, 1f)][SerializeField] private float safeZoneMax = 0.75f;

        [Header("방향 전환 이벤트 설정")]
        [SerializeField] private int maxDirectionChanges = 2;
        [SerializeField] private float requiredSafeTimeForChange = 1.8f;

        [Header("판정 기준 (세이프 존 체류 비율)")]
        [Range(0f, 1f)][SerializeField] private float perfectRatio = 0.55f; // 55% (약 4.4초) 이상 유지 시 PERFECT
        [Range(0f, 1f)][SerializeField] private float goodRatio = 0.35f;    // 35% ~ 54% GOOD
        [Range(0f, 1f)][SerializeField] private float badRatio = 0.15f;     // 15% ~ 34% BAD (구간 현실화)

        [Header("UI 바인딩")]
        [SerializeField] private StewMinigameUI minigameUI;

        private MenuData _targetMenu;
        private Action<CookingResult> _onCompleteCallback;

        private float _currentGauge;
        private float _timeRemaining;
        private float _safeZoneStayDuration;
        private float _outOfSafeZoneDuration;
        private float _safeStreakTimer;
        private int _changedCount;
        private bool _isPlaying;

        private StirDirection _requiredDirection;
        private Vector2 _lastMousePosition;
        private bool _isDragging;

        public float SafeZoneMin => safeZoneMin;
        public float SafeZoneMax => safeZoneMax;
        public float OutOfSafeZoneDuration => _outOfSafeZoneDuration;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void EnsureDependencies()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<MinigameCameraController>(FindObjectsInactive.Include);
            }

            if (stewPot == null)
            {
                stewPot = FindFirstObjectByType<StewPot>(FindObjectsInactive.Include);
            }

            if (cameraViewPoint == null)
            {
                GameObject viewPointObj = GameObject.Find("CameraViewPoint");
                if (viewPointObj == null)
                {
                    Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t.gameObject.scene.isLoaded && t.name == "Stew_CameraViewPoint")
                        {
                            viewPointObj = t.gameObject;
                            break;
                        }
                    }
                }

                if (viewPointObj != null)
                {
                    cameraViewPoint = viewPointObj.transform;
                }
            }

            if (minigameUI == null)
            {
                minigameUI = FindFirstObjectByType<StewMinigameUI>(FindObjectsInactive.Include);
            }

            if (soupTransform == null)
            {
                // stewPot 하위 자식 계층에서 먼저 탐색
                if (stewPot != null)
                {
                    Transform[] potChildren = stewPot.GetComponentsInChildren<Transform>(true);
                    foreach (var child in potChildren)
                    {
                        string childName = child.name.ToLower();
                        if (child != stewPot.transform && (childName.Contains("soup") || childName.Contains("국물")))
                        {
                            soupTransform = child;
                            break;
                        }
                    }
                }

                // stewPot 하위에서 못 찾았을 경우 씬 전체 탐색
                if (soupTransform == null)
                {
                    Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t.gameObject.scene.isLoaded)
                        {
                            string tName = t.name.ToLower();
                            if (tName.Contains("soup") || tName.Contains("국물"))
                            {
                                soupTransform = t;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            _targetMenu = menu;
            _onCompleteCallback = onComplete;

            // 시작 즉시 세이프 존 중앙에서 시작 (초반 손실 방지)
            _currentGauge = (safeZoneMin + safeZoneMax) * 0.5f;
            _timeRemaining = gameDuration;
            _safeZoneStayDuration = 0f;
            _outOfSafeZoneDuration = 0f;
            _safeStreakTimer = 0f;
            _changedCount = 0;

            PickRandomDirection();

            if (stewPot != null)
            {
                stewPot.ResetPosition();
            }

            if (cameraController != null && cameraViewPoint != null)
            {
                cameraController.MoveToViewPoint(cameraViewPoint);
            }

            if (minigameUI != null)
            {
                minigameUI.Setup(this);
                minigameUI.UpdateDirectionGuide(_requiredDirection);
                minigameUI.Open();
            }

            _isPlaying = true;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            ProcessInput();
            SimulateGaugeDecay();
            EvaluateSafeZone();

            _timeRemaining -= Time.deltaTime;
            if (minigameUI != null)
            {
                minigameUI.UpdateTimer(_timeRemaining / gameDuration);
                minigameUI.UpdateGauge(_currentGauge);
            }

            if (_timeRemaining <= 0f)
            {
                FinishMinigame();
            }
        }

        private void ProcessInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _isDragging = true;
                _lastMousePosition = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;
            }

            if (!_isDragging) return;

            Vector2 currentPos = mouse.position.ReadValue();
            Vector2 delta = currentPos - _lastMousePosition;
            _lastMousePosition = currentPos;

            if (delta.sqrMagnitude < dragSensitivity * dragSensitivity) return;

            bool isCorrectDirection = false;
            switch (_requiredDirection)
            {
                case StirDirection.Up:
                    if (delta.y > 0f && delta.y >= Mathf.Abs(delta.x) * 0.35f) isCorrectDirection = true;
                    break;
                case StirDirection.Down:
                    if (delta.y < 0f && -delta.y >= Mathf.Abs(delta.x) * 0.35f) isCorrectDirection = true;
                    break;
                case StirDirection.Left:
                    if (delta.x < 0f && -delta.x >= Mathf.Abs(delta.y) * 0.35f) isCorrectDirection = true;
                    break;
                case StirDirection.Right:
                    if (delta.x > 0f && delta.x >= Mathf.Abs(delta.y) * 0.35f) isCorrectDirection = true;
                    break;
            }

            if (isCorrectDirection)
            {
                float pushDelta = (delta.magnitude / Screen.height) * dragPower;
                _currentGauge = Mathf.Clamp01(_currentGauge + pushDelta);

                if (stewPot != null)
                {
                    stewPot.AnimateStir(_requiredDirection);
                }

                // 국자 젓는 방향 및 마우스 드래그 거리에 비례하여 국물 오브젝트 Y축 회전
                RotateSoup(delta.magnitude);
            }
        }

        private void RotateSoup(float dragMagnitude)
        {
            if (soupTransform == null) return;

            // 좌/하 방향이면 반시계(-), 우/상 방향이면 시계(+) 방향 회전
            float directionSign = (_requiredDirection == StirDirection.Left || _requiredDirection == StirDirection.Down) ? -1f : 1f;
            float rotationAmount = (dragMagnitude / Screen.height) * soupRotationSpeed * directionSign;

            soupTransform.Rotate(Vector3.up, rotationAmount, Space.Self);
        }

        private void SimulateGaugeDecay()
        {
            _currentGauge = Mathf.Clamp01(_currentGauge - gaugeDecaySpeed * Time.deltaTime);
        }

        private void EvaluateSafeZone()
        {
            bool inSafeZone = _currentGauge >= safeZoneMin && _currentGauge <= safeZoneMax;

            if (inSafeZone)
            {
                _safeZoneStayDuration += Time.deltaTime;
                _safeStreakTimer += Time.deltaTime;

                if (_changedCount < maxDirectionChanges && _safeStreakTimer >= requiredSafeTimeForChange)
                {
                    TriggerDirectionChangeEvent();
                }
            }
            else
            {
                _outOfSafeZoneDuration += Time.deltaTime;
                _safeStreakTimer = 0f;
            }

            if (minigameUI != null)
            {
                minigameUI.SetSafeZoneStatus(inSafeZone);
            }
        }

        private void TriggerDirectionChangeEvent()
        {
            _changedCount++;
            _safeStreakTimer = 0f;
            PickRandomDirection();

            if (minigameUI != null)
            {
                minigameUI.UpdateDirectionGuide(_requiredDirection);
                minigameUI.PlayDirectionChangeAlert();
            }
        }

        private void PickRandomDirection()
        {
            StirDirection nextDir;
            do
            {
                nextDir = (StirDirection)UnityEngine.Random.Range(0, 4);
            } while (nextDir == _requiredDirection);

            _requiredDirection = nextDir;
        }

        private void FinishMinigame()
        {
            _isPlaying = false;

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            float ratio = Mathf.Clamp01(_safeZoneStayDuration / gameDuration);
            HitGrade grade;
            float multiplier;

            if (ratio >= perfectRatio)
            {
                grade = HitGrade.Perfect;
                multiplier = 1.2f;
            }
            else if (ratio >= goodRatio)
            {
                grade = HitGrade.Good;
                multiplier = 1.0f;
            }
            else if (ratio >= badRatio)
            {
                grade = HitGrade.Bad;
                multiplier = 0.8f;
            }
            else
            {
                grade = HitGrade.Miss;
                multiplier = 0f;
            }

            Debug.LogWarning($"[컨트롤러 판정] 체류율: {ratio * 100f:F1}% | 판정된 등급: {grade}");

            int basePrice = _targetMenu != null ? _targetMenu.BasePrice : 0;
            int finalCalculatedPrice = Mathf.RoundToInt(basePrice * multiplier);

            CookingResult result = new CookingResult
            {
                isSuccess = (grade != HitGrade.Miss),
                finalPrice = finalCalculatedPrice,
                bestGrade = grade,
                menuData = _targetMenu
            };

            StartCoroutine(ShowResultRoutine(result));
        }

        private IEnumerator ShowResultRoutine(CookingResult result)
        {
            if (minigameUI != null)
            {
                minigameUI.ShowResult(result.bestGrade);
            }

            yield return new WaitForSeconds(1.2f);

            if (minigameUI != null)
            {
                minigameUI.Close();
            }

            if (cameraController != null)
            {
                cameraController.ReturnToOriginalPosition();
            }

            if (stewPot != null)
            {
                stewPot.ResetPosition();
            }

            if (result.isSuccess)
            {
                DispatchCookedFood(result);
            }

            _onCompleteCallback?.Invoke(result);
        }

        private void DispatchCookedFood(CookingResult result)
        {
            Sprite icon = _targetMenu != null ? _targetMenu.Icon : null;
            ServeBoard board = FindFirstObjectByType<ServeBoard>();
            ServingStaff[] staffs = FindObjectsByType<ServingStaff>(FindObjectsSortMode.None);

            if (board == null || staffs.Length == 0)
            {
                GiveFoodToPlayer(result);
                return;
            }

            CustomerController target = PickTarget(board, staffs);
            if (target == null)
            {
                GiveFoodToPlayer(result);
                return;
            }

            board.Post(target.transform, icon, () =>
            {
                if (target != null) target.ServeFood(result);
            });
        }

        private CustomerController PickTarget(ServeBoard board, ServingStaff[] staffs)
        {
            CustomerManager manager = FindFirstObjectByType<CustomerManager>();
            if (manager == null) return null;

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

        private void GiveFoodToPlayer(CookingResult result)
        {
            // 플레이어 손에 직접 넣지 않고 CookingMenuUI의 _onCompleteCallback으로 넘겨 조리대에 거치하도록 위임합니다.
        }
    }
}
