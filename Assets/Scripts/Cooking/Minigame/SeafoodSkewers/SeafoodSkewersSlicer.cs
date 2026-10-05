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

        [Header("칼 이동 애니메이션 설정")]
        [SerializeField] private float knifeApproachDuration = 0.15f;
        [SerializeField] private float knifeCutDuration = 0.25f;

        [Tooltip("SliceGuide 기준 칼이 썰기 전에 대기할 위치")]
        [SerializeField] private Vector3 knifeReadyOffset = new Vector3(0f, 0.35f, -0.15f);

        [Tooltip("SliceGuide 기준 칼이 재료를 통과한 뒤 도착할 위치")]
        [SerializeField] private Vector3 knifeEndOffset = new Vector3(0f, -0.15f, 0.15f);

        [Header("클릭 판정 설정")]
        [SerializeField] private float hitMaxDistance = 2.0f;
        [SerializeField] private LayerMask raycastLayerMask = ~0;

        private int _currentGuideIndex;
        private Camera _mainCamera;
        private bool _isSlicingCompleted;
        private bool _isKnifeAnimating;

        private GameObject _originalIngredientTemplate;
        private Transform _originalIngredientParent;
        private Vector3 _originalIngredientLocalPosition;
        private Quaternion _originalIngredientLocalRotation;
        private Vector3 _originalIngredientLocalScale;

        private readonly List<GameObject> _runtimeSlicePieces = new List<GameObject>();

        private Vector3 _knifeInitialPosition;
        private Quaternion _knifeInitialRotation;
        private bool _hasKnifeInitialTransform;

        // --- 컨트롤러 연동용 이벤트 추가 ---
        public event Action OnSlicingCompleted;

        public bool IsCompleted => _isSlicingCompleted;
        public int CurrentSliceCount => _currentGuideIndex;
        public float SliceScoreSum { get; private set; }

        public void ResetSlicer()
        {
            StopAllCoroutines();
            _isKnifeAnimating = false;

            _currentGuideIndex = 0;
            _isSlicingCompleted = false;
            SliceScoreSum = 0f;

            RestoreOriginalIngredient();

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

        public void ResetSlicerState()
        {
            StopAllCoroutines();
            _isKnifeAnimating = false;

            _currentGuideIndex = 0;
            _isSlicingCompleted = false;
            SliceScoreSum = 0f;

            RestoreOriginalIngredient();

            if (targetIngredientObject != null && targetIngredientObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }

            if (timingUI != null)
            {
                timingUI.HideGauge();
            }

            UpdateGuideVisuals();
        }

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (targetIngredientObject != null)
            {
                _originalIngredientTemplate = targetIngredientObject;
                _originalIngredientParent = targetIngredientObject.transform.parent;
                _originalIngredientLocalPosition = targetIngredientObject.transform.localPosition;
                _originalIngredientLocalRotation = targetIngredientObject.transform.localRotation;
                _originalIngredientLocalScale = targetIngredientObject.transform.localScale;

                _originalIngredientTemplate.SetActive(false);
            }

            if (knifeVisual != null)
            {
                _knifeInitialPosition = knifeVisual.transform.localPosition;
                _knifeInitialRotation = knifeVisual.transform.localRotation;
                _hasKnifeInitialTransform = true;
            }
        }

        private void Start()
        {
            ResetSlicer();
        }

        private void Update()
        {
            if (_isSlicingCompleted || _isKnifeAnimating || sliceGuidePoints == null || sliceGuidePoints.Count == 0) return;

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

            StartCoroutine(PerformSliceAnimation(currentGuide, grade, score));
        }

        private IEnumerator PerformSliceAnimation(Transform currentGuide, HitGrade grade, float score)
        {
            _isKnifeAnimating = true;

            Vector3 cutPoint = currentGuide.position;
            Vector3 cutNormal = currentGuide.right;

            Vector3 readyPosition =
                cutPoint + currentGuide.TransformDirection(knifeReadyOffset);

            Vector3 endPosition =
                cutPoint + currentGuide.TransformDirection(knifeEndOffset);

            if (knifeVisual != null)
            {
                Vector3 startPosition = knifeVisual.transform.position;

                // 현재 위치 → 썰기 준비 위치
                yield return MoveKnife(
                    startPosition,
                    readyPosition,
                    knifeApproachDuration
                );

                // 준비 위치 → 실제 절단 위치
                yield return MoveKnife(
                    readyPosition,
                    cutPoint,
                    knifeCutDuration * 0.45f
                );
            }

            // 칼이 실제 재료 위치에 도달했을 때 Slice 처리
            ExecuteEzySlice(targetIngredientObject, cutPoint, cutNormal);

            if (knifeVisual != null)
            {
                // 절단 위치 → 재료를 완전히 통과
                yield return MoveKnife(
                    cutPoint,
                    endPosition,
                    knifeCutDuration * 0.55f
                );
            }

            SliceScoreSum += score;

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

            _isKnifeAnimating = false;
        }

        private IEnumerator MoveKnife(Vector3 startPosition, Vector3 endPosition, float duration)
        {
            if (knifeVisual == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                knifeVisual.transform.position = endPosition;
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0f, 1f, t);

                knifeVisual.transform.position =
                    Vector3.Lerp(startPosition, endPosition, t);

                yield return null;
            }

            knifeVisual.transform.position = endPosition;
        }

        private void ExecuteEzySlice(GameObject objectToCut, Vector3 cutPoint, Vector3 cutNormal)
        {
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
                    _runtimeSlicePieces.Add(upperHull);
                    _runtimeSlicePieces.Add(lowerHull);

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

        private void RestoreOriginalIngredient()
        {
            GameObject currentTarget = targetIngredientObject;

            if (currentTarget != null && currentTarget != _originalIngredientTemplate)
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

            if (_originalIngredientTemplate != null)
            {
                targetIngredientObject = Instantiate(_originalIngredientTemplate, _originalIngredientParent);
                targetIngredientObject.name = _originalIngredientTemplate.name;
                targetIngredientObject.transform.localPosition = _originalIngredientLocalPosition;
                targetIngredientObject.transform.localRotation = _originalIngredientLocalRotation;
                targetIngredientObject.transform.localScale = _originalIngredientLocalScale;
                targetIngredientObject.SetActive(true);
            }

            if (_hasKnifeInitialTransform && knifeVisual != null)
            {
                knifeVisual.transform.localPosition = _knifeInitialPosition;
                knifeVisual.transform.localRotation = _knifeInitialRotation;
            }

            if (sliceEffect != null)
            {
                sliceEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
