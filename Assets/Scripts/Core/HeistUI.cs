using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public Color reticleIdleColor = new Color(1f, 1f, 1f, 0.75f);
    public Color reticleFocusColor = new Color(0.35f, 0.9f, 1f, 1f);
    public float reticleIdleSize = 5f;
    public float reticleFocusSize = 8.5f;
    public float reticleSmoothSpeed = 16f;

    private bool isReticleFocused = false;
    private float currentReticleSize = 5f;
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
    public Vector2 drumAnchoredPosition = new Vector2(165f, 165f);
    [Tooltip("Общий размер барабана в пикселях холста (увеличьте для большего размера)")]
    public float drumSize = 280f;
    [Tooltip("Расстояние от центра барабана до центра камор")]
    public float chamberRadius = 55f;
    [Tooltip("Диаметр каморы барабана")]
    public float chamberSize = 65f;

    [Header("Revolver Drum HUD (Release)")]
    public Sprite drumSprite;
    public GameObject drumRoot;
    public Image hpLiquidImage;
    public Image hpBgImage;
    public TextMeshProUGUI hpText;
    public Image staminaLiquidImage;
    public Image staminaBgImage;
    public TextMeshProUGUI releaseTimerText;

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
    }

    [Header("Hotbar HUD")]
    public GameObject hotbarRoot;
    public HotbarSlotUI[] hotbarSlots = new HotbarSlotUI[3];
    public Color activeSlotColor = new Color(1.0f, 0.843f, 0.0f, 1f); // Золотой
    public Color inactiveSlotColor = new Color(0.22f, 0.23f, 0.27f, 0.75f);

    [Header("Placeholders (Future)")]
    public GameObject healthBarPlaceholder;
    public GameObject inventoryPlaceholder;
    public GameObject universalSlotPlaceholder;

    [Header("Transitions")]
    public GameObject loadingScreenPanel;

    private float timerBlinkSpeed = 5f;
    private int lastTimerSecond = -1;
    private int lastStaminaInt = -1;
    private Sprite cachedCircleSprite;

    private void Awake()
    {
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

        // Автоматически находим старый PlayerInterfaceUi, если debugHudRoot не задан в инспекторе
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
    }

    private void Start()
    {
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

    private void OnValidate()
    {
        ApplyDrumLayout();
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

            // Моргание красным, если таймер на нуле
            Color timerColor = (t <= 0)
                ? Color.Lerp(Color.red, Color.white, (Mathf.Sin(Time.time * timerBlinkSpeed) + 1.0f) / 2.0f)
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

        SetFMODOverlay(debugActive);
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

        UpdateHotbar();
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
        if (staminaLiquidImage != null)
        {
            float ratio = Mathf.Clamp01(current / Mathf.Max(1f, max));
            staminaLiquidImage.fillAmount = ratio;
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
        if (hpLiquidImage != null)
        {
            float ratio = Mathf.Clamp01((float)current / Mathf.Max(1, max));
            hpLiquidImage.fillAmount = ratio;
        }

        if (hpText != null)
        {
            hpText.text = current.ToString();
            hpText.color = current <= 25 ? new Color(1f, 0.4f, 0.4f, 1f) : Color.white;
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
        if (gm == null || gm.activeOperationPreset == null) return;

        int dayIndex = Mathf.Clamp(gm.currentDay - 1, 0, gm.activeOperationPreset.cities.Count - 1);
        string currentCity = gm.activeOperationPreset.cities[dayIndex].cityName;
        if (cityNameText != null) cityNameText.text = $"{gm.activeOperationPreset.levelName}: {currentCity}";

        if (quotaProgressSlider != null)
        {
            quotaProgressSlider.maxValue = gm.operationTargetQuota;
            quotaProgressSlider.value = gm.accumulatedOperationMoney + gm.depositedMoney;
        }
        if (quotaText != null) 
            quotaText.text = $"${gm.accumulatedOperationMoney + gm.depositedMoney} / ${gm.operationTargetQuota}";

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
        if (reticleDot == null) return;

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

        float targetSize = isReticleFocused ? reticleFocusSize : reticleIdleSize;
        Color targetColor = isReticleFocused ? reticleFocusColor : reticleIdleColor;

        currentReticleSize = Mathf.Lerp(currentReticleSize, targetSize, Time.deltaTime * reticleSmoothSpeed);
        currentReticleColor = Color.Lerp(currentReticleColor, targetColor, Time.deltaTime * reticleSmoothSpeed);

        reticleDot.rectTransform.sizeDelta = new Vector2(currentReticleSize, currentReticleSize);
        reticleDot.color = currentReticleColor;

        if (reticleRing != null)
        {
            float ringTargetAlpha = isReticleFocused ? 0.9f : 0f;
            Color ringC = reticleRing.color;
            ringC.a = Mathf.Lerp(ringC.a, ringTargetAlpha, Time.deltaTime * reticleSmoothSpeed);
            reticleRing.color = ringC;

            float ringTargetScale = isReticleFocused ? 1.0f : 0.6f;
            reticleRing.rectTransform.localScale = Vector3.Lerp(reticleRing.rectTransform.localScale, Vector3.one * ringTargetScale, Time.deltaTime * reticleSmoothSpeed);
        }
    }

    private void EnsureReticleCreated()
    {
        if (reticleRoot != null && reticleDot != null) return;

        Transform existing = transform.Find("ReticleRoot");
        if (existing != null)
        {
            reticleRoot = existing.gameObject;
            reticleDot = existing.Find("ReticleDot")?.GetComponent<Image>();
            reticleRing = existing.Find("ReticleRing")?.GetComponent<Image>();
            if (reticleDot != null) return;
        }

        GameObject rRoot = new GameObject("ReticleRoot", typeof(RectTransform));
        rRoot.transform.SetParent(transform, false);
        RectTransform rRt = rRoot.GetComponent<RectTransform>();
        rRt.anchorMin = rRt.anchorMax = rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = Vector2.zero;
        rRt.sizeDelta = new Vector2(40f, 40f);
        reticleRoot = rRoot;

        Sprite circleSprite = GetOrCreateCircleSprite();
        Sprite ringSprite = CreateRingSprite(32, 2);

        GameObject ringObj = new GameObject("ReticleRing", typeof(RectTransform), typeof(Image));
        ringObj.transform.SetParent(rRoot.transform, false);
        RectTransform ringRt = ringObj.GetComponent<RectTransform>();
        ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.anchoredPosition = Vector2.zero;
        ringRt.sizeDelta = new Vector2(10f, 10f);
        reticleRing = ringObj.GetComponent<Image>();
        reticleRing.sprite = ringSprite;
        reticleRing.color = new Color(reticleFocusColor.r, reticleFocusColor.g, reticleFocusColor.b, 0f);
        reticleRing.raycastTarget = false;

        GameObject dotObj = new GameObject("ReticleDot", typeof(RectTransform), typeof(Image));
        dotObj.transform.SetParent(rRoot.transform, false);
        RectTransform dotRt = dotObj.GetComponent<RectTransform>();
        dotRt.anchorMin = dotRt.anchorMax = dotRt.pivot = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = Vector2.zero;
        dotRt.sizeDelta = new Vector2(5f, 5f);
        reticleDot = dotObj.GetComponent<Image>();
        reticleDot.sprite = circleSprite;
        reticleDot.color = reticleIdleColor;
        reticleDot.raycastTarget = false;
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

            Vector2 posHp = new Vector2(0f, chamberRadius);
            Vector2 posAmmo = new Vector2(chamberRadius * Mathf.Cos(30f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(30f * Mathf.Deg2Rad));
            Vector2 posBR = new Vector2(chamberRadius * Mathf.Cos(-30f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(-30f * Mathf.Deg2Rad));
            Vector2 posB = new Vector2(0f, -chamberRadius);
            Vector2 posBL = new Vector2(chamberRadius * Mathf.Cos(-150f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(-150f * Mathf.Deg2Rad));
            Vector2 posStamina = new Vector2(chamberRadius * Mathf.Cos(150f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(150f * Mathf.Deg2Rad));

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

            hpText = CreateChamberText(labelsLayer.transform, "HP_Text", posHp, "100", 14);
        }
        else
        {
            drumRoot = existingDrum.gameObject;
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
            headerRt.anchoredPosition = new Vector2(0f, 8f);
            headerRt.sizeDelta = new Vector2(340f, 60f);
            hotbarHeaderArea = headerObj;

            // 1. Бейдж подсказки взаимодействия ([E] Подобрать предмет)
            GameObject interactBadgeObj = new GameObject("InteractionBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            interactBadgeObj.transform.SetParent(headerObj.transform, false);
            RectTransform ibRt = interactBadgeObj.GetComponent<RectTransform>();
            ibRt.anchorMin = new Vector2(0.5f, 0.5f);
            ibRt.anchorMax = new Vector2(0.5f, 0.5f);
            ibRt.pivot = new Vector2(0.5f, 0.5f);
            ibRt.anchoredPosition = new Vector2(0f, 18f);
            ibRt.sizeDelta = new Vector2(320f, 28f);

            Image ibImg = interactBadgeObj.GetComponent<Image>();
            ibImg.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
            Outline ibOutline = interactBadgeObj.GetComponent<Outline>();
            ibOutline.effectColor = new Color(0.35f, 0.85f, 1f, 0.45f);
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
            releaseInteractionText.fontSize = 13.5f;
            releaseInteractionText.fontStyle = FontStyles.Bold;
            releaseInteractionText.color = new Color(0.9f, 0.96f, 1f, 1f);
            releaseInteractionText.text = "";
            interactionBadge.SetActive(false);

            // 2. Бейдж патронов и подсказок оружия / инструментов
            GameObject ammoBadgeObj = new GameObject("AmmoUsageBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            ammoBadgeObj.transform.SetParent(headerObj.transform, false);
            RectTransform abRt = ammoBadgeObj.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(0.5f, 0.5f);
            abRt.anchorMax = new Vector2(0.5f, 0.5f);
            abRt.pivot = new Vector2(0.5f, 0.5f);
            abRt.anchoredPosition = new Vector2(0f, -10f);
            abRt.sizeDelta = new Vector2(260f, 26f);

            Image abImg = ammoBadgeObj.GetComponent<Image>();
            abImg.color = new Color(0.08f, 0.08f, 0.1f, 0.88f);
            Outline abOutline = ammoBadgeObj.GetComponent<Outline>();
            abOutline.effectColor = new Color(1.0f, 0.843f, 0f, 0.4f);
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
            releaseAmmoUsageText.fontSize = 13f;
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
            }
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
    }

    /// <summary>
    /// Динамически применяет размеры и положение барабана при изменении полей в Инспекторе
    /// </summary>
    public void ApplyDrumLayout()
    {
        if (drumRoot == null) return;

        RectTransform drumRt = drumRoot.GetComponent<RectTransform>();
        if (drumRt != null)
        {
            drumRt.anchoredPosition = drumAnchoredPosition;
            drumRt.sizeDelta = new Vector2(drumSize, drumSize);
        }

        Transform chLayer = drumRoot.transform.Find("ChambersLayer");
        if (chLayer != null)
        {
            Vector2 posHp = new Vector2(0f, chamberRadius);
            Vector2 posAmmo = new Vector2(chamberRadius * Mathf.Cos(30f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(30f * Mathf.Deg2Rad));
            Vector2 posBR = new Vector2(chamberRadius * Mathf.Cos(-30f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(-30f * Mathf.Deg2Rad));
            Vector2 posB = new Vector2(0f, -chamberRadius);
            Vector2 posBL = new Vector2(chamberRadius * Mathf.Cos(-150f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(-150f * Mathf.Deg2Rad));
            Vector2 posStamina = new Vector2(chamberRadius * Mathf.Cos(150f * Mathf.Deg2Rad), chamberRadius * Mathf.Sin(150f * Mathf.Deg2Rad));

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
                RectTransform rt = hpTxtT.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(0f, chamberRadius);
                rt.sizeDelta = new Vector2(chamberSize * 0.9f, chamberSize * 0.5f);
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
        rt.sizeDelta = new Vector2(50f, 26f);

        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8;
        tmp.fontSizeMax = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void UpdateHotbar()
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0) return;
        if (PlayerInventory.Instance == null) return;

        int activeIdx = PlayerInventory.Instance.activeSlotIndex;
        var invSlots = PlayerInventory.Instance.slots;

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
                    slotUI.iconImage.gameObject.SetActive(false);
                }
            }
        }
    }
}
