using System;
using System.Collections;
using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using Marea.Field;
using Marea.Restaurant;
using UnityEngine;

namespace Marea.Cooking
{
    public class SkewerMinigameController : BaseCookingMinigame
    {
        [Header("1단계: 재료 썰기")]
        [SerializeField] private HawaiianSkewerSlicer slicerStep1;

        [Header("2단계: 꼬치 끼우기")]
        [SerializeField] private IngredientController ingredientController;

        [Header("UI 시스템")]
        [SerializeField] private SkewerMinigameUI minigameUI;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private int _currentIngredientIndex;
        private readonly List<HitGrade> _hitHistory = new();
        private readonly List<IngredientData> _targetIngredients = new();
        private bool _step3CompletionRequested;

        public int TotalIngredients => _targetIngredients.Count;
        public bool IsInserting => ingredientController != null && ingredientController.IsInserting;
        public float IngredientTravelPosition => ingredientController != null ? ingredientController.TravelPosition : 0.5f;

        protected override void Awake()
        {
            base.Awake();
            EnsureDependencies();
        }

        private void OnEnable()
        {
            if (slicerStep1 != null)
            {
                slicerStep1.OnSlicingCompleted += HandleStep1Completed;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (slicerStep1 != null)
            {
                slicerStep1.OnSlicingCompleted -= HandleStep1Completed;
            }
        }

        protected override void EnsureDependencies()
        {
            base.EnsureDependencies();

            if (minigameUI == null)
            {
                minigameUI = FindFirstObjectByType<SkewerMinigameUI>(FindObjectsInactive.Include);
            }

            if (ingredientController == null)
            {
                ingredientController = FindFirstObjectByType<IngredientController>(FindObjectsInactive.Include);
            }

            if (slicerStep1 == null)
            {
                slicerStep1 = FindFirstObjectByType<HawaiianSkewerSlicer>(FindObjectsInactive.Include);
            }
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();
            ingredientController?.HideAll();
            _step3CompletionRequested = false;
            Step1Score = Step2Score = Step3Score = 1f;

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(true);
            }

            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            _currentIngredientIndex = 0;
            _hitHistory.Clear();
            _targetIngredients.Clear();

            BuildTargetIngredients(_currentMenu, _targetIngredients);

            if (minigameUI != null)
            {
                minigameUI.Setup(this);
            }

            StartStep1();
            minigameUI?.Open();
        }

        // --- 1단계: 재료 썰기 ---
        protected override void OnStep1Start()
        {
            if (slicerStep1 != null)
            {
                slicerStep1.ResetSlicer();
            }
            minigameUI?.RefreshStageInstructions();
        }

        private void HandleStep1Completed()
        {
            Debug.Log("[SkewerMinigameController] 1단계 재료 썰기 완료!");
            CompleteStep1(1.0f);
        }

        protected override void OnStep1Update()
        {
        }

        // --- 2단계: 꼬치 끼우기 ---
        protected override void OnStep2Start()
        {
            Debug.Log("[SkewerMinigameController] 2단계(타이밍 꼬치 끼우기) 시작");

            _currentIngredientIndex = 0;

            if (ingredientController != null)
            {
                Transform viewPoint = GetStepViewPoint(MinigameStepIndex.Step2);
                ingredientController.InitializeMinigame3D(_targetIngredients, viewPoint != null ? viewPoint.right : Vector3.right);
            }
            minigameUI?.RefreshStageInstructions();
        }

        protected override void OnStep2Update()
        {
        }

        // --- 3단계: 2단계에서 조립한 꼬치에 소스 바르기 ---
        protected override void OnStep3Start()
        {
            Debug.Log("[SkewerMinigameController] 3단계 소스 바르기 시작");
            _step3CompletionRequested = false;
            if (ingredientController != null)
            {
                ingredientController.StopMoving();
                ingredientController.ResetSaucePainting();
            }
            minigameUI?.RefreshStageInstructions();

            if (ingredientController != null && ingredientController.SauceTargetCount == 0)
            {
                CompleteStep3(0f);
            }
        }

        protected override void OnStep3Update()
        {
        }

