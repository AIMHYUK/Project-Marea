using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Marea.Cooking
{
    public sealed class SeafoodSkewersAssemblyUI : MonoBehaviour
    {
        [SerializeField] private TimingBarController timingBar;
        [SerializeField] private TextMeshProUGUI txtFeedback;
        private SeafoodSkewersMinigameController _controller;
        private bool _isPlaying;

        public void Setup(SeafoodSkewersMinigameController controller) => _controller = controller;
        public void Open()
        {
            gameObject.SetActive(true);
            _isPlaying = true;
            timingBar?.Initialize();
            if (txtFeedback != null) txtFeedback.text = "타이밍 영역에 맞춰 스페이스바 또는 클릭으로 해물꼬치를 꽂으세요!";
        }
        public void Close()
        {
            _isPlaying = false;
            timingBar?.StopGauge();
            gameObject.SetActive(false);
        }
        private void Update()
        {
            if (!_isPlaying || _controller == null || _controller.CurrentStepIndex != MinigameStepIndex.Step2) return;
            timingBar?.SetTravelPosition(_controller.IngredientTravelPosition);
            if (_controller.IsInserting) return;
            bool triggered = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (!triggered) return;
            HitGrade grade = timingBar != null ? timingBar.EvaluateHit() : HitGrade.Miss;
            if (txtFeedback != null)
                txtFeedback.text = grade switch
                {
                    HitGrade.Perfect => "<color=yellow>PERFECT!</color>",
                    HitGrade.Good => "<color=green>GOOD!</color>",
                    _ => "<color=red>MISS! 다시 타이밍을 맞추세요.</color>"
                };
            _controller.ProcessHit(grade);
        }
    }
}
