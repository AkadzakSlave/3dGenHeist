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
    public Button instantBuyButton;
    public Image capacityBarFill;

    [Header("License & Exclusivity UI Tags")]
    public TextMeshProUGUI tagBadgeText;

    private ItemData currentItem;

    public void Setup(StoreCatalogEntry entry)
    {
        if (entry == null || entry.itemData == null) return;

        currentItem = entry.itemData;
        if (itemIcon != null) itemIcon.sprite = entry.itemData.icon;

        // Принудительно задаем приятные темные чернильные цвета стилистики 1920-х (Нуар)
        if (itemNameText != null)
        {
            itemNameText.text = entry.itemData.itemName;
            itemNameText.color = new Color(0.12f, 0.08f, 0.05f, 1f);
        }

        if (itemPriceText != null)
        {
            itemPriceText.text = $"${entry.price}";
            itemPriceText.color = Color.white;
        }

        if (itemDescriptionText != null)
        {
            itemDescriptionText.text = entry.description;
            itemDescriptionText.color = new Color(0.35f, 0.28f, 0.22f, 1f);
        }

        int currentStorage = TeamStorageManager.Instance != null ? TeamStorageManager.Instance.GetItemCount(currentItem) : 0;
        if (itemCapacityText != null)
        {
            itemCapacityText.text = $"Storage: {currentStorage}/{entry.maxCapacity}";
            itemCapacityText.color = new Color(0.2f, 0.15f, 0.1f, 1f);
        }

        if (capacityBarFill != null)
        {
            float fillRatio = entry.maxCapacity > 0 ? (float)currentStorage / entry.maxCapacity : 0f;
            capacityBarFill.fillAmount = Mathf.Clamp01(fillRatio);
        }

        // Теги лицензий скрываем, чтобы не загромождать карточки товара
        if (tagBadgeText != null)
        {
            tagBadgeText.gameObject.SetActive(false);
        }

        // 1. Клик по всей карточке -> Добавление в Корзину
        if (addToCartButton != null)
        {
            addToCartButton.interactable = true;
            addToCartButton.onClick.RemoveAllListeners();
            addToCartButton.onClick.AddListener(() =>
            {
                if (StoreUI.Instance != null)
                {
                    StoreUI.Instance.AddToCart(currentItem);
                }
            });
        }

        // 2. Клик по красной кнопке с ценой ($XX) -> Мгновенная прямая покупка в руки / на склад
        if (instantBuyButton != null)
        {
            instantBuyButton.interactable = true;
            instantBuyButton.onClick.RemoveAllListeners();
            instantBuyButton.onClick.AddListener(() =>
            {
                if (StoreUI.Instance != null)
                {
                    StoreUI.Instance.DirectBuyItem(currentItem);
                }
            });
        }
    }
}
