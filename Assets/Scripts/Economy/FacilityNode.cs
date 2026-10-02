using System;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 성장 창의 노드 하나 — 시설 하나. Lv(또는 잠김) · 이름. 누르면 고르기만 한다. (+10/2, 이슈 92)
    ///
    /// 와이어프레임은 단계별 노드 트리지만 노드(선행 · 효과 · 비용) 데이터가 아직 없다.
    /// 지금은 시설 하나가 노드 하나이고, 업그레이드는 오른쪽 상세의 버튼이 한다.
    /// 값은 Refresh 때마다 FacilityLevels에서 직접 읽는다 — 구독하지 않는다 (예전 FacilityCell과 같은 이유).
    /// </summary>
    public class FacilityNode : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [Tooltip("골랐을 때 켜는 표시.")]
        [SerializeField] private GameObject selectedMark;

        public FacilityKind Kind { get; private set; }

        public void Bind(FacilityKind kind, Action<FacilityKind> onSelect)
        {
            Kind = kind;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelect?.Invoke(Kind));
            }
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null) selectedMark.SetActive(selected);
        }

        public void Refresh()
        {
            FacilityLevels levels = FacilityLevels.Instance;
            FacilityData data = levels != null ? levels.DataOf(Kind) : null;
            if (data == null) return;

            if (levelLabel != null) levelLabel.text = levels.IsUnlocked(Kind) ? $"Lv.{levels.LevelOf(Kind)}" : "잠김";
            if (nameLabel != null) nameLabel.text = UpgradeUI.NameOf(Kind);
        }
    }
}
