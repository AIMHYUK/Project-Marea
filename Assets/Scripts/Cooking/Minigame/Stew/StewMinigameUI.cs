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
        [Tooltip("(+10/7) 젓기 게이지 채움 — gaugeFillBar 안, 아래 붙이기. 높이를 게이지만큼 늘린다.")]
        [SerializeField] private Image gaugeFill;
        [SerializeField] private Color gaugeFillColor = new Color(0.35f, 1f, 0.4f, 0.9f);
        [SerializeField] private Color gaugeDrainColor = new Color(1f, 0.35f, 0.3f, 0.9f);

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

        /// <summary>
        /// 게이지·방향 화살표를 보이거나 숨긴다. 1·3단계(받기·거품)에선 숨기고 2단계(젓기)에서만 보인다. (+9/30)
        /// </summary>
        public void SetGaugeVisible(bool visible)
        {
            if (gaugeFillBar != null) gaugeFillBar.gameObject.SetActive(visible);
            if (ladleIcon != null) ladleIcon.gameObject.SetActive(visible);
            if (safeZoneRect != null) safeZoneRect.gameObject.SetActive(visible && gaugeFill == null);   // (+10/7) 채움 게이지에선 구간 표시가 필요 없다
            if (arrowIcon != null) arrowIcon.gameObject.SetActive(visible && gaugeFill == null);   // (+10/7) 방향 화살표는 예전 상하좌우 젓기 것
            if (gaugeFill != null) gaugeFill.gameObject.SetActive(visible);
        }

        /// <summary>
        /// (+10/7) 젓기 게이지 — 채움 높이 = gauge01, 원 안이면 초록 · 밖이면 빨강. 국자 아이콘은 채움 꼭대기에 탄다.
        /// </summary>
        public void UpdateStirGauge(float gauge01, bool inside)
        {
            gauge01 = Mathf.Clamp01(gauge01);
            if (gaugeFill != null)
            {
                RectTransform rt = gaugeFill.rectTransform;
                rt.anchorMax = new Vector2(rt.anchorMax.x, gauge01);
                gaugeFill.color = inside ? gaugeFillColor : gaugeDrainColor;
            }
            UpdateGauge(gauge01);
        }

        /// <summary>안내 문구를 직접 쓴다. 1 · 2 · 3단계 모두 이걸로 쓴다. (+9/30, +10/2)</summary>
        public void SetGuide(string main, string sub)
        {
            if (txtGuideDirection != null) txtGuideDirection.text = main;
            if (txtSubGuide != null) txtSubGuide.text = sub;
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
