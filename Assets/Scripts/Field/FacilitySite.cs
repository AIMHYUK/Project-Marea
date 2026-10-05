using Marea.Core;
using Marea.Economy;
using Marea.Data;
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

        [Header("연출 (+10/6, 이슈 117)")]
        [Tooltip("해금 · 업그레이드 완료 순간 바닥에서 퍼지는 효과. 기획 「시설 업그레이드·확장 완료」 VFX_10.")]
        [SerializeField] private VfxId dustVfx = VfxId.Dust;
        [Tooltip("그 위에서 터지는 빛. 기획 VFX_02.")]
        [SerializeField] private VfxId doneVfx = VfxId.Sparkle;
        [Tooltip("효과 크기 배율. 시설 크기에 맞춰 자동으로 키운 값에 곱한다.")]
        [SerializeField, Min(0.1f)] private float vfxScale = 1f;

        private FacilityLevels _levels;

        /// <summary>이 실물이 어느 시설인가. 해금 클릭(FacilityUnlockClick)이 읽는다.</summary>
        public FacilityKind Kind => kind;

        private void Awake()
        {
            if (brokenVisual == null || builtVisual == null)
                Debug.LogError($"{name}: FacilitySite의 brokenVisual·builtVisual 중 비어 있는 게 있다. "
                             + "해금해도 모습이 안 바뀐다.", this);
        }

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

            _levels.OnUnlocked += HandleUnlocked;
            _levels.OnLevelChanged += HandleLevelChanged;
            Refresh();
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
            Refresh();
            PlayDone();
        }

        private void HandleLevelChanged(FacilityKind changed, int level)
        {
            if (changed != kind) return;
            Refresh();
            PlayDone();
        }

        /// <summary>
        /// (+10/6) 해금 · 업그레이드 완료 — 지금 보이는 모습 아래에서 먼지, 가운데서 빛.
        /// 「완성 시설이 잘 보이도록 제한」(기획) — 크기는 시설 폭에 맞추되 상한을 둔다.
        /// </summary>
        private void PlayDone()
        {
            GameObject shown = builtVisual != null && builtVisual.activeInHierarchy ? builtVisual : gameObject;
            Bounds b = new Bounds(shown.transform.position, Vector3.one);
            bool has = false;
            foreach (Renderer r in shown.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            float size = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z), 1f, 4f) * vfxScale;
            Vfx.Play(dustVfx, new Vector3(b.center.x, b.min.y, b.center.z), size);
            Vfx.Play(doneVfx, b.center + Vector3.up * b.extents.y * 0.5f, size);
        }

        private void Refresh()
            => Apply(_levels.IsUnlocked(kind),
                     baseSlots + Mathf.RoundToInt(_levels.EffectValue(kind, FacilityEffectType.FarmSlotAdd)));

        private void Apply(bool unlocked, int slots)
        {
            if (brokenVisual != null) brokenVisual.SetActive(!unlocked);
            if (builtVisual != null) builtVisual.SetActive(unlocked);

            if (levelSlots == null) return;

            int active = unlocked ? slots : 0;
            for (int i = 0; i < levelSlots.Length; i++)
                if (levelSlots[i] != null) levelSlots[i].SetActive(i < active);
        }
    }
}
