using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marea.Cooking
{
    public class SeafoodSkewersAssembly : MonoBehaviour
    {
        [Header("2단계 3D 바인딩")]
        [SerializeField] private List<IngredientDraggable> draggableIngredients; // 스폰된 드래그 재료들
        [SerializeField] private List<Transform> targetSlots;                    // 꼬치에 끼워질 위치들

        private int _currentSlotIndex;
        private bool _isCompleted;

        public bool IsCompleted => _isCompleted;
        public event Action OnAssemblyCompleted;

        public void ResetAssembly()
        {
            _currentSlotIndex = 0;
            _isCompleted = false;

            for (int i = 0; i < draggableIngredients.Count; i++)
            {
                if (draggableIngredients[i] != null)
                {
                    Transform targetSlot = (i < targetSlots.Count) ? targetSlots[i] : null;
                    draggableIngredients[i].Setup(targetSlot);
                    draggableIngredients[i].OnPlacedSuccess -= HandleIngredientPlaced;
                    draggableIngredients[i].OnPlacedSuccess += HandleIngredientPlaced;
                }
            }
        }

        private void HandleIngredientPlaced(IngredientDraggable ingredient)
        {
            _currentSlotIndex++;
            Debug.Log($"[SeafoodSkewersAssembly] 재료 끼우기 성공! ({_currentSlotIndex}/{targetSlots.Count})");

            if (_currentSlotIndex >= targetSlots.Count)
            {
                _isCompleted = true;
                Debug.Log("[SeafoodSkewersAssembly] 2단계 꼬치 끼우기 완료!");
                OnAssemblyCompleted?.Invoke();
            }
        }
    }
}
