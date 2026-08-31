using System;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public class MenuCardSlot : MonoBehaviour
    {
        [Header("UI ¹ÙÀÎµù")]
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