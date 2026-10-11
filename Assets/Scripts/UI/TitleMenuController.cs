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
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    public void OpenSettings()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (closeSettingsButton != null) closeSettingsButton.Select();
    }

    public void CloseSettings()
    {
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
