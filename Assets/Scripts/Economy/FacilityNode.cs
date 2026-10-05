using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 성장 창의 노드 하나 — 시설의 업그레이드 한 단계. (+10/2, 이슈 92)
    ///
    /// 와이어프레임 「시설 성장 테크」의 노드다. 기획 표의 단계(Lv.1~4)가 한 줄로 이어지고,
    /// 선행 조건은 늘 바로 앞 단계라 왼쪽에서 오른쪽으로 놓으면 그게 트리다.
    /// 누르면 고르기만 한다. 사는 건 오른쪽 상세의 [업그레이드]다.
    /// </summary>
    public class FacilityNode : MonoBehaviour
    {
        public enum State { Owned, Next, Locked }

        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI levelLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [Tooltip("골랐을 때 켜는 표시.")]
        [SerializeField] private GameObject selectedMark;
        [Tooltip("산 단계 표시(체크 등).")]
        [SerializeField] private GameObject ownedMark;
        [Tooltip("아직 못 사는 단계를 흐리게.")]
        [SerializeField] private CanvasGroup group;

        public int Level { get; private set; }

        public void Bind(int level, string title, Action<int> onSelect)
        {
            Level = level;
            if (levelLabel != null) levelLabel.text = $"Lv.{level}";
            if (nameLabel != null) nameLabel.text = title;

            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(Level));
        }

        public void SetState(State state, bool selected)
        {
            if (ownedMark != null) ownedMark.SetActive(state == State.Owned);
            if (group != null) group.alpha = state == State.Locked ? 0.55f : 1f;
            if (selectedMark != null) selectedMark.SetActive(selected);
        }
    }
}
