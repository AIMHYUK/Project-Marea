using System;
using Marea.Data;
using UnityEngine;

namespace Marea.Cooking
{
    public class SeafoodSkewersMinigameController : BaseCookingMinigame
    {
        [Header("1단계: 타이밍 재료 썰기")]
        [SerializeField] private SeafoodSkewersSlicer seafoodSlicer;

        [Header("2단계: 꼬치에 끼우기 (추후 구현)")]
        [SerializeField] private GameObject step2SkewerContainer;

        [Header("3단계: 양면 굽기 (추후 구현)")]
        [SerializeField] private GameObject step3GrillContainer;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;

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
            // Step1Panel(UI + 3D)만 켜고 나머지는 끔
            if (step1Panel != null) step1Panel.SetActive(true);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(false);

            if (seafoodSlicer != null) seafoodSlicer.ResetSlicer();
        }

        protected override void OnStep1Update()
        {
            if (seafoodSlicer != null && seafoodSlicer.IsCompleted)
            {
                float averageScore = seafoodSlicer.SliceScoreSum / Mathf.Max(1, seafoodSlicer.CurrentSliceCount);
                CompleteStep1(averageScore);
            }
        }

        // --- 2단계: 꼬치에 끼우기 ---
        protected override void OnStep2Start()
        {
            // Step2Panel(UI + 3D)만 켜고 나머지는 끔
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(true);
            if (step3Panel != null) step3Panel.SetActive(false);
        }

        protected override void OnStep2Update()
        {
            // 2단계 완료 조건 만족 시 CompleteStep2(score) 호출
        }

        // --- 3단계: 양면 굽기 ---
        protected override void OnStep3Start()
        {
            // Step3Panel(UI + 3D)만 켜고 나머지는 끔
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(true);
        }

        protected override void OnStep3Update()
        {
            // 3단계 완료 조건 만족 시 CompleteStep3(score) 호출
        }

        protected override void OnMinigameCompleted(float finalScore)
        {
            // 미니게임 완결 시 모든 단계 오브젝트(UI + 3D) 비활성화
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
