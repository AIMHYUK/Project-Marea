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
        private bool _isStep3Completed;

        protected override bool KeepFinalPanelUntilCameraReturns => true;

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();

            ResetMinigame();

            _currentMenu = menu;
            _onCompleteCallback = onComplete;

            StartStep1();
        }

        protected override void ResetMinigame()
        {
            base.ResetMinigame();

            _isStep3Completed = false;

            // 1단계는 StartStep1 → OnStep1Start에서 ResetSlicer를 호출하므로
            // 여기서 다시 호출하지 않는다.

            if (panStirController != null)
            {
                panStirController.ResetStep();
            }

            if (seasoningQTE != null)
            {
                seasoningQTE.ResetQTE();
            }

            if (railUI != null)
            {
                railUI.ResetRail();
            }
        }

        // --- 1단계 ---
        protected override void OnStep1Start()
        {
            if (step1Panel != null) step1Panel.SetActive(true);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(false);

            if (onionSlicer != null) onionSlicer.ResetSlicer();
        }

        protected override void OnStep1Update()
        {
            if (onionSlicer != null && onionSlicer.IsCompleted)
            {
                CompleteStep1(1.0f);
            }
        }

        // --- 2단계 ---
        protected override void OnStep2Start()
        {
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(true);
            if (step3Panel != null) step3Panel.SetActive(false);

            if (panStirController != null) panStirController.ResetStep();
        }

        protected override void OnStep2Update()
        {
            if (panStirController != null && panStirController.IsCookCompleted)
            {
                float score = panStirController.CookingScore;
                CompleteStep2(score);
            }
        }

        // --- 3단계 ---
        protected override void OnStep3Start()
        {
            if (step1Panel != null) step1Panel.SetActive(false);
            if (step2Panel != null) step2Panel.SetActive(false);
            if (step3Panel != null) step3Panel.SetActive(true); // 3단계 시작 시 패널 활성화

            _isStep3Completed = false;

            if (seasoningQTE != null)
            {
                seasoningQTE.StartQTEGame(OnStep3Finished);
            }
        }

        protected override void OnStep3Update()
        {
            if (seasoningQTE != null && seasoningQTE.IsQTECompleted && !_isStep3Completed)
            {
                OnStep3Finished();
            }
        }

        private void OnStep3Finished()
        {
            if (_isStep3Completed) return;
            _isStep3Completed = true;

            if (seasoningQTE == null) return;

            int total = seasoningQTE.TotalNoteCount > 0 ? seasoningQTE.TotalNoteCount : 15;
            float score = Mathf.Clamp01((float)seasoningQTE.SuccessCount / total);

            Debug.Log($"[VeggieStirFryMinigameController] 3단계 완료! 계산된 점수: {score}");

            CompleteStep3(score);
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
