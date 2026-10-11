#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleSceneBuilder
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string TitleScenePath = "Assets/Scenes/Title.unity";
    private const string FontPath = "Assets/Fonts/A2Z-Bold SDF.asset";

    private static readonly Color PanelColor = new(0.035f, 0.09f, 0.12f, 0.92f);
    private static readonly Color ButtonColor = new(0.07f, 0.21f, 0.26f, 0.98f);
    private static readonly Color ButtonHoverColor = new(0.11f, 0.34f, 0.40f, 1f);
    private static readonly Color GoldColor = new(0.96f, 0.69f, 0.25f, 1f);
    private static readonly Color CreamColor = new(1f, 0.96f, 0.84f, 1f);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Marea/Rebuild Title Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("플레이 모드에서는 타이틀 씬을 만들 수 없습니다.");

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new System.IO.FileNotFoundException("타이틀 UI용 TMP 폰트를 찾을 수 없습니다.", FontPath);

        GameObject previewCamera = GameObject.Find("__TitlePreviewCamera");
        if (previewCamera != null)
            Object.DestroyImmediate(previewCamera);

        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath))
            throw new System.IO.FileNotFoundException("Main 씬을 찾을 수 없습니다.", MainScenePath);

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null)
            AssetDatabase.DeleteAsset(TitleScenePath);

        if (!AssetDatabase.CopyAsset(MainScenePath, TitleScenePath))
            throw new System.InvalidOperationException("Main 씬을 Title 씬으로 복사하지 못했습니다.");

        AssetDatabase.ImportAsset(TitleScenePath, ImportAssetOptions.ForceUpdate);
        Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

        RemoveGameplayRoots(scene);
        ConfigureEnvironment(scene);
        Camera camera = ConfigureCamera(scene);
        CreateEventSystem(scene);
        CreateTitleUi(scene);
        ConfigureBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = camera.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Title 씬 생성 완료: Main 배경 복사, 게임플레이 구성 제거, 메뉴/설정 UI 연결");
    }

    private static void RemoveGameplayRoots(Scene scene)
    {
        var keepRoots = new HashSet<string>
        {
            "Main Camera", "Global Volume", "Ground", "Object", "Decoration", "FarmDeck",
            "ExplorerDock", "Plant2 (4)", "Plant2 (3)", "Directional Light"
        };

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (!keepRoots.Contains(root.name))
                Object.DestroyImmediate(root);
        }

        GameObject objectRoot = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Object");
        if (objectRoot != null)
        {
            foreach (Transform child in objectRoot.transform.Cast<Transform>().ToArray())
            {
                if (child.name != "Restaurant")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        UnpackScenePrefabs(scene);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            RemoveInactiveBranches(root.transform, true);
            StripRuntimeComponents(root);
        }

        // 일부 편집기 콜백이 구성요소 제거 직후 필수 컴포넌트를 다시 만들 수 있어
        // 한 번 더 정리해 Title 씬에 시각 요소만 남긴다.
        foreach (GameObject root in scene.GetRootGameObjects())
            StripRuntimeComponents(root);
    }

    private static void UnpackScenePrefabs(Scene scene)
    {
        GameObject[] prefabRoots = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .Where(PrefabUtility.IsOutermostPrefabInstanceRoot)
            .Distinct()
            .ToArray();

        foreach (GameObject prefabRoot in prefabRoots)
            PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static void RemoveInactiveBranches(Transform parent, bool isRoot = false)
    {
        foreach (Transform child in parent.Cast<Transform>().ToArray())
        {
            if (!child.gameObject.activeSelf)
            {
                Object.DestroyImmediate(child.gameObject);
                continue;
            }

            RemoveInactiveBranches(child);
        }

        if (!isRoot && !parent.gameObject.activeSelf)
            Object.DestroyImmediate(parent.gameObject);
    }

    private static void StripRuntimeComponents(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (CharacterController controller in root.GetComponentsInChildren<CharacterController>(true))
            Object.DestroyImmediate(controller);
        foreach (UnityEngine.AI.NavMeshAgent agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            Object.DestroyImmediate(agent);
        foreach (AudioSource audioSource in root.GetComponentsInChildren<AudioSource>(true))
            Object.DestroyImmediate(audioSource);
        foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            Object.DestroyImmediate(canvas.gameObject);
        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(animator);

        foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystemRenderer particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
                Object.DestroyImmediate(particleRenderer);
            Object.DestroyImmediate(particle);
        }

        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || IsRenderPipelineComponent(behaviour))
                continue;

            Object.DestroyImmediate(behaviour);
        }
    }

    private static bool IsRenderPipelineComponent(MonoBehaviour behaviour)
    {
        return behaviour is Volume ||
               behaviour is UniversalAdditionalCameraData ||
               behaviour is UniversalAdditionalLightData;
    }

    private static void ConfigureEnvironment(Scene scene)
    {
        RenderSettings.fog = true;

        foreach (Renderer renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true)))
        {
            renderer.allowOcclusionWhenDynamic = true;
            if (renderer.bounds.size.sqrMagnitude < 1.5f)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static Camera ConfigureCamera(Scene scene)
    {
        GameObject cameraObject = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "Main Camera");
        if (cameraObject == null)
            cameraObject = new GameObject("Main Camera");

        foreach (Transform child in cameraObject.transform.Cast<Transform>().ToArray())
            Object.DestroyImmediate(child.gameObject);

        Camera camera = cameraObject.GetComponent<Camera>() ?? cameraObject.AddComponent<Camera>();
        if (cameraObject.GetComponent<AudioListener>() == null)
            cameraObject.AddComponent<AudioListener>();
        if (cameraObject.GetComponent<UniversalAdditionalCameraData>() == null)
            cameraObject.AddComponent<UniversalAdditionalCameraData>();

        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(50f, 28f, 8f);
        cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(75f, 8f, 42f) - cameraObject.transform.position, Vector3.up);
        camera.fieldOfView = 44f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 650f;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.enabled = true;
        return camera;
    }

    private static void CreateEventSystem(Scene scene)
    {
        foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (eventSystem.gameObject.scene == scene)
                Object.DestroyImmediate(eventSystem.gameObject);
        }

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
    }

    private static void CreateTitleUi(Scene scene)
    {
        GameObject canvasObject = new("TitleUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform shade = CreateImage("ScreenShade", canvasObject.transform, new Color(0.01f, 0.025f, 0.035f, 0.18f));
        Stretch(shade);
        shade.GetComponent<Image>().raycastTarget = false;

        RectTransform sidePanel = CreateImage("SidePanel", canvasObject.transform, PanelColor);
        sidePanel.anchorMin = Vector2.zero;
        sidePanel.anchorMax = new Vector2(0.32f, 1f);
        sidePanel.offsetMin = Vector2.zero;
        sidePanel.offsetMax = Vector2.zero;

        RectTransform mainPanel = CreateRect("MainMenuPanel", sidePanel);
        mainPanel.anchorMin = new Vector2(0.5f, 0.5f);
        mainPanel.anchorMax = new Vector2(0.5f, 0.5f);
        mainPanel.sizeDelta = new Vector2(480f, 760f);
        mainPanel.anchoredPosition = new Vector2(0f, -10f);

        TMP_Text title = CreateText("GameTitle", mainPanel, "MAREA", 78f, CreamColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0f, 225f), new Vector2(470f, 100f));
        title.characterSpacing = 7f;

        TMP_Text subtitle = CreateText("Subtitle", mainPanel, "바다 위의 작은 식당", 25f, new Color(0.86f, 0.9f, 0.88f, 1f), TextAlignmentOptions.Center);
        SetRect(subtitle.rectTransform, new Vector2(0f, 158f), new Vector2(440f, 45f));

        RectTransform divider = CreateImage("Divider", mainPanel, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.75f));
        SetRect(divider, new Vector2(0f, 116f), new Vector2(90f, 3f));

        Button startButton = CreateButton("StartButton", mainPanel, "게임 시작", new Vector2(0f, 30f));
        Button settingsButton = CreateButton("SettingsButton", mainPanel, "설정", new Vector2(0f, -72f));
        Button quitButton = CreateButton("QuitButton", mainPanel, "나가기", new Vector2(0f, -174f));

        RectTransform settingsPanel = CreateRect("SettingsPanel", sidePanel);
        settingsPanel.anchorMin = new Vector2(0.5f, 0.5f);
        settingsPanel.anchorMax = new Vector2(0.5f, 0.5f);
        settingsPanel.sizeDelta = new Vector2(480f, 760f);
        settingsPanel.anchoredPosition = new Vector2(0f, -10f);

        TMP_Text settingsTitle = CreateText("SettingsTitle", settingsPanel, "설정", 54f, CreamColor, TextAlignmentOptions.Center);
        SetRect(settingsTitle.rectTransform, new Vector2(0f, 288f), new Vector2(440f, 80f));

        TMP_Text volumeLabel = CreateText("VolumeLabel", settingsPanel, "전체 음량", 25f, CreamColor, TextAlignmentOptions.Left);
        SetRect(volumeLabel.rectTransform, new Vector2(-90f, 180f), new Vector2(260f, 45f));
        TMP_Text volumeValue = CreateText("VolumeValue", settingsPanel, "100%", 23f, GoldColor, TextAlignmentOptions.Right);
        SetRect(volumeValue.rectTransform, new Vector2(160f, 180f), new Vector2(100f, 45f));
        Slider volumeSlider = CreateSlider("VolumeSlider", settingsPanel, new Vector2(0f, 125f));

        TMP_Text resolutionLabel = CreateText("ResolutionLabel", settingsPanel, "해상도", 25f, CreamColor, TextAlignmentOptions.Left);
        SetRect(resolutionLabel.rectTransform, new Vector2(-90f, 36f), new Vector2(260f, 45f));
        TMP_Dropdown resolutionDropdown = CreateDropdown("ResolutionDropdown", settingsPanel, new Vector2(0f, -22f));

        Toggle fullscreenToggle = CreateToggle("FullscreenToggle", settingsPanel, "전체 화면", new Vector2(0f, -126f));
        Button closeButton = CreateButton("CloseSettingsButton", settingsPanel, "돌아가기", new Vector2(0f, -258f));

        TitleMenuController controller = canvasObject.AddComponent<TitleMenuController>();
        SerializedObject serialized = new(controller);
        serialized.FindProperty("mainMenuPanel").objectReferenceValue = mainPanel.gameObject;
        serialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel.gameObject;
        serialized.FindProperty("startButton").objectReferenceValue = startButton;
        serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
        serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
        serialized.FindProperty("volumeSlider").objectReferenceValue = volumeSlider;
        serialized.FindProperty("volumeValueText").objectReferenceValue = volumeValue;
        serialized.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
        serialized.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
        serialized.FindProperty("closeSettingsButton").objectReferenceValue = closeButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        settingsPanel.gameObject.SetActive(false);
        AddLoadingScreen(scene);
        TitleLoadingScreenBuilder.Configure(scene);
    }

    public static void AddLoadingScreen(Scene scene)
    {
        GameObject canvasObject = scene.GetRootGameObjects().First(root => root.name == "TitleUI");
        if (canvasObject.transform.Find("LoadingPanel") != null) return;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        RectTransform loading = CreateImage("LoadingPanel", canvasObject.transform, new Color(0.025f, 0.065f, 0.085f, 1f));
        Stretch(loading);
        loading.SetAsLastSibling();
        TMP_Text title = CreateText("LoadingTitle", loading, "MAREA", 66f, CreamColor, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0f, 100f), new Vector2(700f, 100f));
        title.characterSpacing = 7f;
        TMP_Text message = CreateText("LoadingMessage", loading, "바다 위의 식당을 준비하고 있어요", 28f, CreamColor, TextAlignmentOptions.Center);
        SetRect(message.rectTransform, new Vector2(0f, 5f), new Vector2(800f, 60f));

        RectTransform track = CreateImage("ProgressTrack", loading, new Color(0.13f, 0.23f, 0.27f, 1f));
        SetRect(track, new Vector2(0f, -70f), new Vector2(620f, 12f));
        track.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        RectTransform fill = CreateImage("ProgressFill", track, GoldColor);
        Stretch(fill);
        UnityEngine.UI.Image progressImage = fill.GetComponent<UnityEngine.UI.Image>();
        progressImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        progressImage.type = UnityEngine.UI.Image.Type.Filled;
        progressImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        progressImage.fillOrigin = 0;
        progressImage.fillAmount = 0f;
        progressImage.raycastTarget = false;
        TMP_Text progress = CreateText("ProgressText", loading, "0%", 23f, GoldColor, TextAlignmentOptions.Center);
        SetRect(progress.rectTransform, new Vector2(0f, -115f), new Vector2(240f, 50f));

        SerializedObject serialized = new(canvasObject.GetComponent<TitleMenuController>());
        serialized.FindProperty("loadingPanel").objectReferenceValue = loading.gameObject;
        serialized.FindProperty("loadingProgressFill").objectReferenceValue = progressImage;
        serialized.FindProperty("loadingProgressText").objectReferenceValue = progress;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        loading.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Vector2 position)
    {
        RectTransform rect = CreateImage(name, parent, Color.white);
        SetRect(rect, position, new Vector2(380f, 76f));

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.selectedColor = ButtonHoverColor;
        colors.pressedColor = new Color(0.8f, 0.75f, 0.6f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        TMP_Text text = CreateText("Label", rect, label, 29f, CreamColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 12f, 12f, 8f, 8f);
        return button;
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 position)
    {
        RectTransform root = CreateRect(name, parent);
        SetRect(root, position, new Vector2(380f, 42f));
        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        RectTransform background = CreateImage("Background", root, new Color(0.16f, 0.25f, 0.28f, 1f));
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 10f);

        RectTransform fillArea = CreateRect("Fill Area", root);
        Stretch(fillArea, 8f, 8f, 13f, 13f);
        RectTransform fill = CreateImage("Fill", fillArea, GoldColor);
        Stretch(fill);

        RectTransform handleArea = CreateRect("Handle Slide Area", root);
        Stretch(handleArea, 10f, 10f, 0f, 0f);
        RectTransform handle = CreateImage("Handle", handleArea, CreamColor);
        handle.sizeDelta = new Vector2(26f, 26f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static Toggle CreateToggle(string name, Transform parent, string label, Vector2 position)
    {
        RectTransform root = CreateRect(name, parent);
        SetRect(root, position, new Vector2(380f, 58f));
        Toggle toggle = root.gameObject.AddComponent<Toggle>();

        RectTransform box = CreateImage("Background", root, ButtonColor);
        box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
        box.pivot = new Vector2(0f, 0.5f);
        box.anchoredPosition = new Vector2(4f, 0f);
        box.sizeDelta = new Vector2(38f, 38f);

        RectTransform check = CreateImage("Checkmark", box, GoldColor);
        check.anchorMin = check.anchorMax = new Vector2(0.5f, 0.5f);
        check.sizeDelta = new Vector2(22f, 22f);
        TMP_Text text = CreateText("Label", root, label, 25f, CreamColor, TextAlignmentOptions.Left);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(60f, 0f);
        text.rectTransform.offsetMax = Vector2.zero;

        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = Screen.fullScreen;
        return toggle;
    }

    private static TMP_Dropdown CreateDropdown(string name, Transform parent, Vector2 position)
    {
        RectTransform root = CreateImage(name, parent, ButtonColor);
        SetRect(root, position, new Vector2(380f, 66f));
        TMP_Dropdown dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = root.GetComponent<Image>();

        TMP_Text label = CreateText("Label", root, "1920 × 1080", 23f, CreamColor, TextAlignmentOptions.Left);
        Stretch(label.rectTransform, 22f, 58f, 8f, 8f);
        TMP_Text arrow = CreateText("Arrow", root, "▼", 22f, GoldColor, TextAlignmentOptions.Center);
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.anchoredPosition = new Vector2(-18f, 0f);
        arrow.rectTransform.sizeDelta = new Vector2(34f, 34f);

        RectTransform template = CreateImage("Template", root, new Color(0.035f, 0.10f, 0.13f, 0.99f));
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -6f);
        template.sizeDelta = new Vector2(0f, 230f);

        ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = CreateImage("Viewport", template, Color.white);
        Stretch(viewport, 5f, 5f, 5f, 5f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 46f);
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform item = CreateRect("Item", content);
        item.sizeDelta = new Vector2(0f, 46f);
        LayoutElement itemLayout = item.gameObject.AddComponent<LayoutElement>();
        itemLayout.preferredHeight = 46f;
        Toggle itemToggle = item.gameObject.AddComponent<Toggle>();
        RectTransform itemBackground = CreateImage("Item Background", item, new Color(0.08f, 0.22f, 0.27f, 1f));
        Stretch(itemBackground);
        RectTransform itemCheck = CreateImage("Item Checkmark", item, GoldColor);
        itemCheck.anchorMin = itemCheck.anchorMax = new Vector2(0f, 0.5f);
        itemCheck.pivot = new Vector2(0f, 0.5f);
        itemCheck.anchoredPosition = new Vector2(18f, 0f);
        itemCheck.sizeDelta = new Vector2(16f, 16f);
        TMP_Text itemLabel = CreateText("Item Label", item, "1920 × 1080", 21f, CreamColor, TextAlignmentOptions.Left);
        Stretch(itemLabel.rectTransform, 52f, 12f, 4f, 4f);
        itemToggle.targetGraphic = itemBackground.GetComponent<Image>();
        itemToggle.graphic = itemCheck.GetComponent<Image>();

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        dropdown.template = template;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void ConfigureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new()
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(MainScenePath, true)
        };

        scenes.AddRange(EditorBuildSettings.scenes.Where(scene =>
            scene.path != TitleScenePath && scene.path != MainScenePath));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
