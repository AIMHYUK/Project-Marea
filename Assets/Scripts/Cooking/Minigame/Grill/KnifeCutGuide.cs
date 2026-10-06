using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class KnifeCutGuide : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("3D 칼집 위치 기준점")]
        [SerializeField] private Transform startPoint3D;
        [SerializeField] private Transform endPoint3D;

        [Header("비주얼 요소")]
        [SerializeField] private GameObject guideLineVisual; // 안내선/화살표
        [SerializeField] private GameObject cutMarkVisual;  // 3D 칼집 자국 표식
        [SerializeField] private ParticleSystem sliceEffect;

        [Header("드래그 완성 인정 기준 (0.0 ~ 1.0)")]
        [SerializeField] private float cutThreshold = 0.75f;
        [SerializeField, Min(1f)] private float pointerTolerancePixels = 24f;

        private bool _isCompleted;
        private float _currentProgress;
        private Camera _mainCamera;
        private BaseCookingMinigame _minigame;
        private bool _isDragging;
        private bool _reverseStroke;

        public bool IsCompleted => _isCompleted;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _minigame = GetComponentInParent<BaseCookingMinigame>();
        }

        private void OnEnable()
        {
            ResetGuide();
        }

        public void ResetGuide()
        {
            _isCompleted = false;
            _currentProgress = 0f;
            _isDragging = false;
            if (sliceEffect != null)
                sliceEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (guideLineVisual != null) guideLineVisual.SetActive(true);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(false);
        }

        private void Update()
        {
            if (Mouse.current == null || _isCompleted) return;
            if (_minigame != null && _minigame.CurrentStepIndex != MinigameStepIndex.Step1)
            {
                _isDragging = false;
                return;
            }
            Vector2 position = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame) BeginStroke(position);
            if (_isDragging && Mouse.current.leftButton.isPressed) ContinueStroke(position);
            if (Mouse.current.leftButton.wasReleasedThisFrame) _isDragging = false;
        }

        public void OnPointerDown(PointerEventData eventData) => BeginStroke(eventData.position);
        public void OnDrag(PointerEventData eventData) => ContinueStroke(eventData.position);
        public void OnPointerUp(PointerEventData eventData) => _isDragging = false;

        private bool GetScreenLine(out Vector2 start, out Vector2 end)
        {
            start = end = Vector2.zero;
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || startPoint3D == null || endPoint3D == null) return false;
            Vector3 a = _mainCamera.WorldToScreenPoint(startPoint3D.position);
            Vector3 b = _mainCamera.WorldToScreenPoint(endPoint3D.position);
            if (a.z <= 0f || b.z <= 0f) return false;
            start = a;
            end = b;
            return (end - start).sqrMagnitude > 1f;
        }

        private void BeginStroke(Vector2 position)
        {
            if (_minigame != null && _minigame.CurrentStepIndex != MinigameStepIndex.Step1) return;
            if (_isDragging || _isCompleted || !GetScreenLine(out Vector2 start, out Vector2 end)) return;
            float startDistance = Vector2.Distance(position, start);
            float endDistance = Vector2.Distance(position, end);
            float tolerance = Mathf.Min(pointerTolerancePixels, Vector2.Distance(start, end) * 0.4f);
            if (Mathf.Min(startDistance, endDistance) > tolerance) return;
            _reverseStroke = endDistance < startDistance;
            _currentProgress = 0f;
            _isDragging = true;
        }

        private void ContinueStroke(Vector2 position)
        {
            if (_minigame != null && _minigame.CurrentStepIndex != MinigameStepIndex.Step1) return;
            if (!_isDragging || _isCompleted || !GetScreenLine(out Vector2 start, out Vector2 end)) return;
            if (_reverseStroke) (start, end) = (end, start);
            Vector2 line = end - start;
            float projected = Vector2.Dot(position - start, line) / line.sqrMagnitude;
            Vector2 nearest = start + line * Mathf.Clamp01(projected);
            if (Vector2.Distance(position, nearest) > pointerTolerancePixels) return;
            _currentProgress = Mathf.Max(_currentProgress, Mathf.Clamp01(projected));
            if (_currentProgress >= cutThreshold) CompleteCut();
        }

        public void CompleteCut()
        {
            if (_isCompleted) return;
            if (startPoint3D != null && endPoint3D != null)
            {
                Vector3 stroke = endPoint3D.position - startPoint3D.position;
                if (_reverseStroke) stroke = -stroke;
                CookingSliceVfx.Play(sliceEffect, (startPoint3D.position + endPoint3D.position) * 0.5f,
                    Vector3.Cross(Vector3.up, stroke));
            }
            _isCompleted = true;
            _isDragging = false;
            if (guideLineVisual != null) guideLineVisual.SetActive(false);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(true);
        }
    }
}
