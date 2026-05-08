using UnityEngine;
using TMPro;
using System.Collections;

public class DonResultUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panel;
    public TextMeshProUGUI totalEarnedText;
    public TextMeshProUGUI quotaText;
    public TextMeshProUGUI profitText;
    public TextMeshProUGUI cleanMoneyText;
    public TextMeshProUGUI sessionMoneyText;

    [Header("Settings")]
    public float countAnimationDuration = 2f;

    public void ShowResults(int total, int quota, int clean, int session)
    {
        panel.SetActive(true);
        StartCoroutine(AnimateResults(total, quota, clean, session));
    }

    private IEnumerator AnimateResults(int total, int quota, int clean, int session)
    {
        int profit = total - quota;
        
        // Поэтапное заполнение текстов
        totalEarnedText.text = $"Total Collected: ${total}";
        yield return new WaitForSeconds(0.5f);
        
        quotaText.text = $"Don's Quota: -${quota}";
        yield return new WaitForSeconds(0.5f);
        
        profitText.text = $"Net Profit: ${Mathf.Max(0, profit)}";
        yield return new WaitForSeconds(0.5f);

        if (profit > 0)
        {
            cleanMoneyText.text = $"Laundered (10%): +${clean}";
            sessionMoneyText.text = $"Safehouse Fund (90%): +${session}";
        }
        else
        {
            cleanMoneyText.text = "Laundered: $0 (Quota failed)";
            sessionMoneyText.text = "Safehouse Fund: $0";
        }
    }

    public void CloseAndGoLobby()
    {
        panel.SetActive(false);
        // Здесь можно вызвать переход в лобби через GameManager
    }
}
