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

        public void Setup(SkewerMinigameController controller)
        {
            _controller = controller;
        }

        public void Open()
        {
            if (gameRoot != null) gameRoot.SetActive(true);
            gameObject.SetActive(true);

            if (timingBar != null) timingBar.Initialize();
            if (txtFeedback != null) txtFeedback.text = "타이밍에 맞춰 스페이스바 또는 좌클릭!";

            _isPlaying = true;
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

            bool isTriggered = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                               || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (isTriggered)
            {
                OnAttemptHit();
            }
        }

        private void OnAttemptHit()
        {
            HitGrade grade = timingBar != null ? timingBar.EvaluateHit() : HitGrade.Miss;
            ShowFeedback(grade);

            if (_controller != null)
            {
                _controller.ProcessHit(grade);
            }
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
