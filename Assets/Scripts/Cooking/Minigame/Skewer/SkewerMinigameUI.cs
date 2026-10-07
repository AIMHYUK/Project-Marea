using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public class SkewerMinigameUI : MonoBehaviour
    {
        [Header("UI 바인딩")]
        [SerializeField] private GameObject gameRoot;
        [SerializeField] private TimingBarController timingBar;
        [SerializeField] private TextMeshProUGUI txtFeedback;
        [SerializeField] private GameObject sauceProgressRoot;
        [SerializeField] private Image sauceProgressFill;
        [SerializeField] private TextMeshProUGUI sauceProgressText;

        private SkewerMinigameController _controller;
        private bool _isPlaying;

        public void Setup(SkewerMinigameController controller)
        {
            _controller = controller;
        }

        public void Open()
        {
            if (gameRoot != null) gameRoot.SetActive(true);
            gameObject.SetActive(true);

            _isPlaying = true;

            UpdateUIState();
        }

        public void Close()
        {
            _isPlaying = false;
            if (timingBar != null) timingBar.StopGauge();
            if (gameRoot != null) gameRoot.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_controller != null && _controller.CurrentStepIndex == MinigameStepIndex.Step2)
            {
                timingBar?.SetTravelPosition(_controller.IngredientTravelPosition);
                if (_controller.IsInserting) return;
            }

            if (_controller != null && _controller.CurrentStepIndex == MinigameStepIndex.Step3)
            {
                return;
            }

            bool isTriggered = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                               || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (isTriggered)
            {
                OnAttemptHit();
            }
        }

        private void OnAttemptHit()
        {
            HitGrade grade = HitGrade.Good;

            // 2단계(꼬치 끼우기)에서만 타이밍바 평가 수행
            if (_controller != null && _controller.CurrentStepIndex == MinigameStepIndex.Step2)
            {
                grade = timingBar != null ? timingBar.EvaluateHit() : HitGrade.Miss;
                if (txtFeedback != null)
                    txtFeedback.text = grade switch
                    {
                        HitGrade.Perfect => "<color=yellow>PERFECT!</color>",
                        HitGrade.Good => "<color=green>GOOD!</color>",
                        _ => "<color=red>MISS! 다시 타이밍을 맞추세요.</color>"
                    };
            }

            if (_controller != null)
            {
                _controller.ProcessHit(grade);
            }
        }

        private void UpdateUIState()
        {
            if (_controller == null) return;
            if (sauceProgressRoot != null)
                sauceProgressRoot.SetActive(_controller.CurrentStepIndex == MinigameStepIndex.Step3);

            switch (_controller.CurrentStepIndex)
            {
                case MinigameStepIndex.Step1:
                    // 1단계: 타이밍 바 숨김 (단순 클릭 썰기)
                    if (timingBar != null)
                    {
                        timingBar.StopGauge();
                        timingBar.gameObject.SetActive(false);
                    }
                    if (txtFeedback != null)
                    {
                        txtFeedback.text = "가이드선에 맞춰 클릭하여 재료를 자르세요!";
                    }
                    break;

                case MinigameStepIndex.Step2:
                    // 2단계: 타이밍 바 활성화
                    if (timingBar != null)
                    {
                        timingBar.gameObject.SetActive(true);
                        timingBar.Initialize();
                    }
                    if (txtFeedback != null)
                    {
                        txtFeedback.text = "타이밍에 맞춰 재료를 꼬치에 끼우세요.";
                    }
                    break;

                case MinigameStepIndex.Step3:
                    // 3단계: 꼬치의 각 재료에 소스를 바른다.
                    if (timingBar != null)
                    {
                        timingBar.StopGauge();
                        timingBar.gameObject.SetActive(false);
                    }
                    if (txtFeedback != null)
                    {
                        txtFeedback.text = "브러시를 집어 재료 위에서 여러 번 왕복해 소스를 바르세요.";
                    }
                    UpdateSauceProgress(0f, 0, _controller.TotalIngredients);
                    break;
            }
        }

        public void RefreshStageInstructions()
        {
            UpdateUIState();
        }

        public void UpdateSauceProgress(float progress, int painted, int total)
        {
            if (sauceProgressFill != null)
            {
                sauceProgressFill.fillAmount = Mathf.Clamp01(progress);
                sauceProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            }
            if (sauceProgressText != null)
                sauceProgressText.text = $"소스 바르기 {Mathf.RoundToInt(progress * 100f)}%  ({painted}/{total})";
        }

    }
}
