using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class SeafoodSkewersSlicer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("타이밍 UI 연동")]
        [SerializeField] private SeafoodSkewersTimingUI timingUI;

        [Header("재료 썰기 설정")]
        [SerializeField] private int requiredSliceCount = 4;

        public bool IsCompleted { get; private set; }
        public int CurrentSliceCount { get; private set; }
        public float SliceScoreSum { get; private set; }

        private Vector2 _dragStartPos;
        private bool _isDragging;

        public void ResetSlicer()
        {
            IsCompleted = false;
            CurrentSliceCount = 0;
            SliceScoreSum = 0f;

            if (timingUI != null)
            {
                timingUI.ShowGauge();
            }
        }

        private void Update()
        {
            if (IsCompleted) return;

            // 스크린 화면 어디든 마우스 클릭/드래그 시 작동하는 백업 로직
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    _dragStartPos = Mouse.current.position.ReadValue();
                    _isDragging = true;
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame && _isDragging)
                {
                    _isDragging = false;
                    PerformSlice();
                }
            }
        }

        // IPointerDownHandler (클릭 시작)
        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsCompleted) return;
            _dragStartPos = eventData.position;
            _isDragging = true;
        }

        // IPointerUpHandler (클릭 뗌)
        public void OnPointerUp(PointerEventData eventData)
        {
            if (IsCompleted || !_isDragging) return;
            _isDragging = false;
            PerformSlice();
        }

        private void PerformSlice()
        {
            HitGrade grade = HitGrade.Good;

            if (timingUI != null)
            {
                grade = timingUI.EvaluateSliceTiming();
            }

            float score = grade switch
            {
                HitGrade.Perfect => 1.0f,
                HitGrade.Good => 0.7f,
                _ => 0.3f
            };

            SliceScoreSum += score;
            CurrentSliceCount++;

            Debug.Log($"[SeafoodSkewersSlicer] 썰기 성공! 판정: {grade} ({CurrentSliceCount}/{requiredSliceCount})");

            if (CurrentSliceCount >= requiredSliceCount)
            {
                IsCompleted = true;
                if (timingUI != null) timingUI.HideGauge();
                Debug.Log("[SeafoodSkewersSlicer] 1단계 재료 썰기 완료!");
            }
        }
    }
}
