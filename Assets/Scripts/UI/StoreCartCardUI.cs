using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StoreCartCardUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemQuantityText;
    public TextMeshProUGUI itemSubtotalText;
    public Button addButton;
    public Button removeButton;

    public void Setup(ItemData item, int quantity, int unitPrice)
    {
        if (item == null) return;

        if (itemIcon != null) itemIcon.sprite = item.icon;
        if (itemNameText != null) itemNameText.text = item.itemName;
        if (itemQuantityText != null) itemQuantityText.text = $"x{quantity}";
        if (itemSubtotalText != null) itemSubtotalText.text = $"${unitPrice * quantity}";

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(() =>
            {
                if (StoreUI.Instance != null) StoreUI.Instance.AddToCart(item);
            });
        }

        if (removeButton != null)
        {
            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(() =>
            {
                if (StoreUI.Instance != null) StoreUI.Instance.RemoveFromCart(item);
            });
        }
    }
}
