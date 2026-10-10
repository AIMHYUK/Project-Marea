using System;
using System.Collections.Generic;
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

        /// <param name="lockedLevel">(+10/2) 주방 업그레이드로 잠겼으면 열리는 레벨, 아니면 0.</param>
        public void Refresh(bool selected, int servings, int lockedLevel = 0)
        {
            if (Menu == null) return;

            if (nameLabel != null) nameLabel.text = string.IsNullOrWhiteSpace(Menu.DisplayName) ? Menu.name : Menu.DisplayName;
            if (priceLabel != null) priceLabel.text = $"판매 가격 {Menu.BasePrice:N0} G";
            if (recipeLabel != null)
                recipeLabel.text = lockedLevel > 0 ? $"<color=#B03A2E>주방 시설 Lv.{lockedLevel} 필요</color>\n" + RecipeText(Menu)
                    : servings <= 0 ? "<color=#B03A2E>재료 부족</color>\n" + RecipeText(Menu)
                    : RecipeText(Menu);
            if (icon != null)
            {
                icon.sprite = Menu.Icon;
                icon.enabled = Menu.Icon != null;
            }
            if (checkMark != null) checkMark.SetActive(selected);
            if (selectedFrame != null) selectedFrame.SetActive(selected);
            // 잠김(주방 레벨) > 재료 부족 순으로 흐리게. 고른 카드는 테두리로 구분한다.
            if (group != null) group.alpha = lockedLevel > 0 ? 0.35f : selected || servings > 0 ? 1f : 0.55f;
        }

        // (+10/9) 같은 재료는 합치고, 3종이 넘으면 한 줄에 둘씩 — 카드 아래 칸이 좁아 5줄이면 글자가 10pt 까지 줄었다.
        private static string RecipeText(MenuData menu)
        {
            Warehouse warehouse = Warehouse.Instance;
            if (menu.Recipe == null) return string.Empty;

            var needs = new List<(IngredientData item, int amount)>();
            foreach (RecipeEntry entry in menu.Recipe)
            {
                if (entry.ingredient == null) continue;
                int i = needs.FindIndex(x => x.item == entry.ingredient);
                if (i >= 0) needs[i] = (entry.ingredient, needs[i].amount + entry.requiredAmount);
                else needs.Add((entry.ingredient, entry.requiredAmount));
            }

            int perLine = needs.Count > 3 ? 2 : 1;
            var sb = new StringBuilder();
            for (int i = 0; i < needs.Count; i++)
            {
                int have = warehouse != null ? warehouse.CountOf(needs[i].item) : 0;
                if (i > 0) sb.Append(i % perLine == 0 ? "\n" : "   ");
                sb.Append(needs[i].item.DisplayName).Append(' ').Append(needs[i].amount).Append(" / ").Append(have);
            }
            return sb.ToString();
        }
    }
}
