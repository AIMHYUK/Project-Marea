using Marea.Core;
using Marea.Data;
using Marea.Player;
using Marea.Restaurant;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Marea.Cooking
{
    public class SkewerMinigameController : MonoBehaviour
    {
        [Header("카메라 연출")]
        [SerializeField] private MinigameCameraController cameraController;
        [SerializeField] private Transform cameraViewPoint;

        [Header("3D 연출 및 UI")]
        [SerializeField] private IngredientController ingredientController;
        [SerializeField] private SkewerMinigameUI minigameUI;

        [Header("꼬치 게임 설정")]
        [Range(1, 6)]
        [SerializeField] private int totalIngredients = 4;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private int _currentIngredientIndex;
        private readonly List<HitGrade> _hitHistory = new();
        private bool _isPlaying;

        public int TotalIngredients => totalIngredients;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void EnsureDependencies()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<MinigameCameraController>(FindObjectsInactive.Include);
            }

            if (minigameUI == null)
            {
                minigameUI = FindFirstObjectByType<SkewerMinigameUI>(FindObjectsInactive.Include);
            }

            if (ingredientController == null)
            {
                ingredientController = FindFirstObjectByType<IngredientController>(FindObjectsInactive.Include);
            }

            if (cameraViewPoint == null)
            {
                GameObject viewPointObj = GameObject.Find("SkewerCameraViewPoint");
                if (viewPointObj != null)
                {
                    cameraViewPoint = viewPointObj.transform;
                }
            }
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _currentIngredientIndex = 0;
            _hitHistory.Clear();
            _isPlaying = true;

            if (cameraController != null && cameraViewPoint != null)
            {
                cameraController.MoveToViewPoint(cameraViewPoint);
            }

            if (ingredientController != null)
            {
                ingredientController.InitializeMinigame3D();
            }

            if (minigameUI != null)
            {
                minigameUI.Setup(this);
                minigameUI.Open();
            }
        }

        public void ProcessHit(HitGrade grade)
        {
            if (!_isPlaying) return;

            //Debug.Log($"[SkewerMinigame] 시도 결과: {grade} (진행: {_currentIngredientIndex + 1}/{totalIngredients})");
            _hitHistory.Add(grade);

            if (grade != HitGrade.Miss)
            {
                if (ingredientController != null)
                {
                    ingredientController.AttachIngredient(_currentIngredientIndex);
                }

                _currentIngredientIndex++;

                if (_currentIngredientIndex >= totalIngredients)
                {
                    FinishGame(true);
                }
            }
        }

        private void FinishGame(bool isSuccess)
        {
            _isPlaying = false;

            if (ingredientController != null)
            {
                ingredientController.StopMoving();
            }

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            float multiplier = 1.0f;
            if (_hitHistory.Contains(HitGrade.Perfect)) multiplier += 0.1f;
            if (!isSuccess) multiplier -= 0.2f;

            int finalPrice = _currentMenu != null ? Mathf.RoundToInt(_currentMenu.BasePrice * multiplier) : 0;

            CookingResult result = new CookingResult
            {
                isSuccess = isSuccess,
                finalPrice = finalPrice,
                bestGrade = _hitHistory.Contains(HitGrade.Perfect) ? HitGrade.Perfect : HitGrade.Good,
                menuData = _currentMenu
            };

            StartCoroutine(FinishRoutine(result));
        }

        private IEnumerator FinishRoutine(CookingResult result)
        {
            yield return new WaitForSeconds(0.8f);

            if (minigameUI != null)
            {
                minigameUI.Close();
            }

            if (ingredientController != null)
            {
                ingredientController.HideAll();
            }

            if (cameraController != null)
            {
                cameraController.ReturnToOriginalPosition();
            }

            _onCompleteCallback?.Invoke(result);
        }
    }
}
