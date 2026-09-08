using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 창고 목록의 한 줄. 아이콘 → 이름 → 개수. (+9/9)
    ///
    /// 자기가 어느 재료인지만 알고 창고는 모른다. 수량은 WarehouseUI가 넣어준다 —
    /// 셀이 창고를 직접 구독하면 셀 수만큼 구독이 늘고, 해지를 한 군데서 못 본다.
    /// </summary>
    public class IngredientCell : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI countLabel;

        /// <summary>이 줄이 맡은 재료. WarehouseUI가 어느 줄을 고칠지 찾는 데 쓴다.</summary>
        public IngredientData Ingredient { get; private set; }

        public void Bind(IngredientData ingredient, int count)
        {
            Ingredient = ingredient;

            if (icon != null)
            {
                icon.sprite = ingredient.Icon;

                // 아이콘이 없는 재료가 흰 사각형으로 남지 않게 한다.
                icon.enabled = ingredient.Icon != null;
            }

            if (nameLabel != null)
            {
                // displayName을 안 채운 에셋이 빈 줄로 보이면 어느 재료인지 알 수 없다.
                nameLabel.text = string.IsNullOrWhiteSpace(ingredient.DisplayName)
                               ? ingredient.name
                               : ingredient.DisplayName;
            }

            SetCount(count);
        }

        public void SetCount(int count)
        {
            if (countLabel != null) countLabel.text = count.ToString();
        }
    }
}
