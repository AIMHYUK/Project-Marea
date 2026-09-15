using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Cooking
{
    public class IngredientController : MonoBehaviour
    {
        [Header("3D 오브젝트 설정")]
        [SerializeField] private Transform movingIngredientTransform; // 좌우로 왕복 이동하는 부모 홀더
        [SerializeField] private Transform stickTransform;

        [Header("재료가 꽂힐 3D 위치 슬롯들 (최대 6개)")]
        [SerializeField] private List<Transform> attachPoints;

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
            }
        }

        public void InitializeMinigame3D(List<IngredientData> ingredients)
        {
            CleanUpModels();

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

            // 첫 번째 재료 모델 띄우기
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

            if (targetSlot != null && data != null && data.MinigamePrefab != null)
            {
                GameObject placed = Instantiate(data.MinigamePrefab, targetSlot);
                placed.transform.localPosition = Vector3.zero;
                placed.transform.localRotation = Quaternion.identity;
                placed.transform.localScale = Vector3.one;
                _spawnedStickIngredients.Add(placed);
            }

            // 다음 단계 재료 모델로 이동 홀더 갱신
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
                }
            }
        }

        private void SetMovingIngredientModel(int index)
        {
            if (_currentMovingModel != null)
            {
                Destroy(_currentMovingModel);
            }

            if (index < 0 || index >= _sequenceIngredients.Count) return;

            IngredientData data = _sequenceIngredients[index];
            if (data != null && data.MinigamePrefab != null && movingIngredientTransform != null)
            {
                _currentMovingModel = Instantiate(data.MinigamePrefab, movingIngredientTransform);
                _currentMovingModel.transform.localPosition = Vector3.zero;
                _currentMovingModel.transform.localRotation = Quaternion.identity;
                _currentMovingModel.transform.localScale = Vector3.one;
            }
        }

        public void HideAll()
        {
            _isMoving = false;

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(false);
            }

            CleanUpModels();
        }

        private void CleanUpModels()
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
        }
    }
}
