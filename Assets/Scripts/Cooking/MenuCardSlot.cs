using System;
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

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectHighlight != null)
            {
                selectHighlight.SetActive(selected);
            }
        }

        private void OnClickSlot()
        {
            _onClickCallback?.Invoke(_menuData);
        }
    }
}