        // --- 입력 처리 ---
        public void ProcessHit(HitGrade grade)
        {
            if (CurrentStepIndex == MinigameStepIndex.NotStarted || CurrentStepIndex == MinigameStepIndex.Completed) return;

            if (CurrentStepIndex == MinigameStepIndex.Step1)
            {
                // 1단계: 타이밍 상관없이 클릭 시 순서대로 썰기
                if (slicerStep1 != null)
                {
                    slicerStep1.ExecuteSlice();
                }
            }
            else if (CurrentStepIndex == MinigameStepIndex.Step2)
            {
                if (IsInserting) return;
                if (grade == HitGrade.Miss)
                {
                    _hitHistory.Add(grade);
                    return;
                }
                if (ingredientController != null &&
                    ingredientController.AttachIngredient(_currentIngredientIndex, HandleIngredientInserted))
                    _hitHistory.Add(grade);
            }
        }

        private void HandleIngredientInserted()
        {
            _currentIngredientIndex++;
            if (_currentIngredientIndex < TotalIngredients) return;
            ingredientController.StopMoving();
            CompleteStep2(CalculateSkewerScore());
        }

        public void ProcessSauceDrag(Vector2 screenPosition)
        {
            if (CurrentStepIndex != MinigameStepIndex.Step3 || _step3CompletionRequested || ingredientController == null)
            {
                return;
            }

            Camera paintingCamera = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;
            ingredientController.TryApplySauceAt(screenPosition, paintingCamera);
            if (minigameUI != null)
            {
                minigameUI.UpdateSauceProgress(ingredientController.SaucedIngredientCount, ingredientController.SauceTargetCount);
            }

            if (ingredientController.AllIngredientsSauced)
            {
                _step3CompletionRequested = true;
                CompleteStep3(1.0f);
            }
        }

        private static void BuildTargetIngredients(MenuData menu, List<IngredientData> result)
        {
            result.Clear();
            if (menu == null || menu.Recipe == null) return;

            IReadOnlyList<RecipeEntry> recipe = menu.Recipe;

            var order = new List<int>(recipe.Count);
            for (int i = 0; i < recipe.Count; i++)
            {
                if (recipe[i].ingredient == null || recipe[i].requiredAmount <= 0) continue;
                order.Add(i);
            }

            order.Sort((a, b) =>
            {
                int byOrder = recipe[a].inputOrder.CompareTo(recipe[b].inputOrder);
                return byOrder != 0 ? byOrder : a.CompareTo(b);
            });

            for (int i = 0; i < order.Count; i++)
            {
                RecipeEntry entry = recipe[order[i]];
                for (int n = 0; n < entry.requiredAmount; n++)
                {
                    result.Add(entry.ingredient);
                }
            }
        }

        private float CalculateSkewerScore()
        {
            if (_hitHistory.Count == 0) return 0f;

            float score = 0f;
            foreach (HitGrade grade in _hitHistory)
            {
                score += grade switch
                {
                    HitGrade.Perfect => 1f,
                    HitGrade.Good => 0.7f,
                    _ => 0f
                };
            }

            return score / _hitHistory.Count;
        }

        // --- 미니게임 최종 완료 ---
        protected override void OnMinigameCompleted(float finalScore)
        {
            slicerStep1?.ResetSlicer();
            if (ingredientController != null)
            {
                ingredientController.StopMoving();
                ingredientController.HideAll();
            }

            if (minigameUI != null)
            {
                minigameUI.Close();
            }

            CustomerManager customerManager = FindFirstObjectByType<CustomerManager>();
            if (customerManager != null)
            {
                customerManager.PauseSpawning(false);
            }

            bool isSuccess = finalScore >= 0.5f;
            int finalPrice = _currentMenu != null ? Mathf.RoundToInt(_currentMenu.BasePrice * finalScore) : 0;
            HitGrade bestGrade = _hitHistory.Contains(HitGrade.Perfect)
                ? HitGrade.Perfect
                : (_hitHistory.Contains(HitGrade.Good) ? HitGrade.Good : HitGrade.Miss);

            CookingResult result = new CookingResult
            {
                isSuccess = isSuccess,
                finalPrice = finalPrice,
                bestGrade = bestGrade,
                menuData = _currentMenu
            };

            if (isSuccess)
            {
                DispatchCookedFood(result);
            }

            // Clear round state before the callback, which may immediately start another round.
            Action<CookingResult> callback = _onCompleteCallback;
            _onCompleteCallback = null;
            _currentMenu = null;
            _currentIngredientIndex = 0;
            _step3CompletionRequested = false;
            _hitHistory.Clear();
            _targetIngredients.Clear();
            callback?.Invoke(result);
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
                if (target != null) target.ServeFood(result);
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
        }
    }
}
