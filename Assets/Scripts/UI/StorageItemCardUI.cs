using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StorageItemCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemCapacityText;
    public Image capacityFillBar;

    public void Setup(ItemData item, int ownedCount, int maxCapacity)
    {
        if (item == null) return;

        if (itemIcon != null) itemIcon.sprite = item.icon;
        if (itemNameText != null) itemNameText.text = item.itemName;
        if (itemCapacityText != null) itemCapacityText.text = $"{ownedCount}/{maxCapacity}";

        if (capacityFillBar != null)
        {
            capacityFillBar.fillAmount = maxCapacity > 0 ? (float)ownedCount / maxCapacity : 0f;
        }
    }
}
