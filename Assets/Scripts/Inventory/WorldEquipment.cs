using UnityEngine;

public class WorldEquipment : MonoBehaviour, IInteractable
{
    [Tooltip("Какие данные добавить к игроку в слоты при поднятии")]
    public ItemData itemData;

    [Header("Persistent Bag Data (For Drops)")]
    public int storedMoney = 0;
    public int storedWeight = 0;

    public void Interact()
    {
        if (PlayerInventory.Instance != null && itemData != null)
        {
            // Пытаемся подобрать предмет
            if (PlayerInventory.Instance.PickupItem(itemData))
            {
                // Если это сумка и в ней были деньги (с пола) - переносим их в инвентарь
                BagTool bag = GameManager.Instance.GetHeldBag();
                if (bag != null && itemData.itemType == ItemType.Tool)
                {
                    bag.storedMoney = storedMoney;
                    bag.storedWeight = storedWeight;
                    Debug.Log($"[Persistent] Из сумки с пола извлечено: ${storedMoney}");
                }

                Destroy(gameObject);
            }
            else
            {
                Debug.Log($"[WorldEquipment] Слот для {itemData.itemName} уже занят!");
            }
        }
    }

    public string GetInteractText()
    {
        return itemData != null ? $"Pick up {itemData.itemName}" : "Pick up";
    }
}
