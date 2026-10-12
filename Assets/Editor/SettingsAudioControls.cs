#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsAudioControls
{
    public static void Configure(Transform panel, TitleMenuController controller, bool sidebar)
    {
        CloneIfMissing(panel, "VolumeLabel", "SfxVolumeLabel");
        CloneIfMissing(panel, "VolumeValue", "SfxVolumeValue");
        CloneIfMissing(panel, "VolumeSlider", "SfxVolumeSlider");
        panel.Find("VolumeLabel").GetComponent<TMP_Text>().text = "배경음";
        panel.Find("SfxVolumeLabel").GetComponent<TMP_Text>().text = "효과음";
        float width = sidebar ? 380f : 460f;
        float labelX = sidebar ? -85f : -110f;
        float valueX = sidebar ? 155f : 190f;
        float top = sidebar ? 190f : 200f;
        Layout(panel, "VolumeLabel", labelX, top, sidebar ? 230f : 260f, 45f);
        Layout(panel, "VolumeValue", valueX, top, sidebar ? 90f : 100f, 45f);
        Layout(panel, "VolumeSlider", 0f, top - 50f, width, 44f);
        Layout(panel, "SfxVolumeLabel", labelX, top - 120f, sidebar ? 230f : 260f, 45f);
        Layout(panel, "SfxVolumeValue", valueX, top - 120f, sidebar ? 90f : 100f, 45f);
        Layout(panel, "SfxVolumeSlider", 0f, top - 170f, width, 44f);
        Layout(panel, "ResolutionLabel", labelX, -55f, sidebar ? 230f : 260f, 45f);
        Layout(panel, "ResolutionDropdown", 0f, -110f, width, 80f);
        Layout(panel, "FullscreenToggle", 0f, -200f, width, 58f);
        Layout(panel, "CloseSettingsButton", 0f, sidebar ? -300f : -275f, sidebar ? 380f : 320f, sidebar ? 76f : 80f);
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("sfxVolumeSlider").objectReferenceValue = panel.Find("SfxVolumeSlider").GetComponent<Slider>();
        serialized.FindProperty("sfxVolumeValueText").objectReferenceValue = panel.Find("SfxVolumeValue").GetComponent<TMP_Text>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var entry in new[] { ("VolumeSlider", "VolumeValue", Marea.Core.GameAudioSettings.MusicVolume), ("SfxVolumeSlider", "SfxVolumeValue", Marea.Core.GameAudioSettings.EffectsVolume) })
        {
            panel.Find(entry.Item1).GetComponent<Slider>().SetValueWithoutNotify(entry.Item3);
            panel.Find(entry.Item2).GetComponent<TMP_Text>().text = $"{Mathf.RoundToInt(entry.Item3 * 100f)}%";
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }

    private static void CloneIfMissing(Transform panel, string source, string name)
    {
        if (panel.Find(name) != null) return;
        var clone = Object.Instantiate(panel.Find(source).gameObject, panel, false);
        clone.name = name;
    }

    private static void Layout(Transform panel, string name, float x, float y, float width, float height)
    {
        var rect = (RectTransform)panel.Find(name);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}
#endif
