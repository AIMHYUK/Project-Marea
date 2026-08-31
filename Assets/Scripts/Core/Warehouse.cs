using System;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Core
{
    /// <summary>
    /// 재료 창고. 1차에서는 무엇이 몇 개인지만 안다. 그리드도 무게도 없다.
    ///
    /// B가 쓰는 건 Has / Consume 둘뿐이다. 이 시그니처는 첫날 확정한 계약이니
    /// 바꿀 땐 상대에게 말한다. 내부는 나중에 Model/View로 쪼개도 여기는 그대로 둔다.
    /// </summary>
    public class Warehouse : MonoBehaviour
    {
        [Tooltip("시작할 때 들고 있을 재료. 테스트용.")]
        [SerializeField] private RecipeEntry[] startingStock;

        private readonly Dictionary<int, int> _counts = new();

        /// <summary>(재료 id, 바뀐 뒤 수량). UI가 구독한다.</summary>
        public event Action<int, int> OnCountChanged;

        private void Awake()
        {
            if (startingStock == null) return;
            foreach (var entry in startingStock)
                Add(entry.ingredient, entry.count);
        }

        public int CountOf(IngredientData ingredient)
            => ingredient == null ? 0 : CountOf(ingredient.Id);

        public int CountOf(int ingredientId)
            => _counts.TryGetValue(ingredientId, out var count) ? count : 0;

        /// <summary>이 메뉴를 만들 재료가 다 있는가. 차감하지는 않는다.</summary>
        public bool Has(MenuData menu)
        {
            if (menu == null) return false;

            foreach (var entry in menu.Recipe)
            {
                if (entry.ingredient == null) return false;
                if (CountOf(entry.ingredient.Id) < entry.count) return false;
            }
            return true;
        }

        /// <summary>
        /// 메뉴에 필요한 재료를 한꺼번에 뺀다.
        /// 하나라도 모자라면 아무것도 안 빼고 false를 준다 — 반만 빠지는 일은 없다.
        /// </summary>
        public bool Consume(MenuData menu)
        {
            if (!Has(menu)) return false;

            foreach (var entry in menu.Recipe)
                Take(entry.ingredient.Id, entry.count);

            return true;
        }

        public void Add(IngredientData ingredient, int count)
        {
            if (ingredient == null || count <= 0) return;

            int next = CountOf(ingredient.Id) + count;
            _counts[ingredient.Id] = next;
            OnCountChanged?.Invoke(ingredient.Id, next);
        }

        private void Take(int ingredientId, int count)
        {
            int next = Mathf.Max(0, CountOf(ingredientId) - count);
            _counts[ingredientId] = next;
            OnCountChanged?.Invoke(ingredientId, next);
        }
    }
}
