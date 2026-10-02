using System;
using System.Collections;
using System.Collections.Generic;
using EzySlice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class SeafoodSkewersSlicer : MonoBehaviour
    {
        [Header("타이밍 UI 연동")]
        [SerializeField] private SeafoodSkewersTimingUI timingUI;

        [Header("3D 해물/재료 메쉬 및 슬라이스 설정")]
        [SerializeField] private GameObject targetIngredientObject; // 슬라이스 대상 3D 재료 메쉬
        [SerializeField] private List<Transform> sliceGuidePoints;    // 썰어야 할 위치 가이드 지점들
        [SerializeField] private Material crossSectionMaterial;    // 잘린 단면 재질
        [SerializeField] private GameObject knifeVisual;           // 칼 비주얼

        [Header("슬라이스 연출 설정")]
        [SerializeField] private float cutImpulseForce = 0.6f;
        [SerializeField] private ParticleSystem sliceEffect;
        [SerializeField] private AudioSource sliceAudioSource;

        [Header("클릭 판정 설정")]
        [SerializeField] private float hitMaxDistance = 2.0f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        private int _currentGuideIndex;
        private Camera _mainCamera;
        private bool _isSlicingCompleted;

        // --- 컨트롤러 연동용 이벤트 추가 ---
        public event Action OnSlicingCompleted;

        public bool IsCompleted => _isSlicingCompleted;
        public int CurrentSliceCount => _currentGuideIndex;
        public float SliceScoreSum { get; private set; }

        public void ResetSlicer()
        {
            _currentGuideIndex = 0;
            _isSlicingCompleted = false;
            SliceScoreSum = 0f;

            if (targetIngredientObject != null && targetIngredientObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }

            if (timingUI != null)
            {
                timingUI.ShowGauge();
            }

            UpdateGuideVisuals();
        }

        private void Awake()
        {
            _mainCamera = Camera.main;
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

                if (hit.transform == currentGuide || hit.transform.IsChildOf(currentGuide) || distance <= hitMaxDistance || (targetIngredientObject != null && hit.transform.gameObject == targetIngredientObject))
                {
                    PerformSlice(currentGuide);
                }
            }
        }

        private void PerformSlice(Transform currentGuide)
        {
            HitGrade grade = HitGrade.Good;
            if (timingUI != null)
            {
                grade = timingUI.EvaluateSliceTiming();
            }

            if (grade == HitGrade.Miss)
            {
                Debug.Log("[SeafoodSkewersSlicer] 타이밍 실패! (Miss) 다시 시도하세요.");
                return;
            }

            float score = grade switch
            {
                HitGrade.Perfect => 1.0f,
                HitGrade.Good => 0.7f,
                _ => 0.3f
            };

            SliceScoreSum += score;

            Vector3 cutPoint = currentGuide.position;
            Vector3 cutNormal = currentGuide.right;

            ExecuteEzySlice(targetIngredientObject, cutPoint, cutNormal);

            _currentGuideIndex++;
            Debug.Log($"[SeafoodSkewersSlicer] 썰기 성공! 판정: {grade} ({_currentGuideIndex}/{sliceGuidePoints.Count})");

            if (_currentGuideIndex >= sliceGuidePoints.Count)
            {
                _isSlicingCompleted = true;
                if (timingUI != null) timingUI.HideGauge();
                Debug.Log("[SeafoodSkewersSlicer] 모든 위치 해물 재료 슬라이스 완료!");

                // ⭕ 썰기 완결 시 컨트롤러에 알림 발송
                OnSlicingCompleted?.Invoke();
            }
            else
            {
                UpdateGuideVisuals();
            }
        }

        private void ExecuteEzySlice(GameObject objectToCut, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (knifeVisual != null)
            {
                knifeVisual.transform.position = cutPoint;
            }

            if (objectToCut == null) return;

            if (sliceEffect != null)
            {
                sliceEffect.transform.position = cutPoint;
                sliceEffect.Play();
            }

            if (sliceAudioSource != null)
            {
                sliceAudioSource.Play();
            }

            Vector3 originalWorldPos = objectToCut.transform.position;
            Quaternion originalWorldRot = objectToCut.transform.rotation;
            Vector3 originalLocalScale = objectToCut.transform.localScale;
            Transform originalParent = objectToCut.transform.parent;

            SlicedHull hull = objectToCut.Slice(cutPoint, cutNormal, crossSectionMaterial);

            if (hull != null)
            {
                GameObject upperHull = hull.CreateUpperHull(objectToCut, crossSectionMaterial);
                GameObject lowerHull = hull.CreateLowerHull(objectToCut, crossSectionMaterial);

                if (upperHull != null && lowerHull != null)
                {
                    MatchTransform(upperHull, originalParent, originalWorldPos, originalWorldRot, originalLocalScale);
                    MatchTransform(lowerHull, originalParent, originalWorldPos, originalWorldRot, originalLocalScale);

                    MeshFilter upperMF = upperHull.GetComponent<MeshFilter>();
                    MeshFilter lowerMF = lowerHull.GetComponent<MeshFilter>();

                    Vector3 upperCenter = upperMF != null ? upperHull.transform.TransformPoint(upperMF.sharedMesh.bounds.center) : upperHull.transform.position;
                    Vector3 lowerCenter = lowerMF != null ? lowerHull.transform.TransformPoint(lowerMF.sharedMesh.bounds.center) : lowerHull.transform.position;

                    float upperDot = Vector3.Dot((upperCenter - cutPoint), cutNormal);
                    float lowerDot = Vector3.Dot((lowerCenter - cutPoint), cutNormal);

                    GameObject remainingPiece;
                    GameObject separatedPiece;

                    if (upperDot < lowerDot)
                    {
                        separatedPiece = upperHull;
                        remainingPiece = lowerHull;
                    }
                    else
                    {
                        separatedPiece = lowerHull;
                        remainingPiece = upperHull;
                    }

                    SetupSlicedPiece(remainingPiece, isSeparatedPiece: false);
                    SetupSlicedPiece(separatedPiece, isSeparatedPiece: true);

                    Destroy(objectToCut);
                    targetIngredientObject = remainingPiece;
                }
            }
        }

        private void MatchTransform(GameObject piece, Transform parent, Vector3 worldPos, Quaternion worldRot, Vector3 localScale)
        {
            piece.transform.SetParent(parent, false);
            piece.transform.position = worldPos;
            piece.transform.rotation = worldRot;
            piece.transform.localScale = localScale;
        }

        private void SetupSlicedPiece(GameObject piece, bool isSeparatedPiece)
        {
            if (piece == null) return;

            MeshFilter mf = piece.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider collider = piece.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = true;
            }

            Rigidbody rb = piece.AddComponent<Rigidbody>();

            if (isSeparatedPiece)
            {
                piece.layer = LayerMask.NameToLayer("Ignore Raycast");
                rb.isKinematic = false;
                rb.useGravity = true;

                Vector3 leftDir = (Vector3.left + Vector3.up * 0.2f).normalized;
                rb.AddForce(leftDir * cutImpulseForce, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * (cutImpulseForce * 0.5f), ForceMode.Impulse);
            }
            else
            {
                rb.isKinematic = true;
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
