using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Economy;
using Marea.Data;
using Marea.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Marea.Field
{
    /// <summary>
    /// 월드에 실물이 있는 시설의 겉모습. 해금 전엔 Broken, 해금 뒤엔 Built를 켠다. (+9/28, 이슈 71)
    /// 레벨 단위(농사 데크의 밭)가 있으면 레벨만큼 켠다.
    ///
    /// 프리팹을 통째로 갈아끼우지 않는다 — Destroy·Instantiate는 씬 참조를 끊고 콜라이더·
    /// NavMesh 장애물도 새로 잡아야 한다. 아트가 모양을 바꿀 땐 Broken·Built 자식 안만 바꾼다.
    ///
    /// 상태를 들지 않는다. 매번 FacilityLevels에서 읽는다 — 여기서 따로 들면 둘이 어긋난다.
    /// </summary>
    public class FacilitySite : MonoBehaviour
    {
        [Tooltip("어느 시설의 실물인가.")]
        [SerializeField] private FacilityKind kind;

        [Tooltip("해금 전 모습. 이 안의 모델은 갈아끼워도 된다.")]
        [SerializeField] private GameObject brokenVisual;

        [Tooltip("해금 뒤 모습. 이 안의 모델은 갈아끼워도 된다.")]
        [SerializeField] private GameObject builtVisual;

        // (+10/2) 레벨 × 개수였다가, 기획 표의 FARM_SLOT_ADD(재배 공간 +n칸)로 바뀌었다.
        // 시설이 0레벨에서 시작해서 예전 식이면 해금해도 밭이 0칸이 된다.
        [Header("칸 (선택)")]
        [Tooltip("앞에서부터 켜지는 것들. 농사 데크의 밭 자리다. 탐사정처럼 늘어나는 게 없으면 비워둔다.")]
        [SerializeField] private GameObject[] levelSlots;

        [Tooltip("해금하면 처음 켜지는 칸 수. 업그레이드의 FARM_SLOT_ADD 만큼 더 켜진다.")]
        [FormerlySerializedAs("slotsPerLevel")]
        [SerializeField, Min(1)] private int baseSlots = 2;

        // (+10/6) 식당 부지 — 영업 시설은 처음부터 열려 있어서 "해금되면 완성"으로는 못 쓴다. 레벨로 정한다.
        [Header("완성 조건 (+10/6)")]
        [Tooltip("0이면 해금되면 완성(농사 데크 · 탐사정). 1 이상이면 그 레벨이 돼야 폐허에서 완성으로 — "
               + "처음부터 열린 시설의 부지처럼. 기획 영업 시설 4레벨 「부지 확장」.")]
        [SerializeField, Min(0)] private int requiredLevel;

        [Header("연출 (+10/6, 이슈 117)")]
        [Tooltip("해금 · 업그레이드 완료 순간 바닥에서 퍼지는 효과. 기획 「시설 업그레이드·확장 완료」 VFX_10.")]
        [SerializeField] private VfxId dustVfx = VfxId.Dust;
        [Tooltip("그 위에서 터지는 빛. 기획 VFX_02.")]
        [SerializeField] private VfxId doneVfx = VfxId.Sparkle;
        [Tooltip("효과 크기 배율. 시설 크기에 맞춰 자동으로 키운 값에 곱한다.")]
        [SerializeField, Min(0.1f)] private float vfxScale = 1f;
        [Tooltip("(+10/6) 모습이 바뀌는 순간 \"뚝딱/쿵\" 설치음. 기획 UPG-02 sfx_upgrade_apply. 비우면 조용하다.")]
        [SerializeField] private AudioClip doneClip;
        [SerializeField, Range(0f, 1f)] private float doneVolume = 0.9f;

        // (+10/6, 이슈 117) 해금 · 새 칸이 생기는 레벨업 — 카메라가 와서 보고, 그다음 바뀐다.
        // 순서: 카메라 이동(시간 제한만 있음) → 도착 → lookHold → revealDelay → 이펙트 + 모양 변경 → afterReveal → 복귀.
        [Header("해금 연출 (+10/6, 이슈 117)")]
        [Tooltip("카메라가 도착한 뒤 바뀌기 전 모습을 보여주는 시간.")]
        [SerializeField, Min(0f)] private float lookHold = 0.3f;
        [Tooltip("그다음 이펙트 · 모양 변경까지 더 기다리는 시간.")]
        [SerializeField, Min(0f)] private float revealDelay = 0.0f;
        [Tooltip("바뀐 뒤 플레이어로 돌아가기 전까지 보여주는 시간.")]
        [SerializeField, Min(0f)] private float afterReveal = 1f;
        [Tooltip("카메라가 이 시간 안에 못 오면(카메라가 다른 데 붙잡혀 있는 등) 기다리지 않고 진행한다.")]
        [SerializeField, Min(0.1f)] private float arriveTimeout = 2f;

        // (+10/6) 농사 데크 — 다리에서 한 번, 열린 데크에서 한 번. 비우면 예전처럼 한 번에 바뀐다.
        [Header("해금 단계 (+10/6) — 비우면 한 번에")]
        [Tooltip("해금을 여러 번에 나눠 보여준다. 단계마다 카메라가 그 부품으로 가서 이펙트와 함께 켠다. "
               + "부품은 builtVisual 안의 자식이어야 한다. 밭 칸은 마지막 단계에 같이 켜진다.")]
        [SerializeField] private GameObject[] revealSteps;
        [Tooltip("두 번째 단계부터 — 카메라가 도착한 뒤 켜기까지(초). 첫 단계는 lookHold + revealDelay.")]
        [SerializeField, Min(0f)] private float stepDelay = 0.5f;
        [Tooltip("단계 사이 — 앞 단계 이펙트를 보여주고 다음 부품으로 넘어가기까지(초).")]
        [SerializeField, Min(0f)] private float stepGap = 1f;

        private FacilityLevels _levels;
        private Coroutine _reveal;
        private CameraFollow _revealCam;
        private PlayerController _revealPlayer;
        private readonly List<UiPanel> _hiddenPanels = new();

        /// <summary>이 실물이 어느 시설인가. 해금 클릭(FacilityUnlockClick)이 읽는다.</summary>
        public FacilityKind Kind => kind;

        private void Awake()
        {
            if (brokenVisual == null || builtVisual == null)
                Debug.LogError($"{name}: FacilitySite의 brokenVisual·builtVisual 중 비어 있는 게 있다. "
                             + "해금해도 모습이 안 바뀐다.", this);

            // (+10/6) builtVisual 밖의 부품은 해금 전에도 켜져 있어서 단계 연출이 거짓말이 된다.
            if (revealSteps != null && builtVisual != null)
                foreach (GameObject part in revealSteps)
                    if (part == null || !part.transform.IsChildOf(builtVisual.transform))
                        Debug.LogError($"{name}: revealSteps의 '{(part != null ? part.name : "빈 칸")}'이 builtVisual 안에 없다. "
                                     + "그 단계는 켜고 끄지 못한다.", this);
        }

        private bool HasSteps => revealSteps != null && revealSteps.Length > 0;

        // FacilityLevels.Awake가 표를 만든 뒤라야 IsUnlocked가 맞는 값을 준다.
        private void Start()
        {
            _levels = FacilityLevels.Instance;
            if (_levels == null)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 없다. {kind} 시설이 부서진 채로 남는다.", this);
                Apply(false, 0);
                return;
            }

            if (_levels.DataOf(kind) != null && levelSlots != null && levelSlots.Length > 0)
            {
                // 최대 레벨까지 켤 자리가 모자라면 그 업그레이드가 겉으로 아무 일도 안 한다.
                FacilityData data = _levels.DataOf(kind);
                int needed = baseSlots + Mathf.RoundToInt(data.EffectValueAt(data.MaxLevel, FacilityEffectType.FarmSlotAdd));
                if (levelSlots.Length < needed)
                    Debug.LogError($"{name}: levelSlots가 {levelSlots.Length}개인데 최대 레벨에 "
                                 + $"{needed}개가 필요하다. 자리를 더 깔거나 baseSlots를 줄일 것.", this);
            }

            FacilityData site = _levels.DataOf(kind);
            if (site != null && requiredLevel > site.MaxLevel)
                Debug.LogError($"{name}: requiredLevel {requiredLevel}이 {kind} 최대 레벨 {site.MaxLevel}보다 크다. 영영 폐허로 남는다.", this);

            _levels.OnUnlocked += HandleUnlocked;
            _levels.OnLevelChanged += HandleLevelChanged;
            Refresh();
        }

        // 연출 도중 꺼지면 카메라 · 플레이어를 놓아주고, 미뤄둔 모양 변경을 바로 적용한다.
        // 안 그러면 플레이어가 잠긴 채로 남고 카메라가 시설만 본다.
        private void OnDisable()
        {
            if (_reveal == null) return;
            StopCoroutine(_reveal);
            EndReveal();
            if (_levels != null) Refresh();
        }

        private void OnDestroy()
        {
            if (_levels == null) return;
            _levels.OnUnlocked -= HandleUnlocked;
            _levels.OnLevelChanged -= HandleLevelChanged;
        }

        private void HandleUnlocked(FacilityKind changed)
        {
            if (changed != kind) return;
            if (!IsBuilt) { Refresh(); return; }   // (+10/6) 해금됐지만 아직 requiredLevel 전 — 폐허 그대로
            RevealBuilt();
        }

        /// <summary>폐허 → 완성 연출. 단계가 있으면 단계별, 없으면 폐허 자리를 한 번 본다.</summary>
        private void RevealBuilt()
        {
            if (HasSteps) StartReveal(RevealSteps());
            else StartReveal(Reveal(BoundsOf(brokenVisual != null && brokenVisual.activeInHierarchy ? brokenVisual : gameObject).center));
        }

        /// <summary>(+10/6) 완성된 모습이어야 하나 — 해금 + requiredLevel 이상.</summary>
        private bool IsBuilt => _levels.IsUnlocked(kind) && _levels.LevelOf(kind) >= requiredLevel;

        // (+10/6) 새 칸이 생기는 레벨업만 연출한다. 수치만 오르는 레벨업은 예전처럼 그 자리에서 이펙트.
        private void HandleLevelChanged(FacilityKind changed, int level)
        {
            if (changed != kind) return;
            if (_reveal != null) return;   // 연출 중 — 끝날 때 Refresh가 최신 상태를 읽는다

            // (+10/6) requiredLevel에 막 닿았다 — 폐허가 보이고 있는데 이제 완성이어야 한다. 해금 때와 같은 연출.
            bool showingBroken = brokenVisual != null && brokenVisual.activeSelf;
            if (requiredLevel > 0 && showingBroken && IsBuilt) { RevealBuilt(); return; }
            if (!IsBuilt) { Refresh(); return; }   // 아직 폐허 — 폐허 위에 완성 이펙트를 터뜨리지 않는다

            int shown = ActiveSlotCount();
            int target = _levels.IsUnlocked(kind) ? Mathf.Min(TargetSlots(), levelSlots?.Length ?? 0) : 0;
            if (target > shown)
            {
                StartReveal(Reveal(CenterOfSlots(shown, target)));
                return;
            }
            Refresh();
            PlayDone();
        }

        private void StartReveal(IEnumerator routine)
        {
            if (_reveal != null) return;   // 이미 도는 중 — 끝에서 Refresh가 최신 상태를 읽는다

            _revealCam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (_revealCam == null)
            {
                // 연출만 빠지고 해금은 보여야 한다 — 여기서 멈추면 시설이 영영 안 바뀐다.
                Debug.LogError($"{name}: MainCamera에 CameraFollow가 없다. 해금 연출 없이 바로 바꾼다.", this);
                Refresh();
                PlayDone();
                return;
            }
            // (+10/6) 열린 창은 가렸다가 연출이 끝나면 되돌린다 — 창이 화면을 가리면 연출이 안 보인다.
            // 닫지 않고 가리는 이유는 UiPanel.Suspend 주석.
            foreach (UiPanel panel in FindObjectsByType<UiPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (panel.IsVisible) { panel.Suspend(); _hiddenPanels.Add(panel); }

            _revealPlayer = FindAnyObjectByType<PlayerController>();
            if (_revealPlayer == null)
                Debug.LogError($"{name}: 씬에 PlayerController가 없다. 해금 연출 중 입력을 못 막는다.", this);

            if (_revealPlayer != null) _revealPlayer.BeginBusy();
            _reveal = StartCoroutine(routine);
        }

        // 날아가는 시간은 1초에 안 넣는다 — 도착부터 잰다.
        private IEnumerator FlyTo(Vector3 point)
        {
            _revealCam.FocusOn(point);
            for (float t = 0f; !_revealCam.IsAtFocus && t < arriveTimeout; t += Time.deltaTime)
                yield return null;
        }

        private IEnumerator Reveal(Vector3 focusPoint)
        {
            yield return FlyTo(focusPoint);
            yield return new WaitForSeconds(lookHold + revealDelay);
            Refresh();
            PlayDone();
            yield return new WaitForSeconds(afterReveal);

            EndReveal();
        }

        /// <summary>
        /// (+10/6) 단계별 해금 — 부품마다 카메라가 가서 이펙트와 함께 켠다. 마지막 단계에서 나머지(밭 칸)까지 Refresh로 맞춘다.
        /// 부품은 아직 꺼져 있어 렌더러 상자가 비므로 메시 상자로 초점을 잡는다.
        /// </summary>
        private IEnumerator RevealSteps()
        {
            int last = revealSteps.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                GameObject part = revealSteps[i];
                yield return FlyTo(part != null ? MeshBounds(part).center : transform.position);
                yield return new WaitForSeconds(i == 0 ? lookHold + revealDelay : stepDelay);

                ShowStep(i);
                PlayDoneAt(part != null ? BoundsOf(part) : BoundsOf(builtVisual != null ? builtVisual : gameObject));
                yield return new WaitForSeconds(i < last ? stepGap : afterReveal);
            }
            EndReveal();
        }

        /// <summary>i번 단계까지 켠다. 마지막이면 Refresh — 해금 상태 그대로(밭 칸 포함).</summary>
        private void ShowStep(int i)
        {
            if (i >= revealSteps.Length - 1) { Refresh(); return; }
            if (brokenVisual != null) brokenVisual.SetActive(false);
            if (builtVisual != null) builtVisual.SetActive(true);
            for (int j = 0; j < revealSteps.Length; j++)
                if (revealSteps[j] != null) revealSteps[j].SetActive(j <= i);
        }

        private void EndReveal()
        {
            if (_revealCam != null) _revealCam.ClearFocus();
            if (_revealPlayer != null) _revealPlayer.EndBusy();
            foreach (UiPanel panel in _hiddenPanels)
                if (panel != null) panel.Resume();   // 그사이 진짜로 닫힌 창(해금 확인 창)은 Resume이 아무것도 안 한다
            _hiddenPanels.Clear();
            _revealCam = null;
            _revealPlayer = null;
            _reveal = null;
        }

        /// <summary>
        /// (+10/6) 해금 · 업그레이드 완료 — 지금 보이는 모습 아래에서 먼지, 가운데서 빛.
        /// 「완성 시설이 잘 보이도록 제한」(기획) — 크기는 시설 폭에 맞추되 상한을 둔다.
        /// </summary>
        private void PlayDone()
            => PlayDoneAt(BoundsOf(builtVisual != null && builtVisual.activeInHierarchy ? builtVisual : gameObject));

        private void PlayDoneAt(Bounds b)
        {
            float size = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 1f, 4f) * vfxScale;
            Vfx.Play(dustVfx, new Vector3(b.center.x, b.min.y, b.center.z), size);
            Vfx.Play(doneVfx, b.center + Vector3.up * b.extents.y * 0.5f, size);
            SoundManager.Play(doneClip, doneVolume);   // (+10/6) 연출로 카메라가 와서 보고 있으니 화면 소리로
        }

        /// <summary>켜져 있는 렌더러를 다 감싼 상자. 꺼진 렌더러는 bounds가 비어 있어 못 쓴다.</summary>
        private static Bounds BoundsOf(GameObject shown)
        {
            Bounds b = new Bounds(shown.transform.position, Vector3.one);
            bool has = false;
            foreach (Renderer r in shown.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>(+10/6) 꺼져 있어도 되는 상자 — 메시 상자를 월드로 옮겨 감싼다. 메시가 없으면 그 자리.</summary>
        private static Bounds MeshBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            void Add(Mesh mesh, Transform t)
            {
                if (mesh == null) return;
                Bounds lb = mesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = lb.center + Vector3.Scale(lb.extents,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                    Vector3 w = t.TransformPoint(corner);
                    if (!has) { b = new Bounds(w, Vector3.zero); has = true; }
                    else b.Encapsulate(w);
                }
            }
            foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true)) Add(f.sharedMesh, f.transform);
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(r.sharedMesh, r.transform);
            return b;
        }

        // 새로 켜질 칸들은 아직 꺼져 있어 렌더러 상자를 못 쓴다 — 위치 평균으로 본다.
        private Vector3 CenterOfSlots(int from, int to)
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            for (int i = from; i < to; i++)
                if (levelSlots[i] != null) { sum += levelSlots[i].transform.position; n++; }
            return n > 0 ? sum / n : transform.position;
        }

        private int ActiveSlotCount()
        {
            if (levelSlots == null) return 0;
            int n = 0;
            foreach (GameObject slot in levelSlots)
                if (slot != null && slot.activeSelf) n++;
            return n;
        }

        private int TargetSlots()
            => baseSlots + Mathf.RoundToInt(_levels.EffectValue(kind, FacilityEffectType.FarmSlotAdd));

        private void Refresh()
            => Apply(IsBuilt, TargetSlots());

        private void Apply(bool unlocked, int slots)
        {
            if (brokenVisual != null) brokenVisual.SetActive(!unlocked);
            if (builtVisual != null) builtVisual.SetActive(unlocked);
            // (+10/6) 단계 연출이 중간에 끈 부품을 해금 상태에선 다 켠다. 잠긴 동안은 builtVisual째 꺼져 있어 손대지 않는다.
            if (unlocked && revealSteps != null)
                foreach (GameObject part in revealSteps)
                    if (part != null) part.SetActive(true);

            if (levelSlots == null) return;

            int active = unlocked ? slots : 0;
            for (int i = 0; i < levelSlots.Length; i++)
                if (levelSlots[i] != null) levelSlots[i].SetActive(i < active);
        }
    }
}
