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

        [Header("재료가 꽂힐 빈 슬롯들 (아래부터 위 순서)")]
        [SerializeField] private List<Transform> attachPoints;

        [Header("크기 보정 배율")]
        [Tooltip("재료 모델의 크기를 키우거나 줄이고 싶을 때 조절")]
        [SerializeField] private float ingredientScaleMultiplier = 1.0f;

        [Header("좌우 왕복 이동 설정")]
        [SerializeField] private float moveDistance = 0.5f;
        [SerializeField] private float moveSpeed = 2.0f;

        private Vector3 _startLocalPos;
        private int _direction = 1;
        private bool _isMoving;

        private readonly List<IngredientData> _sequenceIngredients = new();
        private readonly List<GameObject> _spawnedStickIngredients = new();
        private GameObject _currentMovingModel;

        private void Awake()
        {
            if (movingIngredientTransform != null)
            {
                _startLocalPos = movingIngredientTransform.localPosition;

                // 큐브 기본 메쉬가 붙어있다면 숨김 처리
                var meshRenderer = movingIngredientTransform.GetComponent<MeshRenderer>();
                if (meshRenderer != null) meshRenderer.enabled = false;
            }
        }

        public void InitializeMinigame3D(List<IngredientData> ingredients)
        {
            CleanUpAllModels();

            _sequenceIngredients.Clear();
            if (ingredients != null)
            {
                _sequenceIngredients.AddRange(ingredients);
            }

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(true);
                movingIngredientTransform.localPosition = _startLocalPos;
            }

            _isMoving = true;
            _direction = 1;

            SetMovingIngredientModel(0);
        }

        public void StopMoving()
        {
            _isMoving = false;
        }

        private void Update()
        {
            if (!_isMoving || movingIngredientTransform == null) return;

            Vector3 pos = movingIngredientTransform.localPosition;
            pos.x += _direction * moveSpeed * Time.deltaTime;

            if (pos.x >= _startLocalPos.x + moveDistance)
            {
                pos.x = _startLocalPos.x + moveDistance;
                _direction = -1;
            }
            else if (pos.x <= _startLocalPos.x - moveDistance)
            {
                pos.x = _startLocalPos.x - moveDistance;
                _direction = 1;
            }

            movingIngredientTransform.localPosition = pos;
        }

        public void AttachIngredient(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= _sequenceIngredients.Count) return;

            IngredientData data = _sequenceIngredients[stepIndex];
            Transform targetSlot = (stepIndex < attachPoints.Count) ? attachPoints[stepIndex] : null;

            if (data != null && data.MinigamePrefab == null)
            {
                Debug.LogWarning($"[IngredientController] 슬롯 결합 재료 프리팹 누락: 재료='{data.name}' (슬롯 단계: {stepIndex})");
            }

            if (targetSlot != null && data != null && data.MinigamePrefab != null)
            {
                // 슬롯 하위에 자식으로 생성
                GameObject placed = Instantiate(
                    data.MinigamePrefab,
                    targetSlot
                );

                // 프리팹 원본에 설정된 로컬 위치(높이 Y 포함) 및 오프셋 적용
                placed.transform.localPosition = data.MinigamePrefab.transform.localPosition;

                // 프리팹 원본에 설정된 로컬 회전값 적용
                placed.transform.localRotation = data.MinigamePrefab.transform.localRotation;

                // 프리팹 원본 스케일 유지 + 배율 적용
                Vector3 baseScale = data.MinigamePrefab.transform.localScale;
                placed.transform.localScale = baseScale * ingredientScaleMultiplier;

                _spawnedStickIngredients.Add(placed);
            }

            // 다음 재료 모델로 이동 홀더 교체
            int nextIndex = stepIndex + 1;
            if (nextIndex < _sequenceIngredients.Count)
            {
                SetMovingIngredientModel(nextIndex);
            }
            else
            {
                if (_currentMovingModel != null)
                {
                    Destroy(_currentMovingModel);
                    _currentMovingModel = null;
                }
            }
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

            // 이동 홀더에서도 프리팹 원본 로컬 위치(높이 Y 포함)를 유지
            _currentMovingModel.transform.localPosition = data.MinigamePrefab.transform.localPosition;

            // 프리팹 원본 로컬 회전값 유지
            _currentMovingModel.transform.localRotation = data.MinigamePrefab.transform.localRotation;

            Vector3 baseScale = data.MinigamePrefab.transform.localScale;
            _currentMovingModel.transform.localScale = baseScale * ingredientScaleMultiplier;
        }

        public void HideAll()
        {
            _isMoving = false;

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(false);
            }

            CleanUpAllModels();
        }

        private void CleanUpAllModels()
        {
            if (_currentMovingModel != null)
            {
                Destroy(_currentMovingModel);
                _currentMovingModel = null;
            }

            foreach (var model in _spawnedStickIngredients)
            {
                if (model != null) Destroy(model);
            }
            _spawnedStickIngredients.Clear();

            // 슬롯 하위에 남아있는 자식 오브젝트 완전 삭제
            if (attachPoints != null)
            {
                foreach (var slot in attachPoints)
                {
                    if (slot == null) continue;
                    for (int i = slot.childCount - 1; i >= 0; i--)
                    {
                        Destroy(slot.GetChild(i).gameObject);
                    }
                }
            }
        }
    }
}
