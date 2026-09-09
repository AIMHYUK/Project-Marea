using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Player;
using Marea.Restaurant;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public class FishGrillMinigameController : MonoBehaviour
    {
        [Header("카메라 시스템")]
        [SerializeField] private MinigameCameraController cameraController;
        [SerializeField] private Transform cameraViewPoint;

        [Header("3D 연출 및 UI")]
        [SerializeField] private FishGrillVisual fishVisual;
        [SerializeField] private FishGrillMinigameUI minigameUI;

        [Header("굽기 타이머 설정 (초 단위)")]
        [SerializeField] private float totalBakeDuration = 6.5f;

        [Header("판정 기준 구간 (진행률 0.0 ~ 1.0)")]
        [SerializeField] private float perfectMin = 0.65f;
        [SerializeField] private float perfectMax = 0.80f;
        [SerializeField] private float goodMin = 0.45f;
        [SerializeField] private float goodMax = 0.90f;
        [SerializeField] private float badMin = 0.30f;
        [SerializeField] private float badMax = 0.97f;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private float _elapsedTime;
        private bool _isPlaying;
        private bool _hasFlipped;

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

            if (fishVisual == null)
            {
                fishVisual = FindFirstObjectByType<FishGrillVisual>(FindObjectsInactive.Include);
            }

            if (minigameUI == null)
            {
                minigameUI = FindFirstObjectByType<FishGrillMinigameUI>(FindObjectsInactive.Include);
            }

            if (cameraViewPoint == null)
            {
                GameObject viewPointObj = GameObject.Find("FishGrillCameraViewPoint");
                if (viewPointObj != null) cameraViewPoint = viewPointObj.transform;
            }
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(true);

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _elapsedTime = 0f;
            _hasFlipped = false;
            _isPlaying = true;

            if (cameraController != null && cameraViewPoint != null)
            {
                cameraController.MoveToViewPoint(cameraViewPoint);
            }

            if (fishVisual != null)
            {
                fishVisual.InitializeVisual();
            }

            if (minigameUI != null)
            {
                minigameUI.Open();
            }
        }

        private void Update()
        {
            if (!_isPlaying || _hasFlipped) return;

            _elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsedTime / totalBakeDuration);

            if (fishVisual != null)
            {
                fishVisual.UpdateGrillColor(progress);
            }

            bool isTriggered = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                               || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

            if (isTriggered)
            {
                OnFlipInput(progress);
            }
            else if (_elapsedTime >= totalBakeDuration)
            {
                OnTimeExpired();
            }
        }

        private void OnFlipInput(float progress)
        {
            _hasFlipped = true;

            if (fishVisual != null)
            {
                fishVisual.PlayFlipAnimation();
            }

            HitGrade grade;
            float multiplier;

            if (progress >= perfectMin && progress <= perfectMax)
            {
                grade = HitGrade.Perfect;
                multiplier = 1.2f;
            }
            else if (progress >= goodMin && progress <= goodMax)
            {
                grade = HitGrade.Good;
                multiplier = 1.0f;
            }
            else if (progress >= badMin && progress <= badMax)
            {
                grade = HitGrade.Bad;
                multiplier = 0.8f;
            }
            else
            {
                grade = HitGrade.Miss;
                multiplier = 0f;
            }

            FinishMinigame(grade, multiplier);
        }

        private void OnTimeExpired()
        {
            _hasFlipped = true;
            FinishMinigame(HitGrade.Miss, 0f);
        }

        private void FinishMinigame(HitGrade grade, float multiplier)
        {
            _isPlaying = false;

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null) customerManager.PauseSpawning(false);

            int basePrice = _currentMenu != null ? _currentMenu.BasePrice : 0;
            int finalPrice = Mathf.RoundToInt(basePrice * multiplier);

            CookingResult result = new CookingResult
            {
                isSuccess = (grade != HitGrade.Miss),
                finalPrice = finalPrice,
                bestGrade = grade,
                menuData = _currentMenu
            };

            StartCoroutine(FinishRoutine(result));
        }

        private IEnumerator FinishRoutine(CookingResult result)
        {
            if (minigameUI != null)
            {
                minigameUI.ShowResult(result.bestGrade);
            }

            yield return new WaitForSeconds(1.2f);

            if (minigameUI != null)
            {
                minigameUI.Close();
            }

            if (cameraController != null)
            {
                cameraController.ReturnToOriginalPosition();
            }

            if (fishVisual != null)
            {
                fishVisual.ResetVisual();
            }

            if (result.isSuccess)
            {
                DispatchCookedFood(result);
            }

            _onCompleteCallback?.Invoke(result);
        }

        private void DispatchCookedFood(CookingResult result)
        {
            Sprite icon = _currentMenu != null ? _currentMenu.Icon : null;
            ServeBoard board = FindFirstObjectByType<ServeBoard>();
            ServingStaff[] staffs = FindObjectsByType<ServingStaff>(FindObjectsSortMode.None);

            if (board == null || staffs.Length == 0)
            {
                GiveFoodToPlayer(result);
                return;
            }

            CustomerController target = PickTarget(board, staffs);
            if (target == null)
            {
                GiveFoodToPlayer(result);
                return;
            }

            board.Post(target.transform, icon, () =>
            {
                if (target != null) target.ServeFood();
            });
        }

        private CustomerController PickTarget(ServeBoard board, ServingStaff[] staffs)
        {
            CustomerManager manager = FindFirstObjectByType<CustomerManager>();
            if (manager == null) return null;

            foreach (CustomerController c in manager.GetWaitingCustomers())
            {
                if (board.IsTargeted(c.transform)) continue;

                bool taken = false;
                foreach (ServingStaff s in staffs)
                {
                    if (s.DeliverTarget == c.transform) { taken = true; break; }
                }

                if (!taken) return c;
            }

            return null;
        }

        private void GiveFoodToPlayer(CookingResult result)
        {
            PlayerServingController playerServing = FindFirstObjectByType<PlayerServingController>(FindObjectsInactive.Include);
            if (playerServing != null)
            {
                playerServing.PickUpFood(result);
            }
        }
    }
}
