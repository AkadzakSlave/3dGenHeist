using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Тактическая интерактивная мини-карта (Blueprint Radar) с поддержкой:
/// - Полного размещения на сцене в Canvas (без спавна из пустоты при запуске);
/// - Настройки прямо в Инспекторе и Scene View в реальном времени;
/// - Плавного динамического следования за игроком (карта центрируется и двигается вслед за игроком);
/// - RectMask2D отсечения (комнаты не вылезают за границы и не перекрывают шапку);
/// - Горизонтальных, неискаженных надписей ключевых зон ("СЕЙФ", "ВЫХОД");
/// - Тумана Войны (Fog of War) и отслеживания исследованных комнат;
/// - Золотых маркеров лута (горячая клавиша F2 / инспектор);
/// - Очистки всех визуалов при новом заезде/перезапуске миссии.
/// </summary>
[ExecuteAlways]
public class HeistMinimap : MonoBehaviour
{
    public static HeistMinimap Instance { get; private set; }

    [Header("Minimap Layout (Inspector Adjustable)")]
    [Tooltip("Позиция правого верхнего угла карты от правого верхнего угла экрана")]
    public Vector2 mapAnchoredPosition = new Vector2(-25f, -25f);
    [Tooltip("Размер окна радара (Ширина, Высота)")]
    public Vector2 mapPanelSize = new Vector2(290f, 290f);
    [Tooltip("Масштаб карты (зум комнат)")]
    public float mapZoom = 2.0f;

    [Header("Tracking & Follow Player")]
    [Tooltip("Плавно центрировать карту на игроке, чтобы карта двигалась вслед за ним в глубоких ветках")]
    public bool followPlayer = true;
    [Tooltip("Скорость плавного следования камеры карты за игроком")]
    public float followSpeed = 8f;

    [Header("Notification / Ticker (Inspector Adjustable)")]
    [Tooltip("Смещение плашки уведомления относительно низа карты")]
    public Vector2 notificationOffset = new Vector2(0f, -8f);
    [Tooltip("Размер плашки уведомления (Ширина, Высота)")]
    public Vector2 notificationSize = new Vector2(290f, 42f);
    [Tooltip("Размер шрифта уведомления")]
    public float notificationFontSize = 13f;

    [Header("Visibility & Modes")]
    public bool isMinimapVisible = false;
    [Tooltip("Показывать отладочные маркеры врагов (красные точки). Переключается клавишей F4")]
    public bool showEnemiesOnMap = true;
    [Tooltip("Показывать слабый силуэт неисследованных комнат (архитектурный чертеж)")]
    public bool showUnexploredSilhouettes = true;

    [Header("Loot Display (Loot Radar - F2)")]
    [Tooltip("Отображать лут на карте (переключается клавишей F2 или в инспекторе)")]
    public bool showLootOnMap = false;
    [Tooltip("Отображать лут только в исследованных комнатах (false = весь найденный лут)")]
    public bool showLootOnlyInExploredRooms = false;
    public Color lootColor = new Color(1.0f, 0.85f, 0.20f, 0.95f);
    public float lootDotSize = 5.5f;

    [Header("Theme Colors (Warm Noir / Heist Styling)")]
    public Color radarBgColor = new Color(0.08f, 0.07f, 0.05f, 0.94f);
    public Color radarBorderColor = new Color(0.85f, 0.65f, 0.22f, 0.90f);
    public Color headerBgColor = new Color(0.16f, 0.12f, 0.08f, 0.98f);
    public Color exploredRoomColor = new Color(0.24f, 0.18f, 0.11f, 0.88f);
    public Color exploredRoomBorderColor = new Color(0.90f, 0.72f, 0.32f, 0.92f);
    public Color unexploredColor = new Color(0.10f, 0.08f, 0.06f, 0.45f);
    public Color unexploredBorderColor = new Color(0.35f, 0.28f, 0.18f, 0.45f);
    public Color vaultColor = new Color(0.95f, 0.72f, 0.12f, 0.95f);
    public Color vaultBorderColor = new Color(1.0f, 0.88f, 0.35f, 1.0f);
    public Color vanColor = new Color(0.20f, 0.75f, 0.40f, 0.95f);
    public Color vanBorderColor = new Color(0.40f, 0.95f, 0.55f, 1.0f);
    public Color playerColor = new Color(1.0f, 0.85f, 0.35f, 1f);
    public Color enemyColor = new Color(0.95f, 0.25f, 0.22f, 0.95f);

    [Header("Alert State")]
    public string defaultTickerText = "СТАТУС: ОБЪЕКТ ПОД НАБЛЮДЕНИЕМ";
    public Color defaultTickerColor = new Color(0.95f, 0.80f, 0.40f, 0.90f);
    public string currentAlertText = "СТАТУС: ОБЪЕКТ ПОД НАБЛЮДЕНИЕМ";
    public Color currentAlertColor = new Color(0.95f, 0.80f, 0.40f, 0.90f);
    private float alertResetTimer = 0f;

