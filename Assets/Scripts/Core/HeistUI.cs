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

    [Header("Placeholders (Future)")]
    public GameObject healthBarPlaceholder;
    public GameObject inventoryPlaceholder;
    public GameObject universalSlotPlaceholder;

    [Header("Transitions")]
    public GameObject loadingScreenPanel;

    private float timerBlinkSpeed = 5f;

    private int lastTimerSecond = -1;
    private int lastStaminaInt = -1;

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
        if (weaponText != null)
        {
            weaponText.text = text;
            weaponText.gameObject.SetActive(!string.IsNullOrEmpty(text));
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
}
