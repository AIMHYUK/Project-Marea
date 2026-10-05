using UnityEngine;
using UnityEngine.EventSystems;

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

        [Header("드래그 완성 인정 기준 (0.0 ~ 1.0)")]
        [SerializeField] private float cutThreshold = 0.75f;

        private bool _isCompleted;
        private float _currentProgress;
        private Camera _mainCamera;

        public bool IsCompleted => _isCompleted;

        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            ResetGuide();
        }

        public void ResetGuide()
        {
            _isCompleted = false;
            _currentProgress = 0f;
            if (guideLineVisual != null) guideLineVisual.SetActive(true);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isCompleted || startPoint3D == null || endPoint3D == null) return;

            Ray ray = _mainCamera.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 hitPoint = hit.point;
                Vector3 lineVector = endPoint3D.position - startPoint3D.position;
                Vector3 mouseVector = hitPoint - startPoint3D.position;

                float lineLength = lineVector.magnitude;
                if (lineLength <= Mathf.Epsilon) return;

                float progress = Mathf.Clamp01(Vector3.Dot(mouseVector, lineVector.normalized) / lineLength);

                if (progress > _currentProgress)
                {
                    _currentProgress = progress;
                }

                if (_currentProgress >= cutThreshold)
                {
                    CompleteCut();
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData) { }

        public void CompleteCut()
        {
            _isCompleted = true;
            if (guideLineVisual != null) guideLineVisual.SetActive(false);
            if (cutMarkVisual != null) cutMarkVisual.SetActive(true);
        }
    }
}
