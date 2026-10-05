using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>Owns stage-two timing attempts and reports assembly completion to the minigame.</summary>
    public class SkewerTimedAssembly : MonoBehaviour
    {
        [SerializeField] private IngredientController ingredientController;

        private readonly List<HitGrade> _successfulHits = new();
        private int _currentSlotIndex;
        private bool _isCompleted;

        public bool IsCompleted => _isCompleted;
        public int CurrentSlotIndex => _currentSlotIndex;
        public event Action<float> OnAssemblyCompleted;

        public void ResetAssembly()
        {
            _currentSlotIndex = 0;
            _isCompleted = false;
            _successfulHits.Clear();
        }

        /// <summary>A miss leaves the current slot ready for another timing attempt.</summary>
        public bool TryInsert(HitGrade grade)
        {
            if (_isCompleted || grade == HitGrade.Miss || ingredientController == null ||
                ingredientController.IsInserting || _currentSlotIndex >= ingredientController.AssemblyIngredientCount)
                return false;

            int slotIndex = _currentSlotIndex;
            if (!ingredientController.AttachIngredient(slotIndex, HandleIngredientInserted))
                return false;

            _successfulHits.Add(grade);
            return true;
        }

        private void HandleIngredientInserted()
        {
            _currentSlotIndex++;
            if (_currentSlotIndex < ingredientController.AssemblyIngredientCount) return;

            _isCompleted = true;
            float score = 0f;
            foreach (HitGrade grade in _successfulHits)
                score += grade == HitGrade.Perfect ? 1f : 0.7f;

            OnAssemblyCompleted?.Invoke(score / Mathf.Max(1, _successfulHits.Count));
        }
    }
}
