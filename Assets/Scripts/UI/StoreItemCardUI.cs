using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StoreItemCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemPriceText;
    public TextMeshProUGUI itemCapacityText;
    public TextMeshProUGUI itemDescriptionText;
    public Button addToCartButton;

    [Header("License & Exclusivity UI Tags")]
    public TextMeshProUGUI tagBadgeText;

    private ItemData currentItem;

    public void Setup(StoreCatalogEntry entry)
    {
        if (entry == null || entry.itemData == null) return;

        currentItem = entry.itemData;
        if (itemIcon != null) itemIcon.sprite = entry.itemData.icon;
        if (itemNameText != null) itemNameText.text = entry.itemData.itemName;
        if (itemPriceText != null) itemPriceText.text = $"${entry.price}";
        if (itemDescriptionText != null) itemDescriptionText.text = entry.description;

        int currentStorage = TeamStorageManager.Instance != null ? TeamStorageManager.Instance.GetItemCount(currentItem) : 0;
        if (itemCapacityText != null) itemCapacityText.text = $"Storage: {currentStorage}/{entry.maxCapacity}";

        bool isUnlocked = TeamStorageManager.Instance == null || TeamStorageManager.Instance.IsItemUnlocked(entry);
        bool isDonAvailable = TeamStorageManager.Instance == null || TeamStorageManager.Instance.IsDonAvailable(entry);

        if (tagBadgeText != null)
        {
            if (entry.isDonExclusive)
            {
                tagBadgeText.text = "<color=#ffd700>👑 Don Exclusive</color>";
                tagBadgeText.gameObject.SetActive(true);
            }
            else if (entry.requiresLicense)
            {
                tagBadgeText.text = "<color=#00ffff>🔒 License Required</color>";
                tagBadgeText.gameObject.SetActive(true);
            }
            else
            {
                tagBadgeText.gameObject.SetActive(false);
            }
        }

        if (addToCartButton != null)
        {
            addToCartButton.interactable = isUnlocked && isDonAvailable;
            addToCartButton.onClick.RemoveAllListeners();
            addToCartButton.onClick.AddListener(() =>
            {
                if (StoreUI.Instance != null)
                {
                    StoreUI.Instance.AddToCart(currentItem);
                }
            });
        }
    }
}
