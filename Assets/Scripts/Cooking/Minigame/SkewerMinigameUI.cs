using System;
using System.Collections.Generic;
using Marea.Data;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class SkewerMinigameUI : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] private GameObject gameRoot;
        [SerializeField] private TimingBarController timingBar;
        [SerializeField] private TextMeshProUGUI txtFeedback;

        [Header("3D 재료 연출 컨트롤러")]
        [SerializeField] private IngredientController ingredientController;

        [Header("카메라 연동")]
        [SerializeField] private Camera targetCamera;

        [Header("게임 설정")]
        [SerializeField] private int totalIngredients = 4;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private int _currentIngredientIndex;
        private readonly List<HitGrade> _hitHistory = new();
        private bool _isPlaying;

        private Vector3 _originalCamPos;
        private Quaternion _originalCamRot;

        public void StartGame(MenuData menu, Action<CookingResult> onComplete)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // 미니게임 시작 시 손님 스폰 일시정지
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

            SwitchToMinigameCamera();

            if (gameRoot != null) gameRoot.SetActive(true);
            gameObject.SetActive(true);

            if (timingBar != null) timingBar.Initialize();
            if (ingredientController != null) ingredientController.InitializeMinigame3D();

            if (txtFeedback != null) txtFeedback.text = "타이밍에 맞춰 스페이스바 또는 좌클릭!";
        }

        private void SwitchToMinigameCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null || ingredientController == null || ingredientController.MinigameCameraViewPoint == null)
            {
                Debug.LogWarning("[SkewerMinigameUI] 카메라 전환 실패: 타깃 카메라 또는 MinigameCameraViewPoint가 없습니다.");
                return;
            }

            // 기존 월드 위치 및 회전 백업 (부모 계층을 변경하지 않아 렌더링 꺼짐 방지)
            _originalCamPos = targetCamera.transform.position;
            _originalCamRot = targetCamera.transform.rotation;

            Transform viewPoint = ingredientController.MinigameCameraViewPoint;
            viewPoint.gameObject.SetActive(true);

            targetCamera.transform.position = viewPoint.position;
            targetCamera.transform.rotation = viewPoint.rotation;
        }

        private void RestoreCamera()
        {
            if (targetCamera == null) return;

            targetCamera.transform.position = _originalCamPos;
            targetCamera.transform.rotation = _originalCamRot;
        }

        private void Update()
        {
            if (!_isPlaying) return;

            bool isTriggered = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                               || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (isTriggered)
            {
                OnAttemptHit();
            }
        }

        private void OnAttemptHit()
        {
            HitGrade grade = timingBar != null ? timingBar.EvaluateHit() : HitGrade.Miss;
            Debug.Log($"[SkewerMinigame] 시도 결과: {grade} (현재 인덱스: {_currentIngredientIndex})");
            _hitHistory.Add(grade);

            if (grade != HitGrade.Miss)
            {
                if (ingredientController != null)
                {
                    ingredientController.AttachIngredient(_currentIngredientIndex);
                }

                _currentIngredientIndex++;
                ShowFeedback(grade);

                if (_currentIngredientIndex >= totalIngredients)
                {
                    FinishGame(true);
                }
            }
            else
            {
                ShowFeedback(HitGrade.Miss);
            }
        }

        private void ShowFeedback(HitGrade grade)
        {
            if (txtFeedback == null) return;
            txtFeedback.text = grade switch
            {
                HitGrade.Perfect => "<color=yellow>PERFECT!</color>",
                HitGrade.Good => "<color=green>GOOD!</color>",
                _ => "<color=red>MISS!</color>"
            };
        }

        private void FinishGame(bool isSuccess)
        {
            _isPlaying = false;

            if (timingBar != null) timingBar.StopGauge();
            if (ingredientController != null) ingredientController.StopMoving();

            RestoreCamera();

            // 미니게임 종료 시 손님 스폰 재개
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
                bestGrade = _hitHistory.Contains(HitGrade.Perfect) ? HitGrade.Perfect : HitGrade.Good
            };

            // 요리 성공 시 플레이어 손에 음식 지급
            if (isSuccess)
            {
                PlayerServingController playerServing = FindFirstObjectByType<PlayerServingController>();
                if (playerServing != null)
                {
                    playerServing.PickUpFood();
                }
                else
                {
                    // 혹시 비활성화된 오브젝트까지 포함하여 검색
                    playerServing = FindFirstObjectByType<PlayerServingController>(FindObjectsInactive.Include);
                    if (playerServing != null)
                    {
                        playerServing.PickUpFood();
                    }
                    else
                    {
                        Debug.LogError("[SkewerMinigameUI] 씬에서 PlayerServingController를 찾을 수 없습니다!");
                    }
                }
            }


            if (gameRoot != null) gameRoot.SetActive(false);
            if (ingredientController != null) ingredientController.HideAll();

            _onCompleteCallback?.Invoke(result);
        }
    }
}