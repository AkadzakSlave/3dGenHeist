using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class HeistUI : MonoBehaviour
{
    public TextMeshProUGUI bagText;
    public TextMeshProUGUI quotaText;
    public TextMeshProUGUI staminaText;
    public GameObject quotaMetBanner;

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
