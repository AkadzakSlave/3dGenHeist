using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class HeistUI : MonoBehaviour
{
    [Header("Old Debug HUD References")]
    public TextMeshProUGUI bagText;
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI staminaText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponText;
    public Image weaponIcon;
    public GameObject quotaMetBanner;

    [Header("Mission HUD Info")]
    public TextMeshProUGUI cityNameText;
    public UnityEngine.UI.Slider quotaProgressSlider;
    public TextMeshProUGUI quotaText; 
    public TextMeshProUGUI totalMapLootText;
    public GameObject[] difficultySkulls; 
    public TextMeshProUGUI interactionText;

    [Header("Crosshair / Reticle")]
    public GameObject reticleRoot;
    public Image reticleDot;
    public Image reticleRing;
    public Image reticleTrack;
    public Image reticleSweetSpot;
    public RectTransform reticleNeedlePivot;
    public Image reticleNeedle;
    public Color reticleIdleColor = new Color(1f, 1f, 1f, 0.75f);
    public Color reticleFocusColor = new Color(0.35f, 0.9f, 1f, 1f);
    public float reticleIdleSize = 4f;
    public float reticleFocusSize = 4.5f;
    public float reticleRingSize = 34f;
    public float reticleSmoothSpeed = 16f;

    private bool isReticleFocused = false;
    private float currentReticleSize = 4f;
    private Color currentReticleColor = new Color(1f, 1f, 1f, 0.75f);

    [Header("UI Localization/Text")]
    public string bagMoneyFormat = "Bag: ${0}";
    public string weightFormat = "Weight: {0} / {1}kg";
    public string overWeightFormat = "<color=red>Weight: {0} / {1}kg (OVERWEIGHT)</color>";
    public string quotaDayFormat = "Day {0}/3\nVan: ${1}\nProgress: ${2} / ${3}";
    public string mapLootFormat = "Осталось на карте: ${0} ({1} шт.)";
    public string staminaFormat = "Stamina: {0}/{1}";
    public string healthFormat = "HP: {0}/{1}";

    [Header("HUD Mode & F3 Switcher")]
    [Tooltip("Старый интерфейс для отладки")]
    public GameObject debugHudRoot;
    [Tooltip("Новый релизный интерфейс (Барабан револьвера, золотой хотбар)")]
    public GameObject releaseHudRoot;
    [Tooltip("Режим отладочного интерфейса (по умолчанию false -> релизный HUD)")]
    public bool isDebugHudActive = false;

    [Header("Revolver Drum Transform (Inspector Adjustable)")]
    [Tooltip("Автоматически считывать позицию и размер барабана, если вы двигаете объект RevolverDrum в Scene View мышкой (чтобы позиция не сбрасывалась при старте!)")]
    public bool autoSyncFromSceneDrum = true;
    [Tooltip("Позиция барабана от левого нижнего угла")]
    public Vector2 drumAnchoredPosition = new Vector2(175f, 175f);
    [Tooltip("Общий размер барабана в пикселях холста (увеличьте для большего размера)")]
    public float drumSize = 410f;
    [Tooltip("Расстояние от центра барабана до центра камор")]
    public float chamberRadius = 85f;
    [Tooltip("Диаметр каморы барабана (круга фона)")]
    public float chamberSize = 72f;

    [Header("Chamber Liquid Customization")]
    [Tooltip("Ширина жидкости в каморе (множитель Scale X, чтобы жидкость не сжималась в шарик)")]
    [Range(0.8f, 2.0f)]
    public float liquidScaleX = 1.18f;
    [Tooltip("Высота жидкости в каморе (множитель Scale Y)")]
    [Range(0.8f, 2.0f)]
    public float liquidScaleY = 1.0f;
    [Tooltip("Дополнительное смещение каморы здоровья (HP)")]
    public Vector2 hpChamberOffset = Vector2.zero;
    [Tooltip("Дополнительное смещение каморы выносливости (Stamina)")]
    public Vector2 staminaChamberOffset = Vector2.zero;
    [Tooltip("Минимальная видимая часть жидкости при низком ХП (чтобы мениск не исчезал совсем)")]
    [Range(0.0f, 0.2f)]
    public float liquidFillMin = 0.06f;

    [Header("Chamber Liquid Colors")]
    public Color liquidColorHealthy = new Color(0.18f, 0.90f, 0.45f, 1f); // > 40%
    public Color liquidColorWarning = new Color(0.96f, 0.80f, 0.12f, 1f); // 20% - 40%
    public Color liquidColorCritical = new Color(0.85f, 0.15f, 0.15f, 1f); // < 20%

    public static HeistUI Instance { get; private set; }
    private float reticleHitmarkerTimer = 0f;
    private bool isHeadshotHitmarker = false;


    [Header("Chamber Liquid Physics (Water / Honey Feel)")]
    [Tooltip("Вязкость жидкости (выше = как густой мёд/желе, ниже = как подвижная вода)")]
    [Range(1f, 25f)]
    public float liquidViscosity = 4.2f;
    [Tooltip("Упругость возврата мениска жидкости к горизонту")]
    [Range(5f, 120f)]
    public float liquidStiffness = 36f;
    [Tooltip("Чувствительность наклона жидкости при повороте камеры/головы")]
    [Range(0.2f, 5f)]
    public float liquidLookSensitivity = 1.9f;
    [Tooltip("Сила искажения/колебаний при спринте, прыжках и уроне")]
    [Range(0.2f, 4f)]
    public float liquidWobbleSensitivity = 1.5f;

    [Header("Revolver Drum HUD (Release)")]
    public Sprite drumSprite;
    public GameObject drumRoot;
    public Image hpLiquidImage;
    public Image hpBgImage;
    public TextMeshProUGUI hpText;
    public Image staminaLiquidImage;
    public Image staminaBgImage;
    public TextMeshProUGUI releaseTimerText;

    [Header("Drum Notifications (Floating Above Drum)")]
    public GameObject drumNotificationRoot;
    [Tooltip("Смещение контейнера уведомлений относительно верхнего края барабана")]
    public Vector2 notificationOffset = new Vector2(0f, 16f);
    [Tooltip("Высота плашки уведомления")]
    public float notificationBadgeHeight = 34f;
    [Tooltip("Минимальная ширина плашки уведомления")]
    public float notificationMinWidth = 120f;
    [Tooltip("Горизонтальный внутренний отступ текста")]
    public float notificationHorizontalPadding = 34f;
    [Tooltip("Размер шрифта текста уведомления")]
    public float notificationFontSize = 17f;
    [Tooltip("Расстояние между несколькими всплывшими уведомлениями")]
    public float notificationStackSpacing = 38f;
    [Tooltip("Длительность фазы выезда снизу (сек)")]
    public float notificationEnterDuration = 0.22f;
    [Tooltip("Длительность фазы показа текста (сек)")]
    public float notificationHoldDuration = 1.35f;
    [Tooltip("Длительность фазы уезда вверх и растворения (сек)")]
    public float notificationExitDuration = 0.38f;
    [Tooltip("Дистанция выезда снизу из-под барабана (пикс)")]
    public float notificationEnterSlideDistance = 26f;
    [Tooltip("Дистанция уезда вверх при растворении (пикс)")]
    public float notificationExitSlideDistance = 36f;

    [Tooltip("Максимальное количество одновременно видимых уведомлений на экране")]
    public int maxNotificationsOnScreen = 5;

    private class ActiveNotification
    {
        public GameObject root;
        public RectTransform rect;
        public CanvasGroup canvasGroup;
        public float targetBaseY;
        public float currentBaseY;
    }

    private readonly System.Collections.Generic.List<ActiveNotification> activeNotifications = new System.Collections.Generic.List<ActiveNotification>();
    private static Sprite cachedCapsuleSprite;
    private string lastNotificationText = "";
    private float lastNotificationTime = -10f;

    [Header("Hotbar Badges Transform (Inspector Adjustable)")]
    [Tooltip("Позиция области подсказок относительно верха панели хотбара")]
    public Vector2 hotbarHeaderPosition = new Vector2(0f, 12f);
    [Tooltip("Размер бейджа подсказки взаимодействия ([E] Взять...)")]
    public Vector2 interactionBadgeSize = new Vector2(380f, 36f);
    public float interactionFontSize = 15f;
    [Tooltip("Размер бейджа патронов / подсказки оружия")]
    public Vector2 ammoBadgeSize = new Vector2(280f, 32f);
    public float ammoFontSize = 14.5f;
    [Tooltip("Расстояние между бейджами взаимодействия и патронов")]
    public float badgesSpacing = 6f;

    [Header("Above Hotbar Badges (Release HUD)")]
    public GameObject hotbarHeaderArea;
    public GameObject interactionBadge;
    public TextMeshProUGUI releaseInteractionText;
    public GameObject ammoUsageBadge;
    public TextMeshProUGUI releaseAmmoUsageText;

    [System.Serializable]
    public class HotbarSlotUI
    {
        public GameObject slotRoot;
        public Image borderHighlight;
        public Outline borderOutline;
        public Image iconImage;
        public TextMeshProUGUI keyNumberText;
        public TextMeshProUGUI infoText;
    }

    [Header("Hotbar HUD")]
    public GameObject hotbarRoot;
    public HotbarSlotUI[] hotbarSlots = new HotbarSlotUI[3];
    public Color activeSlotColor = new Color(1.0f, 0.843f, 0.0f, 1f); // Золотой
    public Color inactiveSlotColor = new Color(0.22f, 0.23f, 0.27f, 0.75f);

    [Header("Transitions")]
    public GameObject loadingScreenPanel;

    private float timerBlinkSpeed = 5f;
    private int lastTimerSecond = -1;
    private int lastStaminaInt = -1;
    private Sprite cachedCircleSprite;

    private float targetHpRatio = 1f;
    private float currentHpRatio = 1f;
    private float targetStaminaRatio = 1f;
    private float currentStaminaRatio = 1f;
    private float hpDamageFlashTimer = 0f;
    private int lastHealthValue = 100;

    // Физика жидкости (Spring-mass damped oscillator для наклона и искажения камор)
    private float currentSloshAngle = 0f;
    private float sloshVelocity = 0f;
    private float currentWobble = 0f;
    private float wobbleVelocity = 0f;
    private float lastCamYaw = 0f;
    private float lastCamPitch = 0f;
    private bool wasPlayerGrounded = true;
    private float lastPlayerVelY = 0f;

    private void Awake()
    {
        Instance = this;

        if (weaponIcon == null && weaponText != null)
        {
            Transform parent = weaponText.transform.parent;
            if (parent != null)
            {
                Transform found = parent.Find("WeaponIcon");
                if (found != null)
                {
                    weaponIcon = found.GetComponent<Image>();
                }
            }
        }

        if (debugHudRoot == null)
        {
            Transform oldUi = transform.Find("PlayerInterfaceUi");
            if (oldUi != null)
            {
                debugHudRoot = oldUi.gameObject;
            }
        }

        EnsureReticleCreated();
        EnsureReleaseHudCreated();
        ApplyDrumLayout();
        ApplyHotbarHeaderLayout();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            EnsureReleaseHudCreated();
            ApplyDrumLayout();
            ApplyHotbarHeaderLayout();
            SetupEditorPreview();
            return;
        }

        UpdateUI();
        UpdateMissionInfo();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onMoneyChanged.AddListener(OnMoneyOrMissionChanged);
        }

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.onInventoryChanged.AddListener(UpdateUI);
        }
        
        if (quotaMetBanner != null)
        {
            quotaMetBanner.SetActive(false);
        }

        // По умолчанию активируем новый релизный HUD, а старый и FMOD оверлей скрываем
        SetDebugHudActive(false);

        // Если игра началась в Главном Меню — сразу скрываем весь HUD
        bool inMenu = (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive);
        if (inMenu)
        {
            if (releaseHudRoot != null) releaseHudRoot.SetActive(false);
            if (debugHudRoot != null) debugHudRoot.SetActive(false);
            if (reticleRoot != null) reticleRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        EnsureReleaseHudCreated();
        ApplyDrumLayout();
        ApplyHotbarHeaderLayout();

        if (!Application.isPlaying)
        {
            SetupEditorPreview();
        }
    }

    private void OnValidate()
    {
        EnsureReleaseHudCreated();
        ApplyDrumLayout();
        ApplyHotbarHeaderLayout();

        if (!Application.isPlaying)
        {
            SetupEditorPreview();
        }
    }

    [ContextMenu("Rebuild / Refresh Release HUD")]
    public void ContextMenuRebuildReleaseHud()
    {
        EnsureReleaseHudCreated();
        ApplyDrumLayout();
        ApplyHotbarHeaderLayout();
        SetupEditorPreview();
    }

    private void SetupEditorPreview()
    {
        if (Application.isPlaying) return;

        if (releaseHudRoot != null)
        {
            releaseHudRoot.SetActive(!isDebugHudActive);
        }

        if (debugHudRoot != null)
        {
            debugHudRoot.SetActive(isDebugHudActive);
        }

        // Заполняем приятные превью-данные для визуальной настройки в редакторе
        if (hpLiquidImage != null && hpLiquidImage.fillAmount == 0f)
        {
            hpLiquidImage.fillAmount = 1.0f;
        }
        if (hpText != null && (string.IsNullOrEmpty(hpText.text) || hpText.text == "100"))
        {
            hpText.text = "100";
        }
        if (staminaLiquidImage != null && staminaLiquidImage.fillAmount == 0f)
        {
            staminaLiquidImage.fillAmount = 0.85f;
        }
        if (releaseInteractionText != null && string.IsNullOrEmpty(releaseInteractionText.text))
        {
            releaseInteractionText.text = "[E] Подобрать Револьвер";
        }
        if (interactionBadge != null)
        {
            interactionBadge.SetActive(true);
        }
        if (releaseAmmoUsageText != null && string.IsNullOrEmpty(releaseAmmoUsageText.text))
        {
            releaseAmmoUsageText.text = "6 / 24";
        }
        if (ammoUsageBadge != null)
        {
            ammoUsageBadge.SetActive(true);
        }

        currentSloshAngle = 0f;
        sloshVelocity = 0f;
        currentWobble = 0f;
        wobbleVelocity = 0f;
        if (hpLiquidImage != null)
        {
            hpLiquidImage.rectTransform.localRotation = Quaternion.identity;
            hpLiquidImage.rectTransform.localScale = Vector3.one;
        }
        if (staminaLiquidImage != null)
        {
            staminaLiquidImage.rectTransform.localRotation = Quaternion.identity;
            staminaLiquidImage.rectTransform.localScale = Vector3.one;
        }

        UpdateHotbar();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onMoneyChanged.RemoveListener(OnMoneyOrMissionChanged);
        }

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.onInventoryChanged.RemoveListener(UpdateUI);
        }
    }

    private void OnMoneyOrMissionChanged()
    {
        UpdateUI();
        UpdateMissionInfo();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // Переключение интерфейса по F3 (Исключительно через New Input System без вызова устаревшего UnityEngine.Input)
        bool f3Pressed = false;
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            f3Pressed = UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame;
        }

        if (f3Pressed)
        {
            SetDebugHudActive(!isDebugHudActive);
        }

        bool isMenuOrLoading = (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive) ||
                               (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) ||
                               (loadingScreenPanel != null && loadingScreenPanel.activeSelf);

        // В меню паузы, главном меню и на экране загрузки интерфейс должен быть полностью скрыт
        if (isMenuOrLoading)
        {
            if (debugHudRoot != null && debugHudRoot.activeSelf) debugHudRoot.SetActive(false);
            if (releaseHudRoot != null && releaseHudRoot.activeSelf) releaseHudRoot.SetActive(false);
            if (reticleRoot != null && reticleRoot.activeSelf) reticleRoot.SetActive(false);
            return;
        }
        else
        {
            // Восстанавливаем видимость активного интерфейса
            if (debugHudRoot != null && debugHudRoot.activeSelf != isDebugHudActive)
            {
                debugHudRoot.SetActive(isDebugHudActive);
            }
            if (releaseHudRoot != null && releaseHudRoot.activeSelf != !isDebugHudActive)
            {
                releaseHudRoot.SetActive(!isDebugHudActive);
            }
        }

        UpdateReticle();

        // Физическая симуляция камор (Вода / Вязкий мёд с реакцией на наклоны головы, спринт, прыжки и урон)
        UpdateLiquidSimulation(Time.deltaTime);

        if (GameManager.Instance == null) return;

        bool isHeistRunning = GameManager.Instance.isHeistActive;

        // Таймер в старом дебаг интерфейсе
        if (timerText != null)
        {
            timerText.gameObject.SetActive(isHeistRunning);
        }

        // Таймер в новом релизном интерфейсе (виден ТОЛЬКО во время забега в банке)
        if (releaseTimerText != null)
        {
            releaseTimerText.gameObject.SetActive(isHeistRunning);
        }

        if (GameManager.Instance.isInLobby) return;

        // Обновление таймера (только во время активного ограбления)
        if (isHeistRunning)
        {
            float t = GameManager.Instance.heistTimer;
            int totalSeconds = Mathf.FloorToInt(t);
            if (totalSeconds != lastTimerSecond)
            {
                lastTimerSecond = totalSeconds;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                string formattedTimer = string.Format("{0:00}:{1:00}", minutes, seconds);

                if (timerText != null) timerText.text = formattedTimer;
                if (releaseTimerText != null) releaseTimerText.text = formattedTimer;
            }

            // Моргание красным, если таймер на нуле или идет штурм полиции
            bool isAssault = (EnemyManager.Instance != null && EnemyManager.Instance.isAssaultWaveActive) || (t <= 0);
            Color timerColor = isAssault
                ? Color.Lerp(Color.red, new Color(1f, 0.45f, 0.45f, 1f), (Mathf.Sin(Time.time * timerBlinkSpeed) + 1.0f) / 2.0f)
                : Color.white;

            if (timerText != null) timerText.color = timerColor;
            if (releaseTimerText != null) releaseTimerText.color = timerColor;
        }
    }

    public void SetDebugHudActive(bool debugActive)
    {
        isDebugHudActive = debugActive;

        if (debugHudRoot != null)
        {
            debugHudRoot.SetActive(debugActive);
        }

        if (releaseHudRoot != null)
        {
            releaseHudRoot.SetActive(!debugActive);
        }

        if (Application.isPlaying)
        {
            SetFMODOverlay(debugActive);
        }
    }

    public static void SetFMODOverlay(bool enable)
    {
        try
        {
            var helpers = Object.FindObjectsByType<FMODUnity.FMODRuntimeManagerOnGUIHelper>(FindObjectsInactive.Include);
            if (helpers != null)
            {
                foreach (var h in helpers)
                {
                    if (h != null)
                    {
                        h.enabled = enable;
                    }
                }
            }

            var prop = typeof(FMODUnity.RuntimeManager).GetProperty("Instance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (prop != null)
            {
                var rm = prop.GetValue(null);
                if (rm != null)
                {
                    var overlayEnabledField = typeof(FMODUnity.RuntimeManager).GetField("isOverlayEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (overlayEnabledField != null)
                    {
                        overlayEnabledField.SetValue(rm, enable);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[HeistUI] SetFMODOverlay error: {ex.Message}");
        }
    }

    public void UpdateUI()
    {
        if (GameManager.Instance == null) return;

        if (bagText != null)
        {
            bagText.text = string.Format(bagMoneyFormat, GameManager.Instance.bagMoney);
        }

        if (quotaText != null)
        {
            quotaText.text = string.Format(quotaDayFormat, GameManager.Instance.currentDay, GameManager.Instance.depositedMoney, GameManager.Instance.accumulatedOperationMoney, GameManager.Instance.operationTargetQuota);
        }

        if (weightText != null)
        {
            if (GameManager.Instance.currentWeight > GameManager.Instance.maxWeight)
            {
                weightText.text = string.Format(overWeightFormat, GameManager.Instance.currentWeight, GameManager.Instance.maxWeight);
                weightText.color = Color.red;
            }
            else
            {
                weightText.text = string.Format(weightFormat, GameManager.Instance.currentWeight, GameManager.Instance.maxWeight);
                weightText.color = Color.white;
            }
        }

        // Индикатор статуса ограбления (Стелс / Тревога / Штурм)
        if (timerText != null)
        {
            if (GameManager.Instance.isInLobby)
            {
                timerText.text = "<color=#AAAAAA>ЛОББИ</color>";
            }
            else if (!GameManager.Instance.isHeistActive)
            {
                timerText.text = "<color=#55FF55>● СТЕЛС</color>";
            }
            else if (EnemyManager.Instance != null && EnemyManager.Instance.isAssaultWaveActive)
            {
                int min = Mathf.FloorToInt(GameManager.Instance.heistTimer / 60);
                int sec = Mathf.FloorToInt(GameManager.Instance.heistTimer % 60);
                timerText.text = $"<color=#FF0000>⚠ ШТУРМ ПОЛИЦИИ {min:00}:{sec:00}</color>";
            }
            else if (GameManager.Instance.heistTimer > 0)
            {
                int min = Mathf.FloorToInt(GameManager.Instance.heistTimer / 60);
                int sec = Mathf.FloorToInt(GameManager.Instance.heistTimer % 60);
                timerText.text = $"<color=#FF4444>▲ ТРЕВОГА {min:00}:{sec:00}</color>";
            }
            else
            {
                timerText.text = "<color=#FF0000>⚠ ШТУРМ: ПОЛИЦИЯ</color>";
            }
        }

        UpdateMissionInfo();
        UpdateHotbar();
    }

    public void SetInteractionProgress(float progress, string actionName)
    {
        EnsureReticleCreated();
        if (reticleDot != null) reticleDot.gameObject.SetActive(true);
        if (reticleTrack != null)
        {
            reticleTrack.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
            reticleTrack.gameObject.SetActive(true);
        }
        if (reticleSweetSpot != null) reticleSweetSpot.gameObject.SetActive(false);
        if (reticleNeedlePivot != null) reticleNeedlePivot.gameObject.SetActive(false);

        if (reticleRing != null)
        {
            reticleRing.gameObject.SetActive(true);
            reticleRing.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
            reticleRing.type = Image.Type.Filled;
            reticleRing.fillMethod = Image.FillMethod.Radial360;
            reticleRing.fillOrigin = (int)Image.Origin360.Top;
            reticleRing.fillAmount = Mathf.Clamp01(progress);
            reticleRing.color = Color.Lerp(Color.white, new Color(0.2f, 1f, 0.45f, 1f), progress);
            reticleRing.transform.localRotation = Quaternion.identity;
        }

        SetInteractionText($"{actionName} [{Mathf.RoundToInt(progress * 100)}%]");
    }

    public void SetLockpickMode(bool active, float needleNormalizedAngle, float sweetSpotStartNorm, float sweetSpotSizeNorm, string prompt, bool flashSuccess = false, bool flashError = false)
    {
        EnsureReticleCreated();

        if (!active)
        {
            HideInteractionProgress();
            return;
        }

        float lockpickDiameter = 110f; // Увеличенный диаметр для четкой и понятной мини-игры

        if (reticleDot != null) reticleDot.gameObject.SetActive(false);

        if (reticleTrack != null)
        {
            reticleTrack.gameObject.SetActive(true);
            reticleTrack.rectTransform.sizeDelta = new Vector2(lockpickDiameter, lockpickDiameter);
            reticleTrack.color = new Color(0.12f, 0.14f, 0.18f, 0.75f);
        }

        if (reticleRing != null)
        {
            reticleRing.gameObject.SetActive(true);
            reticleRing.rectTransform.sizeDelta = new Vector2(lockpickDiameter, lockpickDiameter);
            reticleRing.fillAmount = 0f;
        }

        if (reticleSweetSpot != null)
        {
            reticleSweetSpot.gameObject.SetActive(true);
            reticleSweetSpot.rectTransform.sizeDelta = new Vector2(lockpickDiameter, lockpickDiameter);
            reticleSweetSpot.type = Image.Type.Filled;
            reticleSweetSpot.fillMethod = Image.FillMethod.Radial360;
            reticleSweetSpot.fillOrigin = (int)Image.Origin360.Top;
            reticleSweetSpot.fillAmount = Mathf.Clamp01(sweetSpotSizeNorm);
            reticleSweetSpot.transform.localRotation = Quaternion.Euler(0f, 0f, -sweetSpotStartNorm * 360f);

            Color baseGreen = new Color(0.2f, 1f, 0.45f, 0.95f);
            reticleSweetSpot.color = flashSuccess ? Color.white : baseGreen;
        }

        if (reticleNeedlePivot != null)
        {
            reticleNeedlePivot.gameObject.SetActive(true);
            reticleNeedlePivot.localRotation = Quaternion.Euler(0f, 0f, -needleNormalizedAngle * 360f);

            if (reticleNeedle != null)
            {
                reticleNeedle.rectTransform.anchoredPosition = new Vector2(0f, lockpickDiameter * 0.5f);
                reticleNeedle.rectTransform.sizeDelta = new Vector2(4.5f, 16f);
                reticleNeedle.color = flashError ? new Color(1f, 0.25f, 0.25f, 1f) : (flashSuccess ? Color.yellow : Color.white);
            }
        }

        SetInteractionText(prompt);
    }

    public void HideInteractionProgress()
    {
        if (reticleRing != null)
        {
            reticleRing.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
            reticleRing.fillAmount = 0f;
            reticleRing.color = reticleIdleColor;
        }
        if (reticleTrack != null)
        {
            reticleTrack.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
            reticleTrack.gameObject.SetActive(false);
        }
        if (reticleSweetSpot != null)
        {
            reticleSweetSpot.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
            reticleSweetSpot.gameObject.SetActive(false);
        }
        if (reticleNeedlePivot != null) reticleNeedlePivot.gameObject.SetActive(false);
        if (reticleDot != null) reticleDot.gameObject.SetActive(true);
    }

    public void UpdateMapLootInfo(int totalMapLoot, int count)
    {
        if (totalMapLootText != null)
        {
            totalMapLootText.text = string.Format(mapLootFormat, totalMapLoot, count);
        }
    }

    public void ShowLoadingScreen()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(true);
    }

    public void HideLoadingScreen()
    {
        if (loadingScreenPanel != null) loadingScreenPanel.SetActive(false);
    }

    public void UpdateStamina(float current, float max)
    {
        // 1. Старый Debug HUD
        if (staminaText != null)
        {
            int displayCurrent = Mathf.RoundToInt(current);
            int displayMax = Mathf.RoundToInt(max);
            if (displayCurrent != lastStaminaInt)
            {
                lastStaminaInt = displayCurrent;
                staminaText.text = string.Format(staminaFormat, displayCurrent, displayMax);
            }
        }

        // 2. Новый Release HUD (Левая верхняя камора: Желтая "жидкость", при тратах бледно-желтый фон)
        float newRatio = Mathf.Clamp01(current / Mathf.Max(1f, max));
        if (newRatio < targetStaminaRatio && Application.isPlaying)
        {
            wobbleVelocity += 0.05f * liquidWobbleSensitivity;
        }
        targetStaminaRatio = newRatio;

        if (!Application.isPlaying && staminaLiquidImage != null)
        {
            staminaLiquidImage.fillAmount = targetStaminaRatio;
        }
    }

    public void UpdateHealth(int current, int max)
    {
        // 1. Старый Debug HUD
        if (healthText != null)
        {
            healthText.text = string.Format(healthFormat, current, max);
            healthText.color = current <= 25 ? Color.red : Color.white;
        }

        // 2. Новый Release HUD (Верхняя камора: Зеленая "жидкость", при уроне красный фон)
        if (current < lastHealthValue && Application.isPlaying)
        {
            hpDamageFlashTimer = 0.35f;
            // Гидродинамический удар в камору ХП при получении урона: резкий всплеск и вибрация
            sloshVelocity += UnityEngine.Random.Range(-38f, 38f) * liquidLookSensitivity;
            wobbleVelocity += 0.52f * liquidWobbleSensitivity;
        }
        lastHealthValue = current;

        targetHpRatio = Mathf.Clamp01((float)current / Mathf.Max(1, max));
        if (!Application.isPlaying && hpLiquidImage != null)
        {
            hpLiquidImage.fillAmount = targetHpRatio;
        }

        if (hpText != null)
        {
            hpText.text = current.ToString();
            hpText.color = current <= 25 ? new Color(1f, 0.4f, 0.4f, 1f) : Color.white;
        }
    }

    /// <summary>
    /// Физическая симуляция камор (Вода / Вязкий мёд с реакцией на наклоны головы, спринт, прыжки и урон)
    /// </summary>
    private void UpdateLiquidSimulation(float dt)
    {
        if (dt <= 0f) return;
        if (dt > 0.08f) dt = 0.08f; // Защита от резких лагов

        float sloshTorque = 0f;
        float wobbleForce = 0f;

        // 1. Поворот головы / наклон камеры
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            float curYaw = cam.eulerAngles.y;
            float curPitch = cam.eulerAngles.x;

            if (lastCamYaw != 0f || lastCamPitch != 0f)
            {
                float yawDelta = Mathf.DeltaAngle(lastCamYaw, curYaw);
                float pitchDelta = Mathf.DeltaAngle(lastCamPitch, curPitch);

                // Инерция: при повороте вправо поверхность жидкости отклоняется влево
                sloshTorque += (-yawDelta * liquidLookSensitivity * 4.5f);
                wobbleForce += (Mathf.Abs(pitchDelta) * liquidWobbleSensitivity * 0.45f);
            }

            lastCamYaw = curYaw;
            lastCamPitch = curPitch;
        }

        // 2. Движение игрока, прыжки, приземление и спринт
        var pm = PlayerMovement.Instance;
        if (pm != null)
        {
            Vector3 vel = pm.Velocity;
            Vector3 localVel = pm.transform.InverseTransformDirection(vel);

            // Усиленная инерция при стрейфе влево/вправо (localVel.x): жидкость заметно колышется и наклоняется
            sloshTorque += (-localVel.x * liquidLookSensitivity * 4.2f);
            wobbleForce += (Mathf.Abs(localVel.x) * liquidWobbleSensitivity * 0.45f);

            bool isGrounded = pm.IsGrounded;
            if (!wasPlayerGrounded && isGrounded)
            {
                // Приземление: энергичный удар жидкости вниз с последующим отскоком и всплеском
                float impactSpeed = Mathf.Max(2.5f, Mathf.Abs(lastPlayerVelY));
                wobbleVelocity -= Mathf.Clamp(impactSpeed * 0.12f, 0.45f, 1.25f) * liquidWobbleSensitivity;
                sloshVelocity += UnityEngine.Random.Range(-30f, 30f) * liquidLookSensitivity;
            }
            else if (wasPlayerGrounded && !isGrounded && vel.y > 0.5f)
            {
                // Прыжок: инерция в первый миг взлета резко вдавливает жидкость вниз
                wobbleVelocity -= 0.52f * liquidWobbleSensitivity;
                sloshVelocity += UnityEngine.Random.Range(-16f, 16f) * liquidLookSensitivity;
            }
            else if (!isGrounded)
            {
                // В воздухе: при снижении скорости и падении жидкость плавно подвсплывает и раскачивается
                wobbleForce += (vel.y < 0f ? 0.95f : -0.35f) * liquidWobbleSensitivity;
            }
            wasPlayerGrounded = isGrounded;
            lastPlayerVelY = vel.y;

            // Спринт: ритмичная раскачка жидкости с каждым шагом
            if (pm.IsSprinting && pm.IsMoving && isGrounded)
            {
                float sprintCadence = Time.time * 13.5f;
                sloshTorque += Mathf.Sin(sprintCadence) * 16f * liquidWobbleSensitivity;
                wobbleForce += Mathf.Abs(Mathf.Sin(sprintCadence)) * 4.5f * liquidWobbleSensitivity;
            }
            else if (pm.IsMoving && isGrounded)
            {
                float walkCadence = Time.time * 8.5f;
                sloshTorque += Mathf.Sin(walkCadence) * 4.5f * liquidWobbleSensitivity;
            }
        }

        // 3. Естественное микро-дыхание покоя (жидкость всегда живая, колышется)
        float idleWave = Mathf.Sin(Time.time * 2.2f) * 1.5f;
        float idleWobble = Mathf.Cos(Time.time * 1.7f) * 0.02f;

        // 4. Пружинный маятник наклона жидкости (Slosh)
        float sloshSpring = -liquidStiffness * (currentSloshAngle - idleWave) - (liquidViscosity * sloshVelocity);
        sloshVelocity += (sloshSpring + sloshTorque) * dt;
        currentSloshAngle += sloshVelocity * dt;
        currentSloshAngle = Mathf.Clamp(currentSloshAngle, -45f, 45f);

        // 5. Пружинный маятник деформации объема (Wobble: вязкое сжатие/растяжение)
        float wobbleSpring = -liquidStiffness * (currentWobble - idleWobble) - ((liquidViscosity * 1.25f) * wobbleVelocity);
        wobbleVelocity += (wobbleSpring + wobbleForce) * dt;
        currentWobble += wobbleVelocity * dt;
        currentWobble = Mathf.Clamp(currentWobble, -0.25f, 0.25f);

        // 6. Сердечная пульсация при критическом здоровье (< 20%)
        float heartBeat = 0f;
        if (currentHpRatio < 0.20f)
        {
            float rate = Mathf.Lerp(7.5f, 4.5f, currentHpRatio / 0.20f);
            float ht = Time.time * rate;
            float b1 = Mathf.Clamp01(Mathf.Sin(ht) * 2.8f - 1.4f);
            float b2 = Mathf.Clamp01(Mathf.Sin(ht - 0.65f) * 2.8f - 1.4f) * 0.65f;
            heartBeat = Mathf.Clamp01(b1 + b2);
        }

        // 7. Применение физики и пульсации к каморе HP
        if (hpLiquidImage != null)
        {
            hpLiquidImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentSloshAngle);
            Vector3 scale = new Vector3(liquidScaleX, liquidScaleY + currentWobble * 0.75f, 1f);
            if (heartBeat > 0f)
            {
                scale *= (1f + heartBeat * 0.14f);
            }
            hpLiquidImage.rectTransform.localScale = scale;

            // Вязкое изменение уровня ХП (как густой мёд) с динамической волной поверхности
            currentHpRatio = Mathf.Lerp(currentHpRatio, targetHpRatio, dt * 7f);
            float visualHpRatio = currentHpRatio > 0.001f ? Mathf.Lerp(liquidFillMin, 1.0f, currentHpRatio) : 0f;
            hpLiquidImage.fillAmount = Mathf.Clamp01(visualHpRatio + (currentWobble * 0.08f));

            Color critLiquidColor = Color.Lerp(liquidColorCritical, new Color(1.0f, 0.35f, 0.35f, 1f), heartBeat);

            if (currentHpRatio > 0.40f)
            {
                float tGreen = Mathf.Clamp01((currentHpRatio - 0.40f) / 0.60f);
                hpLiquidImage.color = Color.Lerp(liquidColorWarning, liquidColorHealthy, tGreen);
            }
            else if (currentHpRatio >= 0.20f)
            {
                float tYellow = Mathf.Clamp01((currentHpRatio - 0.20f) / 0.20f);
                hpLiquidImage.color = Color.Lerp(critLiquidColor, liquidColorWarning, tYellow);
            }
            else
            {
                hpLiquidImage.color = critLiquidColor;
            }
        }

        // Пульсация фона каморы HP при уроне или критическом здоровье
        if (hpBgImage != null)
        {
            if (hpDamageFlashTimer > 0f)
            {
                hpDamageFlashTimer -= dt;
                hpBgImage.color = Color.Lerp(new Color(0.28f, 0.08f, 0.08f, 0.9f), new Color(0.95f, 0.15f, 0.15f, 1f), hpDamageFlashTimer * 2.8f);
            }
            else if (heartBeat > 0f)
            {
                hpBgImage.color = Color.Lerp(new Color(0.25f, 0.06f, 0.06f, 0.95f), new Color(0.85f, 0.12f, 0.12f, 1f), heartBeat);
            }
            else
            {
                hpBgImage.color = new Color(0.28f, 0.08f, 0.08f, 0.9f);
            }
        }

        // Пульсация текста ХП при критическом здоровье
        if (hpText != null)
        {
            if (heartBeat > 0f)
            {
                hpText.rectTransform.localScale = Vector3.one * (1f + heartBeat * 0.15f);
                hpText.color = Color.Lerp(new Color(1f, 0.4f, 0.4f, 1f), Color.white, heartBeat);
            }
            else
            {
                hpText.rectTransform.localScale = Vector3.one;
            }
        }

        // 8. Применение физики к каморе Выносливости (более подвижная реакция)
        if (staminaLiquidImage != null)
        {
            float stamSlosh = currentSloshAngle * 1.18f;
            float stamWobble = currentWobble * 1.25f;
            staminaLiquidImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, stamSlosh);
            staminaLiquidImage.rectTransform.localScale = new Vector3(liquidScaleX, liquidScaleY + stamWobble * 0.75f, 1f);

            currentStaminaRatio = Mathf.Lerp(currentStaminaRatio, targetStaminaRatio, dt * 9f);
            float visualStamRatio = currentStaminaRatio > 0.001f ? Mathf.Lerp(liquidFillMin, 1.0f, currentStaminaRatio) : 0f;
            staminaLiquidImage.fillAmount = Mathf.Clamp01(visualStamRatio + (stamWobble * 0.07f));

            // Если выносливость на исходе (< 20%) — тревожная янтарная пульсация
            if (currentStaminaRatio < 0.20f)
            {
                float stamBlink = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                staminaLiquidImage.color = Color.Lerp(new Color(1.0f, 0.83f, 0.15f, 1f), new Color(1.0f, 0.45f, 0.1f, 1f), stamBlink);
            }
            else
            {
                staminaLiquidImage.color = new Color(1.0f, 0.83f, 0.15f, 1f);
            }
        }
    }

    public void UpdateWeapon(string text)
    {
        UpdateWeapon(text, null);
    }

    public void UpdateWeapon(string text, Sprite icon)
    {
        // 1. Старый Debug HUD
        if (weaponText != null)
        {
            weaponText.text = text;
            weaponText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        if (weaponIcon != null)
        {
            if (icon != null)
            {
                weaponIcon.sprite = icon;
                weaponIcon.gameObject.SetActive(true);
            }
            else
            {
                weaponIcon.gameObject.SetActive(false);
            }
        }

        // 2. Новый Release HUD: Вывод патронов и подсказки оружия НАД Инвентарем
        if (releaseAmmoUsageText != null)
        {
            releaseAmmoUsageText.text = text;
            if (ammoUsageBadge != null)
            {
                ammoUsageBadge.SetActive(!string.IsNullOrEmpty(text));
            }
        }
    }

    private void ShowQuotaMetBanner()
    {
        if (quotaMetBanner != null)
        {
            quotaMetBanner.SetActive(true);
        }
    }

    private void UpdateMissionInfo()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.activeOperationPreset != null && gm.activeOperationPreset.cities != null && gm.activeOperationPreset.cities.Count > 0)
        {
            int dayIndex = Mathf.Clamp(gm.currentDay - 1, 0, gm.activeOperationPreset.cities.Count - 1);
            string currentCity = gm.activeOperationPreset.cities[dayIndex].cityName;
            if (cityNameText != null) cityNameText.text = $"{gm.activeOperationPreset.levelName}: {currentCity}";
        }

        int currentTotal = gm.accumulatedOperationMoney + gm.depositedMoney;
        if (quotaProgressSlider != null)
        {
            quotaProgressSlider.maxValue = Mathf.Max(1, gm.operationTargetQuota);
            quotaProgressSlider.value = currentTotal;
        }
        if (quotaText != null) 
        {
            quotaText.text = $"${currentTotal} / ${gm.operationTargetQuota}";
        }

        int difficulty = 1;
        if (gm.selectedDossier != null) difficulty = gm.selectedDossier.difficultyLevel;
        UpdateSkulls(difficulty); 
    }

    private void UpdateSkulls(int level)
    {
        if (difficultySkulls == null || difficultySkulls.Length == 0) return;

        for (int i = 0; i < difficultySkulls.Length; i++)
        {
            if (difficultySkulls[i] != null)
                difficultySkulls[i].SetActive(i < level);
        }
    }

    public void SetInteractionText(string text)
    {
        // 1. Старый Debug HUD
        if (interactionText != null)
        {
            interactionText.text = text;
            interactionText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // 2. Новый Release HUD: Вывод подсказок об использовании НАД Инвентарем
        if (releaseInteractionText != null)
        {
            releaseInteractionText.text = text;
            if (interactionBadge != null)
            {
                interactionBadge.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        SetReticleFocus(!string.IsNullOrEmpty(text));
    }

    public void SetReticleFocus(bool focused)
    {
        isReticleFocused = focused;
    }

    private void UpdateReticle()
    {
        if (reticleDot == null || reticleRing == null)
        {
            EnsureReticleCreated();
            if (reticleDot == null) return;
        }

        bool isMenuOpen = (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive) ||
                          (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused);

        if (reticleRoot != null)
        {
            if (isMenuOpen && reticleRoot.activeSelf)
            {
                reticleRoot.SetActive(false);
                return;
            }
            else if (!isMenuOpen && !reticleRoot.activeSelf)
            {
                reticleRoot.SetActive(true);
            }
        }

        // Center dot: remains a crisp, small dot (4px idle -> 4.5px focus)
        float targetSize = isReticleFocused ? reticleFocusSize : reticleIdleSize;
        Color targetColor = isReticleFocused ? reticleFocusColor : reticleIdleColor;

        if (reticleHitmarkerTimer > 0f)
        {
            reticleHitmarkerTimer -= Time.deltaTime;
            targetColor = isHeadshotHitmarker ? new Color(1f, 0.12f, 0.12f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
            targetSize = isHeadshotHitmarker ? reticleFocusSize * 2.2f : reticleFocusSize * 1.6f;
        }

        currentReticleSize = Mathf.Lerp(currentReticleSize, targetSize, Time.deltaTime * reticleSmoothSpeed);
        currentReticleColor = Color.Lerp(currentReticleColor, targetColor, Time.deltaTime * reticleSmoothSpeed);

        reticleDot.rectTransform.sizeDelta = new Vector2(currentReticleSize, currentReticleSize);
        reticleDot.color = currentReticleColor;

        // Outer focus ring: expands around the dot and fades in when looking at an interactable
        if (reticleRing != null)
        {
            if (reticleRing.type == Image.Type.Filled && reticleRing.fillAmount > 0.001f)
            {
                // Active hold progress interaction (e.g. turning safe valve or drop-off)! Keep fill progress visible
                return;
            }

            if (reticleRing.sprite == null)
            {
                reticleRing.sprite = CreateRingSprite(64, 3);
            }

            reticleRing.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);

            float ringTargetAlpha = isReticleFocused ? 0.95f : 0f;
            Color ringC = reticleFocusColor;
            ringC.a = Mathf.Lerp(reticleRing.color.a, ringTargetAlpha, Time.deltaTime * reticleSmoothSpeed);
            reticleRing.color = ringC;

            float ringTargetScale = isReticleFocused ? 1.0f : 0.45f;
            reticleRing.rectTransform.localScale = Vector3.Lerp(reticleRing.rectTransform.localScale, Vector3.one * ringTargetScale, Time.deltaTime * reticleSmoothSpeed);
        }
    }

    private void EnsureReticleCreated()
    {
        if (reticleRoot == null)
        {
            Transform existing = transform.Find("ReticleRoot");
            if (existing != null)
            {
                reticleRoot = existing.gameObject;
            }
            else
            {
                GameObject rRoot = new GameObject("ReticleRoot", typeof(RectTransform));
                rRoot.transform.SetParent(transform, false);
                RectTransform rRt = rRoot.GetComponent<RectTransform>();
                rRt.anchorMin = rRt.anchorMax = rRt.pivot = new Vector2(0.5f, 0.5f);
                rRt.anchoredPosition = Vector2.zero;
                rRt.sizeDelta = new Vector2(48f, 48f);
                reticleRoot = rRoot;
            }
        }

        // Ensure ReticleRing exists
        if (reticleRing == null && reticleRoot != null)
        {
            Transform existingRing = reticleRoot.transform.Find("ReticleRing");
            if (existingRing != null)
            {
                reticleRing = existingRing.GetComponent<Image>();
            }
            else
            {
                GameObject ringObj = new GameObject("ReticleRing", typeof(RectTransform), typeof(Image));
                ringObj.transform.SetParent(reticleRoot.transform, false);
                ringObj.transform.SetAsFirstSibling();
                RectTransform ringRt = ringObj.GetComponent<RectTransform>();
                ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
                ringRt.anchoredPosition = Vector2.zero;
                ringRt.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
                reticleRing = ringObj.GetComponent<Image>();
                reticleRing.sprite = CreateRingSprite(64, 3);
                reticleRing.color = new Color(reticleFocusColor.r, reticleFocusColor.g, reticleFocusColor.b, 0f);
                reticleRing.raycastTarget = false;
            }
        }

        if (reticleRing != null)
        {
            if (reticleRing.sprite == null)
            {
                reticleRing.sprite = CreateRingSprite(64, 3);
            }
            reticleRing.rectTransform.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
        }

        // Ensure ReticleTrack (dark background track) exists
        if (reticleTrack == null && reticleRoot != null)
        {
            Transform existingTrack = reticleRoot.transform.Find("ReticleTrack");
            if (existingTrack != null)
            {
                reticleTrack = existingTrack.GetComponent<Image>();
            }
            else
            {
                GameObject trackObj = new GameObject("ReticleTrack", typeof(RectTransform), typeof(Image));
                trackObj.transform.SetParent(reticleRoot.transform, false);
                trackObj.transform.SetAsFirstSibling();
                RectTransform trackRt = trackObj.GetComponent<RectTransform>();
                trackRt.anchorMin = trackRt.anchorMax = trackRt.pivot = new Vector2(0.5f, 0.5f);
                trackRt.anchoredPosition = Vector2.zero;
                trackRt.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
                reticleTrack = trackObj.GetComponent<Image>();
                reticleTrack.sprite = CreateRingSprite(128, 8);
                reticleTrack.color = new Color(0.08f, 0.08f, 0.12f, 0.55f);
                reticleTrack.raycastTarget = false;
                reticleTrack.gameObject.SetActive(false);
            }
        }

        // Ensure ReticleSweetSpot exists
        if (reticleSweetSpot == null && reticleRoot != null)
        {
            Transform existingSpot = reticleRoot.transform.Find("ReticleSweetSpot");
            if (existingSpot != null)
            {
                reticleSweetSpot = existingSpot.GetComponent<Image>();
            }
            else
            {
                GameObject spotObj = new GameObject("ReticleSweetSpot", typeof(RectTransform), typeof(Image));
                spotObj.transform.SetParent(reticleRoot.transform, false);
                RectTransform spotRt = spotObj.GetComponent<RectTransform>();
                spotRt.anchorMin = spotRt.anchorMax = spotRt.pivot = new Vector2(0.5f, 0.5f);
                spotRt.anchoredPosition = Vector2.zero;
                spotRt.sizeDelta = new Vector2(reticleRingSize, reticleRingSize);
                reticleSweetSpot = spotObj.GetComponent<Image>();
                reticleSweetSpot.sprite = CreateRingSprite(128, 8);
                reticleSweetSpot.type = Image.Type.Filled;
                reticleSweetSpot.fillMethod = Image.FillMethod.Radial360;
                reticleSweetSpot.fillOrigin = (int)Image.Origin360.Top;
                reticleSweetSpot.color = new Color(0.2f, 1f, 0.45f, 0.95f);
                reticleSweetSpot.raycastTarget = false;
                reticleSweetSpot.gameObject.SetActive(false);
            }
        }

        // Ensure ReticleNeedlePivot exists
        if (reticleNeedlePivot == null && reticleRoot != null)
        {
            Transform existingPivot = reticleRoot.transform.Find("ReticleNeedlePivot");
            if (existingPivot != null)
            {
                reticleNeedlePivot = existingPivot.GetComponent<RectTransform>();
                reticleNeedle = existingPivot.GetComponentInChildren<Image>();
            }
            else
            {
                GameObject pivotObj = new GameObject("ReticleNeedlePivot", typeof(RectTransform));
                pivotObj.transform.SetParent(reticleRoot.transform, false);
                reticleNeedlePivot = pivotObj.GetComponent<RectTransform>();
                reticleNeedlePivot.anchorMin = reticleNeedlePivot.anchorMax = reticleNeedlePivot.pivot = new Vector2(0.5f, 0.5f);
                reticleNeedlePivot.anchoredPosition = Vector2.zero;
                reticleNeedlePivot.sizeDelta = Vector2.zero;

                GameObject needleObj = new GameObject("Needle", typeof(RectTransform), typeof(Image));
                needleObj.transform.SetParent(pivotObj.transform, false);
                RectTransform nRt = needleObj.GetComponent<RectTransform>();
                nRt.anchorMin = nRt.anchorMax = nRt.pivot = new Vector2(0.5f, 0.5f);
                nRt.anchoredPosition = new Vector2(0f, reticleRingSize * 0.5f);
                nRt.sizeDelta = new Vector2(3.5f, 8f);
                reticleNeedle = needleObj.GetComponent<Image>();
                reticleNeedle.sprite = GetOrCreateCircleSprite();
                reticleNeedle.color = Color.white;
                reticleNeedle.raycastTarget = false;

                reticleNeedlePivot.gameObject.SetActive(false);
            }
        }

        // Ensure ReticleDot exists
        if (reticleDot == null && reticleRoot != null)
        {
            Transform existingDot = reticleRoot.transform.Find("ReticleDot");
            if (existingDot != null)
            {
                reticleDot = existingDot.GetComponent<Image>();
            }
            else
            {
                GameObject dotObj = new GameObject("ReticleDot", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(reticleRoot.transform, false);
                RectTransform dotRt = dotObj.GetComponent<RectTransform>();
                dotRt.anchorMin = dotRt.anchorMax = dotRt.pivot = new Vector2(0.5f, 0.5f);
                dotRt.anchoredPosition = Vector2.zero;
                dotRt.sizeDelta = new Vector2(reticleIdleSize, reticleIdleSize);
                reticleDot = dotObj.GetComponent<Image>();
                reticleDot.sprite = GetOrCreateCircleSprite();
                reticleDot.color = reticleIdleColor;
                reticleDot.raycastTarget = false;
            }
        }

        if (reticleDot != null && reticleDot.sprite == null)
        {
            reticleDot.sprite = GetOrCreateCircleSprite();
        }
    }

    /// <summary>
    /// Вызывается при попадании по врагу для сочного хитмаркер-отклика прицела
    /// </summary>
    public void TriggerHitmarker(bool isHeadshot = false)
    {
        isHeadshotHitmarker = isHeadshot;
        reticleHitmarkerTimer = isHeadshot ? 0.22f : 0.14f;
    }

    private Sprite GetOrCreateCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;
        cachedCircleSprite = CreateCircleSprite(64);
        return cachedCircleSprite;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float radius = center;
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - dist) + 0.5f);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateRingSprite(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        float outerRadius = center;
        float innerRadius = outerRadius - thickness;
        Color[] colors = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float outerAlpha = Mathf.Clamp01((outerRadius - dist) + 0.5f);
                float innerAlpha = Mathf.Clamp01((dist - innerRadius) + 0.5f);
                float alpha = Mathf.Min(outerAlpha, innerAlpha);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Создает или находит новый релизный интерфейс (Барабан револьвера, золотой хотбар с подсказками, таймер)
    /// </summary>
    public void EnsureReleaseHudCreated()
    {
        Transform existingRel = transform.Find("ReleaseHUD");
        if (existingRel != null)
        {
            releaseHudRoot = existingRel.gameObject;
        }
        else
        {
            GameObject relObj = new GameObject("ReleaseHUD", typeof(RectTransform));
            relObj.transform.SetParent(transform, false);
            RectTransform relRt = relObj.GetComponent<RectTransform>();
            relRt.anchorMin = Vector2.zero;
            relRt.anchorMax = Vector2.one;
            relRt.sizeDelta = Vector2.zero;
            relRt.anchoredPosition = Vector2.zero;
            releaseHudRoot = relObj;
        }

        Sprite circleSprite = GetOrCreateCircleSprite();

        if (drumSprite == null)
        {
            drumSprite = Resources.Load<Sprite>("UIbaraban");
        }

        // ==================== 1. БАРАБАН РЕВОЛЬВЕРА (Внизу Слева) ====================
        Transform existingDrum = releaseHudRoot.transform.Find("RevolverDrum");
        if (existingDrum == null)
        {
            GameObject drumObj = new GameObject("RevolverDrum", typeof(RectTransform));
            drumObj.transform.SetParent(releaseHudRoot.transform, false);
            RectTransform drumRt = drumObj.GetComponent<RectTransform>();
            drumRt.anchorMin = new Vector2(0f, 0f);
            drumRt.anchorMax = new Vector2(0f, 0f);
            drumRt.pivot = new Vector2(0.5f, 0.5f);
            drumRt.anchoredPosition = drumAnchoredPosition;
            drumRt.sizeDelta = new Vector2(drumSize, drumSize);
            drumRoot = drumObj;

            // Слой камор (Жидкости и фоны позади стального каркаса)
            GameObject chambersLayer = new GameObject("ChambersLayer", typeof(RectTransform));
            chambersLayer.transform.SetParent(drumObj.transform, false);
            RectTransform chLayRt = chambersLayer.GetComponent<RectTransform>();
            chLayRt.anchorMin = Vector2.zero;
            chLayRt.anchorMax = Vector2.one;
            chLayRt.sizeDelta = Vector2.zero;

            float S = drumSize / 813f;
            Vector2 posHp = new Vector2(8.1f * S, 167.3f * S);
            Vector2 posAmmo = new Vector2(165.3f * S, 79.0f * S);
            Vector2 posBR = new Vector2(165.6f * S, -100.9f * S);
            Vector2 posB = new Vector2(8.2f * S, -194.7f * S);
            Vector2 posBL = new Vector2(-150.0f * S, -101.0f * S);
            Vector2 posStamina = new Vector2(-149.5f * S, 78.8f * S);

            // КАМОРА ХП: Красный фон (при уроне) + Зеленая "жидкость"
            GameObject hpSocket = CreateChamberSocket(chambersLayer.transform, "Socket_HP", posHp, chamberSize);
            hpBgImage = CreateChamberBg(hpSocket.transform, circleSprite, new Color(0.55f, 0.08f, 0.08f, 1f));
            hpLiquidImage = CreateChamberLiquid(hpSocket.transform, circleSprite, new Color(0.18f, 0.85f, 0.45f, 1f));

            // КАМОРА ВЫНОСЛИВОСТИ: Бледно-желтый темный фон + Ярко-желтая "жидкость"
            GameObject stamSocket = CreateChamberSocket(chambersLayer.transform, "Socket_Stamina", posStamina, chamberSize);
            staminaBgImage = CreateChamberBg(stamSocket.transform, circleSprite, new Color(0.28f, 0.25f, 0.08f, 1f));
            staminaLiquidImage = CreateChamberLiquid(stamSocket.transform, circleSprite, new Color(1.0f, 0.83f, 0.15f, 1f));

            // ОСТАЛЬНЫЕ КАМОРЫ: Темные полости для будущих параметров
            CreateChamberBg(CreateChamberSocket(chambersLayer.transform, "Socket_Ammo", posAmmo, chamberSize).transform, circleSprite, new Color(0.08f, 0.08f, 0.1f, 0.95f));
            CreateChamberBg(CreateChamberSocket(chambersLayer.transform, "Socket_Empty_BR", posBR, chamberSize).transform, circleSprite, new Color(0.06f, 0.06f, 0.08f, 0.9f));
            CreateChamberBg(CreateChamberSocket(chambersLayer.transform, "Socket_Empty_B", posB, chamberSize).transform, circleSprite, new Color(0.06f, 0.06f, 0.08f, 0.9f));
            CreateChamberBg(CreateChamberSocket(chambersLayer.transform, "Socket_Empty_BL", posBL, chamberSize).transform, circleSprite, new Color(0.06f, 0.06f, 0.08f, 0.9f));

            // СТАЛЬНОЙ КАРКАС БАРАБАНА (UIbaraban.png лежит ПОВЕРХ жидкостей)
            GameObject frameObj = new GameObject("SteelDrumFrame", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(drumObj.transform, false);
            RectTransform frameRt = frameObj.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.sizeDelta = Vector2.zero;

            Image frameImg = frameObj.GetComponent<Image>();
            frameImg.sprite = drumSprite;
            frameImg.color = Color.white;
            frameImg.preserveAspect = true;
            frameImg.raycastTarget = false;

            // ТЕКСТ ХП (поверх барабана с авто-масштабированием под 100/1000)
            GameObject labelsLayer = new GameObject("LabelsLayer", typeof(RectTransform));
            labelsLayer.transform.SetParent(drumObj.transform, false);
            RectTransform lblLayRt = labelsLayer.GetComponent<RectTransform>();
            lblLayRt.anchorMin = Vector2.zero;
            lblLayRt.anchorMax = Vector2.one;
            lblLayRt.sizeDelta = Vector2.zero;

            hpText = CreateChamberText(labelsLayer.transform, "HP_Text", posHp, "100", 22f);
        }
        else
        {
            drumRoot = existingDrum.gameObject;
            Transform chLayer = drumRoot.transform.Find("ChambersLayer");
            if (chLayer != null)
            {
                Transform hpSock = chLayer.Find("Socket_HP");
                if (hpSock != null)
                {
                    hpBgImage = hpSock.Find("Bg")?.GetComponent<Image>();
                    hpLiquidImage = hpSock.Find("Liquid")?.GetComponent<Image>();
                }
                Transform stamSock = chLayer.Find("Socket_Stamina");
                if (stamSock != null)
                {
                    staminaBgImage = stamSock.Find("Bg")?.GetComponent<Image>();
                    staminaLiquidImage = stamSock.Find("Liquid")?.GetComponent<Image>();
                }
            }
            Transform lblLayer = drumRoot.transform.Find("LabelsLayer");
            if (lblLayer != null)
            {
                hpText = lblLayer.Find("HP_Text")?.GetComponent<TextMeshProUGUI>();
            }
            ApplyDrumLayout();
        }

        // ==================== 2. ХОТБАР И ПОДСКАЗКИ НАД НИМ (Внизу по Центру) ====================
        Transform existingHb = releaseHudRoot.transform.Find("Hotbar");
        if (existingHb == null)
        {
            GameObject hbObj = new GameObject("Hotbar", typeof(RectTransform));
            hbObj.transform.SetParent(releaseHudRoot.transform, false);
            RectTransform hbRt = hbObj.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0f);
            hbRt.anchorMax = new Vector2(0.5f, 0f);
            hbRt.pivot = new Vector2(0.5f, 0f);
            hbRt.anchoredPosition = new Vector2(0f, 32f);
            hbRt.sizeDelta = new Vector2(250f, 72f);
            hotbarRoot = hbObj;

            // --- ЗОНА ПОДСКАЗОК НАД ИНВЕНТАРЕМ ---
            GameObject headerObj = new GameObject("HotbarHeaderArea", typeof(RectTransform));
            headerObj.transform.SetParent(hbObj.transform, false);
            RectTransform headerRt = headerObj.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 0f);
            headerRt.anchoredPosition = hotbarHeaderPosition;
            headerRt.sizeDelta = new Vector2(Mathf.Max(interactionBadgeSize.x, ammoBadgeSize.x), 74f);
            hotbarHeaderArea = headerObj;

            // 1. Бейдж подсказки взаимодействия ([E] Подобрать предмет)
            GameObject interactBadgeObj = new GameObject("InteractionBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            interactBadgeObj.transform.SetParent(headerObj.transform, false);
            RectTransform ibRt = interactBadgeObj.GetComponent<RectTransform>();
            ibRt.anchorMin = new Vector2(0.5f, 0.5f);
            ibRt.anchorMax = new Vector2(0.5f, 0.5f);
            ibRt.pivot = new Vector2(0.5f, 0.5f);
            ibRt.sizeDelta = interactionBadgeSize;

            Image ibImg = interactBadgeObj.GetComponent<Image>();
            ibImg.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            Outline ibOutline = interactBadgeObj.GetComponent<Outline>();
            ibOutline.effectColor = new Color(0.35f, 0.85f, 1f, 0.5f);
            ibOutline.effectDistance = new Vector2(1f, -1f);
            interactionBadge = interactBadgeObj;

            GameObject ibTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            ibTextObj.transform.SetParent(interactBadgeObj.transform, false);
            RectTransform ibtRt = ibTextObj.GetComponent<RectTransform>();
            ibtRt.anchorMin = Vector2.zero;
            ibtRt.anchorMax = Vector2.one;
            ibtRt.sizeDelta = Vector2.zero;

            releaseInteractionText = ibTextObj.GetComponent<TextMeshProUGUI>();
            releaseInteractionText.alignment = TextAlignmentOptions.Center;
            releaseInteractionText.fontSize = interactionFontSize;
            releaseInteractionText.fontStyle = FontStyles.Bold;
            releaseInteractionText.color = new Color(0.92f, 0.97f, 1f, 1f);
            releaseInteractionText.text = "";
            interactionBadge.SetActive(false);

            // 2. Бейдж патронов и подсказок оружия / инструментов
            GameObject ammoBadgeObj = new GameObject("AmmoUsageBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            ammoBadgeObj.transform.SetParent(headerObj.transform, false);
            RectTransform abRt = ammoBadgeObj.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0.5f, 0.5f);
            abRt.anchorMax = new Vector2(0.5f, 0.5f);
            abRt.pivot = new Vector2(0.5f, 0.5f);
            abRt.sizeDelta = ammoBadgeSize;

            Image abImg = ammoBadgeObj.GetComponent<Image>();
            abImg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            Outline abOutline = ammoBadgeObj.GetComponent<Outline>();
            abOutline.effectColor = new Color(1.0f, 0.843f, 0f, 0.45f);
            abOutline.effectDistance = new Vector2(1f, -1f);
            ammoUsageBadge = ammoBadgeObj;

            GameObject abTextObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            abTextObj.transform.SetParent(ammoBadgeObj.transform, false);
            RectTransform abtRt = abTextObj.GetComponent<RectTransform>();
            abtRt.anchorMin = Vector2.zero;
            abtRt.anchorMax = Vector2.one;
            abtRt.sizeDelta = Vector2.zero;

            releaseAmmoUsageText = abTextObj.GetComponent<TextMeshProUGUI>();
            releaseAmmoUsageText.alignment = TextAlignmentOptions.Center;
            releaseAmmoUsageText.fontSize = ammoFontSize;
            releaseAmmoUsageText.fontStyle = FontStyles.Bold;
            releaseAmmoUsageText.color = Color.white;
            releaseAmmoUsageText.text = "";
            ammoUsageBadge.SetActive(false);

            // --- 3 СЛОТА ИНВЕНТАРЯ ---
            hotbarSlots = new HotbarSlotUI[3];
            string[] keyLabels = new string[] { "[1]", "[2]", "[3]" };
            float[] xOffsets = new float[] { -82f, 0f, 82f };

            for (int i = 0; i < 3; i++)
            {
                hotbarSlots[i] = new HotbarSlotUI();

                GameObject slotObj = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image), typeof(Outline));
                slotObj.transform.SetParent(hbObj.transform, false);
                RectTransform slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = slotRt.anchorMax = slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(xOffsets[i], 0f);
                slotRt.sizeDelta = new Vector2(66f, 66f);

                Image slotBg = slotObj.GetComponent<Image>();
                slotBg.color = new Color(0.08f, 0.08f, 0.11f, 0.9f);
                hotbarSlots[i].slotRoot = slotObj;

                Outline outline = slotObj.GetComponent<Outline>();
                outline.effectColor = inactiveSlotColor;
                outline.effectDistance = new Vector2(2.5f, -2.5f);
                outline.useGraphicAlpha = false;
                hotbarSlots[i].borderOutline = outline;
                hotbarSlots[i].borderHighlight = slotBg;

                GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(slotObj.transform, false);
                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.12f, 0.12f);
                iconRt.anchorMax = new Vector2(0.88f, 0.88f);
                iconRt.sizeDelta = Vector2.zero;

                Image iconImg = iconObj.GetComponent<Image>();
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                iconImg.gameObject.SetActive(false);
                hotbarSlots[i].iconImage = iconImg;

                GameObject keyObj = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                keyObj.transform.SetParent(slotObj.transform, false);
                RectTransform keyRt = keyObj.GetComponent<RectTransform>();
                keyRt.anchorMin = keyRt.anchorMax = keyRt.pivot = new Vector2(0f, 1f);
                keyRt.anchoredPosition = new Vector2(5f, -3f);
                keyRt.sizeDelta = new Vector2(30f, 20f);

                TextMeshProUGUI keyTmp = keyObj.GetComponent<TextMeshProUGUI>();
                keyTmp.text = keyLabels[i];
                keyTmp.fontSize = 12;
                keyTmp.fontStyle = FontStyles.Bold;
                keyTmp.color = Color.white;
                hotbarSlots[i].keyNumberText = keyTmp;

                // Текст информации о предмете в слоте (вес / патроны)
                GameObject infoObj = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
                infoObj.transform.SetParent(slotObj.transform, false);
                RectTransform infoRt = infoObj.GetComponent<RectTransform>();
                infoRt.anchorMin = infoRt.anchorMax = infoRt.pivot = new Vector2(1f, 0f);
                infoRt.anchoredPosition = new Vector2(-4f, 4f);
                infoRt.sizeDelta = new Vector2(56f, 18f);

                TextMeshProUGUI infoTmp = infoObj.GetComponent<TextMeshProUGUI>();
                infoTmp.alignment = TextAlignmentOptions.Right;
                infoTmp.fontSize = 10;
                infoTmp.fontStyle = FontStyles.Bold;
                infoTmp.color = Color.white;
                infoTmp.raycastTarget = false;
                hotbarSlots[i].infoText = infoTmp;
            }

            ApplyHotbarHeaderLayout();
        }
        else
        {
            hotbarRoot = existingHb.gameObject;
            hotbarHeaderArea = existingHb.Find("HotbarHeaderArea")?.gameObject;
            if (hotbarHeaderArea != null)
            {
                interactionBadge = hotbarHeaderArea.transform.Find("InteractionBadge")?.gameObject;
                if (interactionBadge != null)
                {
                    releaseInteractionText = interactionBadge.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
                }
                ammoUsageBadge = hotbarHeaderArea.transform.Find("AmmoUsageBadge")?.gameObject;
                if (ammoUsageBadge != null)
                {
                    releaseAmmoUsageText = ammoUsageBadge.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
                }
            }
            for (int i = 0; i < hotbarSlots.Length; i++)
            {
                if (hotbarSlots[i] != null && hotbarSlots[i].slotRoot != null)
                {
                    Transform it = hotbarSlots[i].slotRoot.transform.Find("InfoText");
                    if (it != null)
                    {
                        hotbarSlots[i].infoText = it.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
            ApplyHotbarHeaderLayout();
        }

        // ==================== 3. ТАЙМЕР ЗАБЕГА (Появляется ТОЛЬКО во время забега) ====================
        Transform existingTimer = releaseHudRoot.transform.Find("ReleaseTimer");
        if (existingTimer == null)
        {
            GameObject timerObj = new GameObject("ReleaseTimer", typeof(RectTransform), typeof(TextMeshProUGUI));
            timerObj.transform.SetParent(releaseHudRoot.transform, false);
            RectTransform tRt = timerObj.GetComponent<RectTransform>();
            tRt.anchorMin = tRt.anchorMax = tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = new Vector2(0f, -38f);
            tRt.sizeDelta = new Vector2(200f, 50f);

            releaseTimerText = timerObj.GetComponent<TextMeshProUGUI>();
            releaseTimerText.alignment = TextAlignmentOptions.Center;
            releaseTimerText.fontSize = 32;
            releaseTimerText.fontStyle = FontStyles.Bold;
            releaseTimerText.color = Color.white;
            releaseTimerText.text = "00:00";
            releaseTimerText.gameObject.SetActive(false);
        }
        else
        {
            releaseTimerText = existingTimer.GetComponent<TextMeshProUGUI>();
        }

        // ==================== 4. ПЛАВАЮЩИЕ УВЕДОМЛЕНИЯ НАД БАРАБАНОМ ====================
        Transform existingNotif = releaseHudRoot.transform.Find("DrumNotificationRoot");
        if (existingNotif == null)
        {
            GameObject notifObj = new GameObject("DrumNotificationRoot", typeof(RectTransform));
            notifObj.transform.SetParent(releaseHudRoot.transform, false);
            RectTransform nRt = notifObj.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0f);
            nRt.anchorMax = new Vector2(0f, 0f);
            nRt.pivot = new Vector2(0.5f, 0f);
            nRt.anchoredPosition = new Vector2(drumAnchoredPosition.x + notificationOffset.x, drumAnchoredPosition.y + drumSize * 0.5f + notificationOffset.y);
            nRt.sizeDelta = new Vector2(300f, 260f);
            drumNotificationRoot = notifObj;
        }
        else
        {
            drumNotificationRoot = existingNotif.gameObject;
        }
    }

    private Vector2 lastInspectorDrumPos;
    private float lastInspectorDrumSize;
    private Vector2 lastSceneDrumPos;
    private float lastSceneDrumSize;

    /// <summary>
    /// Динамически применяет размеры и положение барабана при изменении полей в Инспекторе
    /// </summary>
    public void ApplyDrumLayout()
    {
        if (drumRoot == null) return;

        RectTransform drumRt = drumRoot.GetComponent<RectTransform>();
        if (drumRt != null)
        {
            if (autoSyncFromSceneDrum && !Application.isPlaying)
            {
                bool inspectorChanged = (drumAnchoredPosition != lastInspectorDrumPos) || (Mathf.Abs(drumSize - lastInspectorDrumSize) > 0.1f);
                bool sceneChanged = (drumRt.anchoredPosition != lastSceneDrumPos) || (Mathf.Abs(drumRt.sizeDelta.x - lastSceneDrumSize) > 0.1f);

                if (inspectorChanged)
                {
                    // Пользователь изменил поля в Инспекторе HeistUI -> применяем к объекту в сцене
                    drumRt.anchoredPosition = drumAnchoredPosition;
                    drumRt.sizeDelta = new Vector2(drumSize, drumSize);
                    lastInspectorDrumPos = drumAnchoredPosition;
                    lastInspectorDrumSize = drumSize;
                    lastSceneDrumPos = drumRt.anchoredPosition;
                    lastSceneDrumSize = drumRt.sizeDelta.x;
                }
                else if (sceneChanged && drumRt.sizeDelta.x > 10f)
                {
                    // Пользователь передвинул или смасштабировал RevolverDrum в окне Scene View мышкой ->
                    // считываем в поля HeistUI, чтобы при старте игры ничего не сбрасывалось!
                    drumAnchoredPosition = drumRt.anchoredPosition;
                    drumSize = drumRt.sizeDelta.x;
                    lastInspectorDrumPos = drumAnchoredPosition;
                    lastInspectorDrumSize = drumSize;
                    lastSceneDrumPos = drumRt.anchoredPosition;
                    lastSceneDrumSize = drumRt.sizeDelta.x;
                }
                else
                {
                    drumRt.anchoredPosition = drumAnchoredPosition;
                    drumRt.sizeDelta = new Vector2(drumSize, drumSize);
                }
            }
            else
            {
                drumRt.anchoredPosition = drumAnchoredPosition;
                drumRt.sizeDelta = new Vector2(drumSize, drumSize);
            }
        }

        if (drumNotificationRoot != null)
        {
            RectTransform nRt = drumNotificationRoot.GetComponent<RectTransform>();
            if (nRt != null)
            {
                nRt.anchoredPosition = new Vector2(drumAnchoredPosition.x + notificationOffset.x, drumAnchoredPosition.y + drumSize * 0.5f + notificationOffset.y);
            }
        }

        Transform chLayer = drumRoot.transform.Find("ChambersLayer");
        if (chLayer != null)
        {
            float S = drumSize / 813f;
            Vector2 posHp = new Vector2(8.1f * S, 167.3f * S) + hpChamberOffset;
            Vector2 posAmmo = new Vector2(165.3f * S, 79.0f * S);
            Vector2 posBR = new Vector2(165.6f * S, -100.9f * S);
            Vector2 posB = new Vector2(8.2f * S, -194.7f * S);
            Vector2 posBL = new Vector2(-150.0f * S, -101.0f * S);
            Vector2 posStamina = new Vector2(-149.5f * S, 78.8f * S) + staminaChamberOffset;

            UpdateSocketTransform(chLayer.Find("Socket_HP"), posHp, chamberSize);
            UpdateSocketTransform(chLayer.Find("Socket_Ammo"), posAmmo, chamberSize);
            UpdateSocketTransform(chLayer.Find("Socket_Empty_BR"), posBR, chamberSize);
            UpdateSocketTransform(chLayer.Find("Socket_Empty_B"), posB, chamberSize);
            UpdateSocketTransform(chLayer.Find("Socket_Empty_BL"), posBL, chamberSize);
            UpdateSocketTransform(chLayer.Find("Socket_Stamina"), posStamina, chamberSize);
        }

        Transform lblLayer = drumRoot.transform.Find("LabelsLayer");
        if (lblLayer != null)
        {
            Transform hpTxtT = lblLayer.Find("HP_Text");
            if (hpTxtT != null)
            {
                float S = drumSize / 813f;
                Vector2 posHp = new Vector2(8.1f * S, 167.3f * S) + hpChamberOffset;
                RectTransform rt = hpTxtT.GetComponent<RectTransform>();
                rt.anchoredPosition = posHp;
                rt.sizeDelta = new Vector2(chamberSize * 0.95f, chamberSize * 0.55f);

                TextMeshProUGUI tmp = hpTxtT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSizeMin = 10;
                    tmp.fontSizeMax = 22f;
                    tmp.fontStyle = FontStyles.Bold;
                }
            }
        }
    }

    /// <summary>
    /// Динамически применяет размеры и положение бейджей над инвентарем
    /// </summary>
    public void ApplyHotbarHeaderLayout()
    {
        if (hotbarHeaderArea != null)
        {
            RectTransform headerRt = hotbarHeaderArea.GetComponent<RectTransform>();
            if (headerRt != null)
            {
                headerRt.anchoredPosition = hotbarHeaderPosition;
                float totalHeight = interactionBadgeSize.y + ammoBadgeSize.y + badgesSpacing + 8f;
                float maxWidth = Mathf.Max(interactionBadgeSize.x, ammoBadgeSize.x);
                headerRt.sizeDelta = new Vector2(maxWidth, totalHeight);
            }
        }

        if (interactionBadge != null)
        {
            RectTransform ibRt = interactionBadge.GetComponent<RectTransform>();
            if (ibRt != null)
            {
                float yPos = (ammoBadgeSize.y * 0.5f) + badgesSpacing + (interactionBadgeSize.y * 0.5f);
                ibRt.anchoredPosition = new Vector2(0f, yPos);
                ibRt.sizeDelta = interactionBadgeSize;
            }
            if (releaseInteractionText != null)
            {
                releaseInteractionText.fontSize = interactionFontSize;
            }
        }

        if (ammoUsageBadge != null)
        {
            RectTransform abRt = ammoUsageBadge.GetComponent<RectTransform>();
            if (abRt != null)
            {
                abRt.anchoredPosition = new Vector2(0f, 0f);
                abRt.sizeDelta = ammoBadgeSize;
            }
            if (releaseAmmoUsageText != null)
            {
                releaseAmmoUsageText.fontSize = ammoFontSize;
            }
        }
    }

    private static void UpdateSocketTransform(Transform socketT, Vector2 pos, float size)
    {
        if (socketT == null) return;
        RectTransform rt = socketT.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
        }
    }

    private static GameObject CreateChamberSocket(Transform parent, string name, Vector2 pos, float size)
    {
        GameObject socket = new GameObject(name, typeof(RectTransform));
        socket.transform.SetParent(parent, false);
        RectTransform rt = socket.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        return socket;
    }

    private static Image CreateChamberBg(Transform parent, Sprite circleSprite, Color color)
    {
        GameObject bgObj = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(parent, false);
        RectTransform rt = bgObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = bgObj.GetComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Image CreateChamberLiquid(Transform parent, Sprite circleSprite, Color color)
    {
        GameObject liqObj = new GameObject("Liquid", typeof(RectTransform), typeof(Image));
        liqObj.transform.SetParent(parent, false);
        RectTransform rt = liqObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Image img = liqObj.GetComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Vertical;
        img.fillOrigin = (int)Image.OriginVertical.Bottom;
        img.fillAmount = 1.0f;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateChamberText(Transform parent, string name, Vector2 pos, string defaultText, float fontSize)
    {
        GameObject txtObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(parent, false);
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(70f, 34f);

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void UpdateHotbar()
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0) return;

        int activeIdx = 0;
        EquipableItem[] invSlots = null;
        if (PlayerInventory.Instance != null)
        {
            activeIdx = PlayerInventory.Instance.activeSlotIndex;
            invSlots = PlayerInventory.Instance.slots;
        }

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            var slotUI = hotbarSlots[i];
            if (slotUI == null || slotUI.slotRoot == null) continue;

            bool isCurrent = (activeIdx == i);

            // Золотая рамка для активного слота
            if (slotUI.borderOutline != null)
            {
                slotUI.borderOutline.effectColor = isCurrent ? activeSlotColor : inactiveSlotColor;
                slotUI.borderOutline.effectDistance = isCurrent ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);
            }

            // Подсветка текста клавиши
            if (slotUI.keyNumberText != null)
            {
                slotUI.keyNumberText.color = isCurrent ? activeSlotColor : Color.white;
            }

            // Иконка предмета в слоте
            if (slotUI.iconImage != null)
            {
                if (invSlots != null && i < invSlots.Length && invSlots[i] != null && invSlots[i].itemData != null && invSlots[i].itemData.icon != null)
                {
                    slotUI.iconImage.sprite = invSlots[i].itemData.icon;
                    slotUI.iconImage.gameObject.SetActive(true);
                    slotUI.iconImage.color = isCurrent ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.55f);
                }
                else
                {
                    if (PlayerInventory.Instance != null)
                    {
                        slotUI.iconImage.gameObject.SetActive(false);
                    }
                }
            }

            // Текст информации о предмете (вес сумки / патроны)
            if (slotUI.infoText != null)
            {
                if (invSlots != null && i < invSlots.Length && invSlots[i] != null)
                {
                    if (invSlots[i] is BagTool b)
                    {
                        string moneyStr = b.storedMoney >= 1000 ? $"${(b.storedMoney / 1000f):0.#}k" : $"${b.storedMoney}";
                        slotUI.infoText.text = b.storedMoney > 0 ? $"{moneyStr}\n{b.storedWeight}/{b.maxCapacity}k" : $"{b.storedWeight}/{b.maxCapacity}k";
                        slotUI.infoText.color = (b.storedWeight >= b.maxCapacity) ? new Color(1f, 0.35f, 0.35f, 1f) : 
                            (b.storedMoney > 0 ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(0.95f, 0.85f, 0.45f, 1f));
                        slotUI.infoText.gameObject.SetActive(true);
                    }
                    else if (invSlots[i] is WeaponItem w)
                    {
                        slotUI.infoText.text = $"{w.ammoInMagazine}/{w.reserveAmmo}";
                        slotUI.infoText.color = Color.white;
                        slotUI.infoText.gameObject.SetActive(true);
                    }
                    else
                    {
                        slotUI.infoText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    slotUI.infoText.gameObject.SetActive(false);
                }
            }
        }
    }

    private static Sprite GetOrCreateCapsuleSprite()
    {
        if (cachedCapsuleSprite != null) return cachedCapsuleSprite;
        int w = 64;
        int h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[w * h];
        float radius = 15f;
        Vector2 cLeft = new Vector2(15.5f, 15.5f);
        Vector2 cRight = new Vector2(47.5f, 15.5f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = 0f;
                if (x < cLeft.x) d = Vector2.Distance(new Vector2(x, y), cLeft);
                else if (x > cRight.x) d = Vector2.Distance(new Vector2(x, y), cRight);
                else d = Mathf.Abs(y - 15.5f);

                float alpha = Mathf.Clamp01((radius - d) + 0.5f);
                cols[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        cachedCapsuleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16, 15, 16, 15));
        return cachedCapsuleSprite;
    }

    /// <summary>
    /// Выводит анимированное всплывающее уведомление над барабаном револьвера:
    /// выезд снизу из-под экрана -> мягкая фиксация над барабаном -> плавный уезд вверх и растворение.
    /// </summary>
    public void ShowNotification(string message, Color textColor)
    {
        if (string.IsNullOrEmpty(message)) return;

        // Защита от спама дублирующимися уведомлениями
        if (message == lastNotificationText && Time.time - lastNotificationTime < 0.4f)
        {
            return;
        }
        lastNotificationText = message;
        lastNotificationTime = Time.time;

        if (drumNotificationRoot == null)
        {
            EnsureReleaseHudCreated();
            if (drumNotificationRoot == null) return;
        }

        // Очищаем удаленные объекты и ограничиваем максимум 5 уведомлений на экране
        activeNotifications.RemoveAll(x => x == null || x.root == null);
        while (activeNotifications.Count >= maxNotificationsOnScreen)
        {
            var oldest = activeNotifications[0];
            activeNotifications.RemoveAt(0);
            if (oldest != null && oldest.root != null)
            {
                Destroy(oldest.root);
            }
        }

        // 1. Создаем плашку уведомления
        GameObject itemObj = new GameObject("Notif_" + message, typeof(RectTransform), typeof(CanvasGroup));
        itemObj.transform.SetParent(drumNotificationRoot.transform, false);

        RectTransform itemRt = itemObj.GetComponent<RectTransform>();
        itemRt.anchorMin = itemRt.anchorMax = itemRt.pivot = new Vector2(0.5f, 0f);
        itemRt.sizeDelta = new Vector2(notificationMinWidth, notificationBadgeHeight);
        itemRt.anchoredPosition = new Vector2(0f, -notificationEnterSlideDistance);
        itemRt.localScale = Vector3.one * 0.85f;

        CanvasGroup cg = itemObj.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Фон-капсула (темно-графитовый полупрозрачный со стильным свечением)
        GameObject bgObj = new GameObject("Bg", typeof(RectTransform), typeof(Image), typeof(Outline));
        bgObj.transform.SetParent(itemObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.sprite = GetOrCreateCapsuleSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.06f, 0.07f, 0.10f, 0.88f);
        bgImg.raycastTarget = false;

        Outline outline = bgObj.GetComponent<Outline>();
        outline.effectColor = new Color(textColor.r * 0.85f, textColor.g * 0.85f, textColor.b * 0.85f, 0.45f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = false;

        // Текст уведомления
        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(itemObj.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = notificationFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;

        // Авто-ширина под текст + настраиваемые отступы
        float prefWidth = tmp.preferredWidth;
        itemRt.sizeDelta = new Vector2(Mathf.Max(notificationMinWidth, prefWidth + notificationHorizontalPadding), notificationBadgeHeight);

        ActiveNotification activeItem = new ActiveNotification
        {
            root = itemObj,
            rect = itemRt,
            canvasGroup = cg,
            targetBaseY = 0f,
            currentBaseY = -notificationEnterSlideDistance
        };
        activeNotifications.Add(activeItem);

        // Строгий шаг между уведомлениями: высота бейджа + гарантированный зазор 8px, чтобы плашки не наслаивались
        float step = Mathf.Max(notificationStackSpacing, notificationBadgeHeight + 8f);
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            int slotFromBottom = activeNotifications.Count - 1 - i;
            if (activeNotifications[i] != null)
            {
                activeNotifications[i].targetBaseY = slotFromBottom * step;
            }
        }

        if (Application.isPlaying)
        {
            StartCoroutine(AnimateNotificationRoutine(activeItem));
        }
        else
        {
            cg.alpha = 1f;
            itemRt.anchoredPosition = Vector2.zero;
            itemRt.localScale = Vector3.one;
        }
    }

    private System.Collections.IEnumerator AnimateNotificationRoutine(ActiveNotification item)
    {
        float enterDuration = notificationEnterDuration;
        float holdDuration = notificationHoldDuration;
        float exitDuration = notificationExitDuration;

        float timer = 0f;

        // 1. ФАЗА ВЪЕЗДА СНИЗУ (Выезд из-под барабана, появление из прозрачности с сочным попом)
        while (timer < enterDuration)
        {
            if (item == null || item.root == null) yield break;
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / enterDuration);
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            item.canvasGroup.alpha = ease;
            float popScale = Mathf.LerpUnclamped(0.85f, 1.05f, ease);
            item.rect.localScale = Vector3.one * (t < 0.8f ? popScale : Mathf.Lerp(1.05f, 1.0f, (t - 0.8f) / 0.2f));

            item.currentBaseY = Mathf.Lerp(item.currentBaseY, item.targetBaseY, Time.deltaTime * 16f);
            float animOffset = (1f - ease) * -notificationEnterSlideDistance;
            item.rect.anchoredPosition = new Vector2(0f, item.currentBaseY + animOffset);

            yield return null;
        }

        if (item != null && item.rect != null)
        {
            item.rect.localScale = Vector3.one;
            item.canvasGroup.alpha = 1f;
        }

        // 2. ФАЗА УДЕРЖАНИЯ (Игрок спокойно видит информацию, плашки мягко плывут вверх при новых поступлениях)
        timer = 0f;
        while (timer < holdDuration)
        {
            if (item == null || item.root == null) yield break;
            timer += Time.deltaTime;

            item.currentBaseY = Mathf.Lerp(item.currentBaseY, item.targetBaseY, Time.deltaTime * 14f);
            item.rect.anchoredPosition = new Vector2(0f, item.currentBaseY);

            yield return null;
        }

        // 3. ФАЗА УЕЗДА ВВЕРХ И РАСТВОРЕНИЯ (Уезжает вверх и растворяется)
        timer = 0f;
        while (timer < exitDuration)
        {
            if (item == null || item.root == null) yield break;
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / exitDuration);
            float ease = t * t;

            item.canvasGroup.alpha = 1f - ease;
            item.rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, ease);

            item.currentBaseY = Mathf.Lerp(item.currentBaseY, item.targetBaseY, Time.deltaTime * 14f);
            float exitOffset = ease * notificationExitSlideDistance;
            item.rect.anchoredPosition = new Vector2(0f, item.currentBaseY + exitOffset);

            yield return null;
        }

        // Завершение
        activeNotifications.Remove(item);
        if (item != null && item.root != null)
        {
            Destroy(item.root);
        }

        // Плавный сдвиг оставшихся уведомлений
        float step = Mathf.Max(notificationStackSpacing, notificationBadgeHeight + 8f);
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            int slotFromBottom = activeNotifications.Count - 1 - i;
            if (activeNotifications[i] != null)
            {
                activeNotifications[i].targetBaseY = slotFromBottom * step;
            }
        }
    }

    // ==================== ТЕСТОВЫЕ КНОПКИ В ИНСПЕКТОРЕ ДЛЯ БЫСТРОЙ ПРОВЕРКИ ====================
    [ContextMenu("🔔 Тест: Патроны (+15% Патронов)")]
    public void TestAmmoNotification()
    {
        ShowNotification("+15% Патронов", new Color(1f, 0.88f, 0.35f, 1f));
    }

    [ContextMenu("💚 Тест: Лечение (+40 HP)")]
    public void TestHealNotification()
    {
        ShowNotification("+40 HP", new Color(0.2f, 1f, 0.55f, 1f));
    }

    [ContextMenu("💰 Тест: Деньги (+$500)")]
    public void TestMoneyNotification()
    {
        ShowNotification("+$500", new Color(0.35f, 0.95f, 0.45f, 1f));
    }

    [ContextMenu("🔥 Тест: Ожог!")]
    public void TestBurnNotification()
    {
        ShowNotification("Ожог!", new Color(1f, 0.38f, 0.15f, 1f));
    }
}
