using System;
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

        [Header("3단계: 양면 굽기")]
        [SerializeField] private SeafoodSkewersGrill skewersGrill;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;

        private void OnEnable()
        {
            if (seafoodSlicer != null) seafoodSlicer.OnSlicingCompleted += HandleStep1Completed;
            if (skewersAssembly != null) skewersAssembly.OnAssemblyCompleted += HandleStep2Completed;
            if (skewersGrill != null) skewersGrill.OnGrillCompleted += HandleStep3Completed;
        }

        private void OnDisable()
        {
            if (seafoodSlicer != null) seafoodSlicer.OnSlicingCompleted -= HandleStep1Completed;
            if (skewersAssembly != null) skewersAssembly.OnAssemblyCompleted -= HandleStep2Completed;
            if (skewersGrill != null) skewersGrill.OnGrillCompleted -= HandleStep3Completed;
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();
            _currentMenu = menu;
            _onCompleteCallback = onComplete;

            StartStep1();
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
        }

        private void HandleStep2Completed()
        {
            CompleteStep2(1.0f);
        }

        protected override void OnStep2Update() { }

        // --- 3단계: 양면 굽기 ---
        protected override void OnStep3Start()
        {
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
            _onCompleteCallback?.Invoke(result);
        }
    }
}
