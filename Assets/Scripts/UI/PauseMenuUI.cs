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

    private float lastToggleTime = -1f;

    private void Update()
    {
        if (isPaused)
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            if (!Cursor.visible)
            {
                Cursor.visible = true;
            }

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
                {
                    OnTogglePauseInput();
                }
            }
        }
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
        if (Time.unscaledTime - lastToggleTime < 0.2f) return;
        lastToggleTime = Time.unscaledTime;

        // If Settings panel is open inside pause menu, pressing Esc closes settings instead of unpausing immediately
        if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            OnSettingsCloseButtonClicked();
            return;
        }

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

        if (inputReader != null) inputReader.DisableGameplayActions();

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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SurrenderToLobby();
        }
        else
        {
            PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                player.Die();
            }
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
