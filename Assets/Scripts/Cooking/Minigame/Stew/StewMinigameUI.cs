using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Cooking
{
    public class StewMinigameUI : MonoBehaviour
    {
        [Header("루트 바인딩")]
        [SerializeField] private GameObject rootCanvas;

        [Header("방향 지시 가이드 UI")]
        [SerializeField] private RectTransform arrowIcon;
        [SerializeField] private TextMeshProUGUI txtGuideDirection;
        [SerializeField] private TextMeshProUGUI txtSubGuide;

        [Header("게이지 바")]
        [SerializeField] private RectTransform gaugeFillBar;
        [SerializeField] private RectTransform ladleIcon;
        [SerializeField] private RectTransform safeZoneRect;
        [SerializeField] private Image safeZoneHighlight;

        [Header("진행도 타이머")]
        [SerializeField] private Slider timerSlider;

        [Header("결과 창")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI txtResultGrade;

        public void Setup(StewMinigameController controller)
        {
            if (safeZoneRect != null && gaugeFillBar != null)
            {
                float totalHeight = gaugeFillBar.rect.height;
                float bottom = controller.SafeZoneMin * totalHeight;
                float height = (controller.SafeZoneMax - controller.SafeZoneMin) * totalHeight;

                safeZoneRect.anchoredPosition = new Vector2(safeZoneRect.anchoredPosition.x, bottom);
                safeZoneRect.sizeDelta = new Vector2(safeZoneRect.sizeDelta.x, height);
            }

            if (resultPanel != null) resultPanel.SetActive(false);
        }

        public void Open()
        {
            if (rootCanvas != null) rootCanvas.SetActive(true);
        }

        public void Close()
        {
            if (rootCanvas != null) rootCanvas.SetActive(false);
        }

        public void UpdateDirectionGuide(StirDirection direction)
        {
            if (arrowIcon != null)
            {
                float angle = direction switch
                {
                    StirDirection.Up => 0f,
                    StirDirection.Right => -90f,
                    StirDirection.Down => 180f,
                    StirDirection.Left => 90f,
                    _ => 0f
                };
                arrowIcon.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (txtGuideDirection != null)
            {
                txtGuideDirection.text = direction switch
                {
                    StirDirection.Up => "위로 저으세요!",
                    StirDirection.Down => "아래로 저으세요!",
                    StirDirection.Left => "왼쪽으로 저으세요!",
                    StirDirection.Right => "오른쪽으로 저으세요!",
                    _ => string.Empty
                };
            }

            if (txtSubGuide != null)
            {
                txtSubGuide.text = direction switch
                {
                    StirDirection.Up => "↑ 방향으로 젓기",
                    StirDirection.Down => "↓ 방향으로 젓기",
                    StirDirection.Left => "← 방향으로 젓기",
                    StirDirection.Right => "→ 방향으로 젓기",
                    _ => string.Empty
                };
            }
        }

        public void PlayDirectionChangeAlert()
        {
            if (arrowIcon != null)
            {
                arrowIcon.localScale = Vector3.one * 1.3f;
                LeanTweenScaleNormal();
            }
        }

        private void LeanTweenScaleNormal()
        {
            arrowIcon.localScale = Vector3.one;
        }

        public void UpdateGauge(float gauge01)
        {
            if (gaugeFillBar != null && ladleIcon != null)
            {
                float height = gaugeFillBar.rect.height;
                ladleIcon.anchoredPosition = new Vector2(ladleIcon.anchoredPosition.x, gauge01 * height);
            }
        }

        public void SetSafeZoneStatus(bool inSafeZone)
        {
            if (safeZoneHighlight != null)
            {
                safeZoneHighlight.color = inSafeZone
                    ? new Color(0.2f, 1f, 0.2f, 0.6f)
                    : new Color(0.2f, 0.8f, 0.2f, 0.25f);
            }
        }

        public void UpdateTimer(float normalizedTime)
        {
            if (timerSlider != null)
            {
                timerSlider.value = normalizedTime;
            }
        }

        public void ShowResult(HitGrade grade)
        {
            Debug.LogWarning($"[UI 수신 등급] ShowResult에 전달된 grade: {grade}");

            if (resultPanel != null) resultPanel.SetActive(true);
            if (txtResultGrade != null)
            {
                txtResultGrade.text = grade switch
                {
                    HitGrade.Miss => "FAIL",
                    _ => grade.ToString().ToUpper()
                };

                txtResultGrade.color = grade switch
                {
                    HitGrade.Perfect => Color.yellow,
                    HitGrade.Good => Color.green,
                    HitGrade.Bad => new Color(1f, 0.5f, 0f),
                    _ => Color.red
                };

                Debug.LogWarning($"[UI 텍스트 반영 완료] txtResultGrade.text = {txtResultGrade.text}");
            }
        }
    }
}
