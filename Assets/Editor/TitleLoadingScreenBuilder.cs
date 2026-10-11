#if UNITY_EDITOR
using System.Linq;
using Marea.Field;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleLoadingScreenBuilder
{
    public static void Configure(Scene title)
    {
        var ui = title.GetRootGameObjects().First(root => root.name == "TitleUI");
        var panel = ui.transform.Find("LoadingPanel");
        panel.GetComponent<Image>().color = new Color(0.015f, 0.04f, 0.06f, 0.12f);
        SetBottom(panel.Find("LoadingTitle"), 260f, new Vector2(700f, 90f));
        panel.Find("LoadingTitle").GetComponent<TMP_Text>().fontSize = 48f;
        SetBottom(panel.Find("LoadingMessage"), 185f, new Vector2(800f, 50f));
        SetBottom(panel.Find("ProgressTrack"), 125f, new Vector2(900f, 16f));
        SetBottom(panel.Find("ProgressText"), 80f, new Vector2(240f, 45f));
        AlignLoadingProgress(title);

        var clone = title.GetRootGameObjects().FirstOrDefault(root => root.name == "LoadingWhale");
        if (clone == null)
        {
            var main = SceneManager.GetSceneByPath("Assets/Scenes/Main.unity");
            bool opened = !main.isLoaded;
            if (opened) main = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Additive);
            try
            {
                var source = main.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WhaleSwimmer>(true)).First();
                clone = Object.Instantiate(source.gameObject);
                clone.name = "LoadingWhale";
                clone.transform.SetParent(null);
                SceneManager.MoveGameObjectToScene(clone, title);
                if (PrefabUtility.IsPartOfPrefabInstance(clone))
                    PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var debug in clone.GetComponentsInChildren<WhaleDebugKey>(true)) Object.DestroyImmediate(debug);
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(main, true);
                SceneManager.SetActiveScene(title);
            }
        }

        var swimmer = clone.GetComponent<WhaleSwimmer>();
        var whaleSettings = new SerializedObject(swimmer);
        whaleSettings.FindProperty("lap").boolValue = true;
        var points = whaleSettings.FindProperty("lapPoints");
        points.arraySize = 2;
        points.GetArrayElementAtIndex(0).vector3Value = new Vector3(30f, 0f, 4f);
        points.GetArrayElementAtIndex(1).vector3Value = new Vector3(62f, 0f, -2f);
        whaleSettings.FindProperty("surfaceDepth").floatValue = 3.2f;
        whaleSettings.FindProperty("hiddenDepth").floatValue = 3.2f;
        whaleSettings.FindProperty("riseTime").floatValue = 0.1f;
        whaleSettings.FindProperty("firstDelay").floatValue = 10000f;
        whaleSettings.FindProperty("jumpInterval").vector2Value = new Vector2(1000f, 1000f);
        whaleSettings.FindProperty("callClip").objectReferenceValue = null;
        whaleSettings.ApplyModifiedPropertiesWithoutUndo();
        clone.SetActive(false);

        var view = ui.GetComponent<LoadingWhaleView>() ?? ui.AddComponent<LoadingWhaleView>();
        var viewSettings = new SerializedObject(view);
        viewSettings.FindProperty("whale").objectReferenceValue = swimmer;
        viewSettings.FindProperty("viewCamera").objectReferenceValue = title.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).First();
        viewSettings.FindProperty("menuBackdrop").objectReferenceValue = ui.transform.Find("SidePanel").gameObject;
        viewSettings.FindProperty("sideDistance").floatValue = 20f;
        viewSettings.FindProperty("height").floatValue = 8f;
        viewSettings.FindProperty("lookUp").floatValue = 2f;
        viewSettings.ApplyModifiedPropertiesWithoutUndo();
        var controller = new SerializedObject(ui.GetComponent<TitleMenuController>());
        controller.FindProperty("loadingWhaleView").objectReferenceValue = view;
        controller.FindProperty("extraLoadingSeconds").floatValue = 2.5f;
        controller.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(title);
    }

    public static void AlignLoadingProgress(Scene title)
    {
        var ui = title.GetRootGameObjects().First(root => root.name == "TitleUI");
        var track = ui.transform.Find("LoadingPanel/ProgressTrack");
        var background = track.GetComponent<Image>();
        background.sprite = null;
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.raycastTarget = false;
        var fill = track.Find("ProgressFill").GetComponent<Image>();
        fill.sprite = null;
        fill.type = Image.Type.Simple;
        fill.preserveAspect = false;
        fill.raycastTarget = false;
        fill.fillAmount = 0f;
        var rect = fill.rectTransform;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(0f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        EditorSceneManager.MarkSceneDirty(title);
    }

    private static void SetBottom(Transform transform, float y, Vector2 size)
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = size;
    }
}
#endif
