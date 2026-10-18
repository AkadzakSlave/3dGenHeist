using UnityEngine;

public class HeistExtractionZone : MonoBehaviour
{
    [Header("Settings")]
    public string zoneName = "Truck Cargo Area";
    public Color debugColor = Color.green;

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, не сумка ли это прилетела
        WorldEquipment droppedItem = other.GetComponent<WorldEquipment>();
        
        if (droppedItem != null && droppedItem.itemData != null)
        {
            if (droppedItem.itemData.itemType == ItemType.Tool && droppedItem.storedMoney > 0)
            {
                ExtractBag(droppedItem);
            }
        }
    }

    private void ExtractBag(WorldEquipment bagOnFloor)
    {
        if (GameManager.Instance == null) return;

        int moneyExtracted = bagOnFloor.storedMoney;
        Debug.Log($"<color=green>[Extraction] Сумка попала в зону! Выгружено: ${moneyExtracted}</color>");
        
        // Добавляем деньги к общему счету миссии
        GameManager.Instance.depositedMoney += moneyExtracted;
        
        // Обнуляем данные в объекте на полу (чтобы нельзя было забрать дважды)
        bagOnFloor.storedMoney = 0;
        bagOnFloor.storedWeight = 0;

        // Вызываем событие обновления UI
        GameManager.Instance.onMoneyChanged?.Invoke();

        if (HeistUI.Instance != null && moneyExtracted > 0)
        {
            HeistUI.Instance.ShowNotification($"+${moneyExtracted}", new Color(0.35f, 0.95f, 0.45f, 1f));
        }
    }

    private void OnDrawGizmos()
    {
        // Визуализация зоны в редакторе
        Gizmos.color = debugColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
