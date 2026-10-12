using Marea.Data;
using Marea.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도움말 창 프리팹을 만들고 열린 씬 루트에 놓는다. (+10/12) 자체 캔버스 · F1로 연다.
/// (+10/12) 아트가 PF_AUI를 고치는 중이라 HUD엔 붙이지 않는다.
///
/// 모양은 붕괴: 스타레일 튜토리얼 창을 따랐다 — 어두운 바탕 · 왼쪽 탭 · 검색 · 목록, 오른쪽 밝은 판에 그림 · 제목 · 설명 · 페이지 넘김.
/// 다시 돌리면 PF_HelpWindow를 덮어쓰고 열린 씬의 인스턴스를 새로 놓는다. HelpBook 에셋(내용)은 있으면 안 건드린다.
/// 기준 해상도 1920×1080(PF_AUI CanvasScaler와 같다).
/// </summary>
public static class HelpWindowBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/PF_HelpWindow.prefab";
    private const string BookPath = "Assets/Scripts/Data/HelpBook.asset";

    private static readonly Color Gold = new(0.85f, 0.72f, 0.48f, 1f);
    private static readonly Color Paper = new(0.91f, 0.91f, 0.9f, 1f);
    private static readonly Color Ink = new(0.2f, 0.2f, 0.2f, 1f);

    private static TMP_FontAsset _bold, _semi;
    private static Sprite _round, _knob;

    [MenuItem("Marea/UI/도움말 창 만들기")]
    public static void Build()
    {
        _bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/A2Z-Bold SDF.asset");
        _semi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/A2Z-SemiBold SDF.asset");
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var cross = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Bamao/BamaoUIPack/Sprites/Character sheet/Button_cross.png");

        var book = AssetDatabase.LoadAssetAtPath<HelpBook>(BookPath);
        if (book == null)
        {
            book = ScriptableObject.CreateInstance<HelpBook>();
            book.categories = new string[0];
            book.topics = new HelpBook.Topic[0];
            AssetDatabase.CreateAsset(book, BookPath);
        }

        // --- 창 ---
        var root = new GameObject("PF_HelpWindow", typeof(RectTransform));
        Stretch(root);
        var window = root.AddComponent<HelpWindow>();
        // HUD의 시계 · 골드는 따로 된 캔버스라 같은 캔버스 맨 아래에 둬도 위에 그려진다 — 자체 정렬 순서로 덮는다.
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        GameObject panel = Child(root, "Panel"); Stretch(panel);
        Img(panel, null, new Color(0.04f, 0.04f, 0.06f, 0.97f));   // 뒤 화면을 막는다

        // 왼쪽 위 머리
        GameObject bulb = Child(panel, "Bulb"); Place(bulb, TopLeft, new Vector2(92, -100), new Vector2(44, 44));
        Img(bulb, _knob, Gold);
        Text(Child(panel, "Header"), "도움말 목록", 24, Gold, TopLeft, new Vector2(130 + 200, -84), new Vector2(400, 32), TextAlignmentOptions.Left, _bold);
        TextMeshProUGUI categoryLabel = Text(Child(panel, "Category"), "전체", 38, Color.white, TopLeft, new Vector2(130 + 200, -122), new Vector2(400, 50), TextAlignmentOptions.Left, _bold);

        // 닫기
        GameObject close = Child(panel, "CloseButton"); Place(close, TopRight, new Vector2(-70, -70), new Vector2(64, 64));
        Button closeButton = close.AddComponent<Button>();
        Img(close, cross, Color.white);

        // 탭 줄
        GameObject tabs = Child(panel, "Tabs"); Place(tabs, TopLeft, new Vector2(80 + 540, -195), new Vector2(1080, 64));   // (+10/12) 탭 7개(전체 + 시트 분류 6)
        var tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 12; tabLayout.childForceExpandWidth = true; tabLayout.childForceExpandHeight = true; tabLayout.childControlWidth = true; tabLayout.childControlHeight = true;
        GameObject tab = Child(tabs, "TabTemplate");
        Img(tab, _round, new Color(1, 1, 1, 0f));
        Button tabTemplate = tab.AddComponent<Button>();
        Text(Child(tab, "Label"), "탭", 24, Color.white, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        GameObject underline = Child(tab, "Underline"); Place(underline, new Anchor(new Vector2(0.1f, 0), new Vector2(0.9f, 0), new Vector2(0.5f, 0)), Vector2.zero, new Vector2(0, 4));
        Img(underline, null, Gold);
        GameObject divider = Child(panel, "Divider"); Place(divider, TopLeft, new Vector2(80 + 540, -229), new Vector2(1080, 2));
        Img(divider, null, new Color(1, 1, 1, 0.18f));

        // 검색
        GameObject searchGo = Child(panel, "Search"); Place(searchGo, TopLeft, new Vector2(95 + 245, -282), new Vector2(490, 56));
        Image searchBg = Img(searchGo, _round, new Color(1, 1, 1, 0.06f));
        searchGo.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.45f);
        GameObject area = Child(searchGo, "Text Area"); Stretch(area, new Vector2(24, 6), new Vector2(-24, -6));
        area.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Text(Child(area, "Placeholder"), "도움말 검색", 24, new Color(1, 1, 1, 0.45f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);
        TextMeshProUGUI searchText = Text(Child(area, "Text"), "", 24, Color.white, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);
        var search = searchGo.AddComponent<TMP_InputField>();
        search.targetGraphic = searchBg; search.textViewport = area.GetComponent<RectTransform>();
        search.textComponent = searchText; search.placeholder = placeholder; search.fontAsset = _semi; search.pointSize = 24;

        // 목록
        GameObject scroll = Child(panel, "List"); Place(scroll, new Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1)), new Vector2(95, -330), new Vector2(500, 0));
        RectTransform srt = scroll.GetComponent<RectTransform>(); srt.offsetMin = new Vector2(95, 40); srt.offsetMax = new Vector2(595, -330);
        var sr = scroll.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
        GameObject viewport = Child(scroll, "Viewport"); Stretch(viewport); viewport.AddComponent<RectMask2D>();
        GameObject content = Child(viewport, "Content");
        Place(content, new Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)), Vector2.zero, new Vector2(0, 0));
        var vl = content.AddComponent<VerticalLayoutGroup>(); vl.spacing = 10; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(30, 0, 0, 0);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport.GetComponent<RectTransform>(); sr.content = content.GetComponent<RectTransform>();
        GameObject item = Child(content, "ItemTemplate");
        Img(item, null, new Color(1, 1, 1, 0.06f));
        item.AddComponent<LayoutElement>().preferredHeight = 88;
        Button itemTemplate = item.AddComponent<Button>();
        GameObject accent = Child(item, "Accent"); Place(accent, new Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f)), Vector2.zero, new Vector2(6, 0));
        Img(accent, null, Gold);
        Text(Child(item, "Label"), "항목", 28, new Color(0.92f, 0.92f, 0.92f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _bold).margin = new Vector4(34, 0, 10, 0);
        GameObject marker = Child(item, "Marker"); Place(marker, new Anchor(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f)), new Vector2(-18, 0), new Vector2(18, 18));
        Img(marker, null, Gold); marker.transform.localRotation = Quaternion.Euler(0, 0, 45);
        GameObject empty = Child(panel, "EmptyHint");
        Text(empty, "찾는 도움말이 없습니다", 24, new Color(1, 1, 1, 0.5f), TopLeft, new Vector2(345, -400), new Vector2(490, 40), TextAlignmentOptions.Center, _semi);

        // 오른쪽 판
        GameObject sheet = Child(panel, "Sheet");
        RectTransform shrt = Place(sheet, new Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f)), Vector2.zero, Vector2.zero);
        shrt.offsetMin = new Vector2(650, 0); shrt.offsetMax = new Vector2(-60, -250);
        Img(sheet, null, Paper);
        GameObject band = Child(sheet, "TopBand"); Place(band, new Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)), Vector2.zero, new Vector2(0, 220));
        Img(band, null, new Color(0.13f, 0.13f, 0.14f, 1f));
        GameObject frame = Child(sheet, "ImageFrame"); Place(frame, new Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1)), new Vector2(0, -40), new Vector2(720, 330));   // (+10/12) 글이 길어져 줄였다
        Img(frame, null, new Color(1, 1, 1, 1f));
        frame.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.25f);
        GameObject pic = Child(frame, "Image"); Stretch(pic, new Vector2(8, 8), new Vector2(-8, -8));
        Image image = Img(pic, null, Color.white); image.preserveAspect = true;
        GameObject body = Child(sheet, "Body");
        RectTransform bodyRt = Place(body, new Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1)), new Vector2(0, -410), new Vector2(1000, 0));
        var bodyLayout = body.AddComponent<VerticalLayoutGroup>(); bodyLayout.spacing = 18; bodyLayout.childControlHeight = true; bodyLayout.childControlWidth = true; bodyLayout.childForceExpandHeight = false;
        body.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI title = Text(Child(body, "Title"), "제목", 40, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        TextMeshProUGUI text = Text(Child(body, "Text"), "설명", 25, new Color(0.3f, 0.3f, 0.3f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);   // (+10/12) 여러 문단이라 왼쪽 정렬
        text.lineSpacing = 8;
        // (+10/12) 아버지의 한마디 — 설명 아래 금빛 기울임
        TextMeshProUGUI quote = Text(Child(body, "Quote"), "한마디", 26, new Color(0.62f, 0.47f, 0.22f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _semi);
        quote.fontStyle = FontStyles.Italic;

        GameObject dots = Child(sheet, "Dots"); Place(dots, new Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0)), new Vector2(0, 128), new Vector2(200, 14));
        var dl = dots.AddComponent<HorizontalLayoutGroup>(); dl.spacing = 10; dl.childAlignment = TextAnchor.MiddleCenter; dl.childControlWidth = false; dl.childControlHeight = false; dl.childForceExpandWidth = false;
        GameObject dot = Child(dots, "DotTemplate"); Place(dot, TopLeft, Vector2.zero, new Vector2(12, 12));
        Image dotTemplate = Img(dot, _knob, Ink);
        Button prev = Pill(sheet, "PrevButton", "이전 페이지", new Vector2(-170, 56));
        Button next = Pill(sheet, "NextButton", "다음 페이지", new Vector2(170, 56));

        // 연결
        var so = new SerializedObject(window);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("book").objectReferenceValue = book;
        so.FindProperty("categoryLabel").objectReferenceValue = categoryLabel;
        so.FindProperty("tabTemplate").objectReferenceValue = tabTemplate;
        so.FindProperty("search").objectReferenceValue = search;
        so.FindProperty("itemTemplate").objectReferenceValue = itemTemplate;
        so.FindProperty("emptyHint").objectReferenceValue = empty;
        so.FindProperty("image").objectReferenceValue = image;
        so.FindProperty("imageFrame").objectReferenceValue = frame;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("body").objectReferenceValue = text;
        so.FindProperty("quote").objectReferenceValue = quote;
        so.FindProperty("dotTemplate").objectReferenceValue = dotTemplate;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();
        tab.SetActive(false); item.SetActive(false); dot.SetActive(false);
        panel.SetActive(false);   // (+10/12) 켜진 채 저장하면 편집 상태 Game 뷰에 빈 창이 보인다

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        foreach (HelpWindow old in Object.FindObjectsByType<HelpWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(old.gameObject);
        PrefabUtility.InstantiatePrefab(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[도움말] {PrefabPath} 생성, 열린 씬에 놓았다(F1). 내용: {BookPath}");
    }

    // --- 만들기 도우미 ---

    private readonly struct Anchor
    {
        public readonly Vector2 Min, Max, Pivot;
        public Anchor(Vector2 min, Vector2 max, Vector2 pivot) { Min = min; Max = max; Pivot = pivot; }
    }

    private static readonly Anchor TopLeft = new(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f));
    private static readonly Anchor TopRight = new(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
    private static readonly Anchor Stretched = new(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

    private static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static RectTransform Place(GameObject go, Anchor a, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = a.Min; rt.anchorMax = a.Max; rt.pivot = a.Pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(GameObject go) => Stretch(go, Vector2.zero, Vector2.zero);

    private static void Stretch(GameObject go, Vector2 min, Vector2 max)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = min; rt.offsetMax = max;
    }

    private static Image Img(GameObject go, Sprite sprite, Color color)
    {
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color;
        if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    private static TextMeshProUGUI Text(GameObject go, string text, float size, Color color, Anchor a, Vector2 pos, Vector2 box, TextAlignmentOptions align, TMP_FontAsset font)
    {
        Place(go, a, pos, box);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        return t;
    }

    private static Button Pill(GameObject parent, string name, string label, Vector2 pos)
    {
        GameObject go = Child(parent, name);
        Place(go, new Anchor(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0)), pos, new Vector2(300, 60));
        Img(go, _round, Color.white);
        go.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.18f);
        Text(Child(go, "Label"), label, 26, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        return go.AddComponent<Button>();
    }
}
