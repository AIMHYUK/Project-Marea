using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Core
{
    /// <summary>
    /// 화면 위쪽에 잠깐 떴다 사라지는 한 줄 알림. (+10/7)
    /// 쓰는 쪽은 <c>Toast.Show("무언가 떠내려온 것 같다…")</c>만 부른다.
    ///
    /// 씬에 Toast 하나를 두고 폰트만 넣는다. 캔버스 · 배경 · 글자는 Awake에서 스스로 만든다 —
    /// 알림 하나 띄우자고 UI 프리팹을 B의 UI 폴더에 만들 필요가 없게.
    /// 이미 떠 있는데 또 오면 글자만 바꾸고 시간을 다시 잰다.
    /// </summary>
    public class Toast : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(8f)] private float fontSize = 34f;
        [SerializeField] private Color textColor = new(1f, 0.97f, 0.88f, 1f);
        [SerializeField] private Color backColor = new(0.18f, 0.12f, 0.07f, 0.78f);
        [Tooltip("화면 위에서 얼마나 내려와 뜨는가(px, 1080p 기준).")]
        [SerializeField] private float topMargin = 170f;
        [SerializeField, Min(0.1f)] private float showSeconds = 2.5f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private static Toast _instance;
        private CanvasGroup _group;
        private TextMeshProUGUI _text;
        private Coroutine _routine;

        /// <summary>알림을 띄운다. 씬에 Toast가 없으면 에러만 남긴다.</summary>
        public static void Show(string message)
        {
            if (_instance == null)
            {
                Debug.LogError($"씬에 Toast가 없다. 알림 \"{message}\"을 못 띄운다.");
                return;
            }
            _instance.Display(message);
        }

        private void Awake()
        {
            _instance = this;
            if (font == null)
                Debug.LogError($"{name}: Toast.font가 비어 있다. 한글이 네모로 나온다.", this);
            Build();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            var canvasGo = new GameObject("ToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;   // 다른 창 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;   // 클릭을 막지 않는다
            _group.interactable = false;

            var back = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            back.transform.SetParent(canvasGo.transform, false);
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topMargin);
            back.GetComponent<Image>().color = backColor;
            back.GetComponent<Image>().raycastTarget = false;
            var layout = back.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 14, 14);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = back.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(back.transform, false);
            _text = textGo.GetComponent<TextMeshProUGUI>();
            if (font != null) _text.font = font;
            _text.fontSize = fontSize;
            _text.color = textColor;
            _text.alignment = TextAlignmentOptions.Center;
            _text.raycastTarget = false;
        }

        private void Display(string message)
        {
            _text.text = message;
            SoundManager.Play(clip, volume);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            for (float t = _group.alpha * fadeSeconds; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = t / fadeSeconds;
                yield return null;
            }
            _group.alpha = 1f;
            yield return new WaitForSecondsRealtime(showSeconds);
            for (float t = fadeSeconds; t > 0f; t -= Time.unscaledDeltaTime)
            {
                _group.alpha = t / fadeSeconds;
                yield return null;
            }
            _group.alpha = 0f;
            _routine = null;
        }
    }
}
