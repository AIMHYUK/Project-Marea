using System;
using Marea.Economy;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 업그레이드 한 단계. 기획 FacilityUpgradeData 한 줄이다. (+10/2)
    /// 몇 레벨짜리인지는 <see cref="FacilityData.steps"/> 안의 순서가 정한다 — 0번이 Lv.1.
    /// 선행 조건(PrerequisiteID)은 항상 "같은 시설의 바로 앞 단계"라 따로 안 든다.
    /// </summary>
    [Serializable]
    public struct FacilityUpgradeStep
    {
        [Tooltip("기획 UpgradeID. 예: UPG_SALES_001")]
        public string upgradeId;

        [Tooltip("이 단계로 올리는 골드 (기획 CostResourceID ITEM_001).")]
        [Min(0)] public int goldCost;

        [Tooltip("골드 외에 드는 특수 자원. 비우면 골드만.")]
        public IngredientData resource;
        [Min(0)] public int resourceAmount;

        public FacilityEffectType effectType;
        [Tooltip("효과값. 같은 효과가 여러 단계에 있으면 그 단계까지의 총합을 적는다 (기획 표가 그렇게 돼 있다).")]
        public float effectValue;
        [Tooltip("효과 확률(0~1). 0이면 항상.")]
        [Range(0f, 1f)] public float effectChance;
        [Tooltip("효과 대상 ID. 여러 개면 쉼표로. 예: MENU_HAWAIIAN_SKEWER, MENU_GRILLED_CLAM")]
        public string effectTarget;

        [Tooltip("성장 창에 보일 한 줄 설명. 예: 영업 시간 +30초")]
        public string description;
    }

    /// <summary>
    /// 시설 한 종류의 정의 — 이름 · 해금 · 단계별 업그레이드.
    /// 변하지 않는 값만 넣는다. "지금 몇 레벨인가"는 FacilityLevels가 들고 있다 (설계 결정 3).
    ///
    /// (+10/2) 기획 「업그레이드_해금 데이터테이블」로 갈아엎었다. 예전엔 레벨 사이 비용 배열과
    /// 영업 시설 전용 효과 배열 둘이었는데, 시설 다섯이 각자 다른 효과를 갖게 되면서 예전 주석이
    /// 예고한 대로 "(효과 종류, 값) 목록" = <see cref="FacilityUpgradeStep"/> 배열이 됐다.
    /// 시설은 Lv.0에서 시작하고, steps[0]을 사면 Lv.1이다 (기획 표의 적용 레벨 1이 첫 구매).
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Facility", fileName = "Facility_")]
    public class FacilityData : ScriptableObject
    {
        [Tooltip("어느 시설인가. FacilityLevels가 이걸로 에셋과 레벨을 잇는다.")]
        [SerializeField] private FacilityKind kind;

        [Tooltip("성장 창에 보일 이름 (기획 성장 분야). 예: 영업 시설")]
        [SerializeField] private string displayName;

        [Tooltip("성장 창 세부 설명에 보일 시설 설명 (기획 FacilityData.Description).")]
        [SerializeField, TextArea(1, 3)] private string description;

        // ── 해금 (+9/28, 이슈 71). 여기 있는 건 정의뿐이다. "지금 해금됐나"는 FacilityLevels가
        //    든다 — SO에 넣으면 플레이 중에 바뀐 값이 에셋에 그대로 남아 다음 플레이가
        //    해금된 채로 시작한다 (설계 결정 3).

        [Tooltip("처음부터 열려 있나. 영업·주방·직원·창고는 켠다. 수급(농사터)·탐사(탐사정)는 끄고, 해금해야 업그레이드할 수 있다.")]
        [SerializeField] private bool startsUnlocked = true;

        [Tooltip("해금 창에 보일 이름 (기획 UnlockData.Name). 비우면 displayName. 예: 농사터")]
        [SerializeField] private string unlockName;

        [Tooltip("해금 비용(골드). startsUnlocked면 안 읽는다.")]
        [SerializeField, Min(0)] private int unlockCost;

        [Tooltip("이것들이 먼저 해금돼 있어야 해금할 수 있다. 비워두면 조건 없음.")]
        [SerializeField] private FacilityKind[] prerequisites;

        [Tooltip("해금 창에 보일 「해금 후 이용 가능한 기능」. 기획 7-2 해금 대상 선택 2번. (+9/28)")]
        [SerializeField, TextArea(2, 4)] private string unlockDescription;

        [Tooltip("업그레이드 단계. 0번이 Lv.1, 칸 수가 곧 최대 레벨이다. (+10/2)")]
        [SerializeField] private FacilityUpgradeStep[] steps;

        public FacilityKind Kind => kind;
        public string DisplayName => displayName;
        public string Description => description;
        public bool StartsUnlocked => startsUnlocked;
        public string UnlockName => string.IsNullOrWhiteSpace(unlockName) ? displayName : unlockName;
        public int UnlockCost => unlockCost;
        public FacilityKind[] Prerequisites => prerequisites ?? Array.Empty<FacilityKind>();
        public string UnlockDescription => unlockDescription;

        /// <summary>시설은 0레벨(업그레이드 없음)에서 시작한다.</summary>
        public const int MinLevel = 0;

        /// <summary>더 못 올리는 레벨 = 단계 수.</summary>
        public int MaxLevel => steps?.Length ?? 0;

        public bool IsMaxLevel(int currentLevel) => currentLevel >= MaxLevel;

        /// <summary>그 레벨의 단계 (1 ~ MaxLevel). 범위 밖이면 false.</summary>
        public bool TryGetStep(int level, out FacilityUpgradeStep step)
        {
            if (steps == null || level < 1 || level > steps.Length)
            {
                step = default;
                return false;
            }
            step = steps[level - 1];
            return true;
        }

        /// <summary>지금 레벨에서 한 단계 올리는 골드. 최대면 0.</summary>
        public int CostToNext(int currentLevel)
            => TryGetStep(currentLevel + 1, out FacilityUpgradeStep s) ? s.goldCost : 0;

        /// <summary>
        /// 이 레벨에서 그 효과의 값 — 지금 레벨까지의 단계 중 그 효과를 가진 가장 높은 단계의 값.
        /// 없으면 0. 기획 표의 값이 누적 총합이라 더하지 않는다.
        /// </summary>
        public float EffectValueAt(int level, FacilityEffectType type)
            => TryGetEffectAt(level, type, out FacilityUpgradeStep s) ? s.effectValue : 0f;

        /// <summary>(+10/2) 지금 레벨까지 중 그 효과를 가진 가장 높은 단계. 값 · 확률을 같이 읽을 때 쓴다.</summary>
        public bool TryGetEffectAt(int level, FacilityEffectType type, out FacilityUpgradeStep step)
        {
            if (steps != null)
                for (int i = Mathf.Min(level, steps.Length) - 1; i >= 0; i--)
                    if (steps[i].effectType == type) { step = steps[i]; return true; }
            step = default;
            return false;
        }

        /// <summary>
        /// (+10/2) 이 대상(메뉴 · 조리대 · 해역 · 등급 키)을 여는 단계의 레벨. 없으면 0.
        /// 해금 효과는 누적이라(주방 Lv.1 야채볶음 + Lv.3 조개구이) "가장 높은 단계"가 아니라 대상 키로 찾는다.
        /// 대상 칸은 "A, B"처럼 쉼표로 여럿 적혀 있을 수 있다.
        /// </summary>
        public int LevelForTarget(FacilityEffectType type, string key)
        {
            if (steps == null || string.IsNullOrWhiteSpace(key)) return 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].effectType != type || string.IsNullOrEmpty(steps[i].effectTarget)) continue;
                foreach (string part in steps[i].effectTarget.Split(','))
                    if (part.Trim() == key) return i + 1;
            }
            return 0;
        }
    }
}
