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
/// 배치는 붕괴: 스타레일 튜토리얼 창을 따랐다 — 왼쪽 검색 · 목록, 위 분류 탭, 오른쪽 판에 그림 · 제목 · 설명 · 페이지 넘김.
/// (+10/13) 깔은 아트(요리 선택 창)에 맞췄다 — "도움말" 리본, 늘 쓰는 버튼(베이지 · 고르면 초록), 오른쪽은 양피지, 뒤는 옅게만 가린다.
/// 다시 돌리면 PF_HelpWindow를 덮어쓰고 열린 씬의 인스턴스를 새로 놓는다. HelpBook 에셋(내용)은 있으면 안 건드린다.
/// 기준 해상도 1920×1080(PF_AUI CanvasScaler와 같다).
/// </summary>
public static class HelpWindowBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/PF_HelpWindow.prefab";
    private const string BookPath = "Assets/Scripts/Data/HelpBook.asset";

    // (+10/13) 다른 창(요리 선택)과 같은 진한 갈색
    private static readonly Color Ink = new(0.29f, 0.184f, 0.106f, 1f);
    private const string Bamao = "Assets/Art/UI/Bamao/BamaoUIPack/Sprites/";

    private static TMP_FontAsset _bold, _semi;
    private static Sprite _round, _knob, _btn, _btnOn, _paper, _ribbon;

    [MenuItem("Marea/UI/도움말 창 만들기")]
    public static void Build()
    {
        _bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/A2Z-Bold SDF.asset");
        _semi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/A2Z-SemiBold SDF.asset");
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var cross = AssetDatabase.LoadAssetAtPath<Sprite>(Bamao + "Character sheet/Button_cross.png");
        // (+10/13) 아트 깔 — 늘 쓰는 버튼 · 양피지 · 제목 리본
        _btn = AssetDatabase.LoadAssetAtPath<Sprite>(Bamao + "Button/button short.png");
        _btnOn = AssetDatabase.LoadAssetAtPath<Sprite>(Bamao + "Button/button short green.png");
        _paper = AssetDatabase.LoadAssetAtPath<Sprite>(Bamao + "Button/Popup_paper_bg.png");
        _ribbon = AssetDatabase.LoadAssetAtPath<Sprite>(Bamao + "Button/Popup_paper_title.png");
        if (_btn == null || _btnOn == null || _paper == null || _ribbon == null)
            Debug.LogError("[도움말] Bamao 버튼 · 양피지 · 리본 그림 중 못 찾은 게 있다. 경로를 확인할 것.");

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
        Img(panel, null, new Color(0f, 0f, 0f, 0.45f));   // (+10/13) 뒤 화면을 살짝만 가린다 — 가벼운 느낌

        // (+10/13) 머리 — "도움말" 리본만. 요리 선택 창과 같은 그림 · 글자색.
        GameObject ribbon = Child(panel, "TitleRibbon"); Place(ribbon, TopLeft, new Vector2(80 + 260, -70), new Vector2(520, 96));
        Img(ribbon, _ribbon, Color.white);
        Text(Child(ribbon, "Label"), "도움말", 40, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        // 분류 이름은 탭이 보여 주니 안 띄운다 — HelpWindow가 쓰는 칸이라 꺼 둔 채 남긴다.
        TextMeshProUGUI categoryLabel = Text(Child(panel, "Category"), "전체", 1, Ink, TopLeft, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _bold);
        categoryLabel.gameObject.SetActive(false);

        // 닫기
        GameObject close = Child(panel, "CloseButton"); Place(close, TopRight, new Vector2(-70, -70), new Vector2(64, 56));
        Button closeButton = close.AddComponent<Button>();
        Img(close, cross, Color.white);

        // 탭 줄 — 오른쪽 양피지 위. 늘 쓰는 버튼(평소 베이지 · 고르면 초록)
        GameObject tabs = Child(panel, "Tabs"); Place(tabs, TopLeft, new Vector2(650 + 605, -165), new Vector2(1210, 60));
        var tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 10; tabLayout.childForceExpandWidth = true; tabLayout.childForceExpandHeight = true; tabLayout.childControlWidth = true; tabLayout.childControlHeight = true;
        GameObject tab = Child(tabs, "TabTemplate");
        Img(tab, _btn, Color.white);
        Button tabTemplate = tab.AddComponent<Button>();
        Text(Child(tab, "Label"), "탭", 24, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);

        // 검색 — 왼쪽 목록 위, 탭과 같은 줄
        GameObject searchGo = Child(panel, "Search"); Place(searchGo, TopLeft, new Vector2(95 + 250, -165), new Vector2(500, 60));
        Image searchBg = Img(searchGo, _btn, new Color(1f, 0.97f, 0.9f, 1f));
        GameObject area = Child(searchGo, "Text Area"); Stretch(area, new Vector2(28, 6), new Vector2(-28, -6));
        area.AddComponent<RectMask2D>();
        TextMeshProUGUI placeholder = Text(Child(area, "Placeholder"), "도움말 검색", 24, new Color(Ink.r, Ink.g, Ink.b, 0.5f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);
        TextMeshProUGUI searchText = Text(Child(area, "Text"), "", 24, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);
        var search = searchGo.AddComponent<TMP_InputField>();
        search.targetGraphic = searchBg; search.textViewport = area.GetComponent<RectTransform>();
        search.textComponent = searchText; search.placeholder = placeholder; search.fontAsset = _semi; search.pointSize = 24;

        // 목록 — 늘 쓰는 버튼
        GameObject scroll = Child(panel, "List"); Place(scroll, new Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 1)), new Vector2(95, -215), new Vector2(500, 0));
        RectTransform srt = scroll.GetComponent<RectTransform>(); srt.offsetMin = new Vector2(95, 40); srt.offsetMax = new Vector2(595, -215);
        var sr = scroll.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
        GameObject viewport = Child(scroll, "Viewport"); Stretch(viewport); viewport.AddComponent<RectMask2D>();
        GameObject content = Child(viewport, "Content");
        Place(content, new Anchor(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1)), Vector2.zero, new Vector2(0, 0));
        var vl = content.AddComponent<VerticalLayoutGroup>(); vl.spacing = 8; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 0, 0, 0);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport = viewport.GetComponent<RectTransform>(); sr.content = content.GetComponent<RectTransform>();
        GameObject item = Child(content, "ItemTemplate");
        Img(item, _btn, Color.white);
        item.AddComponent<LayoutElement>().preferredHeight = 76;
        Button itemTemplate = item.AddComponent<Button>();
        Text(Child(item, "Label"), "항목", 28, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _bold).margin = new Vector4(30, 0, 10, 0);
        GameObject empty = Child(panel, "EmptyHint");
        Text(empty, "찾는 도움말이 없습니다", 24, new Color(1, 1, 1, 0.8f), TopLeft, new Vector2(345, -280), new Vector2(490, 40), TextAlignmentOptions.Center, _semi);

        // 오른쪽 — 양피지
        GameObject sheet = Child(panel, "Sheet");
        RectTransform shrt = Place(sheet, new Anchor(new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f)), Vector2.zero, Vector2.zero);
        shrt.offsetMin = new Vector2(630, 20); shrt.offsetMax = new Vector2(-40, -200);
        Img(sheet, _paper, Color.white);
        GameObject frame = Child(sheet, "ImageFrame"); Place(frame, new Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1)), new Vector2(0, -70), new Vector2(720, 330));
        Img(frame, null, new Color(1f, 1f, 1f, 0f));   // (+10/13) 바탕 없음 — 그림 비율이 칸과 달라도 남는 자리가 양피지로 보이게
        GameObject pic = Child(frame, "Image"); Stretch(pic);
        Image image = Img(pic, null, Color.white); image.preserveAspect = true;
        GameObject body = Child(sheet, "Body");
        RectTransform bodyRt = Place(body, new Anchor(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1)), new Vector2(0, -425), new Vector2(1000, 0));
        var bodyLayout = body.AddComponent<VerticalLayoutGroup>(); bodyLayout.spacing = 16; bodyLayout.childControlHeight = true; bodyLayout.childControlWidth = true; bodyLayout.childForceExpandHeight = false;
        body.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI title = Text(Child(body, "Title"), "제목", 40, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        TextMeshProUGUI text = Text(Child(body, "Text"), "설명", 25, new Color(0.36f, 0.25f, 0.16f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, _semi);   // (+10/12) 여러 문단이라 왼쪽 정렬
        text.lineSpacing = 8;
        // (+10/12) 아버지의 한마디 — 설명 아래 금빛 기울임
        TextMeshProUGUI quote = Text(Child(body, "Quote"), "한마디", 26, new Color(0.58f, 0.38f, 0.14f), Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _semi);
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
        // (+10/13) 고르면 초록 버튼, 아니면 베이지 — 바탕색은 그림 그대로(흰색), 글자는 늘 진한 갈색
        so.FindProperty("selectedSprite").objectReferenceValue = _btnOn;
        so.FindProperty("normalSprite").objectReferenceValue = _btn;
        so.FindProperty("tabOn").colorValue = Ink; so.FindProperty("tabOff").colorValue = Ink;
        so.FindProperty("itemOn").colorValue = Color.white; so.FindProperty("itemOff").colorValue = Color.white;
        so.FindProperty("itemTextOn").colorValue = Ink; so.FindProperty("itemTextOff").colorValue = Ink;
        so.FindProperty("dotOn").colorValue = Ink; so.FindProperty("dotOff").colorValue = new Color(Ink.r, Ink.g, Ink.b, 0.3f);
        so.FindProperty("bodyYWithImage").floatValue = -425f; so.FindProperty("bodyYWithoutImage").floatValue = -80f;
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
        Img(go, _btn, Color.white);   // (+10/13) 늘 쓰는 버튼
        Text(Child(go, "Label"), label, 26, Ink, Stretched, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, _bold);
        return go.AddComponent<Button>();
    }
}
