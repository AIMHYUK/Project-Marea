using System;
using System.Collections.Generic;
using Marea.Data;
using UnityEngine;

namespace Marea.Cooking
{
    public class SeafoodSkewersMinigameController : BaseCookingMinigame
    {
        [Header("1단계: 타이밍 재료 썰기")]
        [SerializeField] private SeafoodSkewersSlicer seafoodSlicer;

        [Header("2단계: 꼬치에 끼우기")]
        [SerializeField] private SeafoodSkewersAssembly skewersAssembly;
        [SerializeField] private IngredientController ingredientController;
        [SerializeField] private SeafoodSkewersAssemblyUI assemblyUI;

        [Header("3단계: 양면 굽기")]
        [SerializeField] private SeafoodSkewersGrill skewersGrill;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;
        private readonly List<IngredientData> _targetIngredients = new();
        public bool IsInserting => ingredientController != null && ingredientController.IsInserting;
        public float IngredientTravelPosition => ingredientController != null ? ingredientController.TravelPosition : 0.5f;

        protected override void EnsureDependencies()
        {
            base.EnsureDependencies();
            if (ingredientController == null) ingredientController = GetComponentInChildren<IngredientController>(true);
            if (skewersAssembly == null) skewersAssembly = GetComponentInChildren<SeafoodSkewersAssembly>(true);
            if (assemblyUI == null) assemblyUI = GetComponentInChildren<SeafoodSkewersAssemblyUI>(true);
        }

        private void OnEnable()
        {
            if (seafoodSlicer != null) seafoodSlicer.OnSlicingCompleted += HandleStep1Completed;
            if (skewersAssembly != null) skewersAssembly.OnAssemblyCompleted += HandleStep2Completed;
            if (skewersGrill != null) skewersGrill.OnGrillCompleted += HandleStep3Completed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (seafoodSlicer != null) seafoodSlicer.OnSlicingCompleted -= HandleStep1Completed;
            if (skewersAssembly != null) skewersAssembly.OnAssemblyCompleted -= HandleStep2Completed;
            if (skewersGrill != null) skewersGrill.OnGrillCompleted -= HandleStep3Completed;
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();
            ingredientController?.HideAll();
            Step1Score = Step2Score = Step3Score = 1f;
            _currentMenu = menu;
            _onCompleteCallback = onComplete;
            BuildTargetIngredients(menu);

            StartStep1();
        }

        protected override void ResetMinigame()
        {
            base.ResetMinigame();
            ingredientController?.HideAll();
            assemblyUI?.Close();
            _targetIngredients.Clear();

            if (seafoodSlicer != null)
            {
                seafoodSlicer.ResetSlicerState();
            }

            if (skewersAssembly != null)
            {
                skewersAssembly.ResetAssembly();
            }

            if (skewersGrill != null)
            {
                skewersGrill.ResetGrillState();
            }

            _currentMenu = null;
            _onCompleteCallback = null;
        }

        // --- 1단계: 재료 썰기 ---
        protected override void OnStep1Start()
        {
            if (step1Panel != null) step1Panel.SetActive(true);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(false);

            if (seafoodSlicer != null) seafoodSlicer.ResetSlicer();
        }

        private void HandleStep1Completed()
        {
            if (seafoodSlicer == null) return;
            float averageScore = seafoodSlicer.SliceScoreSum / Mathf.Max(1, seafoodSlicer.CurrentSliceCount);
            CompleteStep1(averageScore);
        }

        protected override void OnStep1Update() { }

        // --- 2단계: 꼬치에 끼우기 ---
        protected override void OnStep2Start()
        {
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(true);
            if (step3Panel != null) step3Panel.SetActive(false);

            if (skewersAssembly != null) skewersAssembly.ResetAssembly();
            Transform viewPoint = GetStepViewPoint(MinigameStepIndex.Step2);
            ingredientController?.InitializeMinigame3D(_targetIngredients, viewPoint != null ? viewPoint.right : Vector3.right);
            assemblyUI?.Setup(this);
            assemblyUI?.Open();
        }

        private void HandleStep2Completed(float score)
        {
            ingredientController?.StopMoving();
            CompleteStep2(score);
        }

        protected override void OnStep2Update() { }

        public void ProcessHit(HitGrade grade)
        {
            if (CurrentStepIndex != MinigameStepIndex.Step2 || IsInserting) return;
            skewersAssembly?.TryInsert(grade);
        }

        private void BuildTargetIngredients(MenuData menu)
        {
            _targetIngredients.Clear();
            if (menu == null || menu.Recipe == null) return;
            var order = new List<int>();
            for (int i = 0; i < menu.Recipe.Count; i++)
                if (menu.Recipe[i].ingredient != null && menu.Recipe[i].requiredAmount > 0) order.Add(i);
            order.Sort((a, b) =>
            {
                int compared = menu.Recipe[a].inputOrder.CompareTo(menu.Recipe[b].inputOrder);
                return compared != 0 ? compared : a.CompareTo(b);
            });
            foreach (int index in order)
                for (int n = 0; n < menu.Recipe[index].requiredAmount; n++)
                    _targetIngredients.Add(menu.Recipe[index].ingredient);
        }

        // --- 3단계: 양면 굽기 ---
        protected override void OnStep3Start()
        {
            assemblyUI?.Close();
            ingredientController?.HideAll();
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(true);

            if (skewersGrill != null) skewersGrill.ResetGrill();
        }

        private void HandleStep3Completed()
        {
            float step3Score = skewersGrill != null ? skewersGrill.FinalGrillScore : 1.0f;
            CompleteStep3(step3Score);
        }

        protected override void OnStep3Update() { }

        // --- 전체 미니게임 완결 ---
        protected override void OnMinigameCompleted(float finalScore)
        {
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(false);

            CookingResult result = new CookingResult
            {
                isSuccess = true,
                finalPrice = Mathf.RoundToInt((_currentMenu != null ? _currentMenu.BasePrice : 100) * finalScore),
                bestGrade = finalScore >= 0.8f ? HitGrade.Perfect : HitGrade.Good,
                menuData = _currentMenu
            };

            SetPhysicsRaycasterState(false);
            Action<CookingResult> callback = _onCompleteCallback;
            ResetMinigame();
            callback?.Invoke(result);
        }
    }
}
