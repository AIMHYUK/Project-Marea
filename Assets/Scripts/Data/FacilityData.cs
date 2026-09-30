using Marea.Economy;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 시설 한 종류의 정의 — 이름과 레벨별 업그레이드 비용.
    /// 변하지 않는 값만 넣는다. "지금 몇 레벨인가"는 FacilityLevels가 들고 있다 (설계 결정 3).
    ///
    /// 효과 배열 둘은 영업 시설만 쓴다. 주방·창고 에셋에서는 비어 있고 아무도 안 읽는다 —
    /// 기획이 그 둘의 효과를 아직 확정하지 않았기 때문이다.
    ///
    /// 설계 결정 4(IngredientData에 growSeconds를 넣지 않는다)를 여기서는 따르지 않는다.
    /// 거기는 재료가 20종으로 늘 때 인스펙터에서 매번 "이건 심을 수 있나"를 판단해야 하는
    /// 문제였고, 여기는 3종 중 2종에 칸 두 개다. 타입을 갈라 상속을 두면 설계 결정 5가
    /// 거부한 그 모양(필드 몇 개 때문에 상속)이 되고, 나중에 주방·창고 효과가 확정돼
    /// 셋 다 서로 다른 효과를 갖게 되면 그때 답은 상속이 아니라 (효과키, 값) 목록이다.
    /// 그때 걷어낼 것을 지금 만들지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Facility", fileName = "Facility_")]
    public class FacilityData : ScriptableObject
    {
        [Tooltip("어느 시설인가. FacilityLevels가 이걸로 에셋과 레벨을 잇는다.")]
        [SerializeField] private FacilityKind kind;

        [Tooltip("업그레이드 목록에 보일 이름.")]
        [SerializeField] private string displayName;

        // ── 해금 (+9/28, 이슈 71). 여기 있는 건 정의뿐이다. "지금 해금됐나"는 FacilityLevels가
        //    든다 — SO에 넣으면 플레이 중에 바뀐 값이 에셋에 그대로 남아 다음 플레이가
        //    해금된 채로 시작한다 (설계 결정 3).

        [Tooltip("처음부터 열려 있나. 영업·주방·창고는 켠다. 끄면 업그레이드 목록에서 "
               + "'해금'부터 해야 레벨을 올릴 수 있다.")]
        [SerializeField] private bool startsUnlocked = true;

        [Tooltip("해금 비용(골드). startsUnlocked면 안 읽는다.")]
        [SerializeField, Min(0)] private int unlockCost;

        [Tooltip("이것들이 먼저 해금돼 있어야 해금할 수 있다. 비워두면 조건 없음.")]
        [SerializeField] private FacilityKind[] prerequisites;

        [Tooltip("해금 창에 보일 「해금 후 이용 가능한 기능」. 기획 7-2 해금 대상 선택 2번. (+9/28)")]
        [SerializeField, TextArea(2, 4)] private string unlockDescription;

        [Tooltip("레벨을 올리는 데 드는 골드. 0번이 1→2레벨 비용이다. "
               + "칸 수가 곧 상한을 정한다 — 3칸이면 최대 4레벨.")]
        [SerializeField] private int[] upgradeCosts;

        // ── 아래 둘은 영업 시설 전용이다. 비용 배열과 길이가 다르다는 데 주의:
        //    비용은 "단계 사이"라 3칸이면 1→2, 2→3, 3→4. 효과는 "각 레벨의 값"이라
        //    1·2·3·4레벨 몫으로 4칸이다. 즉 효과 칸 수 = 비용 칸 수 + 1 = MaxLevel.
        //    길이가 어긋나면 FacilityLevels가 Awake에서 에러를 낸다.

        [Tooltip("영업 시설 전용. 레벨별 영업 시간(초). 주방·창고는 비워둔다 — 아무도 안 읽는다. "
               + "칸 수는 MaxLevel과 같아야 한다 (비용 칸 수 + 1).")]
        [SerializeField] private float[] businessDurations;

        [Tooltip("영업 시설 전용. 레벨별 정산 배수 (1 = 보너스 없음). 주방·창고는 비워둔다. "
               + "칸 수는 MaxLevel과 같아야 한다 (비용 칸 수 + 1).")]
        [SerializeField] private float[] settlementBonuses;

        public FacilityKind Kind => kind;
        public string DisplayName => displayName;
        public bool StartsUnlocked => startsUnlocked;
        public int UnlockCost => unlockCost;
        public FacilityKind[] Prerequisites => prerequisites ?? System.Array.Empty<FacilityKind>();
        public string UnlockDescription => unlockDescription;

        /// <summary>시설은 1레벨에서 시작한다. 0레벨은 없다.</summary>
        public const int MinLevel = 1;

        /// <summary>더 못 올리는 레벨. 비용 칸이 3개면 4다.</summary>
        public int MaxLevel => MinLevel + (upgradeCosts?.Length ?? 0);

        /// <summary>지금 레벨에서 한 단계 올리는 비용. 최대면 0.</summary>
        public int CostToNext(int currentLevel)
            => IsMaxLevel(currentLevel) ? 0 : upgradeCosts[currentLevel - MinLevel];

        public bool IsMaxLevel(int currentLevel) => currentLevel >= MaxLevel;

        /// <summary>이 레벨에서의 영업 시간(초). 영업 시설 에셋에만 값이 있다.</summary>
        public float BusinessDurationAt(int level) => businessDurations[level - MinLevel];

        /// <summary>이 레벨에서의 정산 배수. 영업 시설 에셋에만 값이 있다.</summary>
        public float SettlementBonusAt(int level) => settlementBonuses[level - MinLevel];

        /// <summary>효과 배열이 MaxLevel만큼 채워져 있나. FacilityLevels가 Awake에서 본다.</summary>
        public bool HasEffectTable =>
            businessDurations != null && businessDurations.Length == MaxLevel &&
            settlementBonuses != null && settlementBonuses.Length == MaxLevel;
    }
}
