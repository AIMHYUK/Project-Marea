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

        private SkewerMinigameController _controller;
        private bool _isPlaying;
        private Vector2 _lastBrushPosition;
        private bool _wasBrushing;

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
            _wasBrushing = false;
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
                if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    Vector2 position = Mouse.current.position.ReadValue();
                    Vector2 start = _wasBrushing ? _lastBrushPosition : position;
                    int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, position) / 6f), 1, 64);
                    for (int i = 1; i <= samples; i++)
                    {
                        _controller.ProcessSauceDrag(Vector2.Lerp(start, position, (float)i / samples));
                    }
                    _lastBrushPosition = position;
                    _wasBrushing = true;
                }
                else _wasBrushing = false;
                return;
            }
            _wasBrushing = false;

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
                ShowFeedback(grade);
            }

            if (_controller != null)
            {
                _controller.ProcessHit(grade);
            }
        }

        private void UpdateUIState()
        {
            if (_controller == null) return;

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
                        txtFeedback.text = "타이밍 영역에 맞춰 스페이스바 또는 클릭으로 꼬치를 꽂으세요!";
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
                        txtFeedback.text = "마우스를 누른 채 꼬치의 재료 위를 드래그해 소스를 바르세요!";
                    }
                    break;
            }
        }

        public void RefreshStageInstructions()
        {
            UpdateUIState();
        }

        public void UpdateSauceProgress(int painted, int total)
        {
            if (txtFeedback == null || total <= 0) return;
            txtFeedback.text = $"소스 바르기 {painted}/{total} — 꼬치의 재료를 모두 드래그하세요!";
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
    }
}
