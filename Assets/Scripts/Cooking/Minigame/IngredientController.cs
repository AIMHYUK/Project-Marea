using System.Collections.Generic;
using UnityEngine;

namespace Marea.Cooking
{
    public class IngredientController : MonoBehaviour
    {
        [Header("카메라 연출")]
        [SerializeField] private Transform minigameCameraViewPoint; // 미니게임 시점 트랜스폼

        [Header("3D 오브젝트 설정")]
        [SerializeField] private Transform movingIngredientTransform;
        [SerializeField] private Transform stickTransform;

        [Header("재료가 꽂힐 3D 위치 슬롯들")]
        [SerializeField] private List<Transform> attachPoints;
        [SerializeField] private List<GameObject> placedIngredientModels;

        [Header("좌우 왕복 이동 설정")]
        [SerializeField] private float moveDistance = 0.5f;
        [SerializeField] private float moveSpeed = 2.0f;

        public Transform MinigameCameraViewPoint => minigameCameraViewPoint;

        private Vector3 _startLocalPos;
        private int _direction = 1;
        private bool _isMoving;

        private void Awake()
        {
            if (movingIngredientTransform != null)
            {
                _startLocalPos = movingIngredientTransform.localPosition;
            }
        }

        public void InitializeMinigame3D()
        {
            foreach (var model in placedIngredientModels)
            {
                if (model != null) model.SetActive(false);
            }

            if (movingIngredientTransform != null)
            {
                movingIngredientTransform.gameObject.SetActive(true);
                movingIngredientTransform.localPosition = _startLocalPos;
            }

            _isMoving = true;
            _direction = 1;
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
            if (stepIndex >= 0 && stepIndex < placedIngredientModels.Count)
            {
                if (placedIngredientModels[stepIndex] != null)
                {
                    placedIngredientModels[stepIndex].SetActive(true);
                }
            }
        }

        public void HideAll()
        {
            _isMoving = false;
            if (movingIngredientTransform != null) movingIngredientTransform.gameObject.SetActive(false);
            foreach (var model in placedIngredientModels)
            {
                if (model != null) model.SetActive(false);
            }
        }
    }
}