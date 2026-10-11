using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public sealed class MainSceneMenu : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button settingsButton;
    [SerializeField] private UnityEngine.UI.Button titleButton;
    [SerializeField] private TitleMenuController settingsController;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private UnityEngine.UI.Button confirmButton;
    [SerializeField] private UnityEngine.UI.Button cancelButton;

    private void Awake()
    {
        settingsButton.onClick.AddListener(settingsController.OpenSettings);
        titleButton.onClick.AddListener(OpenConfirmation);
        confirmButton.onClick.AddListener(ReturnToTitle);
        cancelButton.onClick.AddListener(CloseConfirmation);
        confirmationPanel.SetActive(false);
    }

    private void Update()
    {
        if (confirmationPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseConfirmation();
    }

    public void OpenConfirmation()
    {
        settingsController.CloseSettings();
        confirmationPanel.SetActive(true);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    public void CloseConfirmation()
    {
        confirmationPanel.SetActive(false);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ReturnToTitle()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene("Title");
    }

    private void OnDestroy()
    {
        if (settingsButton != null && settingsController != null) settingsButton.onClick.RemoveListener(settingsController.OpenSettings);
        if (titleButton != null) titleButton.onClick.RemoveListener(OpenConfirmation);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(ReturnToTitle);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(CloseConfirmation);
    }
}
