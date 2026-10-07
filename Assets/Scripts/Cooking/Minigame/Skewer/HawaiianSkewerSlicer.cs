using System;
using System.Collections;
using System.Collections.Generic;
using EzySlice;
using UnityEngine;

namespace Marea.Cooking
{
    public class HawaiianSkewerSlicer : MonoBehaviour
    {
        [Header("3D 대상 및 가이드 지점")]
        [SerializeField] private GameObject targetIngredientObject; // 썰 대상 3D 재료
        [SerializeField] private List<Transform> sliceGuidePoints;    // 썰 지점 가이드들
        [SerializeField] private Material crossSectionMaterial;    // 단면 재질
        [SerializeField] private GameObject knifeVisual;           // 칼 모델

        [Header("슬라이스 연출")]
        [SerializeField] private float cutImpulseForce = 0.6f;
        [SerializeField] private ParticleSystem sliceEffect;
        [SerializeField] private AudioSource sliceAudioSource;

        [Header("칼 썰기 애니메이션")]
        [SerializeField] private float knifeApproachDuration = 0.15f;
        [SerializeField] private float knifeCutDuration = 0.25f;

        [Tooltip("가이드 기준 칼이 썰기 전에 이동할 위치")]
        [SerializeField]
        private Vector3 knifeReadyOffset =
            new Vector3(0f, 0.35f, -0.15f);

        [Tooltip("가이드 기준 칼이 재료를 완전히 통과한 뒤 위치")]
        [SerializeField]
        private Vector3 knifeEndOffset =
            new Vector3(0f, -0.15f, 0.15f);

        private int _currentGuideIndex;
        private bool _isSlicingCompleted;
        private bool _isKnifeAnimating;

        private GameObject _originalIngredient;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private Vector3 _originalScale;
        private Vector3 _knifePosition;
        private Quaternion _knifeRotation;
        private bool _initialStateCaptured;

        private readonly List<GameObject> _generatedPieces = new();
        private readonly HashSet<Mesh> _generatedMeshes = new();

        public event Action OnSlicingCompleted;
        public bool IsCompleted => _isSlicingCompleted;
        public int CurrentSliceCount => _currentGuideIndex;

        public void ResetSlicer()
        {
            StopAllCoroutines();
            _isKnifeAnimating = false;

            CaptureInitialState();
            ClearGeneratedPieces();

            targetIngredientObject = _originalIngredient;

            if (_originalIngredient != null)
            {
                _originalIngredient.transform.localPosition = _originalPosition;
                _originalIngredient.transform.localRotation = _originalRotation;
                _originalIngredient.transform.localScale = _originalScale;
                _originalIngredient.SetActive(true);
            }

            if (knifeVisual != null)
            {
                knifeVisual.transform.localPosition = _knifePosition;
                knifeVisual.transform.localRotation = _knifeRotation;
                knifeVisual.SetActive(true);
            }

            if (sliceEffect != null)
            {
                sliceEffect.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }

            if (sliceAudioSource != null)
            {
                sliceAudioSource.Stop();
            }

            _currentGuideIndex = 0;
            _isSlicingCompleted = false;

            if (targetIngredientObject != null &&
                targetIngredientObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
            }

            UpdateGuideVisuals();

            if (targetIngredientObject != null)
            {
                Debug.Log(
                    $"[HawaiianSkewerSlicer] 재료 초기화 완료 / " +
                    $"Name={targetIngredientObject.name}, " +
                    $"activeSelf={targetIngredientObject.activeSelf}, " +
                    $"activeInHierarchy={targetIngredientObject.activeInHierarchy}, " +
                    $"Parent={targetIngredientObject.transform.parent?.name}"
                );
            }
        }

        public void ShowSlicerObjects()
        {
            gameObject.SetActive(true);

            if (targetIngredientObject != null)
            {
                targetIngredientObject.SetActive(true);
            }

            if (knifeVisual != null)
            {
                knifeVisual.SetActive(true);
            }

            UpdateGuideVisuals();
        }

        private void CaptureInitialState()
        {
            if (_initialStateCaptured) return;

            _initialStateCaptured = true;

            _originalIngredient = targetIngredientObject;

            if (_originalIngredient != null)
            {
                _originalPosition =
                    _originalIngredient.transform.localPosition;

                _originalRotation =
                    _originalIngredient.transform.localRotation;

                _originalScale =
                    _originalIngredient.transform.localScale;
            }

            if (knifeVisual != null)
            {
                _knifePosition =
                    knifeVisual.transform.localPosition;

                _knifeRotation =
                    knifeVisual.transform.localRotation;
            }
        }

