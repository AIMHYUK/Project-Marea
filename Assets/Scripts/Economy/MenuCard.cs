using System;
using System.Text;
using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Economy
{
    /// <summary>
    /// 오늘의 메뉴 카드 한 장 — 이름 · 그림 · 판매 가격 · 재료 필요량 / 보유량 · 선택 체크. (+10/2, 이슈 92)
    /// 고른 카드는 테두리 · 체크, 재료가 모자라 한 그릇도 못 만드는 카드는 흐리게 "재료 부족"으로 보인다. (+10/2 아트 적용)
    /// </summary>
    public class MenuCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI priceLabel;
        [SerializeField] private TextMeshProUGUI recipeLabel;
        [SerializeField] private GameObject checkMark;
        [Tooltip("고른 카드 뒤에 켜지는 테두리. (+10/2)")]
        [SerializeField] private GameObject selectedFrame;
        [Tooltip("재료가 모자라 못 만드는 카드를 흐리게 할 때 쓴다. (+10/2 — 예전엔 안 고른 카드를 흐렸다)")]
        [SerializeField] private CanvasGroup group;

        public MenuData Menu { get; private set; }

        public void Bind(MenuData menu, Action<MenuData> onClick)
        {
            Menu = menu;
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke(Menu));
        }

        public void Refresh(bool selected, int servings)
        {
            if (Menu == null) return;

            if (nameLabel != null) nameLabel.text = string.IsNullOrWhiteSpace(Menu.DisplayName) ? Menu.name : Menu.DisplayName;
            if (priceLabel != null) priceLabel.text = $"판매 가격 {Menu.BasePrice:N0} G";
            if (recipeLabel != null) recipeLabel.text = servings <= 0 ? "<color=#B03A2E>재료 부족</color>\n" + RecipeText(Menu) : RecipeText(Menu);
            if (icon != null)
            {
                icon.sprite = Menu.Icon;
                icon.enabled = Menu.Icon != null;
            }
            if (checkMark != null) checkMark.SetActive(selected);
            if (selectedFrame != null) selectedFrame.SetActive(selected);
            if (group != null) group.alpha = selected || servings > 0 ? 1f : 0.55f;
        }

        // 기호를 안 쓴다 — Pretendard-Bold SDF에 · 가 없다. "/" 는 있다.
        private static string RecipeText(MenuData menu)
        {
            Warehouse warehouse = Warehouse.Instance;
            var sb = new StringBuilder();
            if (menu.Recipe == null) return string.Empty;
            foreach (RecipeEntry entry in menu.Recipe)
            {
                if (entry.ingredient == null) continue;
                int have = warehouse != null ? warehouse.CountOf(entry.ingredient) : 0;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(entry.ingredient.DisplayName).Append(' ').Append(entry.requiredAmount).Append(" / ").Append(have);
            }
            return sb.ToString();
        }
    }
}
