using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapStateCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI stateNameText;
    public TextMeshProUGUI feeText;
    public Image iconImage;
    public Button selectButton;
    public GameObject selectedBorder;

    private LevelPreset presetData;
    private MapSelectionUI ownerUI;

    public void Setup(LevelPreset preset, MapSelectionUI owner, bool isSelected)
    {
        presetData = preset;
        ownerUI = owner;

        if (stateNameText != null) stateNameText.text = preset.levelName;
        if (feeText != null) feeText.text = preset.entryFee > 0 ? $"${preset.entryFee}" : "Бесплатно";
        if (iconImage != null && preset.stateIcon != null)
        {
            iconImage.sprite = preset.stateIcon;
            iconImage.enabled = true;
        }

        if (selectedBorder != null) selectedBorder.SetActive(isSelected);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                if (ownerUI != null) ownerUI.SelectState(presetData);
            });
        }
    }
}
