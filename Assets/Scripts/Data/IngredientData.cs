using UnityEngine;

namespace Marea.Data
{
    /// <summary>
    /// 창고에 쌓이는 식재료 한 종류의 정의.
    /// 변하지 않는 값만 넣는다. 보유 수량 같은 건 Warehouse가 들고 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Ingredient", fileName = "Ingredient_")]
    public class IngredientData : ScriptableObject
    {
        [SerializeField] private int id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        public int Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
    }
}
