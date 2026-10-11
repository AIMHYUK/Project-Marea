using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TitleMenuController : MonoBehaviour
{
    private const string VolumeKey = "Title.MasterVolume";
    private const string ResolutionKey = "Title.ResolutionIndex";
    private const string FullscreenKey = "Title.Fullscreen";

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Main Menu")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Loading")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private UnityEngine.UI.Image loadingProgressFill;
    [SerializeField] private TMP_Text loadingProgressText;
    [SerializeField] private LoadingWhaleView loadingWhaleView;
    [SerializeField, Range(1f, 5f)] private float extraLoadingSeconds = 2.5f;
    private bool isLoading;

    [Header("Settings")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Button closeSettingsButton;

    private readonly List<Resolution> availableResolutions = new();
    private bool isInitializing;

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(SetResolution);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        InitializeSettings();
        CloseSettings();
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
        if (closeSettingsButton != null) closeSettingsButton.onClick.RemoveListener(CloseSettings);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(SetResolution);
        if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
    }

    private void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettings();
    }

    public void StartGame()
    {
        if (isLoading) return;
        isLoading = true;
        Time.timeScale = 1f;
        if (startButton != null) startButton.interactable = false;
        if (settingsButton != null) settingsButton.interactable = false;
        if (quitButton != null) quitButton.interactable = false;
        SetLoadingProgress(0f);
        StartCoroutine(LoadMainScene());
    }

    private IEnumerator LoadMainScene()
    {
        var fade = SceneTransitionFade.Create();
        yield return fade.FadeTo(1f, 0.45f);
        if (loadingWhaleView != null) yield return loadingWhaleView.WaitUntilPrepared();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingWhaleView != null) loadingWhaleView.Begin();
        var previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
        yield return null;
        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            if (operation == null) throw new System.InvalidOperationException("Main 씬 로드를 시작할 수 없습니다.");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
            isLoading = false;
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (loadingWhaleView != null) loadingWhaleView.End();
            if (startButton != null) startButton.interactable = true;
            if (settingsButton != null) settingsButton.interactable = true;
            if (quitButton != null) quitButton.interactable = true;
            CloseSettings();
            Application.backgroundLoadingPriority = previousPriority;
            StartCoroutine(RecoverLoadingFade(fade));
            yield break;
        }

        operation.allowSceneActivation = false;
        // 로드 시작의 무거운 프레임은 암전 중 처리하고 고래 화면을 부드럽게 밝힌다.
        yield return null;
        yield return null;
        yield return fade.FadeTo(0f, 0.55f);
        float displayedProgress = 0f;
        while (operation.progress < 0.9f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, operation.progress / 0.9f * 0.85f, Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) * 0.5f);
            SetLoadingProgress(displayedProgress);
            yield return null;
        }

        // 로드 준비가 끝난 뒤에도 고래 연출과 마지막 게이지 이동을 잠시 보여준다.
        float startProgress = displayedProgress;
        float duration = Mathf.Clamp(extraLoadingSeconds, 1f, 5f);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            SetLoadingProgress(Mathf.Lerp(startProgress, 1f, elapsed / duration));
            yield return null;
        }
        SetLoadingProgress(1f);
        yield return fade.FadeTo(1f, 0.45f);
        // Title의 코루틴은 씬 전환 시 사라지므로 암전막이 Main 첫 렌더 후 페이드인을 담당한다.
        fade.RevealAfterSceneLoad("Main");
        Application.backgroundLoadingPriority = previousPriority;
        operation.allowSceneActivation = true;
        while (!operation.isDone) yield return null;
    }

    private IEnumerator RecoverLoadingFade(SceneTransitionFade fade)
    {
        yield return fade.FadeTo(0f, 0.55f);
        Destroy(fade.gameObject);
    }

    private void SetLoadingProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (loadingProgressFill != null)
        {
            // 스프라이트의 투명 여백 없이 배경의 왼쪽 끝부터 정확히 채운다.
            loadingProgressFill.fillAmount = progress;
            var rect = loadingProgressFill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(progress, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        if (loadingProgressText != null) loadingProgressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    public void OpenSettings()
    {
        if (isLoading) return;
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (closeSettingsButton != null) closeSettingsButton.Select();
    }

    public void CloseSettings()
    {
        if (isLoading) return;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        Debug.Log("게임 종료 버튼이 호출되었습니다. 빌드에서는 애플리케이션이 종료됩니다.");
#else
        Application.Quit();
#endif
    }

    private void InitializeSettings()
    {
        isInitializing = true;

        float volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        AudioListener.volume = volume;
        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(volume);
        UpdateVolumeLabel(volume);

        BuildResolutionOptions();

        bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        isInitializing = false;
    }

    private void BuildResolutionOptions()
    {
        if (resolutionDropdown == null)
            return;

        availableResolutions.Clear();
        availableResolutions.AddRange(Screen.resolutions
            .GroupBy(r => (r.width, r.height))
            .Select(group => group.OrderByDescending(r => r.refreshRateRatio.value).First())
            .OrderBy(r => r.width)
            .ThenBy(r => r.height));

        if (availableResolutions.Count == 0)
            availableResolutions.Add(new Resolution { width = Screen.width, height = Screen.height });

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(availableResolutions
            .Select(r => $"{r.width} × {r.height}")
            .ToList());

        int currentIndex = availableResolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        int savedIndex = PlayerPrefs.GetInt(ResolutionKey, currentIndex >= 0 ? currentIndex : availableResolutions.Count - 1);
        savedIndex = Mathf.Clamp(savedIndex, 0, availableResolutions.Count - 1);
        resolutionDropdown.SetValueWithoutNotify(savedIndex);
        resolutionDropdown.RefreshShownValue();
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        AudioListener.volume = value;
        UpdateVolumeLabel(value);
        PlayerPrefs.SetFloat(VolumeKey, value);
        if (!isInitializing)
            PlayerPrefs.Save();
    }

    public void SetResolution(int index)
    {
        if (isInitializing || index < 0 || index >= availableResolutions.Count)
            return;

        Resolution resolution = availableResolutions[index];
        bool fullscreen = fullscreenToggle == null ? Screen.fullScreen : fullscreenToggle.isOn;
        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
        PlayerPrefs.SetInt(ResolutionKey, index);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool fullscreen)
    {
        if (isInitializing)
            return;

        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValueText != null)
            volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