        private void TrackPiece(GameObject piece)
        {
            if (piece == null) return;

            _generatedPieces.Add(piece);

            foreach (MeshFilter filter in
                     piece.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    _generatedMeshes.Add(filter.sharedMesh);
                }
            }
        }

        private void ClearGeneratedPieces()
        {
            foreach (GameObject piece in _generatedPieces)
            {
                if (piece == null) continue;

                piece.SetActive(false);
                Destroy(piece);
            }

            _generatedPieces.Clear();

            foreach (Mesh mesh in _generatedMeshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            _generatedMeshes.Clear();
        }

        private void OnDestroy()
        {
            ClearGeneratedPieces();
        }

        // 타이밍 상관없이 호출 시 가이드선 위치에서 바로 썰기 수행
        public void ExecuteSlice()
        {
            CaptureInitialState();

            if (_isKnifeAnimating)
            {
                return;
            }

            if (_isSlicingCompleted ||
                sliceGuidePoints == null ||
                sliceGuidePoints.Count == 0)
            {
                return;
            }

            Transform currentGuide =
                sliceGuidePoints[_currentGuideIndex];

            if (currentGuide == null)
            {
                return;
            }

            StartCoroutine(
                PerformSliceAnimation(currentGuide)
            );
        }

        private IEnumerator PerformSliceAnimation(
            Transform currentGuide)
        {
            _isKnifeAnimating = true;

            Vector3 cutPoint =
                currentGuide.position;

            Vector3 cutNormal =
                currentGuide.right;

            Vector3 readyPosition =
                cutPoint +
                currentGuide.TransformDirection(
                    knifeReadyOffset
                );

            Vector3 endPosition =
                cutPoint +
                currentGuide.TransformDirection(
                    knifeEndOffset
                );

            if (knifeVisual != null)
            {
                Vector3 startPosition =
                    knifeVisual.transform.position;

                // 현재 위치 → 재료 위의 준비 위치
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

            // 칼이 실제 재료에 닿는 순간 절단
            bool success =
                PerformEzySlice(
                    targetIngredientObject,
                    cutPoint,
                    cutNormal
                );

            if (!success)
            {
                Debug.LogWarning(
                    "[HawaiianSkewerSlicer] Slice 실패. 다음 가이드로 진행하지 않습니다."
                );

                _isKnifeAnimating = false;
                yield break;
            }

            if (knifeVisual != null)
            {
                // 절단 위치 → 재료 아래까지 칼이 통과
                yield return MoveKnife(
                    cutPoint,
                    endPosition,
                    knifeCutDuration * 0.55f
                );
            }

            _currentGuideIndex++;

            if (_currentGuideIndex >=
                sliceGuidePoints.Count)
            {
                _isSlicingCompleted = true;

                OnSlicingCompleted?.Invoke();
            }
            else
            {
                UpdateGuideVisuals();
            }

            _isKnifeAnimating = false;
        }

        private IEnumerator MoveKnife(
            Vector3 startPosition,
            Vector3 endPosition,
            float duration)
        {
            if (knifeVisual == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                knifeVisual.transform.position =
                    endPosition;

                yield break;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed / duration
                    );

                t = Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

                knifeVisual.transform.position =
                    Vector3.Lerp(
                        startPosition,
                        endPosition,
                        t
                    );

                yield return null;
            }

            knifeVisual.transform.position =
                endPosition;
        }

        private bool PerformEzySlice(
            GameObject objectToCut,
            Vector3 cutPoint,
            Vector3 cutNormal)
        {
            if (objectToCut == null)
            {
                Debug.LogWarning(
                    "[HawaiianSkewerSlicer] targetIngredientObject가 없습니다."
                );

                return false;
            }

            SlicedHull hull =
                objectToCut.Slice(
                    cutPoint,
                    cutNormal,
                    crossSectionMaterial
                );

            if (hull == null)
            {
                Debug.LogWarning(
                    "[HawaiianSkewerSlicer] EzySlice 결과가 null입니다. " +
                    "가이드가 실제 Mesh를 통과하는지 확인하세요."
                );

                return false;
            }

            if (sliceAudioSource != null)
            {
                sliceAudioSource.Play();
            }

            Vector3 originalWorldPos =
                objectToCut.transform.position;

            Quaternion originalWorldRot =
                objectToCut.transform.rotation;

            Vector3 originalLocalScale =
                objectToCut.transform.localScale;

            Transform originalParent =
                objectToCut.transform.parent;

            GameObject upperHull =
                hull.CreateUpperHull(
                    objectToCut,
                    crossSectionMaterial
                );

            GameObject lowerHull =
                hull.CreateLowerHull(
                    objectToCut,
                    crossSectionMaterial
                );

            TrackPiece(upperHull);
            TrackPiece(lowerHull);

            if (upperHull != null &&
                lowerHull != null)
            {
                CookingSliceVfx.Play(sliceEffect, cutPoint, cutNormal);
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

                MeshFilter upperMF =
                    upperHull.GetComponent<MeshFilter>();

                MeshFilter lowerMF =
                    lowerHull.GetComponent<MeshFilter>();

                Vector3 upperCenter =
                    upperMF != null &&
                    upperMF.sharedMesh != null
                        ? upperHull.transform.TransformPoint(
                            upperMF.sharedMesh.bounds.center
                        )
                        : upperHull.transform.position;

                Vector3 lowerCenter =
                    lowerMF != null &&
                    lowerMF.sharedMesh != null
                        ? lowerHull.transform.TransformPoint(
                            lowerMF.sharedMesh.bounds.center
                        )
                        : lowerHull.transform.position;

                float upperDot =
                    Vector3.Dot(
                        upperCenter - cutPoint,
                        cutNormal
                    );

                float lowerDot =
                    Vector3.Dot(
                        lowerCenter - cutPoint,
                        cutNormal
                    );

                GameObject remainingPiece =
                    upperDot < lowerDot
                        ? lowerHull
                        : upperHull;

                GameObject separatedPiece =
                    upperDot < lowerDot
                        ? upperHull
                        : lowerHull;

                SetupSlicedPiece(
                    remainingPiece,
                    false
                );

                SetupSlicedPiece(
                    separatedPiece,
                    true
                );

                // Keep the uncut source so every round can restore the same ingredient.
                objectToCut.SetActive(false);

                if (objectToCut !=
                    _originalIngredient)
                {
                    Destroy(objectToCut);
                }

                targetIngredientObject =
                    remainingPiece;

                return true;
            }

            if (upperHull != null)
            {
                upperHull.SetActive(false);
                Destroy(upperHull);
            }

            if (lowerHull != null)
            {
                lowerHull.SetActive(false);
                Destroy(lowerHull);
            }

            return false;
        }

        private void MatchTransform(
            GameObject piece,
            Transform parent,
            Vector3 worldPos,
            Quaternion worldRot,
            Vector3 localScale)
        {
            piece.transform.SetParent(
                parent,
                false
            );

            piece.transform.position =
                worldPos;

            piece.transform.rotation =
                worldRot;

            piece.transform.localScale =
                localScale;
        }

        private void SetupSlicedPiece(
            GameObject piece,
            bool isSeparated)
        {
            if (piece == null) return;

            MeshFilter mf =
                piece.GetComponent<MeshFilter>();

            if (mf != null &&
                mf.sharedMesh != null)
            {
                MeshCollider collider =
                    piece.AddComponent<MeshCollider>();

                collider.sharedMesh =
                    mf.sharedMesh;

                collider.convex = true;
            }

            Rigidbody rb =
                piece.AddComponent<Rigidbody>();

            if (isSeparated)
            {
                piece.layer =
                    LayerMask.NameToLayer(
                        "Ignore Raycast"
                    );

                rb.isKinematic = false;
                rb.useGravity = true;

                rb.collisionDetectionMode =
                    CollisionDetectionMode.Continuous;

                rb.interpolation =
                    RigidbodyInterpolation.Interpolate;

                Vector3 impulseDir =
                    (Vector3.left +
                     Vector3.up * 0.2f)
                    .normalized;

                rb.AddForce(
                    impulseDir *
                    cutImpulseForce,
                    ForceMode.Impulse
                );

                rb.AddTorque(
                    UnityEngine.Random.insideUnitSphere *
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
            if (sliceGuidePoints == null)
            {
                return;
            }

            for (int i = 0;
                 i < sliceGuidePoints.Count;
                 i++)
            {
                if (sliceGuidePoints[i] != null)
                {
                    sliceGuidePoints[i]
                        .gameObject
                        .SetActive(
                            i == _currentGuideIndex
                        );
                }
            }
        }
    }
}
