using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Restaurant;
using System;
using System.Collections;
using System.Collections.Generic;
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

    /// <summary>
    /// 해물스튜 — 3단계. (+9/30) BaseCookingMinigame으로 옮겼다 (생선구이와 같은 틀).
    ///   1 준비  MG_OBJECT_CATCH : 냄비를 좌우로 움직여 떨어지는 재료를 받는다 (CatchGame 부품)
    ///   2 핵심  MG_GAUGE_KEEP   : 방향대로 저어 게이지를 안전 구간에 유지 (예전 스튜 로직 그대로)
    ///   3 마무리 MG_POPUP_TOUCH : 국물 위 거품을 터뜨린다 (PopupTouchGame 부품)
    /// 판매가는 2단계 배율만 쓴다 (생선구이와 같다). 1·3단계 점수는 Step1Score/Step3Score에만 남는다.
    ///
    /// 컨트롤러는 미니게임 뷰(CookingTable_MinigameView)의 StewCookingStation 하나뿐이다.
    /// 예전엔 UI/StewMinigameUI에도 하나 더 있었고, 냄비·시점을 이름·타입으로 찾아서 식당 장식 냄비가
    /// 잡힐 수 있었다 → 냄비·시점·단계 부품은 프리팹 안에서 직접 연결한다. UI만 씬 오브젝트라 찾는다.
    /// </summary>
    public class StewMinigameController : BaseCookingMinigame
    {
        [Header("3D 냄비 컨트롤러")]
        [SerializeField] private StewPot stewPot;

        [Header("국물 연출")]
        // soupTransform을 지웠다 (#48). 국물은 StewPot이 프리팹 안에서 이미 알고 있다 —
        // 컨트롤러가 이름으로 뒤질 이유가 없었고, 뒤져봤자 그 이름의 오브젝트가 없었다.
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

        [Header("1단계: 재료 받기 (+9/30)")]
        [SerializeField] private CatchGame catchGame;

        [Header("3단계: 거품 터뜨리기 (+9/30)")]
        [SerializeField] private PopupTouchGame popupTouch;

        private MenuData _targetMenu;
        private Action<CookingResult> _onCompleteCallback;
        private readonly List<GameObject> _catchItems = new();

        private float _currentGauge;
        private float _timeRemaining;
        private float _safeZoneStayDuration;
        private float _outOfSafeZoneDuration;
        private float _safeStreakTimer;
        private int _changedCount;
        private HitGrade _stirGrade = HitGrade.Miss;

        private StirDirection _requiredDirection;
        private Vector2 _lastMousePosition;
        private bool _isDragging;

        public float SafeZoneMin => safeZoneMin;
        public float SafeZoneMax => safeZoneMax;
        public float OutOfSafeZoneDuration => _outOfSafeZoneDuration;

        protected override void EnsureDependencies()
        {
            base.EnsureDependencies();

            // UI는 씬 오브젝트라 프리팹이 못 든다 — 한 번 찾는다 (생선구이와 같다).
            if (minigameUI == null)
                minigameUI = FindFirstObjectByType<StewMinigameUI>(FindObjectsInactive.Include);
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            if (stewPot == null)
                Debug.LogError($"{name}: StewMinigameController.stewPot이 비어 있다. 냄비가 안 움직인다. "
                             + "StewCookingStation 아래 PF_Soup_Set을 연결할 것.", this);
            if (cameraViewPoint == null)
                Debug.LogError($"{name}: cameraViewPoint가 비어 있다. 카메라가 냄비로 안 간다.", this);
            if (minigameUI == null)
                Debug.LogError($"{name}: 씬에 StewMinigameUI가 없다. 게이지·안내가 안 보인다.", this);

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(true);

            _targetMenu = menu;
            _onCompleteCallback = onComplete;
            _stirGrade = HitGrade.Miss;

            if (stewPot != null) stewPot.ResetPosition();

            if (minigameUI != null)
            {
                minigameUI.Setup(this);
                minigameUI.Open();
            }

            StartStep1();
        }

        // ==========================================
        // 1단계: 재료 받기
        // ==========================================
        protected override void OnStep1Start()
        {
            if (minigameUI != null)
            {
                minigameUI.SetGaugeVisible(false);
                minigameUI.SetGuide("냄비를 움직여 재료를 받으세요!", "마우스를 좌우로");
            }

            if (catchGame == null)
            {
                Debug.LogError($"{name}: catchGame이 비어 있다. 1단계를 건너뛴다.", this);
                return;
            }

            // 레시피 재료를 필요한 수만큼 떨어뜨린다. 재료 모델은 IngredientData.MinigamePrefab (없으면 구).
            _catchItems.Clear();
            if (_targetMenu != null && _targetMenu.Recipe != null)
            {
                foreach (RecipeEntry entry in _targetMenu.Recipe)
                {
                    GameObject model = entry.ingredient != null ? entry.ingredient.MinigamePrefab : null;
                    for (int i = 0; i < Mathf.Max(1, entry.requiredAmount); i++) _catchItems.Add(model);
                }
            }
            catchGame.Begin(_catchItems);
        }

        protected override void OnStep1Update()
        {
            if (catchGame == null) { CompleteStep1(1f); return; }

            if (minigameUI != null)
            {
                minigameUI.UpdateTimer(catchGame.TimeLeft01);
                minigameUI.SetGuide("냄비를 움직여 재료를 받으세요!", $"{catchGame.Caught} / {catchGame.Total}");
            }

            if (catchGame.IsFinished) CompleteStep1(catchGame.Score);
        }

        // ==========================================
        // 2단계: 젓기 (예전 스튜 미니게임)
        // ==========================================
        protected override void OnStep2Start()
        {
            // 시작 즉시 세이프 존 중앙에서 시작 (초반 손실 방지)
            _currentGauge = (safeZoneMin + safeZoneMax) * 0.5f;
            _timeRemaining = gameDuration;
            _safeZoneStayDuration = 0f;
            _outOfSafeZoneDuration = 0f;
            _safeStreakTimer = 0f;
            _changedCount = 0;
            _isDragging = false;

            PickRandomDirection();

            if (minigameUI != null)
            {
                minigameUI.SetGaugeVisible(true);
                minigameUI.UpdateDirectionGuide(_requiredDirection);
            }
        }

        protected override void OnStep2Update()
        {
            ProcessInput();
            SimulateGaugeDecay();
            EvaluateSafeZone();

            _timeRemaining -= Time.deltaTime;
            if (minigameUI != null)
            {
                minigameUI.UpdateTimer(_timeRemaining / gameDuration);
                minigameUI.UpdateGauge(_currentGauge);
            }

            if (_timeRemaining <= 0f) FinishStir();
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
            if (stewPot == null) return;

            // 좌/하 방향이면 반시계(-), 우/상 방향이면 시계(+) 방향 회전
            float directionSign = (_requiredDirection == StirDirection.Left || _requiredDirection == StirDirection.Down) ? -1f : 1f;
            float rotationAmount = (dragMagnitude / Screen.height) * soupRotationSpeed * directionSign;

            stewPot.RotateLiquid(rotationAmount);
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

        /// <summary>2단계 끝 — 체류 비율로 판정하고 배율을 Step2Score로 넘긴다.</summary>
        private void FinishStir()
        {
            float ratio = Mathf.Clamp01(_safeZoneStayDuration / gameDuration);
            float multiplier;

            if (ratio >= perfectRatio)
            {
                _stirGrade = HitGrade.Perfect;
                multiplier = 1.2f;
            }
            else if (ratio >= goodRatio)
            {
                _stirGrade = HitGrade.Good;
                multiplier = 1.0f;
            }
            else if (ratio >= badRatio)
            {
                _stirGrade = HitGrade.Bad;
                multiplier = 0.8f;
            }
            else
            {
                _stirGrade = HitGrade.Miss;
                multiplier = 0f;
            }

            Debug.LogWarning($"[컨트롤러 판정] 체류율: {ratio * 100f:F1}% | 판정된 등급: {_stirGrade}");
            if (stewPot != null) stewPot.ResetPosition();
            CompleteStep2(multiplier);
        }

        // ==========================================
        // 3단계: 거품 터뜨리기
        // ==========================================
        protected override void OnStep3Start()
        {
            if (minigameUI != null)
            {
                minigameUI.SetGaugeVisible(false);
                minigameUI.SetGuide("거품을 터뜨리세요!", "거품을 클릭");
            }

            if (popupTouch == null)
            {
                Debug.LogError($"{name}: popupTouch가 비어 있다. 3단계를 건너뛴다.", this);
                return;
            }
            popupTouch.Begin();
        }

        protected override void OnStep3Update()
        {
            if (popupTouch == null) { CompleteStep3(1f); return; }

            if (minigameUI != null)
            {
                minigameUI.UpdateTimer(popupTouch.TimeLeft01);
                minigameUI.SetGuide("거품을 터뜨리세요!", $"{popupTouch.Popped} 개");
            }

            if (popupTouch.IsFinished) CompleteStep3(popupTouch.Score);
        }

        // ==========================================
        // 최종 결과 — 판매가는 2단계 배율만 (생선구이와 같다)
        // ==========================================
        protected override void OnMinigameCompleted(float finalScore)
        {
            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(false);

            int basePrice = _targetMenu != null ? _targetMenu.BasePrice : 0;
            CookingResult result = new CookingResult
            {
                isSuccess = (_stirGrade != HitGrade.Miss),
                finalPrice = Mathf.RoundToInt(basePrice * Step2Score),
                bestGrade = _stirGrade,
                menuData = _targetMenu
            };

            StartCoroutine(ShowResultRoutine(result));
        }

        private IEnumerator ShowResultRoutine(CookingResult result)
        {
            if (minigameUI != null) minigameUI.ShowResult(result.bestGrade);

            yield return new WaitForSeconds(1.2f);

            if (minigameUI != null)
            {
                minigameUI.SetGaugeVisible(true);
                minigameUI.Close();
            }

            // 카메라 원복 · 클릭 레이 끄기는 BaseCookingMinigame.FinishMinigame이 이미 했다.
            if (stewPot != null) stewPot.ResetPosition();

            if (result.isSuccess) DispatchCookedFood(result);

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
