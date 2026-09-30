using System.Collections.Generic;
using com.marufhow.meshslicer.core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class DynamicOnionSlicer : MonoBehaviour
    {
        [Header("3D 양파 메쉬 및 슬라이스 설정")]
        [SerializeField] private GameObject targetOnionObject; // 슬라이스 대상 3D 양파
        [SerializeField] private List<Transform> sliceGuidePoints; // 썰어야 할 위치 가이드 지점들
        [SerializeField] private GameObject knifeVisual; // 칼 비주얼

        [Header("Dynamic Mesh Slicer 연동")]
        [SerializeField] private MHCutter mhCutter; // 에셋의 MHCutter 컴포넌트 참조

        [Header("클릭 판정 설정")]
        [SerializeField] private float hitMaxDistance = 1.5f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        private int _currentGuideIndex;
        private Camera _mainCamera;
        private bool _isSlicingCompleted;

        public bool IsCompleted => _isSlicingCompleted;

        public void ResetSlicer()
        {
            _currentGuideIndex = 0;
            _isSlicingCompleted = false;

            if (targetOnionObject != null && targetOnionObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }

            UpdateGuideVisuals();
        }

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (mhCutter == null)
            {
                mhCutter = FindFirstObjectByType<MHCutter>();
            }
        }

        private void Start()
        {
            ResetSlicer();
        }

        private void Update()
        {
            if (_isSlicingCompleted || sliceGuidePoints == null || sliceGuidePoints.Count == 0) return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CheckSliceHit();
            }
        }

        private void CheckSliceHit()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = _mainCamera.ScreenPointToRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, raycastLayerMask))
            {
                Transform currentGuide = sliceGuidePoints[_currentGuideIndex];
                if (currentGuide == null) return;

                float distance = Vector3.Distance(hit.point, currentGuide.position);

                // 클릭 대상이 가이드이거나 가이드 근처/양파 클릭 시
                if (hit.transform == currentGuide || hit.transform.IsChildOf(currentGuide) || distance <= hitMaxDistance || hit.transform.gameObject == targetOnionObject)
                {
                    // hit.collider.gameObject 대신 절단 대상인 targetOnionObject를 직접 전달
                    ExecuteMeshSlice(targetOnionObject, currentGuide.position, currentGuide.up);
                    _currentGuideIndex++;

                    if (_currentGuideIndex >= sliceGuidePoints.Count)
                    {
                        _isSlicingCompleted = true;
                        Debug.Log("[DynamicOnionSlicer] 모든 위치 양파 슬라이스 완료!");
                    }
                    else
                    {
                        UpdateGuideVisuals();
                    }
                }
            }
        }

        private void ExecuteMeshSlice(GameObject objectToCut, Vector3 cutPoint, Vector3 cutDirection)
        {
            if (knifeVisual != null)
            {
                knifeVisual.transform.position = cutPoint;
            }

            if (objectToCut == null)
            {
                Debug.LogWarning("[DynamicOnionSlicer] 절단할 targetOnionObject가 할당되지 않았습니다.");
                return;
            }

            // MHCutter 컴포넌트로 실제 메쉬 슬라이스 실행
            if (mhCutter != null)
            {
                mhCutter.Cut(objectToCut, cutPoint, cutDirection);
                Debug.Log($"[DynamicOnionSlicer] {objectToCut.name} 메쉬 슬라이스 실행! 위치: {cutPoint}");
            }
            else
            {
                Debug.LogWarning("[DynamicOnionSlicer] 씬 내에서 MHCutter 컴포넌트를 찾지 못했습니다.");
            }
        }

        private void UpdateGuideVisuals()
        {
            for (int i = 0; i < sliceGuidePoints.Count; i++)
            {
                if (sliceGuidePoints[i] != null)
                {
                    sliceGuidePoints[i].gameObject.SetActive(i == _currentGuideIndex);
                }
            }
        }
    }
}
