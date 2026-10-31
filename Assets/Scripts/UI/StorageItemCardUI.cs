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
    public Button equipButton;
    public TextMeshProUGUI equipButtonText;

    private ItemData currentItem;

    private void Awake()
    {
        if (equipButton == null)
        {
            equipButton = GetComponentInChildren<Button>();
        }
        if (equipButton != null && equipButtonText == null)
        {
            equipButtonText = equipButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(OnEquipButtonClicked);
        }
    }

    public void Setup(ItemData item, int ownedCount, int maxCapacity)
    {
        currentItem = item;
        if (item == null) return;

        if (itemIcon != null) itemIcon.sprite = item.icon;
        if (itemNameText != null) itemNameText.text = item.itemName;
        if (itemCapacityText != null) itemCapacityText.text = $"{ownedCount}/{maxCapacity}";

        if (capacityFillBar != null)
        {
            capacityFillBar.fillAmount = maxCapacity > 0 ? (float)ownedCount / maxCapacity : 0f;
        }

        // Проверяем, надет ли этот предмет прямо сейчас
        bool isEquipped = false;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
        {
            int slotIdx = (int)item.itemType;
            if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
            {
                var curSlot = PlayerInventory.Instance.slots[slotIdx];
                if (curSlot != null && curSlot.itemData == item)
                {
                    isEquipped = true;
                }
            }
        }

        if (equipButton != null)
        {
            if (isEquipped)
            {
                equipButton.interactable = true;
                if (equipButtonText != null)
                {
                    equipButtonText.text = "СНЯТЬ";
                    equipButtonText.color = new Color(1f, 0.6f, 0.3f, 1f);
                }
            }
            else if (ownedCount > 0)
            {
                equipButton.interactable = true;
                if (equipButtonText != null)
                {
                    equipButtonText.text = "ВЗЯТЬ";
                    equipButtonText.color = Color.white;
                }
            }
            else
            {
                equipButton.interactable = false;
                if (equipButtonText != null)
                {
                    equipButtonText.text = "НЕТ НА СКЛАДЕ";
                    equipButtonText.color = Color.gray;
                }
            }
        }
    }

    private void OnEquipButtonClicked()
    {
        if (currentItem == null || PlayerInventory.Instance == null || TeamStorageManager.Instance == null) return;

        int slotIdx = (int)currentItem.itemType;
        bool isCurrentlyEquipped = false;
        if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
        {
            var cur = PlayerInventory.Instance.slots[slotIdx];
            if (cur != null && cur.itemData == currentItem)
            {
                isCurrentlyEquipped = true;
            }
        }

        if (isCurrentlyEquipped)
        {
            // Снимаем предмет со слота и возвращаем на склад команды
            var oldEquipped = PlayerInventory.Instance.slots[slotIdx];
            if (oldEquipped != null)
            {
                oldEquipped.Unequip();
                PlayerInventory.Instance.slots[slotIdx] = null;
                TeamStorageManager.Instance.AddItem(currentItem, 1);
                PlayerInventory.Instance.onInventoryChanged?.Invoke();
                PlayerInventory.Instance.UpdateWeaponText();
            }
        }
        else
        {
            // Берем со склада в руки
            int owned = TeamStorageManager.Instance.GetItemCount(currentItem);
            if (owned <= 0) return;

            // Если в слоте уже лежит другой предмет — возвращаем его на склад без выбрасывания на пол
            EquipableItem replacedItem = (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length) ? PlayerInventory.Instance.slots[slotIdx] : null;
            ItemData replacedData = replacedItem != null ? replacedItem.itemData : null;

            if (replacedItem != null)
            {
                replacedItem.Unequip();
                PlayerInventory.Instance.slots[slotIdx] = null;
            }

            if (PlayerInventory.Instance.PickupItem(currentItem))
            {
                TeamStorageManager.Instance.RemoveItem(currentItem, 1);
                if (replacedData != null && replacedData != currentItem)
                {
                    TeamStorageManager.Instance.AddItem(replacedData, 1);
                }
            }
            else if (replacedItem != null)
            {
                // Если по какой-то причине не удалось взять новый, восстанавливаем прежний
                PlayerInventory.Instance.slots[slotIdx] = replacedItem;
                replacedItem.Equip();
            }
        }

        if (StoreUI.Instance != null)
        {
            StoreUI.Instance.RefreshUI();
        }
    }
}
