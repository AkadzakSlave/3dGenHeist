using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapStateNodeUI : MonoBehaviour
{
    [Header("State Settings")]
    public LevelPreset statePreset;

    [Header("Visual Components")]
    public Image stateImage;           // Спрайт силуэта/картинки штата на карте
    public GameObject selectedBorder;  // Рамка или обводка/свечение при выборе
    public TextMeshProUGUI stateLabel; // Название штата поверх (опционально)
    public Button nodeButton;

    [Header("Lock Status")]
    public GameObject lockIcon;        // Иконка замочка на закрытом штате
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    [Header("Color Highlights")]
    public bool changeImageColor = true;
    public Color normalColor = new Color(0.8f, 0.8f, 0.8f, 0.6f);
    public Color selectedColor = new Color(1f, 0.85f, 0.2f, 1f);

    private MapSelectionUI ownerUI;

    public bool IsUnlocked()
    {
        if (statePreset == null) return false;
        if (GameManager.Instance == null) return true;

        if (GameManager.Instance.unlockedPresets == null || GameManager.Instance.unlockedPresets.Count == 0)
        {
            return true;
        }

        return GameManager.Instance.unlockedPresets.Contains(statePreset);
    }

    public void InitNode(MapSelectionUI owner)
    {
        ownerUI = owner;
        if (nodeButton == null) nodeButton = GetComponent<Button>();

        if (stateLabel != null && statePreset != null)
        {
            stateLabel.text = statePreset.levelName;
        }

        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.onClick.AddListener(OnNodeClicked);
        }
    }

    public void SetSelected(bool isSelected)
    {
        bool unlocked = IsUnlocked();

        if (lockIcon != null)
        {
            lockIcon.SetActive(!unlocked);
        }

        if (selectedBorder != null)
        {
            selectedBorder.SetActive(isSelected);
        }

        if (changeImageColor && stateImage != null)
        {
            if (!unlocked)
            {
                stateImage.color = lockedColor;
            }
            else
            {
                stateImage.color = isSelected ? selectedColor : normalColor;
            }
        }
    }

    private void OnNodeClicked()
    {
        if (ownerUI != null && statePreset != null)
        {
            ownerUI.SelectState(statePreset);
        }
    }
}
