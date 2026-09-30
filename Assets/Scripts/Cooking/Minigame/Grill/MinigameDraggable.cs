using UnityEngine;
using UnityEngine.EventSystems;

namespace Marea.Cooking
{
    [RequireComponent(typeof(Collider))]
    public class MinigameDraggable : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("목표 위치 (접시 위의 Target Point)")]
        [SerializeField] private Transform targetPoint;
        [SerializeField] private float snapDistance = 0.5f; // 스냅 인정 거리

        [Header("가이드/하이라이트 표시 (선택)")]
        [SerializeField] private GameObject highlightGuide;

        private Camera _mainCamera;
        private Vector3 _initialWorldPos;
        private Quaternion _initialRotation;
        private Vector3 _dragOffset;
        private float _cameraDistance;

        public bool IsPlaced { get; private set; }

        private void Awake()
        {
            _mainCamera = Camera.main;
            _initialWorldPos = transform.position;
            _initialRotation = transform.rotation;
        }

        private void OnEnable()
        {
            ResetObject();
        }

        public void ResetObject()
        {
            IsPlaced = false;
            transform.position = _initialWorldPos;
            transform.rotation = _initialRotation;
            if (highlightGuide != null) highlightGuide.SetActive(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("생선 클릭됨!");
            if (IsPlaced) return;

            // 카메라와 오브젝트 간의 깊이 거리 계산
            _cameraDistance = Vector3.Distance(_mainCamera.transform.position, transform.position);
            Vector3 mouseWorldPos = GetMouseWorldPosition(eventData.position);
            _dragOffset = transform.position - mouseWorldPos;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Debug.Log("생선 드래그 중!");
            if (IsPlaced) return;

            Vector3 currentMouseWorld = GetMouseWorldPosition(eventData.position);
            transform.position = currentMouseWorld + _dragOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (IsPlaced) return;

            // 접시 위 목표 지점과의 거리 체크
            if (targetPoint != null)
            {
                float distance = Vector3.Distance(transform.position, targetPoint.position);
                if (distance <= snapDistance)
                {
                    // 목표 위치 및 회전값으로 스냅 고정
                    transform.position = targetPoint.position;
                    transform.rotation = targetPoint.rotation;
                    IsPlaced = true;

                    if (highlightGuide != null) highlightGuide.SetActive(false);
                    return;
                }
            }

            // 스냅 실패 시 원래 시작 위치로 제자리 원복
            transform.position = _initialWorldPos;
            transform.rotation = _initialRotation;
        }

        private Vector3 GetMouseWorldPosition(Vector2 screenPos)
        {
            Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, _cameraDistance);
            return _mainCamera.ScreenToWorldPoint(screenPoint);
        }
    }
}
