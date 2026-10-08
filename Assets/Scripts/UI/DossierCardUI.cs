using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DossierCardUI : MonoBehaviour
{
    [Header("UI Text Fields (Stylized Document)")]
    public TextMeshProUGUI dossierHeaderNumberText; // e.g. "DOSSIER №1"
    public TextMeshProUGUI bankNameText;            // e.g. "FIRST LONE BANK"
    public TextMeshProUGUI difficultyText;          // e.g. "DIFFICULTY: 3 / 10"
    public TextMeshProUGUI timeLimitText;           // e.g. "TIME LIMIT: 04:30"
    public TextMeshProUGUI targetInfoText;          // e.g. "TARGET: 3-5 Rooms"
    public TextMeshProUGUI estimatedLootText;      // e.g. "EST. LOOT: ~$4,500 - $9,500"
    
    [Header("Visual Selection State")]
    public GameObject selectedStampOrBorder;       // Штамп "SELECTED" или обводка
    public Button cardButton;

    private BankDossier dossierData;
    private DossierSelectionUI ownerUI;

    public void Setup(BankDossier dossier, int index, DossierSelectionUI owner, bool isSelected)
    {
        dossierData = dossier;
        ownerUI = owner;

        if (dossierHeaderNumberText != null)
        {
            dossierHeaderNumberText.text = $"DOSSIER №{index + 1}";
        }

        if (dossier != null)
        {
            if (bankNameText != null) bankNameText.text = dossier.bankName;
            
            if (difficultyText != null)
            {
                difficultyText.text = $"Сложность: {dossier.difficultyLevel}";
            }

            if (timeLimitText != null)
            {
                string formattedTime = $"{(dossier.timeLimit / 60):00}:{(dossier.timeLimit % 60):00}";
                timeLimitText.text = $"Время: {formattedTime}";
            }

            if (targetInfoText != null)
            {
                targetInfoText.text = $"Комнаты: {dossier.minRooms}-{dossier.maxRooms}";
            }

            if (estimatedLootText != null)
            {
                estimatedLootText.text = $"${dossier.estimatedMinLoot}-${dossier.estimatedMaxLoot}";
            }
        }

        if (selectedStampOrBorder != null)
        {
            selectedStampOrBorder.SetActive(isSelected);
        }

        if (cardButton == null) cardButton = GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    private void OnCardClicked()
    {
        if (ownerUI != null && dossierData != null)
        {
            ownerUI.SelectDossier(dossierData);
        }
    }
}
