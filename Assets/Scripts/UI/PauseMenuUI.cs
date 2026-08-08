using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject pauseMenuPanel;
    public GameObject settingsPanel;

    [Header("Buttons")]
    public Button resumeButton;
    public Button settingsButton;
    public Button settingsCloseButton;
    public Button surrenderButton;
    public Button exitToMainMenuButton;

    [Header("Input")]
    public InputReader inputReader;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

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
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        if (settingsCloseButton != null) settingsCloseButton.onClick.AddListener(OnSettingsCloseButtonClicked);
        if (surrenderButton != null) surrenderButton.onClick.AddListener(OnSurrenderButtonClicked);
        if (exitToMainMenuButton != null) exitToMainMenuButton.onClick.AddListener(OnExitToMainMenuButtonClicked);

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.TogglePauseMenuEvent += OnTogglePauseInput;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.TogglePauseMenuEvent -= OnTogglePauseInput;
        }
    }

    private void OnTogglePauseInput()
    {
        // Don't toggle pause if Main Menu, Store UI, Map Selection UI, or Dossier Selection UI is currently open
        if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive) return;
        if (MapSelectionUI.Instance != null && MapSelectionUI.Instance.IsMapOpen) return;
        if (DossierSelectionUI.Instance != null && DossierSelectionUI.Instance.IsDossierOpen) return;
        var store = FindAnyObjectByType<StoreUI>();
        if (store != null && store.IsStoreOpen) return;

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Surrender button disabled/hidden in Lobby
        bool isInLobby = GameManager.Instance != null && GameManager.Instance.isInLobby;
        if (surrenderButton != null)
        {
            surrenderButton.gameObject.SetActive(!isInLobby);
        }

        if (inputReader != null) inputReader.DisableAllActions();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (inputReader != null) inputReader.EnableAllActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnSettingsButtonClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnSettingsCloseButtonClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnSurrenderButtonClicked()
    {
        bool isInLobby = GameManager.Instance != null && GameManager.Instance.isInLobby;
        if (isInLobby)
        {
            Debug.LogWarning("[PauseMenu] Surrender is disabled in Lobby!");
            return;
        }

        Debug.Log("[PauseMenu] Player chose to SURRENDER!");
        ResumeGame();

        PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(9999);
        }
    }

    public void OnExitToMainMenuButtonClicked()
    {
        Debug.Log("[PauseMenu] Returning to Main Menu...");
        ResumeGame();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToMainMenu();
        }
        else if (MainMenuUI.Instance != null)
        {
            MainMenuUI.Instance.ShowMainMenu();
        }
    }
}
