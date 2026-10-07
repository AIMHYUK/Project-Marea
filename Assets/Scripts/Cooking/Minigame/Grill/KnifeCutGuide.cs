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
        [SerializeField] private Marea.Food.CutDecalReveal cutDecalReveal;
        [SerializeField] private ParticleSystem sliceEffect;

        [Header("화살표 안내 애니메이션")]
        [SerializeField] private bool animateArrow = true;
        [InspectorName("화살표 이동 거리")]
        [SerializeField, Min(0f)] private float arrowTravelDistance = 0.035f;
        [InspectorName("화살표 반복 시간 (초)")]
        [SerializeField, Min(0.1f)] private float arrowCycleSeconds = 1.3f;

        [Header("드래그 완성 인정 기준 (0.0 ~ 1.0)")]
        [SerializeField] private float cutThreshold = 0.75f;
        [SerializeField, Min(1f)] private float pointerTolerancePixels = 24f;

        private bool _isCompleted;
        private float _currentProgress;
        private Camera _mainCamera;
        private BaseCookingMinigame _minigame;
        private bool _isDragging;
        private bool _reverseStroke;
        private float _strokeStartProjection;
        private Vector3 _guideInitialLocalPosition;
        private Vector3 _guideWorldOffset;
        private float _arrowElapsed;
        private bool _guidePoseCached;

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
            RestoreArrowPose();
            _isCompleted = false;
            _currentProgress = 0f;
            _isDragging = false;
            if (sliceEffect != null)
                sliceEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (guideLineVisual != null) guideLineVisual.SetActive(true);
            if (cutDecalReveal != null) cutDecalReveal.SetProgress(0f);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(false);
        }

        private void Update()
        {
            if (_isCompleted) return;
            if (_minigame != null && _minigame.CurrentStepIndex != MinigameStepIndex.Step1)
            {
                _isDragging = false;
                RestoreArrowPose();
                return;
            }
            AnimateArrow();
            if (Mouse.current == null) return;
            Vector2 position = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame) BeginStroke(position);
            if (_isDragging && Mouse.current.leftButton.isPressed) ContinueStroke(position);
            if (Mouse.current.leftButton.wasReleasedThisFrame) _isDragging = false;
        }

        public void OnPointerDown(PointerEventData eventData) => BeginStroke(eventData.position);
        public void OnDrag(PointerEventData eventData) => ContinueStroke(eventData.position);
        public void OnPointerUp(PointerEventData eventData) => _isDragging = false;

        private void RestoreArrowPose()
        {
            if (guideLineVisual == null) return;
            if (!_guidePoseCached)
            {
                _guideInitialLocalPosition = guideLineVisual.transform.localPosition;
                _guidePoseCached = true;
            }
            guideLineVisual.transform.localPosition = _guideInitialLocalPosition;
            _guideWorldOffset = Vector3.zero;
            _arrowElapsed = 0f;
        }

        private void AnimateArrow()
        {
            if (!animateArrow || _isDragging || guideLineVisual == null ||
                startPoint3D == null || endPoint3D == null)
            {
                RestoreArrowPose();
                return;
            }
            if (!_guidePoseCached) RestoreArrowPose();
            _arrowElapsed += Time.unscaledDeltaTime;
            float phase = _arrowElapsed / Mathf.Max(0.1f, arrowCycleSeconds);
            float travel = (1f - Mathf.Cos(phase * Mathf.PI * 2f)) * 0.5f;
            Vector3 direction = Vector3.ProjectOnPlane(
                endPoint3D.position - startPoint3D.position, Vector3.up).normalized;
            Transform arrow = guideLineVisual.transform;
            Vector3 origin = arrow.parent != null
                ? arrow.parent.TransformPoint(_guideInitialLocalPosition) : _guideInitialLocalPosition;
            _guideWorldOffset = direction * (travel * arrowTravelDistance);
            arrow.position = origin + _guideWorldOffset;
        }

        private void OnDisable() => RestoreArrowPose();

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
            Vector2 line = end - start;
            float earliest = 0f;
            // The arrow sits beside the fish. Include its shaft in the input corridor.
            if (guideLineVisual != null)
            {
                foreach (LineRenderer renderer in guideLineVisual.GetComponentsInChildren<LineRenderer>(false))
                {
                    for (int i = 0; i < renderer.positionCount; i++)
                    {
                        Vector3 world = renderer.useWorldSpace ? renderer.GetPosition(i)
                            : renderer.transform.TransformPoint(renderer.GetPosition(i));
                        // Use the resting arrow position for a stable input corridor.
                        if (!renderer.useWorldSpace) world -= _guideWorldOffset;
                        Vector3 screen = _mainCamera.WorldToScreenPoint(world);
                        if (screen.z <= 0f) continue;
                        float projection = Vector2.Dot((Vector2)screen - start, line) / line.sqrMagnitude;
                        earliest = Mathf.Min(earliest, Mathf.Max(-2f, projection));
                    }
                }
            }
            float pointerProjection = Vector2.Dot(position - start, line) / line.sqrMagnitude;
            Vector2 nearest = start + line * Mathf.Clamp(pointerProjection, earliest, 1f);
            if (Vector2.Distance(position, nearest) > pointerTolerancePixels) return;
            _reverseStroke = pointerProjection > 0.5f;
            _strokeStartProjection = _reverseStroke ? 1f - pointerProjection : pointerProjection;
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
            Vector2 nearest = start + line * projected;
            if (Vector2.Distance(position, nearest) > pointerTolerancePixels) return;
            // Require a real stroke even when the player starts partway along the fish.
            _currentProgress = Mathf.Max(_currentProgress,
                Mathf.Clamp01((projected - _strokeStartProjection)
                    / Mathf.Max(0.5f, 1f - _strokeStartProjection)));
            if (_currentProgress >= cutThreshold) CompleteCut();
        }

        public void CompleteCut()
        {
            if (_isCompleted) return;
            if (_minigame is FishGrillMinigameController fishGrill)
                fishGrill.PlayCutSound();
            if (startPoint3D != null && endPoint3D != null)
            {
                Vector3 stroke = endPoint3D.position - startPoint3D.position;
                if (_reverseStroke) stroke = -stroke;
                CookingSliceVfx.Play(sliceEffect, (startPoint3D.position + endPoint3D.position) * 0.5f,
                    Vector3.Cross(Vector3.up, stroke));
            }
            _isCompleted = true;
            _isDragging = false;
            RestoreArrowPose();
            if (guideLineVisual != null) guideLineVisual.SetActive(false);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(true);
            if (cutDecalReveal != null) cutDecalReveal.SetProgress(1f);
        }
    }
}
