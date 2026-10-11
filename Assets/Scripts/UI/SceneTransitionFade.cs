using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>씬 활성화 중에도 유지되는 전환용 암전막.</summary>
public sealed class SceneTransitionFade : MonoBehaviour
{
    private CanvasGroup group;
    private string revealScene;

    public static SceneTransitionFade Create()
    {
        var root = new GameObject("SceneTransitionFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        var curtain = new GameObject("Curtain", typeof(RectTransform), typeof(Image));
        curtain.transform.SetParent(root.transform, false);
        var rect = curtain.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        curtain.GetComponent<Image>().color = new Color(0.015f, 0.035f, 0.05f, 1f);
        var fade = root.AddComponent<SceneTransitionFade>();
        fade.group = root.GetComponent<CanvasGroup>();
        fade.group.alpha = 0f;
        return fade;
    }

    public IEnumerator FadeTo(float alpha, float seconds)
    {
        float start = group.alpha;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // 부하가 발생한 한 프레임 때문에 페이드가 한 번에 뛰지 않게 한다.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            group.alpha = Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }
        group.alpha = alpha;
    }

    public void RevealAfterSceneLoad(string sceneName)
    {
        revealScene = sceneName;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != revealScene) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        // Main의 Start와 첫 렌더링이 끝날 때까지 암전막을 유지한다.
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return null;
        yield return FadeTo(0f, 0.6f);
        Destroy(gameObject);
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;
}
