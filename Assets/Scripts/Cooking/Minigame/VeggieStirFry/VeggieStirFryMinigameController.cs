using System;
using Marea.Data;
using UnityEngine;

namespace Marea.Cooking
{
    public class VeggieStirFryMinigameController : BaseCookingMinigame
    {
        [Header("1단계: Dynamic Mesh Slicer 양파 썰기")]
        [SerializeField] private DynamicOnionSlicer onionSlicer;

        [Header("2단계: 야채 투입 & 주걱 볶기")]
        [SerializeField] private PanStirCookingController panStirController;

        [Header("3단계: 3D 양념통 클릭 & 리듬 QTE")]
        [SerializeField] private Seasoning3DQTE seasoningQTE;
        [SerializeField] private SequenceInputRailUI railUI;

        private MenuData _currentMenu;
        private Action<CookingResult> _onCompleteCallback;

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();
            _currentMenu = menu;
            _onCompleteCallback = onComplete;

            StartStep1();
        }

        // --- 1단계 ---
        protected override void OnStep1Start()
        {
            if (onionSlicer != null) onionSlicer.ResetSlicer();
        }

        protected override void OnStep1Update()
        {
            if (onionSlicer != null && onionSlicer.IsCompleted)
            {
                CompleteStep1(1.0f); // 1.5초 딜레이 후 2단계 전이
            }
        }

        // --- 2단계 ---
        protected override void OnStep2Start()
        {
            if (panStirController != null) panStirController.ResetStep();
        }

        protected override void OnStep2Update()
        {
            if (panStirController != null && panStirController.IsCookCompleted)
            {
                float score = Mathf.Clamp01(1.0f - (panStirController.BurnProgress / 100f));
                CompleteStep2(score); // 1.5초 딜레이 후 3단계 전이
            }
        }

        // --- 3단계 ---
        protected override void OnStep3Start()
        {
            // 3단계 리듬 QTE 시작
        }

        protected override void OnStep3Update()
        {
            // QTE 완결 시 CompleteStep3(score) 호출
        }

        protected override void OnMinigameCompleted(float finalScore)
        {
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
