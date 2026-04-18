using UnityEngine;
using TMPro;

public class LobbyBalanceDisplay : MonoBehaviour
{
    public TextMeshProUGUI balanceText;
    public string prefix = "Global Savings: $";

    void Update()
    {
        if (GameManager.Instance != null && balanceText != null)
        {
            balanceText.text = $"{prefix}{GameManager.Instance.globalBankBalance:N0}\nCompleted Quotas: {GameManager.Instance.completedQuotas}";
        }
    }
}
