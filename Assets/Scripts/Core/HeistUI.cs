using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class HeistUI : MonoBehaviour
{
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
    public TextMeshProUGUI totalMapLootText; // Показывает суммарную стоимость лута на карте
    public GameObject[] difficultySkulls; 
    public TextMeshProUGUI interactionText; // Текст при наведении на предмет

    [Header("UI Localization/Text")]
    public string bagMoneyFormat = "Bag: ${0}";
    public string weightFormat = "Weight: {0} / {1}kg";
    public string overWeightFormat = "<color=red>Weight: {0} / {1}kg (OVERWEIGHT)</color>";
    public string quotaDayFormat = "Day {0}/3\nVan: ${1}\nProgress: ${2} / ${3}";
    public string mapLootFormat = "Осталось на карте: ${0} ({1} шт.)";
    public string staminaFormat = "Stamina: {0}/{1}";
    public string healthFormat = "HP: {0}/{1}";

    [System.Serializable]
    public class HotbarSlotUI
    {
        public GameObject slotRoot;
        public Image borderHighlight;
        public Image iconImage;
        public TextMeshProUGUI keyNumberText;
    }

    [Header("Hotbar HUD")]
    public GameObject hotbarRoot;
    public HotbarSlotUI[] hotbarSlots = new HotbarSlotUI[3];
    public Color activeSlotColor = new Color(1f, 0.85f, 0.2f, 1f);
    public Color inactiveSlotColor = new Color(0.2f, 0.2f, 0.2f, 0.65f);

    [Header("Placeholders (Future)")]
    public GameObject healthBarPlaceholder;
    public GameObject inventoryPlaceholder;
    public GameObject universalSlotPlaceholder;

    [Header("Transitions")]
    public GameObject loadingScreenPanel;

    private float timerBlinkSpeed = 5f;

    private int lastTimerSecond = -1;
    private int lastStaminaInt = -1;

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

        EnsureHotbarCreated();
    }

    void Start()
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

    void Update()
    {
        if (GameManager.Instance == null) return;

        bool isInLobby = GameManager.Instance.isInLobby;

        // Показываем/скрываем элементы
        if (timerText != null)
        {
            // Таймер виден только тогда, когда ограбление началось (разрушена стена)
            timerText.gameObject.SetActive(GameManager.Instance.isHeistActive);
        }

        if (isInLobby) return;

        // Обновление таймера (только на уровне)
        if (timerText != null && GameManager.Instance.isHeistActive)
        {
            float t = GameManager.Instance.heistTimer;
            int totalSeconds = Mathf.FloorToInt(t);
            if (totalSeconds != lastTimerSecond)
            {
                lastTimerSecond = totalSeconds;
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
            }

            // Моргание красным, если таймер на нуле
            if (t <= 0)
            {
                float alpha = (Mathf.Sin(Time.time * timerBlinkSpeed) + 1.0f) / 2.0f;
                timerText.color = Color.Lerp(Color.red, Color.white, alpha);
            }
            else
            {
                timerText.color = Color.white;
            }
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
            // Если перегруз - меняем формат/цвет
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

    // Этот метод будет вызываться из PlayerMovement
    public void UpdateStamina(float current, float max)
    {
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
    }

    public void UpdateHealth(int current, int max)
    {
        if (healthText != null)
        {
            healthText.text = string.Format(healthFormat, current, max);
            healthText.color = current <= 25 ? Color.red : Color.white;
        }
    }

    public void UpdateWeapon(string text)
    {
        UpdateWeapon(text, null);
    }

    public void UpdateWeapon(string text, Sprite icon)
    {
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

        // 1. Город (из структуры CityConfig)
        int dayIndex = Mathf.Clamp(gm.currentDay - 1, 0, gm.activeOperationPreset.cities.Count - 1);
        string currentCity = gm.activeOperationPreset.cities[dayIndex].cityName;
        if (cityNameText != null) cityNameText.text = $"{gm.activeOperationPreset.levelName}: {currentCity}";

        // 2. Прогресс квоты
        if (quotaProgressSlider != null)
        {
            quotaProgressSlider.maxValue = gm.operationTargetQuota;
            quotaProgressSlider.value = gm.accumulatedOperationMoney + gm.depositedMoney;
        }
        if (quotaText != null) 
            quotaText.text = $"${gm.accumulatedOperationMoney + gm.depositedMoney} / ${gm.operationTargetQuota}";

        // 3. Черепа сложности (берем из выбранного досье)
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
        if (interactionText != null)
        {
            interactionText.text = text;
            interactionText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    private void EnsureHotbarCreated()
    {
        if (hotbarRoot != null && hotbarSlots != null && hotbarSlots.Length >= 3 && hotbarSlots[0] != null && hotbarSlots[0].slotRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("Hotbar");
        if (existing != null)
        {
            hotbarRoot = existing.gameObject;
        }
        else
        {
            GameObject hbObj = new GameObject("Hotbar", typeof(RectTransform));
            hbObj.transform.SetParent(transform, false);
            RectTransform hbRt = hbObj.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0f);
            hbRt.anchorMax = new Vector2(0.5f, 0f);
            hbRt.pivot = new Vector2(0.5f, 0f);
            hbRt.anchoredPosition = new Vector2(0f, 30f);
            hbRt.sizeDelta = new Vector2(240f, 70f);
            hotbarRoot = hbObj;

            hotbarSlots = new HotbarSlotUI[3];
            string[] keyLabels = new string[] { "[1]", "[2]", "[3]" };
            float[] xOffsets = new float[] { -75f, 0f, 75f };

            for (int i = 0; i < 3; i++)
            {
                hotbarSlots[i] = new HotbarSlotUI();

                // Slot Container Background
                GameObject slotObj = new GameObject($"Slot_{i + 1}", typeof(RectTransform), typeof(Image));
                slotObj.transform.SetParent(hbObj.transform, false);
                RectTransform slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(xOffsets[i], 0f);
                slotRt.sizeDelta = new Vector2(64f, 64f);

                Image slotBg = slotObj.GetComponent<Image>();
                slotBg.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
                hotbarSlots[i].slotRoot = slotObj;

                // Border Highlight
                GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Outline));
                borderObj.transform.SetParent(slotObj.transform, false);
                RectTransform borderRt = borderObj.GetComponent<RectTransform>();
                borderRt.anchorMin = Vector2.zero;
                borderRt.anchorMax = Vector2.one;
                borderRt.sizeDelta = Vector2.zero;

                // We also add an Image for the border frame
                Image borderImg = slotObj.GetComponent<Image>();
                hotbarSlots[i].borderHighlight = borderImg;

                // Icon image
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

                // Key Number Text
                GameObject keyObj = new GameObject("KeyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                keyObj.transform.SetParent(slotObj.transform, false);
                RectTransform keyRt = keyObj.GetComponent<RectTransform>();
                keyRt.anchorMin = new Vector2(0f, 1f);
                keyRt.anchorMax = new Vector2(0f, 1f);
                keyRt.pivot = new Vector2(0f, 1f);
                keyRt.anchoredPosition = new Vector2(5f, -3f);
                keyRt.sizeDelta = new Vector2(30f, 20f);

                TextMeshProUGUI keyTmp = keyObj.GetComponent<TextMeshProUGUI>();
                keyTmp.text = keyLabels[i];
                keyTmp.fontSize = 13;
                keyTmp.fontStyle = FontStyles.Bold;
                keyTmp.color = Color.white;
                hotbarSlots[i].keyNumberText = keyTmp;
            }
        }
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

            // Border color
            if (slotUI.borderHighlight != null)
            {
                slotUI.borderHighlight.color = isCurrent ? activeSlotColor : inactiveSlotColor;
            }

            // Key text color
            if (slotUI.keyNumberText != null)
            {
                slotUI.keyNumberText.color = isCurrent ? activeSlotColor : Color.white;
            }

            // Icon
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
