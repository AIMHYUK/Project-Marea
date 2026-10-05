using System;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Economy
{
    /// <summary>
    /// 시설이 지금 몇 레벨인지, 그래서 값이 얼마인지. 계약 8이다 (2차 분담).
    ///
    /// 정의(이름·비용·효과표)는 FacilityData 에셋이 들고, 여기는 "지금 몇 레벨인가"만 든다.
    /// 설계 결정 3 그대로다 — 안 변하는 건 SO, 변하는 건 MonoBehaviour.
    ///
    /// <b>B는 원시 레벨을 해석하지 않는다.</b> BusinessDuration·SettlementBonus처럼 이미
    /// 계산된 값을 읽는다. "영업 2레벨이면 몇 초인가"는 전부 이 클래스 안에서 끝나고,
    /// 레벨이 늘든 곡선이 바뀌든 B는 안 고친다. 프로퍼티를 지우거나 이름을 바꾸는 건
    /// B에게 먼저 말한다 — 늘리는 건 A 마음대로 해도 된다.
    ///
    /// 저장·로드는 없다. 씬을 다시 켜면 전부 0레벨로 돌아간다 (이슈 23 범위 밖).
    ///
    /// (+10/2) 기획 「업그레이드_해금 데이터테이블」로 바뀌었다. 0레벨에서 시작하고, 효과는
    /// 단계마다 (종류, 값)으로 든다 — <see cref="EffectValue"/>. BusinessDuration·SettlementBonus는
    /// 이름 그대로 두고 그 위에서 계산한다 (계약 8).
    /// </summary>
    public class FacilityLevels : MonoBehaviour
    {
        public static FacilityLevels Instance { get; private set; }

        [Tooltip("시설 정의 에셋. FacilityKind마다 하나씩, 빠짐없이 넣는다.")]
        [SerializeField] private FacilityData[] facilities;

        // 업그레이드 0레벨의 영업 시간. BusinessManager의 기존 기본값과 같은 수다.
        // 기획 표는 "+30초"처럼 늘어나는 양만 적어서 바닥값이 따로 필요하다.
        private const float BaseBusinessDuration = 180f;

        private readonly Dictionary<FacilityKind, FacilityData> _data = new();
        private readonly Dictionary<FacilityKind, int> _levels = new();
        private readonly HashSet<FacilityKind> _unlocked = new();   // (+9/28)

        /// <summary>(시설, 오른 뒤 레벨). UI가 구독한다.</summary>
        public event Action<FacilityKind, int> OnLevelChanged;

        /// <summary>(+9/28) 방금 해금된 시설. UI와 월드 오브젝트가 구독한다.</summary>
        public event Action<FacilityKind> OnUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"{name}: 씬에 FacilityLevels가 둘 이상이다. '{Instance.name}'이 먼저 "
                             + "잡혔고 이 오브젝트는 무시된다. 하나만 남길 것.", this);
                return;
            }

            Instance = this;
            BuildTable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildTable()
        {
            if (facilities != null)
            {
                foreach (FacilityData data in facilities)
                {
                    if (data == null) continue;

                    // 같은 시설을 두 번 넣으면 뒤엣것이 조용히 이긴다. 증상이 "비용이
                    // 인스펙터와 다르다"로만 나와서 원인이 안 보인다.
                    if (_data.ContainsKey(data.Kind))
                    {
                        Debug.LogError($"{name}: {data.Kind} 에셋이 둘 이상이다 "
                                     + $"('{_data[data.Kind].name}' vs '{data.name}'). 하나만 넣을 것.", this);
                        continue;
                    }

                    _data[data.Kind] = data;
                    _levels[data.Kind] = FacilityData.MinLevel;   // (+10/2) 0레벨
                    if (data.StartsUnlocked) _unlocked.Add(data.Kind);
                }
            }

            // 빠진 시설이 있으면 LevelOf가 0을 돌려주고 업그레이드 목록에서도 사라진다.
            // 둘 다 조용해서, 여기서 이름을 찍어주지 않으면 못 찾는다.
            foreach (FacilityKind kind in Enum.GetValues(typeof(FacilityKind)))
            {
                if (!_data.ContainsKey(kind))
                    Debug.LogError($"{name}: {kind} 시설 에셋이 facilities에 없다. "
                                 + "업그레이드 목록에 안 뜨고 효과도 안 걸린다.", this);
            }
        }

        // ── 아래 넷이 계약 8이다. B가 읽는다. 시그니처를 지우거나 바꿀 땐 먼저 말한다.

        /// <summary>지금 레벨. 에셋이 없으면 0을 돌려준다 (Awake에서 이미 에러를 냈다).</summary>
        public int LevelOf(FacilityKind kind)
            => _levels.TryGetValue(kind, out int level) ? level : FacilityData.MinLevel;

        /// <summary>지금 영업 시간(초). B의 타이머가 읽는다. (+10/2) 기본 180초 + BUSINESS_TIME_ADD.</summary>
        public float BusinessDuration => BaseBusinessDuration + EffectValue(FacilityKind.Business, FacilityEffectType.BusinessTimeAdd);

        /// <summary>지금 정산 배수 (1 = 보너스 없음). B가 정산 금액에 곱한다. (+10/2) SETTLEMENT_MULTIPLIER, 없으면 1.</summary>
        public float SettlementBonus
        {
            get
            {
                float m = EffectValue(FacilityKind.Business, FacilityEffectType.SettlementMultiplier);
                return m > 0f ? m : 1f;
            }
        }

        /// <summary>
        /// (+10/2) 이 시설의 지금 레벨에서 그 효과의 값 (누적 총합). 없으면 0, 잠긴 시설도 0.
        /// B가 테이블 · 직원 · 미니게임 보정을 붙일 때 이걸 읽는다 — 늘리는 건 A 마음대로, 지우는 건 먼저 말한다.
        /// </summary>
        public float EffectValue(FacilityKind kind, FacilityEffectType type)
        {
            FacilityData data = DataOf(kind);
            if (data == null || !IsUnlocked(kind)) return 0f;
            return data.EffectValueAt(LevelOf(kind), type);
        }

        /// <summary>(+10/2) 지금 걸려 있는 그 효과의 단계 (값 · 확률 · 대상). 잠긴 시설이거나 없으면 false.</summary>
        public bool TryGetEffect(FacilityKind kind, FacilityEffectType type, out FacilityUpgradeStep step)
        {
            FacilityData data = DataOf(kind);
            if (data == null || !IsUnlocked(kind)) { step = default; return false; }
            return data.TryGetEffectAt(LevelOf(kind), type, out step);
        }

        /// <summary>
        /// (+10/2) 이 대상(메뉴 · 해역 · 등급 키)이 열렸나. 그 대상을 여는 단계가 데이터에 없으면 true —
        /// 잠글 근거가 없는 것은 막지 않는다. requiredLevel은 잠금 문구("주방 시설 Lv.3 필요")용.
        /// </summary>
        public bool IsTargetUnlocked(FacilityKind kind, FacilityEffectType type, string key, out int requiredLevel)
        {
            FacilityData data = DataOf(kind);
            requiredLevel = data != null ? data.LevelForTarget(type, key) : 0;
            if (requiredLevel == 0) return true;
            return IsUnlocked(kind) && LevelOf(kind) >= requiredLevel;
        }

        // ── 여기부터는 A만 쓴다. UI와 FacilityUpgrade(4번)가 호출자다.

        /// <summary>이 시설의 정의. 목록 UI가 이름·비용을 여기서 꺼낸다. 없으면 null.</summary>
        public FacilityData DataOf(FacilityKind kind)
            => _data.TryGetValue(kind, out FacilityData data) ? data : null;

        /// <summary>더 올릴 수 있나. 에셋이 없으면 false.</summary>
        public bool CanRaise(FacilityKind kind)
        {
            FacilityData data = DataOf(kind);
            return data != null && !data.IsMaxLevel(LevelOf(kind));
        }

        /// <summary>
        /// 레벨을 1 올린다. 최대면 아무것도 안 하고 false — 던지지 않는다.
        /// 골드는 여기서 안 본다. 비용 차감은 FacilityUpgrade가 먼저 한다.
        /// </summary>
        public bool TryRaiseLevel(FacilityKind kind)
        {
            if (!CanRaise(kind)) return false;

            int next = LevelOf(kind) + 1;
            _levels[kind] = next;
            OnLevelChanged?.Invoke(kind, next);
            return true;
        }

        // ── 해금 (+9/28, 이슈 71). 레벨과 별개 축이다 — 잠긴 시설도 레벨은 0으로 들고 있고,
        //    해금 뒤 업그레이드는 기존 경로 그대로다. B는 이걸 안 읽는다.

        /// <summary>열려 있나. 에셋이 없으면 false.</summary>
        public bool IsUnlocked(FacilityKind kind) => _unlocked.Contains(kind);

        /// <summary>
        /// 연다. 이미 열렸거나 에셋이 없으면 false. 골드·선행 조건은 여기서 안 본다 —
        /// FacilityUpgrade.TryUnlock이 먼저 본다 (TryRaiseLevel과 같은 분담).
        /// </summary>
        public bool TryUnlock(FacilityKind kind)
        {
            if (DataOf(kind) == null || !_unlocked.Add(kind)) return false;

            OnUnlocked?.Invoke(kind);
            return true;
        }

        // ── 테스트용. 4번(FacilityUpgrade)이 붙기 전까지 레벨을 올려볼 경로가 없어서,
        //    완료 조건의 "영업 레벨을 올리면 BusinessDuration이 실제로 바뀐다"를
        //    이게 없으면 확인할 수가 없다.

        [ContextMenu("테스트: 영업 레벨 +1 (골드 무시)")]
        private void DebugRaiseBusiness()
        {
            if (!TryRaiseLevel(FacilityKind.Business))
            {
                Debug.Log($"{name}: 영업 시설이 이미 최대 레벨이다.", this);
                return;
            }

            Debug.Log($"{name}: 영업 {LevelOf(FacilityKind.Business)}레벨 — "
                    + $"영업 시간 {BusinessDuration}초, 정산 배수 {SettlementBonus:0.##}", this);
        }
    }
}
