using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;

    [Header("Buttons")]
    public Button playButton;
    public Button settingsButton;
    public Button settingsCloseButton;
    public Button quitButton;

    [Header("Input & References")]
    public InputReader inputReader;

    public bool IsMainMenuActive => mainMenuPanel != null && mainMenuPanel.activeSelf;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayButtonClicked);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(OnSettingsCloseButtonClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitButtonClicked);

        if (settingsPanel != null) settingsPanel.SetActive(false);

        ShowMainMenu();
    }

    private void Update()
    {
        if (IsMainMenuActive)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }
        }
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (inputReader != null) inputReader.DisableAllActions();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.isInLobby = true;
        }
    }

    public void OnPlayButtonClicked()
    {
        StartCoroutine(StartGameRoutine());
    }

    private System.Collections.IEnumerator StartGameRoutine()
    {
        if (GameManager.Instance != null && GameManager.Instance.heistUI != null)
        {
            GameManager.Instance.heistUI.ShowLoadingScreen();
        }

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (inputReader != null) inputReader.EnableAllActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return new WaitForSeconds(0.4f);

        if (GameManager.Instance != null && GameManager.Instance.heistUI != null)
        {
            GameManager.Instance.heistUI.HideLoadingScreen();
        }
    }

    public void OnSettingsButtonClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnSettingsCloseButtonClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnQuitButtonClicked()
    {
        Debug.Log("[MainMenu] Exiting game...");
        Application.Quit();
    }
}
