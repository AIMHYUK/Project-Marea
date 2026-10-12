using System.Collections.Generic;
using Marea.Core;
using Marea.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Marea.Hud
{
    /// <summary>
    /// 도움말 창. (+10/12) 왼쪽 분류 탭 · 검색 · 항목 목록, 오른쪽 그림 · 제목 · 설명 · 페이지 넘김. F1로 여닫는다.
    /// (+10/12) 아트가 PF_AUI를 고치는 중이라 HUD 밖 자체 캔버스로 씬에 따로 둔다. [메뉴] 버튼은 나중에 Toggle만 이으면 된다.
    ///
    /// 내용은 HelpBook 에셋이 든다. 탭 · 목록 · 페이지 점은 템플릿 오브젝트(꺼 둔 것)를 복제해 채운다 — 모양은 프리팹에서 고친다.
    /// 프리팹은 Marea/UI/도움말 창 만들기(HelpWindowBuilder)가 만든다.
    /// </summary>
    public class HelpWindow : UiPanel
    {
        private const string All = "전체";

        [SerializeField] private HelpBook book;
        [Tooltip("여닫는 키.")]
        [SerializeField] private Key toggleKey = Key.F1;
        [Tooltip("HUD의 시계(WorldClockCanvas 10) 등 따로 된 캔버스 위에 그린다. 토스트(500)보다는 아래.")]
        [SerializeField] private int sortingOrder = 100;

        [Header("왼쪽")]
        [SerializeField] private TextMeshProUGUI categoryLabel;
        [SerializeField] private Button tabTemplate;
        [SerializeField] private TMP_InputField search;
        [SerializeField] private Button itemTemplate;
        [SerializeField] private GameObject emptyHint;

        [Header("오른쪽")]
        [SerializeField] private Image image;
        [SerializeField] private GameObject imageFrame;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI body;
        [Tooltip("(+10/12) 아버지의 한마디. 항목의 quote가 비면 숨긴다.")]
        [SerializeField] private TextMeshProUGUI quote;
        [Tooltip("제목 · 설명 묶음의 높이 — 그림이 있을 때 / 없을 때(위로 올린다).")]
        [SerializeField] private float bodyYWithImage = -410f;
        [SerializeField] private float bodyYWithoutImage = -240f;
        [SerializeField] private Image dotTemplate;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("색")]
        [SerializeField] private Color tabOn = new(0.95f, 0.84f, 0.6f, 1f);
        [SerializeField] private Color tabOff = new(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color itemOn = new(0.93f, 0.93f, 0.93f, 1f);
        [SerializeField] private Color itemOff = new(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color itemTextOn = new(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color itemTextOff = new(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] private Color dotOn = new(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color dotOff = new(0.15f, 0.15f, 0.15f, 0.3f);

        private readonly List<Button> _tabs = new();
        private readonly List<(Button button, HelpBook.Topic topic)> _items = new();
        private readonly List<Image> _dots = new();
        private string _category = All;
        private HelpBook.Topic _topic;
        private int _page;

        protected override void Awake()
        {
            base.Awake();
            // 자체 캔버스(씬 루트) — 다른 캔버스 아래에 넣어도 정렬 덮어쓰기로 HUD 위에 그린다.
            Canvas own = GetComponent<Canvas>();
            if (own == null) own = gameObject.AddComponent<Canvas>();
            own.renderMode = RenderMode.ScreenSpaceOverlay;
            own.overrideSorting = true;
            own.sortingOrder = sortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            if (book == null) Debug.LogError($"{name}: HelpWindow.book이 비어 있다. 도움말 내용이 없다.", this);
            if (tabTemplate == null || itemTemplate == null || dotTemplate == null || title == null || body == null)
                Debug.LogError($"{name}: HelpWindow의 템플릿 · 글 칸 중 비어 있는 게 있다. 도움말 창을 다시 만들 것.", this);
            if (prevButton != null) prevButton.onClick.AddListener(() => ShowPage(_page - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(_page + 1));
            if (search != null) search.onValueChanged.AddListener(_ => RebuildList());
            BuildTabs();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb[toggleKey].wasPressedThisFrame) Toggle();
            else if (IsVisible && kb.escapeKey.wasPressedThisFrame) Close();
        }

        protected override void OnOpened()
        {
            if (search != null) search.SetTextWithoutNotify("");
            SelectCategory(All);
        }

        private void BuildTabs()
        {
            if (book == null || tabTemplate == null) return;
            tabTemplate.gameObject.SetActive(false);
            var names = new List<string> { All };
            if (book.categories != null) names.AddRange(book.categories);
            foreach (string n in names)
            {
                Button tab = Instantiate(tabTemplate, tabTemplate.transform.parent);
                tab.gameObject.SetActive(true);
                tab.name = $"Tab_{n}";
                tab.GetComponentInChildren<TextMeshProUGUI>().text = n;
                string captured = n;
                tab.onClick.AddListener(() => SelectCategory(captured));
                _tabs.Add(tab);
            }
        }

        private void SelectCategory(string category)
        {
            _category = category;
            if (categoryLabel != null) categoryLabel.text = category;
            foreach (Button tab in _tabs)
            {
                bool on = tab.name == $"Tab_{category}";
                tab.GetComponentInChildren<TextMeshProUGUI>().color = on ? tabOn : tabOff;
                Transform line = tab.transform.Find("Underline");
                if (line != null) line.gameObject.SetActive(on);
            }
            RebuildList();
        }

        private void RebuildList()
        {
            foreach (var (button, _) in _items) Destroy(button.gameObject);
            _items.Clear();
            if (book == null || itemTemplate == null) return;
            itemTemplate.gameObject.SetActive(false);

            string q = search != null ? search.text.Trim() : "";
            foreach (HelpBook.Topic t in book.topics)
            {
                if (q.Length == 0 && _category != All && t.category != _category) continue;   // 검색어가 있으면 전체에서 찾는다
                if (q.Length > 0 && t.title.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                Button item = Instantiate(itemTemplate, itemTemplate.transform.parent);
                item.gameObject.SetActive(true);
                item.GetComponentInChildren<TextMeshProUGUI>().text = t.title;
                HelpBook.Topic captured = t;
                item.onClick.AddListener(() => SelectTopic(captured));
                _items.Add((item, t));
            }
            if (emptyHint != null) emptyHint.SetActive(_items.Count == 0);
            SelectTopic(_items.Count > 0 ? (_items.Exists(i => i.topic == _topic) ? _topic : _items[0].topic) : null);
        }

        private void SelectTopic(HelpBook.Topic topic)
        {
            _topic = topic;
            foreach (var (button, t) in _items)
            {
                bool on = t == topic;
                button.GetComponent<Image>().color = on ? itemOn : itemOff;
                button.GetComponentInChildren<TextMeshProUGUI>().color = on ? itemTextOn : itemTextOff;
                Transform mark = button.transform.Find("Marker");
                if (mark != null) mark.gameObject.SetActive(on);
            }

            foreach (Image d in _dots) Destroy(d.gameObject);
            _dots.Clear();
            int pages = topic?.pages?.Length ?? 0;
            if (dotTemplate != null)
            {
                dotTemplate.gameObject.SetActive(false);
                for (int i = 0; i < pages && pages > 1; i++)
                {
                    Image dot = Instantiate(dotTemplate, dotTemplate.transform.parent);
                    dot.gameObject.SetActive(true);
                    _dots.Add(dot);
                }
            }
            ShowPage(0);
        }

        private void ShowPage(int page)
        {
            int count = _topic?.pages?.Length ?? 0;
            _page = Mathf.Clamp(page, 0, Mathf.Max(0, count - 1));
            HelpBook.Page p = count > 0 ? _topic.pages[_page] : null;

            if (title != null) title.text = _topic != null ? _topic.title : "";
            if (body != null) body.text = p != null ? p.text : "";
            if (quote != null)
            {
                bool has = _topic != null && !string.IsNullOrEmpty(_topic.quote);
                quote.gameObject.SetActive(has);
                if (has) quote.text = $"“{_topic.quote}”";
            }
            bool hasImage = p != null && p.image != null;
            if (imageFrame != null) imageFrame.SetActive(hasImage);
            if (image != null && hasImage) image.sprite = p.image;
            if (title != null && title.transform.parent is RectTransform group)
                group.anchoredPosition = new Vector2(group.anchoredPosition.x, hasImage ? bodyYWithImage : bodyYWithoutImage);
            for (int i = 0; i < _dots.Count; i++) _dots[i].color = i == _page ? dotOn : dotOff;
            if (prevButton != null) { prevButton.gameObject.SetActive(count > 1); prevButton.interactable = _page > 0; }   // (+10/12) 한 장이면 넘김 버튼을 숨긴다
            if (nextButton != null) { nextButton.gameObject.SetActive(count > 1); nextButton.interactable = _page < count - 1; }
        }
    }
}
