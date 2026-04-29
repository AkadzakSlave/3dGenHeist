using UnityEngine;

public class LootItem : MonoBehaviour, IInteractable
{
    public string itemName = "Cash Bundle";
    public int value = 100;
    public int weight = 5;
    
    [Header("Audio")]
    public AudioClip collectSound;

    public void Interact()
    {
        Collect();
    }

    public string GetInteractText()
    {
        return $"Collect {itemName} (${value})";
    }

    public void Collect()
    {
        if (PlayerInventory.Instance != null)
        {
            EquipableItem activeItem = PlayerInventory.Instance.GetActiveItem();
            if (activeItem is BagTool bag)
            {
                if (bag.AddLoot(value, weight))
                {
                    if (GameManager.Instance != null) GameManager.Instance.onMoneyChanged?.Invoke();
                    
                    if (collectSound != null)
                    {
                        AudioSource.PlayClipAtPoint(collectSound, transform.position);
                    }

                    Debug.Log($"[Loot] Собрано: {itemName} за ${value}");
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.Log("[Loot] Невозможно собрать: Возьмите сумку в руки!");
            }
        }
    }
}
