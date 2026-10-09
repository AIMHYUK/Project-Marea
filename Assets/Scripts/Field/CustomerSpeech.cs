using Marea.Core;
using Marea.Data;
using Marea.Restaurant;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Marea.Field
{
    /// <summary>
    /// 손님 머리 위 한글 말풍선. (+10/8) "<b>고등어구이</b>가 먹고 싶네…"처럼 주문한 메뉴를 강조한다.
    ///
    /// 손님 코드(B)를 건드리지 않고 상태(State · OrderedMenu · WaitingSince)를 지켜보다 바뀌는 순간 한마디 한다.
    /// 앉아서 주문 → 주문 대사 / 오래 기다림 → 재촉 / 음식 받음 → 기뻐함 / 못 받고 떠남 → 투덜.
    /// 말풍선은 손님 프리팹에 붙이면 Awake에서 스스로 만든다(월드 캔버스 + 몸통 · 꼬리 + 글자).
    /// </summary>
    [RequireComponent(typeof(CustomerController))]
    public class CustomerSpeech : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private Sprite tailSprite;
        [Tooltip("손님 원점에서 말풍선 아래 끝까지 높이(m). 주문 아이콘 말풍선보다 위.")]
        [SerializeField] private float height = 2.75f;
        [SerializeField, Min(8f)] private float fontSize = 34f;
        [SerializeField] private Color textColor = new(0.24f, 0.15f, 0.08f, 1f);
        [SerializeField] private Color menuColor = new(0.80f, 0.26f, 0.12f, 1f);

        [Header("언제")]
        [SerializeField, Min(0.5f)] private float showSeconds = 3f;
        [Tooltip("주문하고 이만큼 지나면 재촉한다. 손님의 화남 표시(angryAfterSeconds)와 맞춘다.")]
        [SerializeField, Min(1f)] private float impatientAfter = 30f;

        [Header("대사 — {0}이 메뉴 이름, {1}이 이/가")]
        [SerializeField] private string[] orderLines = { "{0}{1} 먹고 싶네…", "{0} 하나 주세요!", "오늘은 {0}{1} 땡기네~" };
        [SerializeField] private string[] impatientLines = { "{0} 아직인가요…?", "배고파… {0}…" };
        [SerializeField] private string[] happyLines = { "와, 맛있겠다!", "잘 먹겠습니다!" };
        [SerializeField] private string[] leaveAngryLines = { "이건 내가 시킨 게 아닌데!" };

        [Header("소리 (+10/8) — 기획 GST-02 착석 · GST-03 주문 말풍선")]
        [SerializeField] private AudioClip sitClip;
        [SerializeField] private AudioClip orderClip;
        [Tooltip("앉는 소리 뒤 주문 소리까지(초).")]
        [SerializeField, Min(0f)] private float orderDelay = 0.35f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private const float TailScale = 0.45f;   // 원본 그림(8배 해상도) → 캔버스 픽셀

        private CustomerController _customer;
        private CustomerState _last;
        private bool _nagged;
        private GameObject _bubble;
        private TextMeshProUGUI _text;
        private float _hideAt;

        private void Awake()
        {
            _customer = GetComponent<CustomerController>();
            if (font == null)
                Debug.LogError($"{name}: CustomerSpeech.font가 비어 있다. 한글이 네모로 나온다.", this);
            Build();
            _last = _customer.State;
        }

        private void Update()
        {
            CustomerState now = _customer.State;
            if (now != _last)
            {
                OnStateChanged(_last, now);
                _last = now;
            }
            if (now == CustomerState.WaitingOrder && !_nagged && Time.time - _customer.WaitingSince >= impatientAfter)
            {
                _nagged = true;
                Say(impatientLines);
            }
            if (_bubble.activeSelf && Time.time >= _hideAt) _bubble.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!_bubble.activeSelf) return;
            Camera cam = Camera.main;
            if (cam != null) _bubble.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }

        private void OnStateChanged(CustomerState from, CustomerState to)
        {
            if (to == CustomerState.WaitingOrder)
            {
                _nagged = false;
                Say(orderLines);
                // 앉자마자 주문한다(상태가 하나) — 앉는 소리, 조금 뒤 말풍선 소리.
                SoundManager.PlayAt(sitClip, transform.position, volume, 0.05f);
                if (orderClip != null) StartCoroutine(PlayLater(orderClip, orderDelay));
            }
            else if (to == CustomerState.Eating) Say(happyLines);
            else if (to == CustomerState.Leaving && from == CustomerState.WaitingOrder) Say(leaveAngryLines);
        }

        private void Say(string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            MenuData menu = _customer.OrderedMenu;
            string name = menu != null ? menu.DisplayName : "";
            string shown = $"<b><color=#{ColorUtility.ToHtmlStringRGB(menuColor)}>{name}</color></b>";
            _text.text = string.Format(lines[Random.Range(0, lines.Length)], shown, Josa(name));
            _bubble.SetActive(true);
            _hideAt = Time.time + showSeconds;
        }

        private System.Collections.IEnumerator PlayLater(AudioClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);
            SoundManager.PlayAt(clip, transform.position, volume, 0.05f);
        }

        /// <summary>받침이 있으면 "이", 없으면 "가".</summary>
        private static string Josa(string word)
        {
            if (string.IsNullOrEmpty(word)) return "가";
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return "가";
            return (c - 0xAC00) % 28 != 0 ? "이" : "가";
        }

        // --- 말풍선 만들기 ---

        private void Build()
        {
            _bubble = new GameObject("Speech", typeof(RectTransform), typeof(Canvas));
            _bubble.transform.SetParent(transform, false);
            var canvas = _bubble.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            var rt = (RectTransform)_bubble.transform;
            rt.localPosition = Vector3.up * height;
            rt.sizeDelta = new Vector2(10f, 10f);
            float parentScale = Mathf.Max(0.0001f, transform.lossyScale.y);
            rt.localScale = Vector3.one * (0.005f / parentScale);   // 캔버스 1px = 5mm

            // 몸통: 글자 길이에 맞춰 늘어난다. 꼬리는 몸통 아래 가운데.
            var body = new GameObject("Body", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            body.transform.SetParent(_bubble.transform, false);
            var brt = (RectTransform)body.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 26f);
            var img = body.GetComponent<Image>();
            img.sprite = bodySprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / TailScale;   // 테두리 두께를 꼬리와 같은 비율로
            img.raycastTarget = false;
            var layout = body.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 22, 24);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = body.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(body.transform, false);
            _text = textGo.GetComponent<TextMeshProUGUI>();
            if (font != null) _text.font = font;
            _text.fontSize = fontSize;
            _text.color = textColor;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.raycastTarget = false;

            var tail = new GameObject("Tail", typeof(RectTransform), typeof(Image));
            tail.transform.SetParent(_bubble.transform, false);
            var trt = (RectTransform)tail.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = tailSprite != null ? new Vector2(tailSprite.rect.width, tailSprite.rect.height) * TailScale : new Vector2(60f, 30f);
            var timg = tail.GetComponent<Image>();
            timg.sprite = tailSprite;
            timg.raycastTarget = false;

            _bubble.SetActive(false);
        }
    }
}
