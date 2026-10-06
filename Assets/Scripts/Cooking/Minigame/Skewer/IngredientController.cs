using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Cooking
{
    public class IngredientController : MonoBehaviour
    {
        [Header("3D 오브젝트 설정")]
        [SerializeField] private Transform movingIngredientTransform; // 좌우 왕복 이동 홀더
        [SerializeField] private Transform stickTransform;
        [Tooltip("조립한 꼬치와 슬롯 전체를 포함하는 루트. 2·3단계에서 같은 오브젝트를 유지한다.")]
        [SerializeField] private Transform assembledSkewerRoot;

        [Header("재료가 꽂힐 빈 슬롯들 (아래부터 위 순서)")]
        [SerializeField] private List<Transform> attachPoints;

        [Header("크기 보정 배율")]
        [Tooltip("재료 모델의 크기를 키우거나 줄이고 싶을 때 조절")]
        [SerializeField] private float ingredientScaleMultiplier = 1.0f;

        [Header("좌우 왕복 이동 설정")]
        [SerializeField] private float moveDistance = 0.5f;
        [SerializeField] private float moveSpeed = 2.0f;
        [SerializeField] private float aboveStickClearance = 0.45f;
        [SerializeField] private float insertionDuration = 0.5f;

        private Vector3 _travelCenter;
        private Vector3 _travelAxis;
        private float _travelOffset;
        private Coroutine _insertionRoutine;
        private int _direction = 1;
        private bool _isMoving;

        private readonly List<IngredientData> _sequenceIngredients = new();
        private readonly List<GameObject> _spawnedStickIngredients = new();
        private readonly HashSet<GameObject> _saucedIngredients = new();
        private readonly Dictionary<GameObject, float> _sauceCoverage = new();
        private GameObject _currentMovingModel;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        public int SauceTargetCount => _spawnedStickIngredients.Count;
        public int AssemblyIngredientCount => _sequenceIngredients.Count;
        public int SaucedIngredientCount => _saucedIngredients.Count;
        public bool AllIngredientsSauced => SauceTargetCount > 0 && SaucedIngredientCount >= SauceTargetCount;
        public float SauceProgress
        {
            get
            {
                float total = 0f;
                foreach (float coverage in _sauceCoverage.Values) total += coverage;
                return SauceTargetCount > 0 ? total / SauceTargetCount : 0f;
            }
        }
        public Transform AssembledSkewerRoot => assembledSkewerRoot != null ? assembledSkewerRoot : stickTransform;
        public bool IsInserting => _insertionRoutine != null;
        public float TravelPosition => Mathf.InverseLerp(-moveDistance, moveDistance, _travelOffset);

        private void Awake()
        {
            if (movingIngredientTransform != null)
            {
                // 큐브 기본 메쉬가 붙어있다면 숨김 처리
                var meshRenderer = movingIngredientTransform.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = false;
            }
        }

        public void InitializeMinigame3D(List<IngredientData> ingredients, Vector3 horizontalAxis = default)
        {
            CleanUpAllModels();
            _saucedIngredients.Clear();

            _sequenceIngredients.Clear();
            if (ingredients != null)
            {
                _sequenceIngredients.AddRange(ingredients);
            }

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(true);
            }

            // Preserve the complete assembly outside the panels that switch between steps.
            Transform assembly = AssembledSkewerRoot;
            if (assembly != null)
            {
                if (transform.parent != null && assembly.parent != transform.parent)
                {
                    assembly.SetParent(transform.parent, true);
                }
                assembly.gameObject.SetActive(true);
            }

            _isMoving = true;
            _direction = 1;
            _travelOffset = -moveDistance;
            if (horizontalAxis.sqrMagnitude < 0.01f) horizontalAxis = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            _travelAxis = Vector3.ProjectOnPlane(horizontalAxis, Vector3.up).normalized;
            if (_travelAxis.sqrMagnitude < 0.01f) _travelAxis = Vector3.right;
            _travelCenter = stickTransform != null ? stickTransform.position : transform.position;
            if (attachPoints != null)
            {
                foreach (Transform slot in attachPoints)
                    if (slot != null && slot.position.y > _travelCenter.y) _travelCenter.y = slot.position.y;
            }
            _travelCenter += Vector3.up * aboveStickClearance;
            UpdateMovingPosition();

            SetMovingIngredientModel(0);
        }

        public void StopMoving()
        {
            _isMoving = false;
        }

        private void Update()
        {
            if (!_isMoving || IsInserting || movingIngredientTransform == null) return;
            _travelOffset += _direction * moveSpeed * Time.deltaTime;
            if (_travelOffset >= moveDistance)
            {
                _travelOffset = moveDistance;
                _direction = -1;
            }
            else if (_travelOffset <= -moveDistance)
            {
                _travelOffset = -moveDistance;
                _direction = 1;
            }
            UpdateMovingPosition();
        }

        private void UpdateMovingPosition()
        {
            if (movingIngredientTransform != null)
                movingIngredientTransform.position = _travelCenter + _travelAxis * _travelOffset;
        }

        public bool AttachIngredient(int stepIndex, Action onInserted = null)
        {
            if (IsInserting || stepIndex < 0 || stepIndex >= _sequenceIngredients.Count) return false;
            IngredientData data = _sequenceIngredients[stepIndex];
            Transform slot = attachPoints != null && stepIndex < attachPoints.Count ? attachPoints[stepIndex] : null;
            if (slot == null || data == null || data.MinigamePrefab == null || _currentMovingModel == null) return false;
            _insertionRoutine = StartCoroutine(InsertIngredient(stepIndex, slot, data, onInserted));
            return true;
        }

        private IEnumerator InsertIngredient(int index, Transform slot, IngredientData data, Action onInserted)
        {
            // Use the moving model itself, preserving its pose while it leaves the holder.
            GameObject placed = _currentMovingModel;
            _currentMovingModel = null;
            placed.transform.SetParent(slot, true);
            _spawnedStickIngredients.Add(placed);
            Vector3 start = placed.transform.position;
            Vector3 destination = slot.TransformPoint(data.MinigamePrefab.transform.localPosition);
            Vector3 above = new Vector3(destination.x, start.y, destination.z);
            Quaternion startRotation = placed.transform.rotation;
            Quaternion endRotation = slot.rotation * data.MinigamePrefab.transform.localRotation;
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, insertionDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // First align over the tip, then slide vertically down the stick.
                placed.transform.position = t < 0.25f
                    ? Vector3.Lerp(start, above, Mathf.SmoothStep(0f, 1f, t / 0.25f))
                    : Vector3.Lerp(above, destination, Mathf.SmoothStep(0f, 1f, (t - 0.25f) / 0.75f));
                placed.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                yield return null;
            }
            placed.transform.localPosition = data.MinigamePrefab.transform.localPosition;
            placed.transform.localRotation = data.MinigamePrefab.transform.localRotation;
            placed.transform.localScale = data.MinigamePrefab.transform.localScale * ingredientScaleMultiplier;
            EnsurePaintableCollider(placed);
            _insertionRoutine = null;
            if (index + 1 < _sequenceIngredients.Count) SetMovingIngredientModel(index + 1);
            onInserted?.Invoke();
        }

        private void SetMovingIngredientModel(int index)
        {
            if (_currentMovingModel != null)
            {
                Destroy(_currentMovingModel);
                _currentMovingModel = null;
            }

            if (index < 0 || index >= _sequenceIngredients.Count) return;

            IngredientData data = _sequenceIngredients[index];

            if (data != null && data.MinigamePrefab == null)
            {
                Debug.LogWarning($"[IngredientController] 왕복 이동 재료 프리팹 누락: 재료='{data.name}' (단계: {index})");
            }

            if (data == null || data.MinigamePrefab == null || movingIngredientTransform == null) return;

            _currentMovingModel = Instantiate(
                data.MinigamePrefab,
                movingIngredientTransform
            );

            _currentMovingModel.transform.localPosition = Vector3.zero;

            // 프리팹 원본 로컬 회전값 유지
            _currentMovingModel.transform.localRotation = data.MinigamePrefab.transform.localRotation;

            Vector3 baseScale = data.MinigamePrefab.transform.localScale;
            Vector3 slotScale = attachPoints != null && index < attachPoints.Count && attachPoints[index] != null
                ? attachPoints[index].lossyScale : Vector3.one;
            Vector3 holderScale = movingIngredientTransform.lossyScale;
            _currentMovingModel.transform.localScale = Vector3.Scale(baseScale, new Vector3(
                slotScale.x / holderScale.x, slotScale.y / holderScale.y, slotScale.z / holderScale.z)) * ingredientScaleMultiplier;
            if (attachPoints != null && index < attachPoints.Count && attachPoints[index] != null)
                _currentMovingModel.transform.rotation = attachPoints[index].rotation * data.MinigamePrefab.transform.localRotation;
        }

        public void HideAll()
        {
            _isMoving = false;
            _travelOffset = 0f;
            _direction = 1;
            ResetSaucePainting();

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(false);
            }

            if (AssembledSkewerRoot != null)
            {
                AssembledSkewerRoot.gameObject.SetActive(false);
            }

            CleanUpAllModels();
            _sequenceIngredients.Clear();
        }

        public void ResetSaucePainting()
        {
            _saucedIngredients.Clear();
            _sauceCoverage.Clear();
            foreach (GameObject ingredient in _spawnedStickIngredients)
            {
                if (ingredient != null) SetSauceTint(ingredient, 0f);
            }
        }

        /// <summary>Applies sauce to an assembled ingredient under the pointer.</summary>
        public bool TryApplySauceAt(Vector2 screenPosition, Camera camera = null, float coverageAmount = 1f)
        {
            if (coverageAmount <= 0f) return false;
            if (camera == null) camera = Camera.main;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            GameObject hitIngredient = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (GameObject ingredient in _spawnedStickIngredients)
            {
                if (ingredient == null || !ingredient.activeInHierarchy || _saucedIngredients.Contains(ingredient)) continue;
                foreach (Collider collider in ingredient.GetComponentsInChildren<Collider>())
                {
                    if (collider.enabled && collider.Raycast(ray, out RaycastHit hit, camera.farClipPlane) && hit.distance < nearestDistance)
                    {
                        nearestDistance = hit.distance;
                        hitIngredient = ingredient;
                    }
                }
            }

            // Prefabs without colliders can still be painted through their renderer bounds.
            if (hitIngredient == null)
            {
                foreach (GameObject ingredient in _spawnedStickIngredients)
                {
                    if (ingredient != null && ingredient.activeInHierarchy && !_saucedIngredients.Contains(ingredient) &&
                        IsScreenPositionInside(ingredient, screenPosition, camera))
                    {
                        hitIngredient = ingredient;
                        break;
                    }
                }
            }

            if (hitIngredient == null) return false;
            _sauceCoverage.TryGetValue(hitIngredient, out float previous);
            float coverage = Mathf.Clamp01(previous + coverageAmount);
            _sauceCoverage[hitIngredient] = coverage;
            if (coverage >= 1f) _saucedIngredients.Add(hitIngredient);
            SetSauceTint(hitIngredient, coverage);
            return true;
        }

        private static bool IsScreenPositionInside(GameObject ingredient, Vector2 screenPosition, Camera camera)
        {
            Renderer[] renderers = ingredient.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled) continue;
                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                float minX = float.PositiveInfinity;
                float minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity;
                float maxY = float.NegativeInfinity;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldCorner = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 point = camera.WorldToScreenPoint(worldCorner);
                    if (point.z <= 0f) continue;
                    minX = Mathf.Min(minX, point.x);
                    minY = Mathf.Min(minY, point.y);
                    maxX = Mathf.Max(maxX, point.x);
                    maxY = Mathf.Max(maxY, point.y);
                }

                if (screenPosition.x >= minX && screenPosition.x <= maxX &&
                    screenPosition.y >= minY && screenPosition.y <= maxY)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetSauceTint(GameObject ingredient, float coverage)
        {
            const float sauceBlend = 0.65f;
            Color sauceColor = new Color(0.95f, 0.3f, 0.06f, 1f);
            Renderer[] renderers = ingredient.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null) continue;
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, materialIndex);
                    Color baseColor = Color.white;
                    if (material.HasProperty(BaseColorId)) baseColor = material.GetColor(BaseColorId);
                    else if (material.HasProperty(LegacyColorId)) baseColor = material.GetColor(LegacyColorId);
                    Color tint = Color.Lerp(baseColor, sauceColor, sauceBlend * coverage);
                    if (material.HasProperty(BaseColorId)) block.SetColor(BaseColorId, tint);
                    if (material.HasProperty(LegacyColorId)) block.SetColor(LegacyColorId, tint);
                    if (material.HasProperty("_Smoothness"))
                    {
                        block.SetFloat("_Smoothness", Mathf.Lerp(material.GetFloat("_Smoothness"), 0.8f, coverage));
                    }
                    renderer.SetPropertyBlock(block, materialIndex);
                }
            }
        }

        private static void EnsurePaintableCollider(GameObject ingredient)
        {
            if (ingredient.GetComponentInChildren<Collider>() != null) return;

            MeshFilter meshFilter = ingredient.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true;
                return;
            }

            Renderer renderer = ingredient.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                BoxCollider box = renderer.gameObject.AddComponent<BoxCollider>();
                box.center = renderer.transform.InverseTransformPoint(renderer.bounds.center);
                Vector3 localSize = renderer.transform.InverseTransformVector(renderer.bounds.size);
                box.size = new Vector3(
                    Mathf.Max(0.001f, Mathf.Abs(localSize.x)),
                    Mathf.Max(0.001f, Mathf.Abs(localSize.y)),
                    Mathf.Max(0.001f, Mathf.Abs(localSize.z)));
            }
        }

        private void CleanUpAllModels()
        {
            if (_insertionRoutine != null) StopCoroutine(_insertionRoutine);
            _insertionRoutine = null;
            if (_currentMovingModel != null)
            {
                _currentMovingModel.SetActive(false);
                Destroy(_currentMovingModel);
                _currentMovingModel = null;
            }

            foreach (var model in _spawnedStickIngredients)
            {
                if (model != null)
                {
                    model.SetActive(false);
                    Destroy(model);
                }
            }
            _spawnedStickIngredients.Clear();
            _saucedIngredients.Clear();
            _sauceCoverage.Clear();

            // 슬롯 하위에 남아있는 자식 오브젝트 완전 삭제
            if (attachPoints != null)
            {
                foreach (var slot in attachPoints)
                {
                    if (slot == null) continue;
                    for (int i = slot.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = slot.GetChild(i).gameObject;
                        child.SetActive(false);
                        // Detach before deferred destruction, so an immediate restart sees empty slots.
                        child.transform.SetParent(null, true);
                        Destroy(child);
                    }
                }
            }
        }
    }
}
