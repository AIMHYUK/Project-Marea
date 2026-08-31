using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Data
{
    /// <summary>재료 한 줄. 무엇을 몇 개 쓰는가.</summary>
    [Serializable]
    public struct RecipeEntry
    {
        public IngredientData ingredient;
        [Min(1)] public int count;
    }

    /// <summary>
    /// 판매 메뉴 하나의 정의. A와 B가 같이 쓰는 유일한 공유 데이터다.
    /// 필드를 늘릴 땐 상대에게 말하고 늘린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Marea/Menu", fileName = "Menu_")]
    public class MenuData : ScriptableObject
    {
        [SerializeField] private int id;
        [SerializeField] private string displayName;
        [SerializeField] private RecipeEntry[] recipe;

        [Tooltip("요리 판정 배율을 여기에 곱한다.")]
        [SerializeField, Min(0)] private int basePrice;

        public int Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<RecipeEntry> Recipe => recipe;
        public int BasePrice => basePrice;
    }
}
