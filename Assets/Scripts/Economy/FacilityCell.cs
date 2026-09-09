using System;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 업그레이드 목록의 한 줄. 레벨 · 이름 · 지금 효과 · 다음 효과 · 비용 · 버튼. (+9/10)
    ///
    /// 자기가 어느 시설인지만 들고, 값은 Refresh를 부를 때마다 FacilityLevels·Wallet에서
    /// 직접 읽는다. 셀이 이벤트를 구독하지는 않는다 — 구독이 셀 수만큼 늘고 해지를
    /// 한 군데서 못 보게 된다 (IngredientCell과 같은 이유).
    /// </summary>
    public class FacilityCell : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI currentLabel;
        [SerializeField] private TextMeshProUGUI nextLabel;
        [SerializeField] private TextMeshProUGUI costLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TextMeshProUGUI buttonLabel;

        /// <summary>이 줄이 맡은 시설.</summary>
        public FacilityKind Kind { get; private set; }

        private Action<FacilityKind> _onUpgrade;

        public void Bind(FacilityKind kind, Action<FacilityKind> onUpgrade)
        {
            Kind = kind;
            _onUpgrade = onUpgrade;

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(() => _onUpgrade?.Invoke(Kind));
            }

            Refresh();
        }

        /// <summary>지금 상태로 줄을 다시 그린다. 레벨이나 골드가 바뀔 때마다 UI가 부른다.</summary>
        public void Refresh()
        {
            FacilityLevels levels = FacilityLevels.Instance;
            FacilityData data = levels != null ? levels.DataOf(Kind) : null;
            if (data == null) return;

            int level = levels.LevelOf(Kind);
            bool isMax = data.IsMaxLevel(level);
            FacilityUpgrade.Result state = FacilityUpgrade.CanUpgrade(Kind);

            SetText(levelLabel, $"Lv.{level}");
            SetText(nameLabel, string.IsNullOrWhiteSpace(data.DisplayName) ? data.name : data.DisplayName);
            SetText(currentLabel, EffectText(data, level));

            // 효과가 미정인 시설은 다음 줄도 "효과 미정"이라 같은 말이 두 번 나온다.
            // 최대 단계도 다음이 없다. 둘 다 빈 줄로 둔다.
            bool showNext = !isMax && data.HasEffectTable;
            SetText(nextLabel, showNext ? $"다음   {EffectText(data, level + 1)}" : string.Empty);
            SetText(costLabel, isMax ? "—" : $"{data.CostToNext(level):N0} G");

            if (upgradeButton != null) upgradeButton.interactable = state == FacilityUpgrade.Result.Ok;

            SetText(buttonLabel, state switch
            {
                FacilityUpgrade.Result.Ok            => "업그레이드",
                FacilityUpgrade.Result.MaxLevel      => "최대 단계",
                FacilityUpgrade.Result.NotEnoughGold => "골드 부족",
                _                                    => "설정 오류",
            });
        }

        /// <summary>
        /// 그 레벨에서의 효과 문구. 효과표가 없는 시설(주방·창고)은 기획이 수치를
        /// 확정하지 않았다는 걸 그대로 보여준다 — 빈 줄로 두면 버그처럼 보인다.
        /// </summary>
        private static string EffectText(FacilityData data, int level)
        {
            if (!data.HasEffectTable) return "효과 미정";

            // 기호를 안 쓴다. Pretendard-Bold SDF에 ·, ×, → 가 없어서 TMP가 공백으로
            // 바꿔버린다 — 에러가 아니라 경고라 화면에서 글자만 조용히 사라진다.
            return $"영업 {data.BusinessDurationAt(level):0}초   정산 {data.SettlementBonusAt(level):0.00}배";
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
