using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TeamStorageManager : MonoBehaviour
{
    public static TeamStorageManager Instance { get; private set; }

    [Header("Catalog Reference")]
    public StoreCatalogDatabase catalog;

    [Header("Initial Default Storage")]
    public List<StorageItemQuantity> initialStorage = new List<StorageItemQuantity>();

    [Header("Events")]
    public UnityEvent onStorageChanged;

    [Serializable]
    public struct StorageItemQuantity
    {
        public ItemData item;
        public int count;
    }

    private Dictionary<ItemData, int> ownedItems = new Dictionary<ItemData, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeStorage();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeStorage()
    {
        ownedItems.Clear();
        foreach (var entry in initialStorage)
        {
            if (entry.item != null && entry.count > 0)
            {
                ownedItems[entry.item] = entry.count;
            }
        }
    }

    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;
        return ownedItems.TryGetValue(item, out int count) ? count : 0;
    }

    public int GetMaxCapacity(ItemData item)
    {
        if (item == null || catalog == null) return 99;
        StoreCatalogEntry entry = catalog.GetEntry(item);
        return entry != null ? entry.maxCapacity : 99;
    }

    public bool CanAddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        int current = GetItemCount(item);
        int max = GetMaxCapacity(item);
        return (current + amount) <= max;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int current = GetItemCount(item);
        ownedItems[item] = current + amount;

        onStorageChanged?.Invoke();
        SyncStorageMultiplayer();
        return true;
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int current = GetItemCount(item);
        if (current < amount) return false;

        int updated = current - amount;
        if (updated > 0)
        {
            ownedItems[item] = updated;
        }
        else
        {
            ownedItems.Remove(item);
        }

        onStorageChanged?.Invoke();
        SyncStorageMultiplayer();
        return true;
    }

    public bool PurchaseCart(List<KeyValuePair<ItemData, int>> cartItems, int totalCost)
    {
        if (cartItems == null || cartItems.Count == 0) return false;
        if (GameManager.Instance == null) return false;

        int availableMoney = GameManager.Instance.sessionMoney;
        if (availableMoney < totalCost)
        {
            Debug.LogWarning($"[TeamStorageManager] Недостаточно средств для покупки! Требуется: ${totalCost}, имеется: ${availableMoney}");
            return false;
        }

        // Validate capacities before processing
        foreach (var pair in cartItems)
        {
            if (!CanAddItem(pair.Key, pair.Value))
            {
                Debug.LogWarning($"[TeamStorageManager] Превышен лимит вместимости для {pair.Key.itemName}!");
                return false;
            }
        }

        // Deduct money
        GameManager.Instance.sessionMoney -= totalCost;

        // Add items to storage
        foreach (var pair in cartItems)
        {
            int current = GetItemCount(pair.Key);
            ownedItems[pair.Key] = current + pair.Value;
        }

        if (GameManager.Instance.onMoneyChanged != null)
        {
            GameManager.Instance.onMoneyChanged.Invoke();
        }

        onStorageChanged?.Invoke();
        SyncStorageMultiplayer();

        Debug.Log($"[TeamStorageManager] Корзина успешно куплена за ${totalCost}. Предметы отправлены в Хранилище!");
        return true;
    }

    public List<KeyValuePair<ItemData, int>> GetAllOwnedItems()
    {
        var result = new List<KeyValuePair<ItemData, int>>();
        foreach (var kvp in ownedItems)
        {
            result.Add(new KeyValuePair<ItemData, int>(kvp.Key, kvp.Value));
        }
        return result;
    }

    // Extension Hooks
    public bool IsItemUnlocked(StoreCatalogEntry entry)
    {
        if (entry == null) return false;
        if (entry.requiresLicense)
        {
            // Placeholder: Hook for future license system
            return true;
        }
        return true;
    }

    public bool IsDonAvailable(StoreCatalogEntry entry)
    {
        if (entry == null) return false;
        if (entry.isDonExclusive)
        {
            // Placeholder: Hook for Don-exclusive items
            return true;
        }
        return true;
    }

    private void SyncStorageMultiplayer()
    {
        // Placeholder hook for NGO NetworkVariable / ServerRpc synchronization
    }
}
