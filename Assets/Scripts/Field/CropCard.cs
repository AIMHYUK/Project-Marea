using System;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 작물 선택 창의 카드 한 장 — 아이콘 · 이름 · 가격 · 성장 시간. (+10/2, 이슈 92)
    /// 누르면 고르기만 한다. 심기는 창의 [심기] 버튼이 한다 (와이어프레임: 선택 → 상세 → 심기).
    /// </summary>
    public class CropCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI infoLabel;
        [Tooltip("골랐을 때 켜는 테두리/체크.")]
        [SerializeField] private GameObject selectedMark;

        public CropData Crop { get; private set; }

        public void Bind(CropData crop, Action<CropData> onSelect)
        {
            Crop = crop;

            if (nameLabel != null) nameLabel.text = crop.DisplayName;
            if (infoLabel != null) infoLabel.text = $"가격 {crop.SeedPrice:N0} G\n성장 {crop.GrowSeconds:0}초";
            SetIcon(icon, crop);

            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onSelect?.Invoke(crop));
        }

        public void SetSelected(bool selected)
        {
            if (selectedMark != null) selectedMark.SetActive(selected);
        }

        /// <summary>작물에는 아이콘이 따로 없어 수확물 아이콘을 쓴다. 없으면 숨긴다.</summary>
        public static void SetIcon(Image image, CropData crop)
        {
            if (image == null) return;
            Sprite sprite = crop != null && crop.Harvest != null ? crop.Harvest.Icon : null;
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
