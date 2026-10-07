using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class SkewerSauceBrush : MonoBehaviour
    {
        [SerializeField] private SkewerMinigameController controller;
        [SerializeField] private Transform brushTip;
        [Header("카메라 방향 회전")]
        [Tooltip("드래그 중 카메라 방향을 기준으로 적용할 브러시 각도입니다.")]
        [InspectorName("바를 때 각도 (카메라 기준)")]
        [SerializeField] private Vector3 cameraFacingEuler = new Vector3(0f, 0f, 140f);
        [SerializeField, Min(0.1f)] private float rotationSpeed = 12f;
        [Header("위아래 붓질 기울기")]
        [SerializeField, Range(0f, 60f)] private float strokeTiltAngle = 32f;
        [Tooltip("최대 기울기에 도달하는 세로 이동 속도 (1080p 픽셀/초)")]
        [SerializeField, Min(50f)] private float fullTiltSpeed = 360f;
        [SerializeField, Min(30f)] private float maxTurnSpeed = 180f;
        [Tooltip("재료 하나를 완전히 바르는 데 필요한 브러시 이동량 (1080p 픽셀)")]
        [SerializeField, Min(100f)] private float requiredStrokeDistance = 420f;
        private Camera _camera;
        private Collider[] _colliders;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private Vector2 _lastTipPosition;
        private Vector2 _lastDragPointer;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private bool _capturedPose;
        private bool _dragging;

        private void OnEnable()
        {
            if (!_capturedPose)
            {
                _initialPosition = transform.localPosition;
                _initialRotation = transform.localRotation;
                _capturedPose = true;
            }
            transform.localPosition = _initialPosition;
            transform.localRotation = _initialRotation;
            _dragging = false;
            _colliders = GetComponentsInChildren<Collider>();
            if (controller == null) controller = GetComponentInParent<SkewerMinigameController>();
        }

        private void OnDisable() => _dragging = false;

        private void Update()
        {
            if (controller == null || controller.CurrentStepIndex != MinigameStepIndex.Step3)
            {
                _dragging = false;
                return;
            }
            var mouse = Mouse.current;
            if (mouse == null || brushTip == null) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Ray ray = _camera.ScreenPointToRay(pointer);
                foreach (var collider in _colliders)
                {
                    if (collider == null || !collider.enabled ||
                        !collider.Raycast(ray, out _, _camera.farClipPlane)) continue;
                    _dragPlane = new Plane(_camera.transform.forward, brushTip.position);
                    if (!_dragPlane.Raycast(ray, out float distance)) break;
                    _dragOffset = brushTip.position - ray.GetPoint(distance);
                    _lastTipPosition = _camera.WorldToScreenPoint(brushTip.position);
                    _lastDragPointer = pointer;
                    _dragging = true;
                    break;
                }
            }
            if (!mouse.leftButton.isPressed) _dragging = false;
            if (!_dragging)
            {
                RotateForBrushing(false);
                return;
            }
            Ray dragRay = _camera.ScreenPointToRay(pointer);
            if (!_dragPlane.Raycast(dragRay, out float depth)) return;
            // Keep the bristles under the same drag anchor while the handle turns.
            transform.position += dragRay.GetPoint(depth) + _dragOffset - brushTip.position;
            float screenScale = Mathf.Max(0.1f, _camera.pixelHeight / 1080f);
            float verticalSpeed = (pointer.y - _lastDragPointer.y) /
                Mathf.Max(0.001f, Time.deltaTime) / screenScale;
            _lastDragPointer = pointer;
            RotateForBrushing(true, verticalSpeed);
            Vector2 tipPosition = _camera.WorldToScreenPoint(brushTip.position);
            float travel = Vector2.Distance(tipPosition, _lastTipPosition);
            // A stationary held brush does not paint; fast jumps cannot fill a dish at once.
            if (travel < 3f * screenScale) return;
            int samples = Mathf.Clamp(Mathf.CeilToInt(travel / (6f * screenScale)), 1, 64);
            float amount = Mathf.Min(travel / screenScale, 80f) / requiredStrokeDistance / samples;
            for (int i = 1; i <= samples; i++)
                controller.ProcessSauceDrag(Vector2.Lerp(_lastTipPosition, tipPosition, (float)i / samples), amount);
            _lastTipPosition = tipPosition;
        }

        private void RotateForBrushing(bool brushing, float verticalSpeed = 0f)
        {
            Vector3 tipPosition = brushTip.position;
            Quaternion restingRotation = transform.parent != null
                ? transform.parent.rotation * _initialRotation : _initialRotation;
            Quaternion desired = restingRotation;
            if (brushing)
            {
                desired = _camera.transform.rotation * Quaternion.Euler(cameraFacingEuler);
                float stroke = Mathf.Abs(verticalSpeed) < 20f ? 0f :
                    Mathf.Clamp(verticalSpeed / fullTiltSpeed, -1f, 1f);
                Vector3 localTip = transform.InverseTransformDirection(brushTip.position - transform.position).normalized;
                Vector3 facingTip = Quaternion.Euler(cameraFacingEuler) * localTip;
                // Define the bristle slope in camera space, so custom base angles keep the same stroke direction.
                float pitch = Mathf.Atan2(facingTip.y, Mathf.Max(0.001f, Mathf.Abs(facingTip.z))) * Mathf.Rad2Deg;
                float targetPitch = Mathf.Lerp(pitch, -Mathf.Sign(stroke) * strokeTiltAngle, Mathf.Abs(stroke));
                float tilt = (pitch - targetPitch) * (facingTip.z >= 0f ? 1f : -1f);
                desired = _camera.transform.rotation * Quaternion.Euler(tilt, 0f, 0f) * Quaternion.Euler(cameraFacingEuler);
            }
            Quaternion smoothed = Quaternion.Slerp(transform.rotation, desired,
                1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            transform.rotation = Quaternion.RotateTowards(transform.rotation, smoothed,
                maxTurnSpeed * Time.deltaTime);
            // Rotation alone must not move the paint contact or increase progress.
            transform.position += tipPosition - brushTip.position;
        }
    }
}
