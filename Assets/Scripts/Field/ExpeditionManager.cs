using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Economy;
using Marea.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marea.Field
{
    /// <summary>
    /// 탐사정 하나의 파견 · 타이머 · 귀환 · 보상. (+9/28, 이슈 73)
    ///
    /// 선착장(ExpeditionDock)은 클릭을 받아 여기로 넘기기만 한다.
    /// 기획 8페이지의 첫 구현 그대로다 — 귀환한 탐사정을 클릭하면 보상이 자동으로 들어오고,
    /// 연출은 배가 월드에서 사라졌다가 다시 나타나는 것. 결과창은 없다.
    ///
    /// (+9/30) 떠날 때만 연출이 있다 — 배가 바라보는 방향으로 가속하며 나아가다 끝에 가라앉아 사라진다.
    /// 타이머는 파견한 순간부터 돈다. 연출보다 탐사가 먼저 끝나면 연출을 끊고 제자리로 돌려놓는다.
    /// 귀환은 여전히 제자리에 바로 나타난다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 대기 상태다 (보류).
    ///
    /// (+10/2, 이슈 92 — 와이어프레임 「탐사 중 / 귀환」) 결과창이 생겼다.
    /// 보상은 귀환하는 순간 뽑아 <see cref="PendingReward"/>에 들고, 결과창 [확인]에서
    /// <see cref="Collect"/>가 창고에 넣는다. 확인 전에 창을 닫으면 귀환 상태 그대로라 다시 열면 같은
    /// 결과가 보이고, 확인 뒤엔 대기로 돌아가 두 번 받을 길이 없다.
    /// 탐사 중에 누르면 남은 시간 창이 뜬다 — 그래서 탐사 중에도 클릭을 받는다.
    /// </summary>
    public class ExpeditionManager : MonoBehaviour
    {
        public enum State { Idle, Away, Returned }

        [Tooltip("갈 수 있는 해역. 지역 선택 창에 이 순서대로 뜬다.")]
        [FormerlySerializedAs("regions")]
        [SerializeField] private ExpeditionAreaData[] areas;

        [Tooltip("탐사 중에 사라질 배. 선착장 발판은 여기 넣지 않는다.")]
        [SerializeField] private GameObject boat;

        // (+9/28) 월드 텍스트(TMP)를 쓰다가 화면 라벨(WorldLabelUI)로 바꿨다 — junhyuk_main 에서
        // 아트 천막 아래 가려 안 보였다. 여기는 라벨을 띄울 월드 위치만 든다.
        [Tooltip("남은 시간 · 귀환 라벨을 띄울 위치. 비우면 이 오브젝트 위 2.5m.")]
        [SerializeField] private Transform labelAnchor;

        [Tooltip("귀환했을 때 켜는 표식(느낌표).")]
        [SerializeField] private GameObject returnedMarker;

        [Header("연출 (+10/6, 이슈 117) — 기획 「출항·귀환」 VFX_04 · 「탐사 보상 획득」 VFX_02")]
        [Tooltip("출항 · 귀환 순간 배에서 튀는 물방울.")]
        [SerializeField] private VfxId splashVfx = VfxId.Splash;
        [Tooltip("떠나는 동안 배 뒤에 남는 항적.")]
        [SerializeField] private VfxId wakeVfx = VfxId.Wake;
        [SerializeField, Min(0.05f)] private float wakeInterval = 0.2f;
        [Tooltip("보상을 받을 때 배 위에서 반짝.")]
        [SerializeField] private VfxId rewardVfx = VfxId.Sparkle;

        [Header("소리 (+10/6, 이슈 117) — 기획 EXP-01 sfx_explore_depart")]
        [Tooltip("파견하는 순간 출발 엔진음.")]
        [SerializeField] private AudioClip departClip;
        [Tooltip("돌아온 순간 뱃고동.")]
        [SerializeField] private AudioClip returnClip;
        [SerializeField, Range(0f, 1f)] private float clipVolume = 0.9f;

        [Header("떠날 때 연출 (+9/30)")]
        [Tooltip("파견하고 배가 사라질 때까지 걸리는 초.")]
        [SerializeField, Min(0f)] private float departSeconds = 2.5f;

        [Tooltip("그동안 배가 바라보는 방향으로 나아가는 거리(m). 서서히 가속한다.")]
        [SerializeField, Min(0f)] private float departDistance = 8f;

        [Tooltip("끝의 몇 초 동안 가라앉는가. departSeconds보다 길면 처음부터 가라앉는다.")]
        [SerializeField, Min(0f)] private float sinkSeconds = 1f;

        [Tooltip("가라앉는 깊이(m). 배 높이보다 깊어야 물속으로 완전히 들어간다.")]
        [SerializeField, Min(0f)] private float sinkDepth = 3f;

        private ExpeditionUI _ui;
        private ExpeditionStatusUI _statusUI;
        private readonly List<(IngredientData item, int amount)> _pending = new();
        private WorldLabelUI _labels;
        private ExpeditionAreaData _current;
        private float _returnAt;
        private Vector3 _boatHome;      // 배의 제자리(로컬). 연출이 끝나거나 끊기면 여기로 돌린다.
        private Coroutine _depart;

        public State Current { get; private set; } = State.Idle;
        public ExpeditionAreaData[] Areas => areas;

        /// <summary>탐사 중이거나 귀환한 해역. 대기면 null.</summary>
        public ExpeditionAreaData CurrentArea => _current;

        /// <summary>귀환까지 남은 초. 탐사 중이 아니면 0.</summary>
        public float RemainingSeconds => Current == State.Away ? Mathf.Max(0f, _returnAt - Time.time) : 0f;

        /// <summary>귀환해서 아직 안 받은 보상. 결과창이 그린다.</summary>
        public IReadOnlyList<(IngredientData item, int amount)> PendingReward => _pending;

        /// <summary>대기·탐사 중·귀환이 바뀔 때. 메인 HUD 알람이 구독한다. (+10/2, 이슈 92)</summary>
        public event Action<State> OnStateChanged;

        private void Awake()
        {
            _ui = FindAnyObjectByType<ExpeditionUI>(FindObjectsInactive.Include);
            _statusUI = FindAnyObjectByType<ExpeditionStatusUI>(FindObjectsInactive.Include);
            _labels = FindAnyObjectByType<WorldLabelUI>(FindObjectsInactive.Include);

            if (areas == null || areas.Length == 0)
                Debug.LogError($"{name}: ExpeditionManager.areas가 비어 있다. 보낼 곳이 없다.", this);
            if (boat == null)
                Debug.LogError($"{name}: ExpeditionManager.boat가 비어 있다. 파견해도 배가 안 사라진다.", this);
            if (_ui == null)
                Debug.LogError($"{name}: 씬에 ExpeditionUI가 없다. 탐사정을 눌러도 지역 창이 안 뜬다.", this);
            if (_labels == null)
                Debug.LogError($"{name}: 씬에 WorldLabelUI가 없다. 남은 시간과 귀환 표시가 안 뜬다.", this);
            if (_statusUI == null)
                Debug.LogError($"{name}: 씬에 ExpeditionStatusUI가 없다. 탐사 중 · 귀환 결과 창이 안 뜬다.", this);

            if (boat != null) _boatHome = boat.transform.localPosition;
            Show();
        }

        private void Update()
        {
            if (Current == State.Away && Time.time >= _returnAt)
            {
                RollReward();
                Current = State.Returned;
                Show();
                // (+10/7) 떠날 때를 거꾸로 — 가라앉았던 자리에서 떠올라 같은 길을 뒷걸음으로 되돌아온다.
                if (boat != null && isActiveAndEnabled) _depart = StartCoroutine(Return());
                SoundManager.Play(returnClip, clipVolume);   // (+10/6) 화면 밖에서 돌아와도 알게 화면 소리로
            }

            // 라벨은 보이고 싶은 프레임마다 부른다. 대기 중엔 안 불러서 저절로 꺼진다.
            if (_labels == null) return;
            if (Current == State.Away)
                _labels.Set(this, LabelPosition, TimeText.Format(_returnAt - Time.time));
            else if (Current == State.Returned)
                _labels.Set(this, LabelPosition, "귀환");
        }

        private Vector3 LabelPosition => labelAnchor != null ? labelAnchor.position : transform.position + Vector3.up * 2.5f;

        /// <summary>
        /// 언제든 받는다. 탐사 중엔 남은 시간 창만 뜨고 재파견은 안 된다 (기획 8 파견 3번은 그대로).
        /// (+10/2) 예전엔 탐사 중 클릭을 막았다 — 결과창이 생기면서 탐사 중 창이 같이 생겼다.
        /// </summary>
        public bool CanInteract => true;

        /// <summary>
        /// (+10/9, 이슈 128 「자원 수령」) 메인 HUD 탐사 버튼. 귀환이면 결과 창 대신 플레이어를 배 앞으로 걸어가게 한다 —
        /// 도착하면 선착장 상호작용으로 결과 창이 열린다. 대기 · 탐사 중은 예전처럼 바로 창을 연다.
        /// </summary>
        public void InteractFromHud()
        {
            // 배 앞 판정은 선착장(ExpeditionDock.Interact)이 한다 — 멀면 걸어가게 하고, 가까우면 창을 연다.
            ExpeditionDock dock = Current == State.Returned ? GetComponentInChildren<ExpeditionDock>() : null;
            PlayerController player = dock != null ? FindAnyObjectByType<PlayerController>() : null;
            if (player != null) { dock.Interact(player); return; }
            if (Current == State.Returned)
                Debug.LogError($"{name}: 켜진 ExpeditionDock이나 PlayerController가 없다. 배 앞으로 못 보내서 결과 창을 바로 연다.", this);
            Interact();
        }

        /// <summary>대기면 지역 창, 탐사 중이면 남은 시간 창, 귀환이면 결과 창.</summary>
        public void Interact()
        {
            switch (Current)
            {
                case State.Idle:
                    if (_ui != null) _ui.Open(this);
                    break;
                default:
                    if (_statusUI != null) _statusUI.Open(this);
                    break;
            }
        }

        // ── (+10/2) 탐사 시설 업그레이드 효과 (기획 업그레이드 테이블) ──

        /// <summary>업그레이드가 반영된 탐사 시간. 탐사 시간 감소(EXPLORE_TIME_REDUCE, 누적 총합).</summary>
        public float DurationOf(ExpeditionAreaData area)
        {
            if (area == null) return 0f;
            float reduce = FacilityLevels.Instance != null
                ? FacilityLevels.Instance.EffectValue(FacilityKind.Explorer, FacilityEffectType.ExploreTimeReduce)
                : 0f;
            return area.DurationSeconds * Mathf.Clamp01(1f - reduce);
        }

        /// <summary>해역 해금(REGION_UNLOCK). 키가 없는 해역은 늘 열려 있다. 잠겼으면 열리는 레벨을 준다.</summary>
        public bool IsAreaUnlocked(ExpeditionAreaData area, out int requiredLevel)
        {
            requiredLevel = 0;
            if (area == null || string.IsNullOrWhiteSpace(area.UnlockKey) || FacilityLevels.Instance == null) return true;
            return FacilityLevels.Instance.IsTargetUnlocked(FacilityKind.Explorer, FacilityEffectType.RegionUnlock, area.UnlockKey, out requiredLevel);
        }

        /// <summary>탐사 획득량 증가(EXPLORE_REWARD_ADD) — 수량 × (1 + 값). 소수점은 그 확률로 1개 더.</summary>
        private static int ApplyRewardBonus(int amount)
        {
            float add = FacilityLevels.Instance != null
                ? FacilityLevels.Instance.EffectValue(FacilityKind.Explorer, FacilityEffectType.ExploreRewardAdd)
                : 0f;
            if (add <= 0f) return amount;
            float total = amount * (1f + add);
            int whole = Mathf.FloorToInt(total);
            return whole + (UnityEngine.Random.value < total - whole ? 1 : 0);
        }

        /// <summary>이 해역으로 보낸다. 대기가 아니거나 잠긴 해역이면 false.</summary>
        public bool TryDispatch(ExpeditionAreaData area)
        {
            if (Current != State.Idle || area == null) return false;
            if (!IsAreaUnlocked(area, out _)) return false;

            _current = area;
            _returnAt = Time.time + DurationOf(area);
            Current = State.Away;
            Show();
            SoundManager.PlayAt(departClip, boat != null ? boat.transform.position : transform.position, clipVolume);   // (+10/6)
            return true;
        }

        /// <summary>
        /// 귀환하는 순간 보상을 정한다. 결과창이 열릴 때마다 뽑으면 창을 여닫아 원하는 보상을 고를 수 있다.
        /// 보상 그룹에서 가중치로 한 줄을 뽑는다. 몇 번 뽑는지는 기획에 없어서 한 번이다 (ExpeditionRewardData).
        /// </summary>
        private void RollReward()
        {
            _pending.Clear();
            ExpeditionRewardData group = _current != null ? _current.RewardGroup : null;
            if (group != null && group.TryPick(UnityEngine.Random.value, out ExpeditionRewardEntry picked))
            {
                int max = Mathf.Max(picked.amountMin, picked.amountMax);
                _pending.Add((picked.item, ApplyRewardBonus(UnityEngine.Random.Range(picked.amountMin, max + 1))));
            }
            else
            {
                // 빈손으로 귀환시킨다. 그룹이 비었다고 탐사정을 탐사 중에 묶어두면 다시 못 보낸다.
                Debug.LogError($"{name}: '{(_current != null ? _current.name : "?")}'의 보상 그룹이 비었거나 가중치 합이 0이다. 빈손으로 귀환한다.", _current);
            }
        }

        /// <summary>결과창 [확인]. 귀환 상태일 때만 보상을 넣고 대기로 돌린다 — 두 번 불러도 한 번만 들어간다.</summary>
        public bool Collect()
        {
            if (Current != State.Returned) return false;

            Warehouse warehouse = Warehouse.Instance;
            if (warehouse == null)
            {
                // 받기 전에 멈춘다. 대기로 돌려버리면 보상이 그냥 사라진다.
                Debug.LogError($"{name}: 씬에 Warehouse가 없다. 보상을 받을 곳이 없어 귀환 상태로 둔다.", this);
                return false;
            }

            foreach (var (item, amount) in _pending)
                if (item != null && amount > 0) warehouse.Add(item, amount);

            _pending.Clear();
            _current = null;
            Current = State.Idle;
            Show();
            if (boat != null) Vfx.Play(rewardVfx, boat.transform.position + Vector3.up * 1.5f, 1.2f);   // (+10/6)
            return true;
        }

        private void Show()
        {
            if (boat != null)
            {
                if (Current == State.Away)
                {
                    // 떠나는 중이면 그대로 둔다. 이미 꺼져 있으면(연출이 끝났으면) 다시 틀지 않는다.
                    if (_depart == null && boat.activeSelf) _depart = StartCoroutine(Depart());
                }
                else
                {
                    StopDepart();
                    boat.SetActive(true);
                }
            }
            if (returnedMarker != null) returnedMarker.SetActive(Current == State.Returned);
            OnStateChanged?.Invoke(Current);
        }

        /// <summary>바라보는 방향으로 가속하며 나아가다 끝에 가라앉고 꺼진다. (+9/30)</summary>
        private IEnumerator Depart()
        {
            Transform t = boat.transform;
            Vector3 start = t.position;
            Vector3 dir = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;

            // (+10/6) 출항 순간 물방울, 나아가는 동안 뒤에 항적.
            Vfx.Play(splashVfx, start, 2f);
            float nextWake = 0f;

            for (float e = 0f; e < departSeconds; e += Time.deltaTime)
            {
                t.position = DepartPose(start, dir, e);
                if (e >= nextWake)
                {
                    nextWake = e + wakeInterval;
                    Vfx.Play(wakeVfx, new Vector3(t.position.x, start.y, t.position.z) - dir * 1f, 1.5f);
                }
                yield return null;
            }

            boat.SetActive(false);
            t.localPosition = _boatHome;
            _depart = null;
        }

        /// <summary>(+10/7) 출항을 그대로 거꾸로 재생한다 — 가라앉은 자리에서 떠올라 뒷걸음으로 제자리에 선다.</summary>
        private IEnumerator Return()
        {
            Transform t = boat.transform;
            t.localPosition = _boatHome;
            Vector3 home = t.position;
            Vector3 dir = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            boat.SetActive(true);
            float nextWake = 0f;
            bool surfaced = false;
            float sinkFrom = Mathf.Max(0f, departSeconds - sinkSeconds);

            for (float e = departSeconds; e > 0f; e -= Time.deltaTime)
            {
                t.position = DepartPose(home, dir, e);
                if (!surfaced && e <= sinkFrom)
                {
                    surfaced = true;
                    Vfx.Play(splashVfx, new Vector3(t.position.x, home.y, t.position.z), 2f);   // 물 위로 올라온 순간
                }
                if (departSeconds - e >= nextWake)
                {
                    nextWake = departSeconds - e + wakeInterval;
                    Vfx.Play(wakeVfx, new Vector3(t.position.x, home.y, t.position.z) + dir * 1f, 1.5f);
                }
                yield return null;
            }

            t.localPosition = _boatHome;
            _depart = null;
        }

        /// <summary>출항 e초 뒤의 배 위치. 출항 · 귀환이 같은 길을 쓰게 한 곳에서 계산한다.</summary>
        private Vector3 DepartPose(Vector3 start, Vector3 dir, float e)
        {
            float k = departSeconds > 0f ? e / departSeconds : 1f;
            float sinkFrom = Mathf.Max(0f, departSeconds - sinkSeconds);
            float sink = sinkSeconds > 0f ? Mathf.Clamp01((e - sinkFrom) / sinkSeconds) : 0f;
            return start + dir * (departDistance * k * k) + Vector3.down * (sinkDepth * sink * sink);
        }

        private void StopDepart()
        {
            if (_depart != null)
            {
                StopCoroutine(_depart);
                _depart = null;
            }
            if (boat != null) boat.transform.localPosition = _boatHome;
        }
    }
}
