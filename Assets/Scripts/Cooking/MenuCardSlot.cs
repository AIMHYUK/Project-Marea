using System;
using System.Text;
using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public class MenuCardSlot : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] private Image imgIcon; // 메뉴 이미지 컴포넌트
        [SerializeField] private TextMeshProUGUI txtDisplayName;
        [SerializeField] private TextMeshProUGUI txtBasePrice;
        [SerializeField] private GameObject selectHighlight;
        [SerializeField] private Button btnSelect;

        [Header("재료 소모량 표시")]
        [SerializeField] private TextMeshProUGUI txtRecipeInfo; // 재료 목록 및 [보유량/필요량] 표시 텍스트

        [Header("선택 시각 피드백")]
        [SerializeField] private Image cardBackground; // 슬롯 배경 이미지 (선택 시 색상 강조)
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(1f, 0.92f, 0.6f); // 연한 골드/노랑

        private MenuData _menuData;
        private Action<MenuData> _onClickCallback;

        public MenuData MenuData => _menuData;

        private void Awake()
        {
            if (btnSelect != null)
            {
                btnSelect.onClick.AddListener(OnClickSlot);
            }
        }

        public void Setup(MenuData menuData, Action<MenuData> onClickCallback)
        {
            _menuData = menuData;
            _onClickCallback = onClickCallback;

            if (imgIcon != null)
            {
                imgIcon.sprite = menuData.Icon;
                imgIcon.gameObject.SetActive(menuData.Icon != null);
            }

            if (txtDisplayName != null)
            {
                txtDisplayName.text = menuData.DisplayName;
            }

            if (txtBasePrice != null)
            {
                txtBasePrice.text = $"{menuData.BasePrice} G";
            }

            UpdateRecipeDisplay();
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            // 하이라이트 오브젝트 켜기/끄기
            if (selectHighlight != null)
            {
                selectHighlight.SetActive(selected);
            }

            // 카드 배경 색상 전환 피드백
            if (cardBackground != null)
            {
                cardBackground.color = selected ? selectedColor : normalColor;
            }

            // 선택된 카드는 살짝 커지게 하여 시각적 강조
            transform.localScale = selected ? Vector3.one * 1.05f : Vector3.one;
        }

        public void UpdateRecipeDisplay()
        {
            if (txtRecipeInfo == null || _menuData == null) return;

            if (_menuData.Recipe == null || _menuData.Recipe.Count == 0)
            {
                txtRecipeInfo.text = "<color=#888888>필요 재료 없음</color>";
                return;
            }

            StringBuilder sb = new StringBuilder();

            foreach (var entry in _menuData.Recipe)
            {
                if (entry.ingredient == null) continue;

                int currentCount = Warehouse.Instance != null ? Warehouse.Instance.CountOf(entry.ingredient) : 0;
                int requiredCount = entry.count;

                string colorCode = currentCount >= requiredCount ? "#FFFFFF" : "#FF5555";

                sb.AppendLine($"{entry.ingredient.name} : <color={colorCode}>{currentCount}/{requiredCount}</color>");
            }

            txtRecipeInfo.text = sb.ToString();
        }

        private void OnClickSlot()
        {
            _onClickCallback?.Invoke(_menuData);
        }
    }
}
