using System;
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

        private int _currentGuideIndex;
        private bool _isSlicingCompleted;
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
            }
            if (sliceEffect != null) sliceEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sliceAudioSource != null) sliceAudioSource.Stop();
            _currentGuideIndex = 0;
            _isSlicingCompleted = false;

            if (targetIngredientObject != null && targetIngredientObject.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
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
                _originalPosition = _originalIngredient.transform.localPosition;
                _originalRotation = _originalIngredient.transform.localRotation;
                _originalScale = _originalIngredient.transform.localScale;
            }
            if (knifeVisual != null)
            {
                _knifePosition = knifeVisual.transform.localPosition;
                _knifeRotation = knifeVisual.transform.localRotation;
            }
        }

        private void TrackPiece(GameObject piece)
        {
            if (piece == null) return;
            _generatedPieces.Add(piece);
            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) _generatedMeshes.Add(filter.sharedMesh);
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
                if (mesh != null) Destroy(mesh);
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
            if (_isSlicingCompleted || sliceGuidePoints == null || sliceGuidePoints.Count == 0) return;

            Transform currentGuide = sliceGuidePoints[_currentGuideIndex];
            if (currentGuide != null)
            {
                PerformEzySlice(targetIngredientObject, currentGuide.position, currentGuide.right);
            }

            _currentGuideIndex++;

            if (_currentGuideIndex >= sliceGuidePoints.Count)
            {
                _isSlicingCompleted = true;
                OnSlicingCompleted?.Invoke();
            }
            else
            {
                UpdateGuideVisuals();
            }
        }

        private void PerformEzySlice(GameObject objectToCut, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (knifeVisual != null) knifeVisual.transform.position = cutPoint;

            if (objectToCut == null) return;

            if (sliceEffect != null)
            {
                sliceEffect.transform.position = cutPoint;
                sliceEffect.Play();
            }

            if (sliceAudioSource != null) sliceAudioSource.Play();

            Vector3 originalWorldPos = objectToCut.transform.position;
            Quaternion originalWorldRot = objectToCut.transform.rotation;
            Vector3 originalLocalScale = objectToCut.transform.localScale;
            Transform originalParent = objectToCut.transform.parent;

            SlicedHull hull = objectToCut.Slice(cutPoint, cutNormal, crossSectionMaterial);

            if (hull != null)
            {
                GameObject upperHull = hull.CreateUpperHull(objectToCut, crossSectionMaterial);
                GameObject lowerHull = hull.CreateLowerHull(objectToCut, crossSectionMaterial);
                TrackPiece(upperHull);
                TrackPiece(lowerHull);

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

                    GameObject remainingPiece = upperDot < lowerDot ? lowerHull : upperHull;
                    GameObject separatedPiece = upperDot < lowerDot ? upperHull : lowerHull;

                    SetupSlicedPiece(remainingPiece, false);
                    SetupSlicedPiece(separatedPiece, true);

                    // Keep the uncut source so every round can restore the same ingredient.
                    objectToCut.SetActive(false);
                    if (objectToCut != _originalIngredient) Destroy(objectToCut);
                    targetIngredientObject = remainingPiece;
                }
                else
                {
                    if (upperHull != null) { upperHull.SetActive(false); Destroy(upperHull); }
                    if (lowerHull != null) { lowerHull.SetActive(false); Destroy(lowerHull); }
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

        private void SetupSlicedPiece(GameObject piece, bool isSeparated)
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

            if (isSeparated)
            {
                piece.layer = LayerMask.NameToLayer("Ignore Raycast");
                rb.isKinematic = false;
                rb.useGravity = true;

                Vector3 impulseDir = (Vector3.left + Vector3.up * 0.2f).normalized;
                rb.AddForce(impulseDir * cutImpulseForce, ForceMode.Impulse);
                rb.AddTorque(UnityEngine.Random.insideUnitSphere * (cutImpulseForce * 0.5f), ForceMode.Impulse);
            }
            else
            {
                rb.isKinematic = true;
            }
        }

        private void UpdateGuideVisuals()
        {
            if (sliceGuidePoints == null) return;
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
