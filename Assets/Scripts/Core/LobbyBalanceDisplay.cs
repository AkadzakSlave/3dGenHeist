using UnityEngine;
using TMPro;

public class LobbyBalanceDisplay : MonoBehaviour
{
    public enum DisplayType 
    { 
        GlobalBankBalance, 
        CompletedQuotas 
    }

    [Header("UI Reference")]
    public TextMeshProUGUI textElement;

    [Header("Settings")]
    public DisplayType displayType;
    
    [Tooltip("Текст ДО числа (например: 'Общий счет: $')")]
    public string prefix = "Global Savings: $";
    
    [Tooltip("Текст ПОСЛЕ числа (например: ' выполнено')")]
    public string suffix = "";

    void Update()
    {
        if (GameManager.Instance == null || textElement == null) return;

        // В зависимости от выбранного типа, берем нужную переменную из GameManager
        switch (displayType)
        {
            case DisplayType.GlobalBankBalance:
                textElement.text = $"{prefix}{GameManager.Instance.globalBankBalance:N0}{suffix}";
                break;
                
            case DisplayType.CompletedQuotas:
                textElement.text = $"{prefix}{GameManager.Instance.completedQuotas}{suffix}";
                break;
        }
    }
}
