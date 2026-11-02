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

        if (itemNameText != null)
        {
            itemNameText.text = item.itemName;
            itemNameText.color = new Color(0.12f, 0.08f, 0.05f, 1f); // Dark ink
        }

        if (itemQuantityText != null)
        {
            itemQuantityText.text = $"x{quantity}";
            itemQuantityText.color = new Color(0.35f, 0.28f, 0.22f, 1f); // Warm muted
        }

        if (itemSubtotalText != null)
        {
            itemSubtotalText.text = $"${unitPrice * quantity}";
            itemSubtotalText.color = new Color(0.55f, 0.12f, 0.1f, 1f); // Crimson/dark red
        }

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
