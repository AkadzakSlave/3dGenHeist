using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class HeistUI : MonoBehaviour
{
    public TextMeshProUGUI bagText;
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI staminaText;
    public GameObject quotaMetBanner;

    [Header("Mission HUD Info")]
    public TextMeshProUGUI cityNameText;
    public UnityEngine.UI.Slider quotaProgressSlider;
    public TextMeshProUGUI quotaText; 
    public GameObject[] difficultySkulls; 


    [Header("Placeholders (Future)")]
    public GameObject healthBarPlaceholder;
    public GameObject inventoryPlaceholder;
    public GameObject universalSlotPlaceholder;

    [Header("Transitions")]
    public GameObject loadingScreenPanel;

    private float timerBlinkSpeed = 5f;

    void Start()
    {
        UpdateUI();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onMoneyChanged.AddListener(UpdateUI);
        }
        
        if (quotaMetBanner != null)
        {
            quotaMetBanner.SetActive(false);
        }
    }
    void Update()
    {
        if (GameManager.Instance == null) return;

        UpdateUI(); // Постоянно синхронизируем UI для плавности веса и денег
        UpdateMissionInfo(); // Обновляем информацию о городе и квоте
        
        bool isInLobby = GameManager.Instance.isInLobby;

        // Показываем/скрываем элементы
        if (timerText != null)
        {
            // Таймер виден только тогда, когда ограбление началось (разрушена стена)
            timerText.gameObject.SetActive(GameManager.Instance.isHeistActive);
        }

        if (isInLobby) return;

        // Обновление таймера (только на уровне)
        if (timerText != null)
        {
            float t = GameManager.Instance.heistTimer;
            int minutes = Mathf.FloorToInt(t / 60);
            int seconds = Mathf.FloorToInt(t % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

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
            bagText.text = $"Bag: ${GameManager.Instance.bagMoney}";
        }

        if (quotaText != null)
        {
            quotaText.text = $"Day {GameManager.Instance.currentDay}/3\nVan: ${GameManager.Instance.depositedMoney}\nProgress: ${GameManager.Instance.accumulatedOperationMoney} / ${GameManager.Instance.operationTargetQuota}";
        }

        if (weightText != null)
        {
            weightText.text = $"Weight: {GameManager.Instance.currentWeight} / {GameManager.Instance.maxWeight}";
            
            // Если перегруз - меняем цвет на красный
            if (GameManager.Instance.currentWeight > GameManager.Instance.maxWeight)
                weightText.color = Color.red;
            else
                weightText.color = Color.white;
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
            staminaText.text = $"Stamina: {displayCurrent}/{displayMax}";
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
}