    // UI элементы
    [SerializeField] private RectTransform rootPanel;
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private RectTransform headerRect;
    [SerializeField] private Image playerMarker;
    [SerializeField] private TextMeshProUGUI tickerText;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI coordsText;
    [SerializeField] private Image tickerLeftAccent;
    [SerializeField] private RectTransform tickerRect;

    private readonly Dictionary<RoomTracker, RectTransform> roomVisuals = new Dictionary<RoomTracker, RectTransform>();
    private readonly List<Image> enemyMarkers = new List<Image>();
    private readonly List<Image> lootMarkers = new List<Image>();

    private Vector3 bankCenterWorld = Vector3.zero;
    private Vector3 currentMapCenter = Vector3.zero;
    private Transform playerTransform;
    private Transform cameraTransform;

    private float lootScanTimer = 0f;
    private LootItem[] cachedLootItems;

    /// <summary>
    /// Автоматическое исправление битой кириллической кодировки (Mojibake UTF-8)
    /// </summary>
    private string SanitizeRussianText(string input)
    {
        if (string.IsNullOrEmpty(input)) return "СТАТУС: ОБЪЕКТ ПОД НАБЛЮДЕНИЕМ";
        if (input.Contains("РЎ") || input.Contains("Рў") || input.Contains("Рћ") || input.Contains("Рќ") || input.Contains("\u0420\u040E") || input.Contains("Р"))
        {
            return "СТАТУС: ОБЪЕКТ ПОД НАБЛЮДЕНИЕМ";
        }
        return input;
    }

    private void Awake()
    {
        defaultTickerText = SanitizeRussianText(defaultTickerText);
        currentAlertText = SanitizeRussianText(currentAlertText);

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this && Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        BuildMinimapUI();
        if (rootPanel != null) rootPanel.gameObject.SetActive(false);
        if (tickerRect != null) tickerRect.gameObject.SetActive(false);
        transform.SetSiblingIndex(0);
    }

    private void Start()
    {
        defaultTickerText = SanitizeRussianText(defaultTickerText);
        currentAlertText = SanitizeRussianText(currentAlertText);

        BuildMinimapUI();
        ApplyLayout();

        // По умолчанию миникарта выключена, включается по клавише [M]
        isMinimapVisible = false;
        if (rootPanel != null)
        {
            rootPanel.gameObject.SetActive(false);
        }
        if (tickerRect != null)
        {
            tickerRect.gameObject.SetActive(false);
        }

        // Миникарта является частью фонового HUD и всегда рендерится под меню/диалогами
        transform.SetSiblingIndex(0);

        if (Application.isPlaying)
        {
            RoomTracker.OnRoomExplored += HandleRoomExplored;
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            RoomTracker.OnRoomExplored -= HandleRoomExplored;
        }
    }

    private void OnValidate()
    {
        defaultTickerText = SanitizeRussianText(defaultTickerText);
        currentAlertText = SanitizeRussianText(currentAlertText);
        ApplyLayout();
    }

    /// <summary>
    /// Полная очистка мини-карты при переходе на следующий заезд / рестарте
    /// </summary>
    public void ClearMinimap()
    {
        foreach (var kvp in roomVisuals)
        {
            if (kvp.Value != null)
            {
                if (Application.isPlaying) Destroy(kvp.Value.gameObject);
                else DestroyImmediate(kvp.Value.gameObject);
            }
        }
        roomVisuals.Clear();

        foreach (var em in enemyMarkers)
        {
            if (em != null)
            {
                if (Application.isPlaying) Destroy(em.gameObject);
                else DestroyImmediate(em.gameObject);
            }
        }
        enemyMarkers.Clear();

        foreach (var lm in lootMarkers)
        {
            if (lm != null)
            {
                if (Application.isPlaying) Destroy(lm.gameObject);
                else DestroyImmediate(lm.gameObject);
            }
        }
        lootMarkers.Clear();

        bankCenterWorld = Vector3.zero;
        currentMapCenter = Vector3.zero;
        currentAlertText = defaultTickerText;
        currentAlertColor = defaultTickerColor;
        if (tickerText != null)
        {
            tickerText.text = defaultTickerText;
            tickerText.color = defaultTickerColor;
        }
        if (tickerLeftAccent != null)
        {
            tickerLeftAccent.color = defaultTickerColor;
        }

        Debug.Log("<color=cyan>[HeistMinimap] Мини-карта успешно очищена для нового заезда!</color>");
    }

