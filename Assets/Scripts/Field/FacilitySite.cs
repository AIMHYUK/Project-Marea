using Marea.Economy;
using UnityEngine;

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

        [Header("레벨 단위 (선택)")]
        [Tooltip("레벨이 오를 때 앞에서부터 켜지는 것들. 농사 데크의 밭 자리다. "
               + "탐사정처럼 레벨로 늘어나는 게 없으면 비워둔다.")]
        [SerializeField] private GameObject[] levelSlots;

        [Tooltip("한 레벨에 켜지는 개수. 1레벨이면 이만큼, 2레벨이면 두 배.")]
        [SerializeField, Min(1)] private int slotsPerLevel = 2;

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
                // 최대 레벨까지 켤 자리가 모자라면 마지막 업그레이드가 겉으로 아무 일도 안 한다.
                int needed = _levels.DataOf(kind).MaxLevel * slotsPerLevel;
                if (levelSlots.Length < needed)
                    Debug.LogError($"{name}: levelSlots가 {levelSlots.Length}개인데 최대 레벨에 "
                                 + $"{needed}개가 필요하다. 자리를 더 깔거나 slotsPerLevel을 줄일 것.", this);
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
            if (changed == kind) Refresh();
        }

        private void HandleLevelChanged(FacilityKind changed, int level)
        {
            if (changed == kind) Refresh();
        }

        private void Refresh() => Apply(_levels.IsUnlocked(kind), _levels.LevelOf(kind));

        private void Apply(bool unlocked, int level)
        {
            if (brokenVisual != null) brokenVisual.SetActive(!unlocked);
            if (builtVisual != null) builtVisual.SetActive(unlocked);

            if (levelSlots == null) return;

            int active = unlocked ? level * slotsPerLevel : 0;
            for (int i = 0; i < levelSlots.Length; i++)
                if (levelSlots[i] != null) levelSlots[i].SetActive(i < active);
        }
    }
}
