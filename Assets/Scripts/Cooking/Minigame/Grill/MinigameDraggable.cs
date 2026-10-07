using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Marea.Cooking
{
    [RequireComponent(typeof(Collider))]
    public class MinigameDraggable : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("목표 위치 (접시 위의 Target Point)")]
        [SerializeField] private Transform targetPoint;
        [SerializeField] private List<Transform> targetPoints = new List<Transform>();
        [SerializeField] private float snapDistance = 0.5f; // 스냅 인정 거리

        [Header("가이드/하이라이트 표시 (선택)")]
        [SerializeField] private GameObject highlightGuide;
        [SerializeField] private List<GameObject> highlightGuides = new List<GameObject>();

        [Header("플레이팅 효과음")]
        [SerializeField] private AudioSource placeAudioSource;

        private Camera _mainCamera;
        private Vector3 _initialLocalPos;
        private Quaternion _initialRotation;
        private Vector3 _dragOffset;
        private Plane _dragPlane;
        private BaseCookingMinigame _minigame;
        private Collider[] _colliders;
        private bool _hasInitialPose;
        private bool _isDragging;
        private Vector3 _targetPivotOffset;
        private static MinigameDraggable _activeDrag;

        public bool IsPlaced { get; private set; }
        public bool HasPlacementTargets
        {
            get
            {
                if (targetPoint != null) return true;
                if (targetPoints != null)
                    foreach (Transform point in targetPoints) if (point != null) return true;
                return false;
            }
        }

        public void UsePlacementDefaults(MinigameDraggable source)
        {
            if (source == null || source == this) return;
            if (!HasPlacementTargets)
            {
                targetPoint = source.targetPoint;
                targetPoints = source.targetPoints != null
                    ? new List<Transform>(source.targetPoints) : new List<Transform>();
                snapDistance = source.snapDistance;
                highlightGuide = source.highlightGuide;
                highlightGuides = source.highlightGuides != null
                    ? new List<GameObject>(source.highlightGuides) : new List<GameObject>();
                _targetPivotOffset = source.GetVisibleCenterOffset() - GetVisibleCenterOffset();
            }
            if (placeAudioSource == null) placeAudioSource = source.placeAudioSource;
        }

        public void SetPlacementGuidesVisible(bool visible)
        {
            if (highlightGuide != null) highlightGuide.SetActive(visible);
            if (highlightGuides != null)
                foreach (GameObject guide in highlightGuides)
                    if (guide != null) guide.SetActive(visible);
        }

        private Vector3 GetVisibleCenterOffset()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return Quaternion.Inverse(transform.rotation) * (bounds.center - transform.position);
        }

        private Vector3 GetPlacementPosition(Transform point) =>
            point.position + point.rotation * _targetPivotOffset;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            ResetObject();
        }

        public void ResetObject()
        {
            // Reset can be called before Awake while the step panel is inactive.
            EnsureInitialized();
            CancelDrag();
            IsPlaced = false;
            RestoreInitialPose();
            if (highlightGuide != null) highlightGuide.SetActive(true);
            foreach (var guide in highlightGuides)
                if (guide != null) guide.SetActive(true);
        }

        private void EnsureInitialized()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_minigame == null) _minigame = GetComponentInParent<BaseCookingMinigame>();
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>();
            if (_hasInitialPose) return;
            _initialLocalPos = transform.localPosition;
            _initialRotation = transform.localRotation;
            _hasInitialPose = true;
        }

        private bool CanInteract => isActiveAndEnabled && !IsPlaced
            && (_minigame == null || _minigame.CurrentStepIndex == MinigameStepIndex.Step3);

        private void Update()
        {
            if (!CanInteract) { CancelDrag(); return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) BeginDrag(position);
            if (_isDragging && mouse.leftButton.isPressed) MoveDrag(position);
            if (_isDragging && mouse.leftButton.wasReleasedThisFrame) EndDrag(position);
        }

        private void OnDisable() => CancelDrag();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) BeginDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData) => MoveDrag(eventData.position);
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) EndDrag(eventData.position);
        }

        private void BeginDrag(Vector2 screenPosition)
        {
            EnsureInitialized();
            if (!CanInteract || _isDragging || _activeDrag != null || _mainCamera == null) return;
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            bool hitItem = false;
            foreach (Collider itemCollider in _colliders)
            {
                if (itemCollider != null && itemCollider.enabled && itemCollider.gameObject.activeInHierarchy
                    && itemCollider.Raycast(ray, out _, _mainCamera.farClipPlane))
                {
                    hitItem = true;
                    break;
                }
            }
            if (!hitItem) return;
            // Keep the ingredient on the tabletop instead of a camera-facing depth plane.
            _dragPlane = new Plane(Vector3.up, transform.position);
            if (!TryGetDragPosition(screenPosition, out Vector3 position)) return;
            _dragOffset = transform.position - position;
            _isDragging = true;
            _activeDrag = this;
        }

        private void MoveDrag(Vector2 screenPosition)
        {
            if (!CanInteract || !_isDragging) return;
            if (TryGetDragPosition(screenPosition, out Vector3 position))
                transform.position = position + _dragOffset;
        }

        private void EndDrag(Vector2 screenPosition)
        {
            if (!CanInteract || !_isDragging) return;
            MoveDrag(screenPosition);
            CancelDrag();
            Transform placementTarget = FindClosestTarget();
            if (placementTarget != null)
            {
                Vector3 difference = transform.position - GetPlacementPosition(placementTarget);
                difference.y = 0f;
                if (difference.sqrMagnitude <= snapDistance * snapDistance)
                {
                    // 목표 위치 및 회전값으로 스냅 고정
                    transform.position = GetPlacementPosition(placementTarget);
                    transform.rotation = placementTarget.rotation;
                    IsPlaced = true;

                    if (placeAudioSource != null && placeAudioSource.clip != null)
                        placeAudioSource.PlayOneShot(placeAudioSource.clip);

                    if (highlightGuide != null) highlightGuide.SetActive(false);
                    foreach (var guide in highlightGuides)
                        if (guide != null) guide.SetActive(false);
                    return;
                }
            }

            RestoreInitialPose();
        }

        private Transform FindClosestTarget()
        {
            Transform nearest = null;
            float bestDistance = float.PositiveInfinity;
            if (targetPoints != null) foreach (var point in targetPoints)
            {
                if (point == null) continue;
                Vector3 difference = transform.position - GetPlacementPosition(point);
                difference.y = 0f;
                if (difference.sqrMagnitude >= bestDistance) continue;
                bestDistance = difference.sqrMagnitude;
                nearest = point;
            }
            return nearest != null ? nearest : targetPoint;
        }

        private void RestoreInitialPose()
        {
            transform.localPosition = _initialLocalPos;
            transform.localRotation = _initialRotation;
        }

        private void CancelDrag()
        {
            _isDragging = false;
            if (_activeDrag == this) _activeDrag = null;
        }

        private bool TryGetDragPosition(Vector2 screenPosition, out Vector3 position)
        {
            position = default;
            if (_mainCamera == null) return false;
            Ray ray = _mainCamera.ScreenPointToRay(screenPosition);
            if (!_dragPlane.Raycast(ray, out float distance)) return false;
            position = ray.GetPoint(distance);
            return true;
        }
    }
}
