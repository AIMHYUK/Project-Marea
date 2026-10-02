using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class IngredientDraggable : MonoBehaviour
    {
        [Header("재료 정보")]
        [SerializeField] private int ingredientId;

        private Camera _mainCamera;
        private Vector3 _startPosition;
        private bool _isDragging;
        private bool _isPlaced;
        private Transform _currentTargetSlot;
        private float _snapDistance = 1.5f;

        public int IngredientId => ingredientId;
        public bool IsPlaced => _isPlaced;

        public event Action<IngredientDraggable> OnPlacedSuccess;

        private void Awake()
        {
            _mainCamera = Camera.main;
            _startPosition = transform.position;
        }

        public void Setup(Transform targetSlot, float snapDistance = 1.5f)
        {
            _currentTargetSlot = targetSlot;
            _snapDistance = snapDistance;
            _isPlaced = false;
            _isDragging = false;
            _startPosition = transform.position;
        }

        private void Update()
        {
            if (_isPlaced) return;

            if (Mouse.current == null) return;

            // 마우스 눌렀을 때 (Raycast로 자기 자신 선택)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckSelect();
            }
            // 마우스 드래그 중
            else if (_isDragging && Mouse.current.leftButton.isPressed)
            {
                DragFollowMouse();
            }
            // 마우스 뗐을 때
            else if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                EndDrag();
            }
        }

        private void CheckSelect()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    _isDragging = true;
                }
            }
        }

        private void DragFollowMouse()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // 카메라 전방 기준 평면에 마우스 위치 투영
            Plane plane = new Plane(-_mainCamera.transform.forward, transform.position);

            if (plane.Raycast(ray, out float enter))
            {
                transform.position = ray.GetPoint(enter);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;

            if (_currentTargetSlot != null)
            {
                float dist = Vector3.Distance(transform.position, _currentTargetSlot.position);
                if (dist <= _snapDistance)
                {
                    // 스냅 처리
                    transform.position = _currentTargetSlot.position;
                    transform.rotation = _currentTargetSlot.rotation;
                    _isPlaced = true;
                    OnPlacedSuccess?.Invoke(this);
                    return;
                }
            }

            // 실패 시 원래 위치 복귀
            transform.position = _startPosition;
        }
    }
}
