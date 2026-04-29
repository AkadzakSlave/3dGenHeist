using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotUI
    {
        [Tooltip("Картинка самого предмета (В центре слота)")]
        public Image itemIcon;
        [Tooltip("Рамка/Подсветка, которая включается, если слот активен")]
        public GameObject activeHighlight;
    }

    [Header("UI Slots")]
    [Tooltip("0 - Инструмент, 1 - Оружие")]
    public SlotUI[] uiSlots = new SlotUI[2];

    private void Start()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.onInventoryChanged.AddListener(UpdateUI);
            // Запрашиваем изначальную отрисовку при старте с небольшой задержкой, 
            // чтобы PlayerInventory успел инициализироваться
            Invoke("UpdateUI", 0.1f);
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.onInventoryChanged.RemoveListener(UpdateUI);
        }
    }

    public void UpdateUI()
    {
        if (PlayerInventory.Instance == null) return;

        for (int i = 0; i < uiSlots.Length; i++)
        {
            // На всякий случай защита от выхода за пределы массива
            if (i >= PlayerInventory.Instance.slots.Length) continue;

            EquipableItem currentItem = PlayerInventory.Instance.slots[i];
            
            // Если в слоте есть предмет
            if (currentItem != null && currentItem.itemData != null)
            {
                if (currentItem.itemData.icon != null)
                {
                    uiSlots[i].itemIcon.sprite = currentItem.itemData.icon;
                    uiSlots[i].itemIcon.enabled = true;
                }
            }
            else
            {
                // Если слот пуст - выключаем иконку
                uiSlots[i].itemIcon.sprite = null;
                uiSlots[i].itemIcon.enabled = false;
            }

            // Подсветка активного слота
            bool isActive = (i == PlayerInventory.Instance.activeSlotIndex);
            if (uiSlots[i].activeHighlight != null) uiSlots[i].activeHighlight.SetActive(isActive);
        }
    }
}