    [ContextMenu("🛠️ Rebuild Minimap Visuals")]
    public void ContextMenuRebuild()
    {
        if (rootPanel != null)
        {
            if (Application.isPlaying) Destroy(rootPanel.gameObject);
            else DestroyImmediate(rootPanel.gameObject);
            rootPanel = null;
        }
        if (tickerRect != null)
        {
            if (Application.isPlaying) Destroy(tickerRect.gameObject);
            else DestroyImmediate(tickerRect.gameObject);
            tickerRect = null;
        }
        BuildMinimapUI();
        ApplyLayout();

        // По умолчанию миникарта выключена, включается по клавише [M]
        if (rootPanel != null)
        {
            rootPanel.gameObject.SetActive(isMinimapVisible);
        }
        if (tickerRect != null)
        {
            tickerRect.gameObject.SetActive(isMinimapVisible);
        }
    }

    public void BuildMinimapUI()
    {
        // Привязываем сам объект HeistMinimap к правому верхнему углу Canvas
        RectTransform myRt = GetComponent<RectTransform>();
        if (myRt != null)
        {
            myRt.anchorMin = new Vector2(1f, 1f);
            myRt.anchorMax = new Vector2(1f, 1f);
            myRt.pivot = new Vector2(1f, 1f);
            myRt.anchoredPosition = mapAnchoredPosition;
            myRt.sizeDelta = mapPanelSize;
        }

        // Ищем уже существующие дочерние объекты на сцене, чтобы не дублировать
        Transform existingRoot = transform.Find("Minimap_Root");
        if (existingRoot != null) rootPanel = existingRoot.GetComponent<RectTransform>();

        // Очищаем случайные дубликаты текстов или ошибочно вложенных плашек внутри карты
        if (rootPanel != null)
        {
            for (int i = rootPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = rootPanel.GetChild(i);
                if (child.name == "Alert_Ticker_Bg" || child.name == "Alert_Text")
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        Transform existingTicker = transform.Find("Alert_Ticker_Bg");
        if (existingTicker != null)
        {
            tickerRect = existingTicker.GetComponent<RectTransform>();
            Transform tTxt = existingTicker.Find("Alert_Text");
            if (tTxt != null) tickerText = tTxt.GetComponent<TextMeshProUGUI>();
            Transform aLine = existingTicker.Find("Accent_Line");
            if (aLine != null) tickerLeftAccent = aLine.GetComponent<Image>();
        }

        if (rootPanel == null)
        {
            // 1. Корневая панель радара
            GameObject panelObj = new GameObject("Minimap_Root", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObj.transform.SetParent(transform, false);
            SetLayerRecursively(panelObj, 5);

            rootPanel = panelObj.GetComponent<RectTransform>();
            rootPanel.anchorMin = Vector2.zero;
            rootPanel.anchorMax = Vector2.one;
            rootPanel.pivot = new Vector2(1f, 1f);
            rootPanel.anchoredPosition = Vector2.zero;
            rootPanel.sizeDelta = Vector2.zero;

            Image rootBg = panelObj.GetComponent<Image>();
            rootBg.color = radarBgColor;

            Outline outline = panelObj.GetComponent<Outline>();
            outline.effectColor = radarBorderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 2. Контейнер содержимого карты с RectMask2D (чтобы комнаты не вылезали за пределы экрана и под шапку)
        Transform existingContent = rootPanel.Find("Map_Content");
        if (existingContent == null)
        {
            GameObject contentObj = new GameObject("Map_Content", typeof(RectTransform), typeof(RectMask2D));
            contentObj.transform.SetParent(rootPanel, false);
            SetLayerRecursively(contentObj, 5);

            mapContainer = contentObj.GetComponent<RectTransform>();
            mapContainer.anchorMin = Vector2.zero;
            mapContainer.anchorMax = Vector2.one;
            mapContainer.pivot = new Vector2(0.5f, 0.5f);
            mapContainer.offsetMin = new Vector2(4f, 4f);
            mapContainer.offsetMax = new Vector2(-4f, -30f); // Строго под шапкой
        }
        else
        {
            mapContainer = existingContent.GetComponent<RectTransform>();
            if (mapContainer.GetComponent<RectMask2D>() == null)
            {
                mapContainer.gameObject.AddComponent<RectMask2D>();
            }

            // Проверяем и очищаем дубликаты текстов внутри mapContainer
            for (int i = mapContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = mapContainer.GetChild(i);
                if (child.name == "Alert_Ticker_Bg" || child.name == "Alert_Text")
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        // 3. Маркер игрока
        Transform existingPlayer = mapContainer.Find("Player_Marker");
        if (existingPlayer == null)
        {
            GameObject playerObj = new GameObject("Player_Marker", typeof(RectTransform), typeof(Image), typeof(Outline));
            playerObj.transform.SetParent(mapContainer, false);
            SetLayerRecursively(playerObj, 5);

            RectTransform pRect = playerObj.GetComponent<RectTransform>();
            pRect.sizeDelta = new Vector2(12f, 14f);
            playerMarker = playerObj.GetComponent<Image>();
            playerMarker.color = playerColor;

            Outline pOutline = playerObj.GetComponent<Outline>();
            pOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            pOutline.effectDistance = new Vector2(1f, -1f);
        }
        else
        {
            playerMarker = existingPlayer.GetComponent<Image>();
        }

        // 4. Шапка радара (Создается или выносится в верхний слой SetAsLastSibling, чтобы комнаты физически не могли ее перекрыть!)
        Transform existingHead = rootPanel.Find("Radar_Header");
        if (existingHead == null)
        {
            GameObject headObj = new GameObject("Radar_Header", typeof(RectTransform), typeof(Image));
            headObj.transform.SetParent(rootPanel, false);
            SetLayerRecursively(headObj, 5);

            headerRect = headObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 28f);

            Image headBg = headObj.GetComponent<Image>();
            headBg.color = headerBgColor;

            // Название шапки
            GameObject titleObj = new GameObject("Title_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(headObj.transform, false);
            SetLayerRecursively(titleObj, 5);

            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.anchoredPosition = new Vector2(10f, 0f);
            titleRt.sizeDelta = new Vector2(-20f, 0f);

            headerText = titleObj.GetComponent<TextMeshProUGUI>();
            headerText.fontSize = 12.5f;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.color = new Color(1.0f, 0.85f, 0.38f, 0.95f);
            headerText.text = "ТАКТИЧЕСКИЙ ПЛАН [M]";

            // Координаты игрока в правом углу шапки
            GameObject coordsObj = new GameObject("Coords_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            coordsObj.transform.SetParent(headObj.transform, false);
            SetLayerRecursively(coordsObj, 5);

            RectTransform cRt = coordsObj.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.anchoredPosition = new Vector2(-10f, 0f);
            cRt.sizeDelta = new Vector2(-20f, 0f);

            coordsText = coordsObj.GetComponent<TextMeshProUGUI>();
            coordsText.fontSize = 11.5f;
            coordsText.fontStyle = FontStyles.Bold;
            coordsText.alignment = TextAlignmentOptions.MidlineRight;
            coordsText.color = new Color(0.95f, 0.78f, 0.40f, 0.85f);
            coordsText.text = "X: 00  Z: 00";
        }
        else
        {
            headerRect = existingHead.GetComponent<RectTransform>();
            Transform tObj = existingHead.Find("Title_Text");
            if (tObj != null) headerText = tObj.GetComponent<TextMeshProUGUI>();
            Transform cObj = existingHead.Find("Coords_Text");
            if (cObj != null) coordsText = cObj.GetComponent<TextMeshProUGUI>();
        }

        // Шапка ВСЕГДА рендерится поверх содержимого карты!
        if (headerRect != null)
        {
            headerRect.SetAsLastSibling();
        }

        if (tickerRect == null)
        {
            // 5. Плашка Alert Ticker под радаром
            GameObject tickerObj = new GameObject("Alert_Ticker_Bg", typeof(RectTransform), typeof(Image), typeof(Outline));
            tickerObj.transform.SetParent(transform, false);
            SetLayerRecursively(tickerObj, 5);

            tickerRect = tickerObj.GetComponent<RectTransform>();
            tickerRect.anchorMin = new Vector2(0.5f, 0f);
            tickerRect.anchorMax = new Vector2(0.5f, 0f);
            tickerRect.pivot = new Vector2(0.5f, 1f);
            tickerRect.anchoredPosition = notificationOffset;
            tickerRect.sizeDelta = notificationSize;

            Image tImg = tickerObj.GetComponent<Image>();
            tImg.color = new Color(0.04f, 0.08f, 0.15f, 0.95f);

            Outline tOutline = tickerObj.GetComponent<Outline>();
            tOutline.effectColor = new Color(0.20f, 0.60f, 0.95f, 0.65f);
            tOutline.effectDistance = new Vector2(1f, -1f);

            // Акцентная полоска слева
            GameObject accentObj = new GameObject("Accent_Line", typeof(RectTransform), typeof(Image));
            accentObj.transform.SetParent(tickerObj.transform, false);
            SetLayerRecursively(accentObj, 5);

            RectTransform aRt = accentObj.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0f, 0f);
            aRt.anchorMax = new Vector2(0f, 1f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.anchoredPosition = Vector2.zero;
            aRt.sizeDelta = new Vector2(4f, 0f);

            tickerLeftAccent = accentObj.GetComponent<Image>();
            tickerLeftAccent.color = currentAlertColor;

            // Текст бегущей строки
            GameObject tTextObj = new GameObject("Alert_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tTextObj.transform.SetParent(tickerObj.transform, false);
            SetLayerRecursively(tTextObj, 5);

            RectTransform ttRect = tTextObj.GetComponent<RectTransform>();
            ttRect.anchorMin = Vector2.zero;
            ttRect.anchorMax = Vector2.one;
            ttRect.sizeDelta = new Vector2(-16f, -2f);
            ttRect.anchoredPosition = new Vector2(4f, 0f);

            tickerText = tTextObj.GetComponent<TextMeshProUGUI>();
            tickerText.fontSize = notificationFontSize;
            tickerText.fontStyle = FontStyles.Bold;
            tickerText.alignment = TextAlignmentOptions.Center;
            tickerText.textWrappingMode = TextWrappingModes.NoWrap;
            tickerText.overflowMode = TextOverflowModes.Ellipsis;
            tickerText.color = currentAlertColor;
            tickerText.text = currentAlertText;
        }

        ApplyLayout();
    }

    public void ApplyLayout()
    {
        RectTransform myRt = GetComponent<RectTransform>();
        if (myRt != null)
        {
            myRt.anchorMin = new Vector2(1f, 1f);
            myRt.anchorMax = new Vector2(1f, 1f);
            myRt.pivot = new Vector2(1f, 1f);
            myRt.anchoredPosition = mapAnchoredPosition;
            myRt.sizeDelta = mapPanelSize;
        }

        if (rootPanel != null)
        {
            Image rootBg = rootPanel.GetComponent<Image>();
            if (rootBg != null) rootBg.color = radarBgColor;

            Outline outline = rootPanel.GetComponent<Outline>();
            if (outline != null) outline.effectColor = radarBorderColor;
        }

        if (mapContainer != null)
        {
            mapContainer.anchorMin = Vector2.zero;
            mapContainer.anchorMax = Vector2.one;
            mapContainer.pivot = new Vector2(0.5f, 0.5f);
            mapContainer.offsetMin = new Vector2(4f, 4f);
            mapContainer.offsetMax = new Vector2(-4f, -30f);
        }

        if (headerRect != null)
        {
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 28f);
            headerRect.SetAsLastSibling();
        }

        if (headerText != null)
        {
            headerText.fontSize = 12.5f;
            headerText.fontStyle = FontStyles.Bold;
            headerText.color = new Color(1.0f, 0.85f, 0.38f, 0.95f);
        }

        if (coordsText != null)
        {
            coordsText.fontSize = 11.5f;
            coordsText.fontStyle = FontStyles.Bold;
            coordsText.color = new Color(0.95f, 0.78f, 0.40f, 0.85f);
        }

        if (tickerRect != null)
        {
            tickerRect.anchorMin = new Vector2(0.5f, 0f);
            tickerRect.anchorMax = new Vector2(0.5f, 0f);
            tickerRect.pivot = new Vector2(0.5f, 1f);
            tickerRect.anchoredPosition = notificationOffset;
            tickerRect.sizeDelta = notificationSize;
        }

        if (tickerText != null)
        {
            tickerText.fontSize = notificationFontSize;
            tickerText.fontStyle = FontStyles.Bold;
            tickerText.textWrappingMode = TextWrappingModes.NoWrap;
            tickerText.overflowMode = TextOverflowModes.Ellipsis;
            tickerText.text = currentAlertText;
            tickerText.color = currentAlertColor;
        }
    }

    /// <summary>
    /// Проверяет, открыто ли в данный момент любое меню (пауза, магазин, выбор досье, терминал)
    /// </summary>
    public static bool IsAnyMenuOpen()
    {
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return true;
        if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive) return true;
        if (StoreUI.Instance != null && StoreUI.Instance.IsStoreOpen) return true;
        if (DossierSelectionUI.Instance != null && DossierSelectionUI.Instance.IsDossierOpen) return true;
        if (MapSelectionUI.Instance != null && MapSelectionUI.Instance.IsMapOpen) return true;
        if (DonResultUI.Instance != null && DonResultUI.Instance.isOpen) return true;
        if (HeistTruckStorageUI.Instance != null && HeistTruckStorageUI.Instance.IsOpen) return true;
        if (HeistUI.Instance != null && HeistUI.Instance.loadingScreenPanel != null && HeistUI.Instance.loadingScreenPanel.activeSelf) return true;
        return false;
    }

    private void Update()
    {
        // 1. Горячая клавиша [M] для включения/выключения карты (только в геймплее, не во время меню)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
        {
            if (!IsAnyMenuOpen())
            {
                ToggleMinimap();
            }
        }

        // Горячая клавиша [F4] для переключения отладочных меток врагов
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f4Key.wasPressedThisFrame)
        {
            showEnemiesOnMap = !showEnemiesOnMap;
            Debug.Log($"[HeistMinimap] Отображение врагов на карте: {(showEnemiesOnMap ? "ВКЛ" : "ВЫКЛ")}");
        }

        // Горячая клавиша [F2] для переключения отображения лута (разделена с F3 переключения HUD!)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame)
        {
            showLootOnMap = !showLootOnMap;
            Debug.Log($"[HeistMinimap] Отображение лута на карте (F2): {(showLootOnMap ? "ВКЛ" : "ВЫКЛ")}");
        }

        if (rootPanel == null) return;

        // Карта является частью HUD и никогда не перекрывает меню паузы, магазин, досье или результаты
        bool menuOpen = IsAnyMenuOpen();
        bool shouldShow = isMinimapVisible && !menuOpen;

        if (rootPanel.gameObject.activeSelf != shouldShow)
        {
            rootPanel.gameObject.SetActive(shouldShow);
        }
        if (tickerRect != null && tickerRect.gameObject.activeSelf != shouldShow)
        {
            tickerRect.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow) return;

        if (!Application.isPlaying) return;

        // 2. Ссылки на игрока
        FindPlayerReferences();

        // 3. Обновление динамического центра карты (следование за игроком)
        UpdateMapCenterTracking();

        // 4. Синхронизация комнат
        SyncRoomVisuals();

        // 5. Позиция игрока и координаты
        UpdatePlayerPosition();

        // 6. Враги на карте
        UpdateEnemyMarkers();

        // 7. Лут на карте (с поддержкой выключения)
        UpdateLootMarkers();

        // 8. Таймер бегущей строки
        if (alertResetTimer > 0f)
        {
            alertResetTimer -= Time.deltaTime;
            if (alertResetTimer <= 0f)
            {
                currentAlertText = defaultTickerText;
                currentAlertColor = defaultTickerColor;
                if (tickerText != null)
                {
                    tickerText.text = currentAlertText;
                    tickerText.color = currentAlertColor;
                }
                if (tickerLeftAccent != null) tickerLeftAccent.color = currentAlertColor;
            }
        }
    }

    private void FindPlayerReferences()
    {
        if (playerTransform == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                playerTransform = GameManager.Instance.playerTransform;
            }
            else
            {
                PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) playerTransform = pm.transform;
            }
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    /// <summary>
    /// Динамическое следование карты: если генерация ушла дальше или игрок идет вглубь,
    /// карта плавно центрируется на позиции игрока, чтобы он никогда не терялся за границами!
    /// </summary>
    private void UpdateMapCenterTracking()
    {
        if (playerTransform != null)
        {
            if (currentMapCenter == Vector3.zero)
            {
                currentMapCenter = playerTransform.position;
            }
            else if (followPlayer)
            {
                currentMapCenter = Vector3.Lerp(currentMapCenter, playerTransform.position, Time.deltaTime * followSpeed);
            }
        }
        else if (bankCenterWorld != Vector3.zero)
        {
            currentMapCenter = bankCenterWorld;
        }
    }

    private void SyncRoomVisuals()
    {
        if (RoomTracker.All.Count == 0)
        {
            LevelGenerator gen = LevelGenerator.Instance != null ? LevelGenerator.Instance : FindAnyObjectByType<LevelGenerator>();
            if (gen != null && gen.SpawnedRooms != null && gen.SpawnedRooms.Count > 0)
            {
                foreach (var rObj in gen.SpawnedRooms)
                {
                    if (rObj != null && rObj.GetComponent<RoomTracker>() == null)
                    {
                        RoomTracker tracker = rObj.AddComponent<RoomTracker>();
                        tracker.Initialize(rObj.name, new Bounds(Vector3.zero, new Vector3(16f, 4f, 16f)));
                    }
                }
            }
        }

        if (RoomTracker.All.Count == 0) return;

        if (bankCenterWorld == Vector3.zero)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var r in RoomTracker.All)
            {
                if (r != null)
                {
                    sum += r.transform.position;
                    count++;
                }
            }
            if (count > 0) bankCenterWorld = sum / count;
        }

        Vector3 referenceCenter = (followPlayer && currentMapCenter != Vector3.zero) ? currentMapCenter : bankCenterWorld;

        foreach (var tracker in RoomTracker.All)
        {
            if (tracker == null) continue;

            if (!roomVisuals.TryGetValue(tracker, out RectTransform roomRect) || roomRect == null)
            {
                roomRect = CreateRoomVisual(tracker);
                roomVisuals[tracker] = roomRect;
            }

            Vector3 rPos = tracker.transform.position;
            Vector2 mapPos = new Vector2(rPos.x - referenceCenter.x, rPos.z - referenceCenter.z) * mapZoom;
            roomRect.anchoredPosition = mapPos;
            roomRect.localRotation = Quaternion.Euler(0f, 0f, -tracker.transform.eulerAngles.y);

            // Держим надписи комнат всегда строго горизонтальными на экране
            Transform vaultLbl = roomRect.Find("Vault_Label");
            if (vaultLbl != null) vaultLbl.rotation = Quaternion.identity;

            Transform vanLbl = roomRect.Find("Van_Label");
            if (vanLbl != null) vanLbl.rotation = Quaternion.identity;

            Image img = roomRect.GetComponent<Image>();
            Outline outl = roomRect.GetComponent<Outline>();

            if (img != null)
            {
                if (tracker.isExplored)
                {
                    roomRect.gameObject.SetActive(true);
                    if (tracker.isVault)
                    {
                        img.color = vaultColor;
                        if (outl != null) outl.effectColor = vaultBorderColor;
                    }
                    else if (tracker.isStartOrVan)
                    {
                        img.color = vanColor;
                        if (outl != null) outl.effectColor = vanBorderColor;
                    }
                    else
                    {
                        img.color = exploredRoomColor;
                        if (outl != null) outl.effectColor = exploredRoomBorderColor;
                    }
                }
                else
                {
                    if (showUnexploredSilhouettes)
                    {
                        roomRect.gameObject.SetActive(true);
                        img.color = unexploredColor;
                        if (outl != null) outl.effectColor = unexploredBorderColor;
                    }
                    else
                    {
                        roomRect.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    private RectTransform CreateRoomVisual(RoomTracker tracker)
    {
        GameObject rObj = new GameObject($"Map_{tracker.roomName}", typeof(RectTransform), typeof(Image), typeof(Outline));
        rObj.transform.SetParent(mapContainer, false);
        SetLayerRecursively(rObj, 5);
        rObj.transform.SetAsFirstSibling();

        RectTransform rt = rObj.GetComponent<RectTransform>();
        float w = Mathf.Max(8f, tracker.worldBounds.size.x * mapZoom);
        float h = Mathf.Max(8f, tracker.worldBounds.size.z * mapZoom);
        rt.sizeDelta = new Vector2(w, h);

        Image img = rObj.GetComponent<Image>();
        img.color = tracker.isExplored ? exploredRoomColor : unexploredColor;

        Outline outl = rObj.GetComponent<Outline>();
        outl.effectColor = tracker.isExplored ? exploredRoomBorderColor : unexploredBorderColor;
        outl.effectDistance = new Vector2(1f, -1f);

        if (tracker.isVault)
        {
            GameObject txtObj = new GameObject("Vault_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(rObj.transform, false);
            SetLayerRecursively(txtObj, 5);

            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(65f, 24f);

            TextMeshProUGUI lbl = txtObj.GetComponent<TextMeshProUGUI>();
            lbl.fontSize = 12f;
            lbl.fontSizeMin = 8f;
            lbl.fontSizeMax = 12f;
            lbl.enableAutoSizing = true;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap; // Без разбиения слова
            lbl.overflowMode = TextOverflowModes.Overflow;
            lbl.color = Color.white;
            lbl.text = "СЕЙФ";
        }
        else if (tracker.isStartOrVan)
        {
            GameObject txtObj = new GameObject("Van_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(rObj.transform, false);
            SetLayerRecursively(txtObj, 5);

            RectTransform trt = txtObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = new Vector2(65f, 24f);

            TextMeshProUGUI lbl = txtObj.GetComponent<TextMeshProUGUI>();
            lbl.fontSize = 11.5f;
            lbl.fontSizeMin = 8f;
            lbl.fontSizeMax = 11.5f;
            lbl.enableAutoSizing = true;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.textWrappingMode = TextWrappingModes.NoWrap; // Без разбиения слова
            lbl.overflowMode = TextOverflowModes.Overflow;
            lbl.color = Color.white;
            lbl.text = "ВЫХОД";
        }

        return rt;
    }

    private void UpdatePlayerPosition()
    {
        if (playerMarker == null || playerTransform == null) return;

        Vector3 pPos = playerTransform.position;
        Vector3 referenceCenter = (followPlayer && currentMapCenter != Vector3.zero) ? currentMapCenter : bankCenterWorld;

        Vector2 mapPos = new Vector2(pPos.x - referenceCenter.x, pPos.z - referenceCenter.z) * mapZoom;
        playerMarker.rectTransform.anchoredPosition = mapPos;

        float yaw = cameraTransform != null ? cameraTransform.eulerAngles.y : playerTransform.eulerAngles.y;
        playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        playerMarker.transform.SetAsLastSibling();

        if (coordsText != null)
        {
            coordsText.text = $"X: {pPos.x:00}  Z: {pPos.z:00}";
        }
    }

    private void UpdateEnemyMarkers()
    {
        if (!showEnemiesOnMap)
        {
            foreach (var m in enemyMarkers)
            {
                if (m != null) m.gameObject.SetActive(false);
            }
            return;
        }

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude);
        if (enemies == null) return;

        Vector3 referenceCenter = (followPlayer && currentMapCenter != Vector3.zero) ? currentMapCenter : bankCenterWorld;
        int activeCount = 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnemyHealth eHealth = enemy.GetComponent<EnemyHealth>();
            if (eHealth != null && eHealth.isDead)
            {
                continue;
            }

            if (activeCount >= enemyMarkers.Count)
            {
                GameObject eObj = new GameObject($"EnemyMarker_{activeCount}", typeof(RectTransform), typeof(Image));
                eObj.transform.SetParent(mapContainer, false);
                SetLayerRecursively(eObj, 5);

                RectTransform ert = eObj.GetComponent<RectTransform>();
                ert.sizeDelta = new Vector2(7f, 7f);
                Image eimg = eObj.GetComponent<Image>();
                eimg.color = enemyColor;
                enemyMarkers.Add(eimg);
            }

            Image marker = enemyMarkers[activeCount];
            marker.gameObject.SetActive(true);

            EnemyType eType = (enemy.data != null) ? enemy.data.type : EnemyType.Guard;

            if (eType == EnemyType.Elite)
            {
                marker.color = new Color(0.9f, 0.2f, 1.0f, 1f); // Фиолетовый для элиты
                marker.rectTransform.sizeDelta = new Vector2(9f, 9f);
            }
            else if (eType == EnemyType.Assaulter)
            {
                marker.color = new Color(1.0f, 0.45f, 0.1f, 1f); // Оранжевый для штурмовиков/полиции
                marker.rectTransform.sizeDelta = new Vector2(8f, 8f);
            }
            else
            {
                marker.color = enemyColor; // Красный для обычной охраны
                marker.rectTransform.sizeDelta = new Vector2(6.5f, 6.5f);
            }

            Vector3 pos = enemy.transform.position;
            marker.rectTransform.anchoredPosition = new Vector2(pos.x - referenceCenter.x, pos.z - referenceCenter.z) * mapZoom;
            activeCount++;
        }

        for (int i = activeCount; i < enemyMarkers.Count; i++)
        {
            if (enemyMarkers[i] != null) enemyMarkers[i].gameObject.SetActive(false);
        }
    }

    private void UpdateLootMarkers()
    {
        // Возможность полного выключения отображения лута
        if (!showLootOnMap)
        {
            foreach (var m in lootMarkers)
            {
                if (m != null && m.gameObject.activeSelf)
                {
                    m.gameObject.SetActive(false);
                }
            }
            return;
        }

        lootScanTimer -= Time.deltaTime;
        if (lootScanTimer <= 0f)
        {
            lootScanTimer = 0.5f;
            cachedLootItems = FindObjectsByType<LootItem>(FindObjectsInactive.Exclude);
        }

        if (cachedLootItems == null) return;

        Vector3 referenceCenter = (followPlayer && currentMapCenter != Vector3.zero) ? currentMapCenter : bankCenterWorld;
        int activeCount = 0;

        foreach (var item in cachedLootItems)
        {
            if (item == null || !item.gameObject.activeInHierarchy) continue;

            if (showLootOnlyInExploredRooms)
            {
                RoomTracker room = item.GetComponentInParent<RoomTracker>();
                if (room != null && !room.isExplored) continue;
            }

            if (activeCount >= lootMarkers.Count)
            {
                GameObject lObj = new GameObject($"LootMarker_{activeCount}", typeof(RectTransform), typeof(Image));
                lObj.transform.SetParent(mapContainer, false);
                SetLayerRecursively(lObj, 5);

                RectTransform lrt = lObj.GetComponent<RectTransform>();
                lrt.sizeDelta = new Vector2(lootDotSize, lootDotSize);
                Image limg = lObj.GetComponent<Image>();
                limg.color = lootColor;
                lootMarkers.Add(limg);
            }

            Image marker = lootMarkers[activeCount];
            marker.gameObject.SetActive(true);
            marker.color = lootColor;
            marker.rectTransform.sizeDelta = new Vector2(lootDotSize, lootDotSize);
            Vector3 pos = item.transform.position;
            marker.rectTransform.anchoredPosition = new Vector2(pos.x - referenceCenter.x, pos.z - referenceCenter.z) * mapZoom;
            activeCount++;
        }

        for (int i = activeCount; i < lootMarkers.Count; i++)
        {
            if (lootMarkers[i] != null) lootMarkers[i].gameObject.SetActive(false);
        }
    }

    public void SetAlertTicker(string message, Color color, float duration = 5f)
    {
        currentAlertText = message;
        currentAlertColor = color;
        alertResetTimer = duration;

        if (tickerText != null)
        {
            tickerText.text = currentAlertText;
            tickerText.color = currentAlertColor;
        }

        if (tickerLeftAccent != null)
        {
            tickerLeftAccent.color = currentAlertColor;
        }
    }

    private void HandleRoomExplored(RoomTracker tracker)
    {
        if (tracker == null) return;

        if (tracker.isVault)
        {
            SetAlertTicker($"ОБНАРУЖЕНО ХРАНИЛИЩЕ: Сейф найден!", vaultColor, 6f);
        }
        else if (!tracker.isStartOrVan)
        {
            SetAlertTicker($"ИССЛЕДОВАНО: Сектор {tracker.roomName}", new Color(0.35f, 0.95f, 0.55f, 1f), 3f);
        }
    }

    public void ToggleMinimap()
    {
        // Не открываем карту во время паузы, магазина, терминала или выбора миссии
        if (IsAnyMenuOpen()) return;

        isMinimapVisible = !isMinimapVisible;
        if (rootPanel != null)
        {
            rootPanel.gameObject.SetActive(isMinimapVisible);
        }
        if (tickerRect != null)
        {
            tickerRect.gameObject.SetActive(isMinimapVisible);
        }
    }

    public static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
