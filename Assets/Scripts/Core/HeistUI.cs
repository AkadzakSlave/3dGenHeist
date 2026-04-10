using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class HeistUI : MonoBehaviour
{
    public TextMeshProUGUI bagText;
    public TextMeshProUGUI quotaText;
    public TextMeshProUGUI weightText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI staminaText;
    public GameObject quotaMetBanner;

    [Header("Placeholders (Future)")]
    public GameObject healthBarPlaceholder;
    public GameObject inventoryPlaceholder;
    public GameObject universalSlotPlaceholder;

    private float timerBlinkSpeed = 5f;

    void Start()
    {
        UpdateUI();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onMoneyChanged.AddListener(UpdateUI);
            GameManager.Instance.onQuotaMet.AddListener(ShowQuotaMetBanner);
        }
        
        if (quotaMetBanner != null)
        {
            quotaMetBanner.SetActive(false);
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        // Обновление таймера
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
            quotaText.text = $"Quota: ${GameManager.Instance.depositedMoney} / ${GameManager.Instance.targetQuota}";
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
}
