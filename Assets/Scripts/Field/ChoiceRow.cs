using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 목록 창의 한 줄 — 이름 · 설명 · 버튼. (+9/28, 이슈 72·73)
    /// 씨앗 선택 창과 탐사 지역 창이 같이 쓴다. 무엇을 보일지는 창이 정하고, 이 줄은 그리기만 한다.
    /// </summary>
    public class ChoiceRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI infoLabel;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI buttonLabel;

        public void Set(string title, string info, string buttonText, bool interactable, Action onClick)
        {
            if (nameLabel != null) nameLabel.text = title;
            if (infoLabel != null) infoLabel.text = info;
            if (buttonLabel != null) buttonLabel.text = buttonText;

            if (button == null) return;
            button.interactable = interactable;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
