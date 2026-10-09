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
    ///   2 핵심  MG_GAUGE_KEEP   : 원을 그려 저어 게이지를 안전 구간에 유지, 중간에 시계 ↔ 반시계가 바뀐다 (+10/2, 예전엔 상하좌우 직선)
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

        // soupTransform을 지웠다 (#48). 국물은 StewPot이 프리팹 안에서 이미 알고 있다 —
        // 컨트롤러가 이름으로 뒤질 이유가 없었고, 뒤져봤자 그 이름의 오브젝트가 없었다.

        [Header("2단계: 세이프존 따라 젓기 (+10/2)")]
        [Tooltip("냄비 안에서 움직이는 세이프존을 국자로 따라간다. 시간 · 원 크기 · 움직임은 이 부품에서.")]
        [SerializeField] private StirZoneGame stirZone;

        // UI가 게이지 막대 위치를 잡을 때 읽는다. 2단계는 이제 막대를 숨기고 냄비 안 원으로 보여 준다.
        [Header("세이프 존 범위 (0.0 ~ 1.0) — 게이지 막대 표시용")]
        [Range(0f, 1f)][SerializeField] private float safeZoneMin = 0.35f;
        [Range(0f, 1f)][SerializeField] private float safeZoneMax = 0.75f;

        [Header("판정 기준 (세이프 존 체류 비율)")]
        [Range(0f, 1f)][SerializeField] private float perfectRatio = 0.55f; // 55% (약 4.4초) 이상 유지 시 PERFECT
        [Range(0f, 1f)][SerializeField] private float goodRatio = 0.35f;    // 35% ~ 54% GOOD
        [Range(0f, 1f)][SerializeField] private float badRatio = 0.15f;     // 15% ~ 34% BAD (구간 현실화)

        [Header("UI 바인딩")]
        [SerializeField] private StewMinigameUI minigameUI;

        [Header("1단계: 재료 받기 (+9/30)")]
        [SerializeField] private CatchGame catchGame;

        [Header("1단계 떨어지는 모양 (+10/7)")]
        [Tooltip("채우면 레시피 재료 모델 대신 이 모양들을 섞어 떨어뜨린다(해물 · 야채). 점수 · 재료 소모는 레시피 그대로. 비우면 레시피 재료 모델.")]
        [SerializeField] private GameObject[] catchVisuals;
        [Tooltip("1단계(재료 받기) 동안 국물 대신 맑은 물을 보인다(StewPot.waterSurface). 2단계가 시작될 때 수프로 돌아간다.")]
        [SerializeField] private bool waterInStep1 = true;

        [Header("3단계: 거품 터뜨리기 (+9/30)")]
        [SerializeField] private PopupTouchGame popupTouch;

        [Header("연출 (+10/6, 이슈 117) — 기획 「화구 사용」 VFX_01 · 「스튜 등 끓이기」 VFX_03")]
        [Tooltip("미니게임 내내 냄비 밑 화구에서 나는 작은 불꽃. 냄비가 움직여도 화구에 남는다.")]
        [SerializeField] private VfxId burnerVfx = VfxId.Flame;
        [Tooltip("미니게임 내내 국물 위로 올라오는 증기. 냄비를 따라다닌다.")]
        [SerializeField] private VfxId steamVfx = VfxId.Steam;
        [SerializeField, Min(0.1f)] private float burnerScale = 1f;
        [SerializeField, Min(0.1f)] private float steamScale = 1f;
        [Tooltip("(+10/6) 화구 불꽃이 나올 자리. 씬에서 끌어 옮겨 맞춘다. 비우면 냄비 바닥 가운데.")]
        [SerializeField] private Transform burnerAnchor;
        [Tooltip("(+10/6) 화구 불꽃을 더 낼 자리들. 같은 효과 · 크기로 하나씩 더 켠다.")]
        [SerializeField] private Transform[] extraBurnerAnchors;
        [Tooltip("(+10/6) 미니게임 동안만 켤 조명 — 김 · 연기에 가려 어두워진 스튜를 밝힌다. 평소엔 꺼 둔다(식당이 밝아지지 않게).")]
        [SerializeField] private Light[] minigameLights;
        [Tooltip("(+10/6) 김이 나올 자리. 냄비를 따라 움직이게 냄비 아래에 두는 게 좋다. 비우면 국물 가운데 5cm 위.")]
        [SerializeField] private Transform steamAnchor;

        [Header("소리 (+10/6, 이슈 117) — 기획 sfx_cook_boil_loop")]
        [Tooltip("미니게임 내내 보글보글.")]
        [SerializeField] private AudioClip boilLoop;
        [SerializeField, Range(0f, 1f)] private float boilVolume = 0.5f;

        private VfxLoop _burner, _steam;
        private readonly System.Collections.Generic.List<VfxLoop> _extraBurners = new();
        private SoundLoop _boil;
        private bool _ambienceOn;   // (+10/6) 디버거가 "지금 도는 중이면 갈아 끼운다"를 판단할 때 본다

        public VfxId SteamVfx => steamVfx;
        public VfxId BurnerVfx => burnerVfx;
        public bool AmbienceOn => _ambienceOn;

        /// <summary>
        /// (+10/6) 개발용 — StewVfxDebug가 부른다. 증기 · 화구 효과를 바꾸고, 미니게임 중이면 그 자리에서 다시 띄운다.
        /// 프리팹 값은 안 바꾼다(플레이를 끄면 원래대로).
        /// </summary>
        public void DebugSetAmbience(VfxId steam, VfxId burner)
        {
            steamVfx = steam;
            burnerVfx = burner;
            if (_ambienceOn) StartAmbience();
        }

        private MenuData _targetMenu;
        private Action<CookingResult> _onCompleteCallback;
        private readonly List<GameObject> _catchItems = new();

        private HitGrade _stirGrade = HitGrade.Miss;

        public float SafeZoneMin => safeZoneMin;
        public float SafeZoneMax => safeZoneMax;

        protected override void EnsureDependencies()
        {
            base.EnsureDependencies();

            // UI는 씬 오브젝트라 프리팹이 못 든다 — 한 번 찾는다 (생선구이와 같다).
            if (minigameUI == null)
                minigameUI = FindFirstObjectByType<StewMinigameUI>(FindObjectsInactive.Include);
        }

        // (+10/7) 냄비는 스튜를 하는 동안만 보인다. 같은 화구를 생선구이 · 야채볶음 팬도 쓰고, 쉴 때는
        // 식당 장식(CookingDeco)의 냄비가 같은 자리에 있다 — 둘 다 켜 두면 다른 미니게임 화면을 가린다.
        protected override void Awake()
        {
            base.Awake();
            SetPotVisible(false);
        }

        private void SetPotVisible(bool visible)
        {
            if (stewPot != null) stewPot.gameObject.SetActive(visible);
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

            SetPotVisible(true);   // (+10/7)
            if (stewPot != null) stewPot.ResetPosition();
            StartAmbience();

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

            if (stewPot != null) stewPot.ShowWater(waterInStep1);   // (+10/7) 1단계는 물

            // 레시피 재료를 필요한 수만큼 떨어뜨린다. 재료 모델은 IngredientData.MinigamePrefab (없으면 구).
            // (+10/7) catchVisuals가 있으면 그걸 섞어서 — CatchGame이 목록을 돌려 쓰니 순서를 섞어 넘겨 겹치는 게 매번 다르게.
            _catchItems.Clear();
            if (catchVisuals != null && catchVisuals.Length > 0)
            {
                foreach (GameObject v in catchVisuals) if (v != null) _catchItems.Add(v);
                for (int i = _catchItems.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    (_catchItems[i], _catchItems[j]) = (_catchItems[j], _catchItems[i]);
                }
            }
            else if (_targetMenu != null && _targetMenu.Recipe != null)
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
        // 2단계: 세이프존 따라 젓기 (+10/2 — 예전엔 상하좌우 드래그로 게이지 유지)
        // ==========================================
        protected override void OnStep2Start()
        {
            if (stewPot != null) stewPot.ShowWater(false);   // (+10/7) 2단계부터 수프

            if (minigameUI != null)
            {
                minigameUI.SetGaugeVisible(true);   // (+10/7) 화면 옆 세로 게이지 — 안이면 차고 밖이면 준다
                minigameUI.SetGuide("국자를 초록 원 안에 두세요!", "원을 따라 저으세요");
            }

            if (stirZone == null)
            {
                Debug.LogError($"{name}: stirZone이 비어 있다. 2단계를 건너뛴다.", this);
                return;
            }
            stirZone.Begin();
        }

        protected override void OnStep2Update()
        {
            if (stirZone == null) { FinishStir(1f); return; }

            if (minigameUI != null)
            {
                minigameUI.UpdateTimer(stirZone.TimeLeft01);
                minigameUI.UpdateStirGauge(stirZone.Gauge01, stirZone.IsInside);   // (+10/7)
                minigameUI.SetGuide(stirZone.IsInside ? "좋아요! 계속 따라가세요" : "원을 놓치면 게이지가 줄어요!",
                                    $"평균 {stirZone.Score * 100f:F0}%");
            }

            if (stirZone.IsFinished) FinishStir(stirZone.Score);
        }

        /// <summary>2단계 끝 — 게이지 평균(+10/7, 예전엔 체류 비율)으로 판정하고 배율을 Step2Score로 넘긴다.</summary>
        private void FinishStir(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
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
            StopAmbience();
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

        /// <summary>(+10/7) 영업이 끝나 요리를 버릴 때 — 불꽃 · 증기를 끄고 냄비를 숨긴다.</summary>
        protected override void OnAborted()
        {
            StopAmbience();
            if (minigameUI != null) minigameUI.Close();   // 안내 글자 · 게이지
            if (stewPot != null)
            {
                stewPot.ShowWater(false);
                stewPot.ResetPosition();
            }
            SetPotVisible(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAmbience();
            if (stewPot != null) stewPot.ShowWater(false);   // (+10/7) 1단계 중에 꺼져도 물로 남지 않게
        }

        /// <summary>(+10/6) 화구 불꽃은 냄비 제자리 바닥 — 냄비의 부모(조리대)에 단다. 증기는 국물 면 위 — 냄비에 단다.</summary>
        private void StartAmbience()
        {
            StopAmbience();
            _ambienceOn = true;
            SetLights(true);   // (+10/6)
            _boil = SoundManager.PlayLoop(boilLoop, boilVolume);   // (+10/6)
            if (stewPot == null) return;
            Bounds b = new Bounds(stewPot.transform.position, Vector3.zero);
            bool has = false;
            foreach (Renderer r in stewPot.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            Transform station = stewPot.transform.parent != null ? stewPot.transform.parent : stewPot.transform;
            // (+10/6) 기준점이 있으면 그 자리(오프셋 0), 없으면 예전처럼 계산.
            _burner = burnerAnchor != null
                ? Vfx.PlayLoop(burnerVfx, burnerAnchor, Vector3.zero, burnerScale)
                : Vfx.PlayLoop(burnerVfx, station, station.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z)), burnerScale);
            if (extraBurnerAnchors != null)
                foreach (Transform a in extraBurnerAnchors)
                    if (a != null) _extraBurners.Add(Vfx.PlayLoop(burnerVfx, a, Vector3.zero, burnerScale));
            Vector3 surface = stewPot.HasLiquid ? stewPot.StirCenter : b.center;
            _steam = steamAnchor != null
                ? Vfx.PlayLoop(steamVfx, steamAnchor, Vector3.zero, steamScale)
                : Vfx.PlayLoop(steamVfx, stewPot.transform, stewPot.transform.InverseTransformPoint(surface + Vector3.up * 0.05f), steamScale);
        }

        private void SetLights(bool on)
        {
            if (minigameLights == null) return;
            foreach (Light l in minigameLights) if (l != null) l.enabled = on;
        }

        private void StopAmbience()
        {
            _burner.Stop();
            _steam.Stop();
            _boil.Stop();
            foreach (VfxLoop loop in _extraBurners) loop.Stop();
            _extraBurners.Clear();
            SetLights(false);   // (+10/6)
            _ambienceOn = false;
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
            SetPotVisible(false);   // (+10/7)

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
