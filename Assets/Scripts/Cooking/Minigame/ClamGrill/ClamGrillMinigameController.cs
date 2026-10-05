using System;
using System.Collections;
using Marea.Data;
using TMPro;
using UnityEngine;

namespace Marea.Cooking
{
    /// <summary>
    /// 조개구이 미니게임. 기획 MENU_GRILLED_CLAM — 닦기 → 굽기 → 소스 뿌리기. (+10/2, 이슈 85)
    ///
    /// 1. 닦기 (싱크대, MG_SURFACE_DRAG) — <see cref="ClamScrubGame"/>
    /// 2. 굽기 (그릴, MG_STATE_REACTION) — <see cref="PopupTouchGame"/> 고정 슬롯 모드
    /// 3. 소스 (조리대, MG_GUIDE_DRAG 자리) — <see cref="SauceDrizzleGame"/>
    ///
    /// 단계마다 카메라 시점이 다르다 — 베이스의 step1~3ViewPoint에 공용 시점 VP_Sink / VP_Grill / VP_Counter를 넣는다.
    /// 판매가 = 기본가 × 세 단계 평균 점수, 등급은 평균 0.8 이상 Perfect · 0.5 이상 Good · 그 밑 Bad (야채볶음과 같은 방식).
    /// 진입은 CookingMenuUI의 FishGrill → FishGrillSubtype.Clam 분기다.
    /// </summary>
    public class ClamGrillMinigameController : BaseCookingMinigame
    {
        [Header("단계 부품")]
        [SerializeField] private ClamScrubGame scrubGame;
        [SerializeField] private PopupTouchGame grillGame;
        [SerializeField] private SauceDrizzleGame sauceGame;

        [Header("안내 (선택)")]
        [Tooltip("단계 안내 · 진행 · 남은 시간을 찍을 곳. 비워도 게임은 돈다.")]
        [SerializeField] private TextMeshProUGUI guideLabel;
        [Tooltip("guideLabel을 담은 패널. 미니게임 중에만 켠다.")]
        [SerializeField] private GameObject guideRoot;

        [Tooltip("결과를 보여주고 돌려주기까지(초).")]
        [SerializeField, Min(0f)] private float resultDelay = 1.2f;

        private MenuData _currentMenu;
        private Action<CookingResult> _onComplete;

        protected override void Awake()
        {
            base.Awake();
            if (scrubGame == null || grillGame == null || sauceGame == null)
                Debug.LogError($"{name}: ClamGrillMinigameController의 scrubGame · grillGame · sauceGame 중 비어 있는 게 있다.", this);
            if (guideRoot != null) guideRoot.SetActive(false);
        }

        public void StartMinigame(MenuData menu, Action<CookingResult> onComplete)
        {
            EnsureDependencies();
            _currentMenu = menu;
            _onComplete = onComplete;
            if (guideRoot != null) guideRoot.SetActive(true);
            StartStep1();
        }

        // ── 1. 닦기 ──
        protected override void OnStep1Start()
        {
            if (scrubGame != null) scrubGame.Begin();
        }

        protected override void OnStep1Update()
        {
            if (scrubGame == null) { CompleteStep1(0f); return; }
            SetGuide("조개를 문질러 닦으세요", $"깨끗함 {Mathf.RoundToInt(scrubGame.Score * 100f)}%", scrubGame.TimeLeft01);
            if (scrubGame.IsFinished) CompleteStep1(scrubGame.Score);
        }

        // ── 2. 굽기 ──
        protected override void OnStep2Start()
        {
            if (grillGame != null) grillGame.Begin();
        }

        protected override void OnStep2Update()
        {
            if (grillGame == null) { CompleteStep2(0f); return; }
            SetGuide("입이 벌어지면 바로 누르세요", $"{grillGame.Popped} / {grillGame.Spawned}", grillGame.TimeLeft01);
            if (grillGame.IsFinished) CompleteStep2(grillGame.Score);
        }

        // ── 3. 소스 ──
        protected override void OnStep3Start()
        {
            if (sauceGame != null) sauceGame.Begin();
        }

        protected override void OnStep3Update()
        {
            if (sauceGame == null) { CompleteStep3(0f); return; }
            SetGuide("소스통을 집고 조개 위에 뿌리세요", $"{sauceGame.CoatedCount} / {sauceGame.TotalCount}", sauceGame.TimeLeft01);
            if (sauceGame.IsFinished) CompleteStep3(sauceGame.Score);
        }

        private void SetGuide(string what, string progress, float timeLeft01)
        {
            if (guideLabel == null) return;
            guideLabel.text = $"{what}\n<size=70%>{progress}   남은 시간 {Mathf.CeilToInt(timeLeft01 * 100f)}%</size>";
        }

        protected override void OnMinigameCompleted(float finalScore)
        {
            HitGrade grade = finalScore >= 0.8f ? HitGrade.Perfect : finalScore >= 0.5f ? HitGrade.Good : HitGrade.Bad;
            var result = new CookingResult
            {
                isSuccess = true,
                finalPrice = Mathf.RoundToInt((_currentMenu != null ? _currentMenu.BasePrice : 100) * finalScore),
                bestGrade = grade,
                menuData = _currentMenu,
            };

            if (guideLabel != null) guideLabel.text = $"완성!  {grade}\n<size=70%>판매가 {result.finalPrice} G</size>";
            StartCoroutine(FinishRoutine(result));
        }

        private IEnumerator FinishRoutine(CookingResult result)
        {
            yield return new WaitForSeconds(resultDelay);
            if (guideRoot != null) guideRoot.SetActive(false);
            _onComplete?.Invoke(result);
        }
    }
}
