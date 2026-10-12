#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TitleMenuPresentation
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";

    public static void Configure(Scene titleScene)
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("타이틀 UI는 편집 모드에서 적용하세요.");

        Scene previousActive = SceneManager.GetActiveScene();
        Scene mainScene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedMain = !mainScene.IsValid() || !mainScene.isLoaded;
        if (openedMain) mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(titleScene);
            Transform titleUi = titleScene.GetRootGameObjects().Single(g => g.name == "TitleUI").transform;
            Transform mainDialogs = mainScene.GetRootGameObjects().Single(g => g.name == "MainMenuDialogs").transform;
            Transform referenceSettings = mainDialogs.Find("SettingsDialog");
            Transform oldSettings = titleUi.Find("SettingsDialog")
                ?? titleUi.Find("SidePanel/SettingsDialog") ?? titleUi.Find("SidePanel/SettingsPanel");
            var settings = UnityEngine.Object.Instantiate(referenceSettings.gameObject, titleUi, false);
            settings.name = "SettingsDialog";
            settings.SetActive(false);
            Transform panel = settings.transform.Find("SettingsPanel");
            var settingsRect = (RectTransform)panel;
            settingsRect.anchorMin = settingsRect.anchorMax = new Vector2(0f, 0.5f);
            settingsRect.pivot = new Vector2(0f, 0.5f);
            settingsRect.anchoredPosition = new Vector2(64f, -10f);

            var controller = titleUi.GetComponent<TitleMenuController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("mainMenuPanel").objectReferenceValue = titleUi.Find("SidePanel/MainMenuPanel").gameObject;
            serialized.FindProperty("settingsPanel").objectReferenceValue = settings;
            serialized.FindProperty("volumeSlider").objectReferenceValue = panel.Find("VolumeSlider").GetComponent<UnityEngine.UI.Slider>();
            serialized.FindProperty("volumeValueText").objectReferenceValue = panel.Find("VolumeValue").GetComponent<TMP_Text>();
            serialized.FindProperty("resolutionDropdown").objectReferenceValue = panel.Find("ResolutionDropdown").GetComponent<TMP_Dropdown>();
            serialized.FindProperty("fullscreenToggle").objectReferenceValue = panel.Find("FullscreenToggle").GetComponent<UnityEngine.UI.Toggle>();
            serialized.FindProperty("closeSettingsButton").objectReferenceValue = panel.Find("CloseSettingsButton").GetComponent<UnityEngine.UI.Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (oldSettings != null) UnityEngine.Object.DestroyImmediate(oldSettings.gameObject);

            var referenceButton = panel.Find("CloseSettingsButton").GetComponent<UnityEngine.UI.Button>();
            var referenceImage = referenceButton.GetComponent<UnityEngine.UI.Image>();
            var referenceLabel = referenceButton.GetComponentInChildren<TMP_Text>(true);
            Transform mainMenu = titleUi.Find("SidePanel/MainMenuPanel");
            foreach (string name in new[] { "StartButton", "SettingsButton", "QuitButton" })
            {
                var button = mainMenu.Find(name).GetComponent<UnityEngine.UI.Button>();
                var image = button.GetComponent<UnityEngine.UI.Image>();
                image.sprite = referenceImage.sprite;
                image.color = referenceImage.color;
                image.type = referenceImage.type;
                image.pixelsPerUnitMultiplier = referenceImage.pixelsPerUnitMultiplier;
                image.raycastTarget = true;
                button.targetGraphic = image;
                button.transition = referenceButton.transition;
                button.colors = referenceButton.colors;
                var label = button.GetComponentInChildren<TMP_Text>(true);
                label.font = referenceLabel.font;
                label.fontSharedMaterial = referenceLabel.fontSharedMaterial;
                label.fontSize = referenceLabel.fontSize;
                label.fontStyle = referenceLabel.fontStyle;
                label.fontWeight = referenceLabel.fontWeight;
                label.color = referenceLabel.color;
                label.alignment = referenceLabel.alignment;
                label.enableAutoSizing = false;
                label.raycastTarget = false;
            }

            var mainClick = Components<Marea.Hud.UiClickSound>(mainScene).Single();
            var titleClick = Components<Marea.Hud.UiClickSound>(titleScene).SingleOrDefault();
            if (titleClick == null)
            {
                var go = new GameObject("TitleUiClickSound", typeof(AudioSource), typeof(Marea.Hud.UiClickSound));
                SceneManager.MoveGameObjectToScene(go, titleScene);
                titleClick = go.GetComponent<Marea.Hud.UiClickSound>();
            }
            EditorUtility.CopySerialized(mainClick, titleClick);
            EditorUtility.CopySerialized(mainClick.GetComponent<AudioSource>(), titleClick.GetComponent<AudioSource>());

            var mainBgm = Components<Marea.Core.BgmPlayer>(mainScene).Single();
            var titleBgm = Components<Marea.Core.BgmPlayer>(titleScene).SingleOrDefault();
            if (titleBgm == null)
            {
                var go = new GameObject("TitleBgm", typeof(Marea.Core.BgmPlayer));
                SceneManager.MoveGameObjectToScene(go, titleScene);
                titleBgm = go.GetComponent<Marea.Core.BgmPlayer>();
            }
            EditorUtility.CopySerialized(mainBgm, titleBgm);
            ApplySidebarSettings(titleScene);
            // The loading overlay must remain above settings when starting the game.
            titleUi.Find("LoadingPanel")?.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(titleScene);
        }
        finally
        {
            if (openedMain) EditorSceneManager.CloseScene(mainScene, true);
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
        }
    }

    public static void ApplySidebarSettings(Scene scene)
    {
        Transform ui = scene.GetRootGameObjects().Single(g => g.name == "TitleUI").transform;
        Transform side = ui.Find("SidePanel");
        Transform settings = ui.Find("SettingsDialog") ?? side.Find("SettingsDialog");
        settings.SetParent(side, false);
        var settingsRect = (RectTransform)settings;
        settingsRect.anchorMin = Vector2.zero;
        settingsRect.anchorMax = Vector2.one;
        settingsRect.offsetMin = settingsRect.offsetMax = Vector2.zero;
        var overlay = settings.GetComponent<UnityEngine.UI.Image>();
        overlay.color = Color.clear;
        overlay.raycastTarget = false;

        Transform panel = settings.Find("SettingsPanel");
        foreach (string name in new[] { "Deco_Back", "TitleRibbon" })
        {
            Transform decoration = panel.Find(name);
            if (decoration != null) UnityEngine.Object.DestroyImmediate(decoration.gameObject);
        }
        var background = panel.GetComponent<UnityEngine.UI.Image>();
        background.color = Color.clear;
        background.raycastTarget = false;
        SetRect(panel, new Vector2(0f, -10f), new Vector2(480f, 760f));
        SetRect(panel.Find("VolumeLabel"), new Vector2(-85f, 180f), new Vector2(230f, 45f));
        SetRect(panel.Find("VolumeValue"), new Vector2(155f, 180f), new Vector2(90f, 45f));
        SetRect(panel.Find("VolumeSlider"), new Vector2(0f, 125f), new Vector2(380f, 44f));
        SetRect(panel.Find("ResolutionLabel"), new Vector2(-85f, 32f), new Vector2(230f, 45f));
        SetRect(panel.Find("ResolutionDropdown"), new Vector2(0f, -25f), new Vector2(380f, 80f));
        SetRect(panel.Find("FullscreenToggle"), new Vector2(0f, -130f), new Vector2(380f, 58f));
        SetRect(panel.Find("CloseSettingsButton"), new Vector2(0f, -258f), new Vector2(380f, 76f));
        Color lightText = new Color(1f, 0.971f, 0.871f, 1f);
        foreach (string path in new[] { "VolumeLabel", "VolumeValue", "ResolutionLabel", "FullscreenToggle/Label" })
            panel.Find(path).GetComponent<TMP_Text>().color = lightText;
        SettingsAudioControls.Configure(panel, ui.GetComponent<TitleMenuController>(), true);
        foreach (string path in new[] { "SfxVolumeLabel", "SfxVolumeValue" })
            panel.Find(path).GetComponent<TMP_Text>().color = lightText;
        AddSettingsHeading(scene);

        var controller = new SerializedObject(ui.GetComponent<TitleMenuController>());
        controller.FindProperty("mainMenuPanel").objectReferenceValue = side.Find("MainMenuPanel").gameObject;
        controller.ApplyModifiedPropertiesWithoutUndo();
        settings.gameObject.SetActive(false);
        side.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    public static void AddSettingsHeading(Scene scene)
    {
        Transform ui = scene.GetRootGameObjects().Single(g => g.name == "TitleUI").transform;
        Transform panel = ui.Find("SidePanel/SettingsDialog/SettingsPanel");
        Transform heading = panel.Find("SettingsTitle");
        if (heading == null)
        {
            var go = new GameObject("SettingsTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(panel, false);
            heading = go.transform;
        }
        var reference = panel.Find("VolumeLabel").GetComponent<TMP_Text>();
        var text = heading.GetComponent<TMP_Text>();
        text.text = "설정";
        text.font = reference.font;
        text.fontSharedMaterial = reference.fontSharedMaterial;
        text.fontSize = 54f;
        text.enableAutoSizing = false;
        text.color = reference.color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        SetRect(heading, new Vector2(0f, 288f), new Vector2(380f, 80f));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void SetRect(Transform target, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)target;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static T[] Components<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
}
#endif
