using System.Collections.Generic;
using EzySlice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class DynamicOnionSlicer : MonoBehaviour
    {
        [Header("3D 양파 메쉬 및 슬라이스 설정")]
        [SerializeField] private GameObject targetOnionObject; // 슬라이스 대상 3D 양파
        [SerializeField] private List<Transform> sliceGuidePoints; // 썰어야 할 위치 가이드 지점들
        [SerializeField] private Material crossSectionMaterial; // 잘린 단면 재질
        [SerializeField] private GameObject knifeVisual; // 칼 비주얼

        [Header("슬라이스 연출 설정")]
        [SerializeField] private float cutImpulseForce = 0.6f; // 잘린 조각이 튕겨 나가는 힘 (살짝 감소)
        [SerializeField] private ParticleSystem sliceEffect; // 썰 때 터지는 이펙트
        [SerializeField] private AudioSource sliceAudioSource; // 싹둑 소리 사운드

        [Header("클릭 판정 설정")]
        [SerializeField] private float hitMaxDistance = 2.0f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        private int _currentGuideIndex;
        private Camera _mainCamera;
        private bool _isSlicingCompleted;

        private GameObject _originalOnionTemplate;
        private Transform _originalOnionParent;
        private Vector3 _originalOnionLocalPosition;
        private Quaternion _originalOnionLocalRotation;
        private Vector3 _originalOnionLocalScale;

        private readonly List<GameObject> _runtimeSlicePieces = new List<GameObject>();

        private Vector3 _knifeInitialLocalPosition;
        private Quaternion _knifeInitialLocalRotation;
        private bool _hasKnifeInitialTransform;

        public bool IsCompleted => _isSlicingCompleted;

        public void ResetSlicer()
        {
            _currentGuideIndex = 0;
            _isSlicingCompleted = false;

            RestoreOriginalOnion();

            if (targetOnionObject != null && targetOnionObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }

            if (_hasKnifeInitialTransform && knifeVisual != null)
            {
                knifeVisual.transform.localPosition = _knifeInitialLocalPosition;
                knifeVisual.transform.localRotation = _knifeInitialLocalRotation;
            }

            if (sliceEffect != null)
            {
                sliceEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (sliceAudioSource != null)
            {
                sliceAudioSource.Stop();
            }

            UpdateGuideVisuals();
        }

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (targetOnionObject != null)
            {
                _originalOnionTemplate = targetOnionObject;
                _originalOnionParent = targetOnionObject.transform.parent;
                _originalOnionLocalPosition = targetOnionObject.transform.localPosition;
                _originalOnionLocalRotation = targetOnionObject.transform.localRotation;
                _originalOnionLocalScale = targetOnionObject.transform.localScale;

                // 원본은 복구용 템플릿으로 보관
                _originalOnionTemplate.SetActive(false);
            }

            if (knifeVisual != null)
            {
                _knifeInitialLocalPosition = knifeVisual.transform.localPosition;
                _knifeInitialLocalRotation = knifeVisual.transform.localRotation;
                _hasKnifeInitialTransform = true;
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

                if (hit.transform == currentGuide ||
                    hit.transform.IsChildOf(currentGuide) ||
                    distance <= hitMaxDistance ||
                    hit.transform.gameObject == targetOnionObject)
                {
                    Vector3 cutPoint = currentGuide.position;

                    // 세로 절단을 위한 가이드의 right 방향 사용
                    Vector3 cutNormal = currentGuide.right;

                    bool sliceSuccess = ExecuteEzySlice(
                        targetOnionObject,
                        cutPoint,
                        cutNormal
                    );

                    // 실제 Slice에 성공했을 때만 다음 가이드로 이동
                    if (!sliceSuccess)
                    {
                        return;
                    }

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

        private bool ExecuteEzySlice(GameObject objectToCut, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (knifeVisual != null)
            {
                knifeVisual.transform.position = cutPoint;
            }

            if (objectToCut == null) return false;

            Vector3 originalWorldPos = objectToCut.transform.position;
            Quaternion originalWorldRot = objectToCut.transform.rotation;
            Vector3 originalLocalScale = objectToCut.transform.localScale;
            Transform originalParent = objectToCut.transform.parent;

            SlicedHull hull = objectToCut.Slice(
                cutPoint,
                cutNormal,
                crossSectionMaterial
            );

            if (hull == null)
            {
                Debug.LogWarning(
                    "[DynamicOnionSlicer] Slice 실패! 절단 가이드가 Mesh를 통과하는지 확인해주세요."
                );

                return false;
            }

            GameObject upperHull =
                hull.CreateUpperHull(objectToCut, crossSectionMaterial);

            GameObject lowerHull =
                hull.CreateLowerHull(objectToCut, crossSectionMaterial);

            if (upperHull == null || lowerHull == null)
            {
                if (upperHull != null)
                {
                    Destroy(upperHull);
                }

                if (lowerHull != null)
                {
                    Destroy(lowerHull);
                }

                return false;
            }

            if (sliceEffect != null)
            {
                sliceEffect.transform.position = cutPoint;
                sliceEffect.Play();
            }

            if (sliceAudioSource != null)
            {
                sliceAudioSource.Play();
            }

            _runtimeSlicePieces.Add(upperHull);
            _runtimeSlicePieces.Add(lowerHull);

            MatchTransform(
                upperHull,
                originalParent,
                originalWorldPos,
                originalWorldRot,
                originalLocalScale
            );

            MatchTransform(
                lowerHull,
                originalParent,
                originalWorldPos,
                originalWorldRot,
                originalLocalScale
            );

            // 각 조각의 메쉬 중심점(Bounds Center)을 절단 평면 기준으로 판별
            MeshFilter upperMF = upperHull.GetComponent<MeshFilter>();
            MeshFilter lowerMF = lowerHull.GetComponent<MeshFilter>();

            Vector3 upperCenter =
                upperMF != null && upperMF.sharedMesh != null
                    ? upperHull.transform.TransformPoint(upperMF.sharedMesh.bounds.center)
                    : upperHull.transform.position;

            Vector3 lowerCenter =
                lowerMF != null && lowerMF.sharedMesh != null
                    ? lowerHull.transform.TransformPoint(lowerMF.sharedMesh.bounds.center)
                    : lowerHull.transform.position;

            // 절단 위치에서 중심점으로 향하는 벡터와 절단 법선 벡터를 내적하여 좌/우 판별
            float upperDot =
                Vector3.Dot((upperCenter - cutPoint), cutNormal);

            float lowerDot =
                Vector3.Dot((lowerCenter - cutPoint), cutNormal);

            GameObject remainingPiece;
            GameObject separatedPiece;

            // 내적 값이 음수인 쪽(왼쪽/법선 반대 방향)을 튕겨 나갈 조각으로 선택
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

            // 도마에 남을 오른쪽/본체 조각: 고정
            SetupSlicedPiece(
                remainingPiece,
                isSeparatedPiece: false
            );

            // 왼쪽으로 튕겨 나갈 조각: 물리 및 왼쪽 임펄스 부여
            SetupSlicedPiece(
                separatedPiece,
                isSeparatedPiece: true
            );

            Destroy(objectToCut);
            targetOnionObject = remainingPiece;

            return true;
        }

        private void RestoreOriginalOnion()
        {
            GameObject currentTarget = targetOnionObject;

            if (currentTarget != null &&
                currentTarget != _originalOnionTemplate)
            {
                Destroy(currentTarget);
            }

            for (int i = 0; i < _runtimeSlicePieces.Count; i++)
            {
                GameObject piece = _runtimeSlicePieces[i];

                if (piece != null && piece != currentTarget)
                {
                    Destroy(piece);
                }
            }

            _runtimeSlicePieces.Clear();

            if (_originalOnionTemplate != null)
            {
                targetOnionObject = Instantiate(
                    _originalOnionTemplate,
                    _originalOnionParent
                );

                targetOnionObject.name =
                    _originalOnionTemplate.name;

                targetOnionObject.transform.localPosition =
                    _originalOnionLocalPosition;

                targetOnionObject.transform.localRotation =
                    _originalOnionLocalRotation;

                targetOnionObject.transform.localScale =
                    _originalOnionLocalScale;

                targetOnionObject.SetActive(true);
            }
        }

        private void MatchTransform(
            GameObject piece,
            Transform parent,
            Vector3 worldPos,
            Quaternion worldRot,
            Vector3 localScale)
        {
            piece.transform.SetParent(parent, false);
            piece.transform.position = worldPos;
            piece.transform.rotation = worldRot;
            piece.transform.localScale = localScale;
        }

        private void SetupSlicedPiece(
            GameObject piece,
            bool isSeparatedPiece)
        {
            if (piece == null) return;

            MeshFilter meshFilter =
                piece.GetComponent<MeshFilter>();

            if (meshFilter != null &&
                meshFilter.sharedMesh != null)
            {
                MeshCollider collider =
                    piece.AddComponent<MeshCollider>();

                collider.sharedMesh =
                    meshFilter.sharedMesh;

                collider.convex = true;
            }

            Rigidbody rb =
                piece.AddComponent<Rigidbody>();

            if (isSeparatedPiece)
            {
                rb.isKinematic = false;
                rb.useGravity = true;

                rb.collisionDetectionMode =
                    CollisionDetectionMode.Continuous;

                rb.interpolation =
                    RigidbodyInterpolation.Interpolate;

                // 월드 좌표 기준 왼쪽(Vector3.left) + 미세한 위쪽(Vector3.up * 0.2f)으로 부드럽게 튕김
                Vector3 leftDir =
                    (Vector3.left + Vector3.up * 0.2f).normalized;

                rb.AddForce(
                    leftDir * cutImpulseForce,
                    ForceMode.Impulse
                );

                rb.AddTorque(
                    Random.insideUnitSphere *
                    (cutImpulseForce * 0.5f),
                    ForceMode.Impulse
                );
            }
            else
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void UpdateGuideVisuals()
        {
            if (sliceGuidePoints == null) return;

            for (int i = 0; i < sliceGuidePoints.Count; i++)
            {
                if (sliceGuidePoints[i] != null)
                {
                    sliceGuidePoints[i].gameObject.SetActive(
                        i == _currentGuideIndex
                    );
                }
            }
        }
    }
}
