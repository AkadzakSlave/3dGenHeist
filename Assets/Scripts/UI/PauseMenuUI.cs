using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PauseMenuUI : MonoBehaviour
{
    public static PauseMenuUI Instance { get; private set; }

    [Header("Panels")]
    [Tooltip("Главная панель меню паузы с кнопками")]
    public GameObject pauseMenuPanel;
    [Tooltip("Панель настроек")]
    public GameObject settingsPanel;
    [Tooltip("Статический тёмный фон паузы на сцене, помещенный ниже всех элементов")]
    public GameObject darkBackgroundPanel;

    [Header("Controls Plate Customization (Инспектор)")]
    [Tooltip("Плажка со списком управления и клавиш")]
    public GameObject controlsPlate;
    [Tooltip("Позиция плажки относительно центра экрана (увеличьте X, чтобы сдвинуть правее)")]
    public Vector2 controlsPlatePosition = new Vector2(540f, 0f);
    [Tooltip("Размер плажки управления (ширина, высота)")]
    public Vector2 controlsPlateSize = new Vector2(400f, 570f);
    [Tooltip("Размер шрифта содержимого")]
    public float controlsPlateFontSize = 13f;
    [Tooltip("Межстрочный интервал")]
    public float controlsPlateLineSpacing = 6f;

    [Header("Controls Plate Warm Colors (Стилистика ограбления)")]
    [Tooltip("Глубокий тёмный тёплый фон (эспрессо/уголь)")]
    public Color controlsPlateBgColor = new Color(0.08f, 0.07f, 0.05f, 0.95f);
    [Tooltip("Рамка плажки (состаренное золото / янтарь)")]
    public Color controlsPlateBorderColor = new Color(0.85f, 0.65f, 0.22f, 0.90f);
    [Tooltip("Фон шапки (кожа / темная латунь)")]
    public Color controlsPlateHeaderBgColor = new Color(0.18f, 0.13f, 0.08f, 0.98f);
    [Tooltip("Цвет текста шапки (яркое золото)")]
    public Color controlsPlateHeaderTextColor = new Color(1.0f, 0.85f, 0.38f, 1.0f);
    [Tooltip("Цвет обычного текста (тёплый пергамент)")]
    public Color controlsPlateTextColor = new Color(0.95f, 0.90f, 0.82f, 0.95f);
    [Tooltip("Цвет клавиш передвижения и боя [W, A, S, D, ЛКМ, ПКМ]")]
    public Color keyHighlightColor = new Color(1.0f, 0.78f, 0.28f, 1.0f);
    [Tooltip("Цвет особого взаимодействия [E (зажатие)]")]
    public Color actionSpecialColor = new Color(0.95f, 0.55f, 0.22f, 1.0f);
    [Tooltip("Цвет клавиши карты [M]")]
    public Color actionMapColor = new Color(0.85f, 0.80f, 0.45f, 1.0f);
    [Tooltip("Цвет клавиши паузы [ESC / P]")]
    public Color actionPauseColor = new Color(0.92f, 0.42f, 0.35f, 1.0f);

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

        SetupButtonDeselection();

        if (darkBackgroundPanel != null) darkBackgroundPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        EnsureControlsPlate();
        if (controlsPlate != null) controlsPlate.SetActive(false);
    }

    private void OnValidate()
    {
        ApplyControlsPlateSettings();
    }

    private void SetupButtonDeselection()
    {
        Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
        foreach (var s in selectables)
        {
            if (s == null) continue;
            Navigation nav = s.navigation;
            nav.mode = Navigation.Mode.None;
            s.navigation = nav;

            if (s is Button btn)
            {
                btn.onClick.AddListener(() =>
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null)
                    {
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                    }
                });
            }
        }
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

        if (darkBackgroundPanel != null) darkBackgroundPanel.SetActive(true);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        EnsureControlsPlate();
        ApplyControlsPlateSettings();
        if (controlsPlate != null) controlsPlate.SetActive(true);

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

        if (darkBackgroundPanel != null) darkBackgroundPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (controlsPlate != null) controlsPlate.SetActive(false);

        if (inputReader != null) inputReader.EnableAllActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnSettingsButtonClicked()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsPlate != null) controlsPlate.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnSettingsCloseButtonClicked()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (controlsPlate != null) controlsPlate.SetActive(true);
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

    [ContextMenu("🛠️ Rebuild Controls Plate")]
    public void ContextMenuRebuildPlate()
    {
        Transform existing = transform.Find("ControlsPlate");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        controlsPlate = null;
        EnsureControlsPlate();
        ApplyControlsPlateSettings();
    }

    /// <summary>
    /// Автоматически строит стильную плажку со всеми клавишами и управлением справа от меню паузы
    /// </summary>
    public void EnsureControlsPlate()
    {
        if (controlsPlate != null) return;

        Transform existing = transform.Find("ControlsPlate");
        if (existing != null)
        {
            controlsPlate = existing.gameObject;
            return;
        }

        // 1. Создаем корневой объект панели с темным теплым фоном
        GameObject panelObj = new GameObject("ControlsPlate", typeof(RectTransform), typeof(Image), typeof(Outline));
        panelObj.transform.SetParent(transform, false);
        panelObj.layer = 5;

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = controlsPlatePosition;
        rt.sizeDelta = controlsPlateSize;

        Image bg = panelObj.GetComponent<Image>();
        bg.color = controlsPlateBgColor;

        Outline outline = panelObj.GetComponent<Outline>();
        outline.effectColor = controlsPlateBorderColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 2. Шапка плажки
        GameObject headObj = new GameObject("Header", typeof(RectTransform), typeof(Image));
        headObj.transform.SetParent(panelObj.transform, false);
        headObj.layer = 5;

        RectTransform headRt = headObj.GetComponent<RectTransform>();
        headRt.anchorMin = new Vector2(0f, 1f);
        headRt.anchorMax = new Vector2(1f, 1f);
        headRt.pivot = new Vector2(0.5f, 1f);
        headRt.anchoredPosition = Vector2.zero;
        headRt.sizeDelta = new Vector2(0f, 38f);
        headObj.GetComponent<Image>().color = controlsPlateHeaderBgColor;

        GameObject headTextObj = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        headTextObj.transform.SetParent(headObj.transform, false);
        headTextObj.layer = 5;

        RectTransform headTextRt = headTextObj.GetComponent<RectTransform>();
        headTextRt.anchorMin = Vector2.zero;
        headTextRt.anchorMax = Vector2.one;
        headTextRt.offsetMin = new Vector2(14f, 0f);
        headTextRt.offsetMax = new Vector2(-14f, 0f);

        TextMeshProUGUI headTMP = headTextObj.GetComponent<TextMeshProUGUI>();
        headTMP.text = "УПРАВЛЕНИЕ И БИНДЫ";
        headTMP.fontSize = 15f;
        headTMP.fontStyle = FontStyles.Bold;
        headTMP.alignment = TextAlignmentOptions.MidlineLeft;
        headTMP.color = controlsPlateHeaderTextColor;

        // 3. Текстовое содержимое с биндами
        GameObject contentObj = new GameObject("ContentText", typeof(RectTransform), typeof(TextMeshProUGUI));
        contentObj.transform.SetParent(panelObj.transform, false);
        contentObj.layer = 5;

        RectTransform contRt = contentObj.GetComponent<RectTransform>();
        contRt.anchorMin = Vector2.zero;
        contRt.anchorMax = Vector2.one;
        contRt.offsetMin = new Vector2(16f, 14f);
        contRt.offsetMax = new Vector2(-16f, -44f);

        TextMeshProUGUI contTMP = contentObj.GetComponent<TextMeshProUGUI>();
        contTMP.fontSize = controlsPlateFontSize;
        contTMP.lineSpacing = controlsPlateLineSpacing;
        contTMP.richText = true;
        contTMP.alignment = TextAlignmentOptions.TopLeft;
        contTMP.color = controlsPlateTextColor;

        controlsPlate = panelObj;
        ApplyControlsPlateSettings();
        controlsPlate.SetActive(false);
    }

    /// <summary>
    /// Применяет текущие параметры из инспектора (позицию, размер, цвета) к плажке управления
    /// </summary>
    public void ApplyControlsPlateSettings()
    {
        if (controlsPlate == null) return;

        RectTransform rt = controlsPlate.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = controlsPlatePosition;
            rt.sizeDelta = controlsPlateSize;
        }

        Image bg = controlsPlate.GetComponent<Image>();
        if (bg != null) bg.color = controlsPlateBgColor;

        Outline outline = controlsPlate.GetComponent<Outline>();
        if (outline != null) outline.effectColor = controlsPlateBorderColor;

        Transform headTr = controlsPlate.transform.Find("Header");
        if (headTr != null)
        {
            Image headBg = headTr.GetComponent<Image>();
            if (headBg != null) headBg.color = controlsPlateHeaderBgColor;

            Transform htTr = headTr.Find("HeaderText");
            if (htTr != null)
            {
                TextMeshProUGUI ht = htTr.GetComponent<TextMeshProUGUI>();
                if (ht != null) ht.color = controlsPlateHeaderTextColor;
            }
        }

        Transform contentTr = controlsPlate.transform.Find("ContentText");
        if (contentTr != null)
        {
            TextMeshProUGUI cont = contentTr.GetComponent<TextMeshProUGUI>();
            if (cont != null)
            {
                cont.fontSize = controlsPlateFontSize;
                cont.lineSpacing = controlsPlateLineSpacing;
                cont.color = controlsPlateTextColor;

                string goldHex = ColorUtility.ToHtmlStringRGB(keyHighlightColor);
                string orangeHex = ColorUtility.ToHtmlStringRGB(actionSpecialColor);
                string mapHex = ColorUtility.ToHtmlStringRGB(actionMapColor);
                string pauseHex = ColorUtility.ToHtmlStringRGB(actionPauseColor);

                cont.text =
                    $"<color=#{goldHex}><b>[W, A, S, D]</b></color>  —  Передвижение\n" +
                    $"<color=#{goldHex}><b>[Shift]</b></color>  —  Спринт / Бег\n" +
                    $"<color=#{goldHex}><b>[Пробел]</b></color>  —  Прыжок\n" +
                    $"<color=#{goldHex}><b>[C / Ctrl]</b></color>  —  Присесть\n" +
                    $"<color=#{goldHex}><b>[ЛКМ]</b></color>  —  Огонь / Использовать предмет\n" +
                    $"<color=#{goldHex}><b>[ПКМ]</b></color>  —  Прицеливание\n" +
                    $"<color=#{goldHex}><b>[R]</b></color>  —  Перезарядка оружия\n" +
                    $"<color=#{goldHex}><b>[1, 2, 3]</b></color>  —  Слоты снаряжения\n" +
                    $"<color=#{orangeHex}><b>[E (зажатие)]</b></color>  —  Взлом сейфа / Выгрузка / Действие\n" +
                    $"<color=#{goldHex}><b>[Q]</b></color>  —  Бросить предмет в руках\n" +
                    $"<color=#{goldHex}><b>[G]</b></color>  —  Сбросить сумку с лутом\n" +
                    $"<color=#{goldHex}><b>[F]</b></color>  —  Фонарик (вкл / выкл)\n" +
                    $"<color=#{mapHex}><b>[M]</b></color>  —  Миникарта (по умолч. выкл)\n" +
                    $"<color=#{goldHex}><b>[F8]</b></color>  —  Статусы охраны над головой\n" +
                    $"<color=#{pauseHex}><b>[ESC / P]</b></color>  —  Пауза";
            }
        }
    }
}
