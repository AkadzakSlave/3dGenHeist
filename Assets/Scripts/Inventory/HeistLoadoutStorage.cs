using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Хранилище вылазки (снаряжение, загруженное в машину/фургон для текущей 3-дневной операции).
/// Игроки берут отсюда предметы в руки перед заходом в банк.
/// Если снаряжение вынесено на улицу или эвакуировано в руках — оно сохраняется здесь.
/// После 3-го дня (или визита к Дону) все оставшиеся предметы возвращаются в Общее хранилище банды.
/// </summary>
public class HeistLoadoutStorage : MonoBehaviour
{
    public static HeistLoadoutStorage Instance { get; private set; }

    [Header("Events")]
    public UnityEvent onStorageChanged;

    private readonly Dictionary<ItemData, int> itemsOnHeist = new Dictionary<ItemData, int>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Автоматически гарантирует существование менеджера на сцене.
    /// </summary>
    public static HeistLoadoutStorage EnsureInstance()
    {
        if (Instance == null)
        {
            Instance = FindAnyObjectByType<HeistLoadoutStorage>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                GameObject go = new GameObject("HeistLoadoutStorage");
                Instance = go.AddComponent<HeistLoadoutStorage>();
                DontDestroyOnLoad(go);
            }
        }
        return Instance;
    }

    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;
        return itemsOnHeist.TryGetValue(item, out int count) ? count : 0;
    }

    public int GetTotalCount()
    {
        int sum = 0;
        foreach (var kvp in itemsOnHeist) sum += kvp.Value;
        return sum;
    }

    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int cur = GetItemCount(item);
        itemsOnHeist[item] = cur + amount;

        onStorageChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;

        int cur = GetItemCount(item);
        if (cur < amount) return false;

        int rem = cur - amount;
        if (rem > 0)
        {
            itemsOnHeist[item] = rem;
        }
        else
        {
            itemsOnHeist.Remove(item);
        }

        onStorageChanged?.Invoke();
        return true;
    }

    public List<KeyValuePair<ItemData, int>> GetAllItems()
    {
        var list = new List<KeyValuePair<ItemData, int>>();
        foreach (var kvp in itemsOnHeist)
        {
            list.Add(new KeyValuePair<ItemData, int>(kvp.Key, kvp.Value));
        }
        return list;
    }

    /// <summary>
    /// Переносит предмет из Общего склада (TeamStorageManager) в Хранилище вылазки (в машину).
    /// </summary>
    public bool LoadFromMainStorage(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (TeamStorageManager.Instance == null) return false;

        if (TeamStorageManager.Instance.GetItemCount(item) < amount)
        {
            Debug.LogWarning($"[HeistStorage] На основном складе недостаточно '{item.itemName}'!");
            return false;
        }

        if (TeamStorageManager.Instance.RemoveItem(item, amount))
        {
            AddItem(item, amount);
            Debug.Log($"<color=cyan>[HeistStorage] '{item.itemName}' ({amount} шт.) загружен в Хранилище вылазки (машину)!</color>");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Возвращает предмет из Хранилища вылазки обратно на Общий склад.
    /// </summary>
    public bool UnloadToMainStorage(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        if (TeamStorageManager.Instance == null) return false;

        if (GetItemCount(item) < amount) return false;

        if (RemoveItem(item, amount))
        {
            TeamStorageManager.Instance.AddItem(item, amount);
            Debug.Log($"<color=cyan>[HeistStorage] '{item.itemName}' ({amount} шт.) возвращен на Общий склад!</color>");
            return true;
        }

        return false;
    }

    /// <summary>
    /// После 3-го дня: всё оставшееся снаряжение в машине и в руках игроков переносится обратно в Общий склад банды.
    /// </summary>
    public void ReturnAllToMainStorage()
    {
        if (TeamStorageManager.Instance == null) return;

        int returnedCount = 0;

        // 1. Возвращаем всё из машины
        foreach (var kvp in itemsOnHeist)
        {
            if (kvp.Key != null && kvp.Value > 0)
            {
                TeamStorageManager.Instance.AddItem(kvp.Key, kvp.Value);
                returnedCount += kvp.Value;
            }
        }
        itemsOnHeist.Clear();

        // 2. Возвращаем снаряжение из рук игроков
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
        {
            for (int i = 0; i < PlayerInventory.Instance.slots.Length; i++)
            {
                var slot = PlayerInventory.Instance.slots[i];
                if (slot != null && slot.itemData != null)
                {
                    TeamStorageManager.Instance.AddItem(slot.itemData, 1);
                    returnedCount++;
                }
            }
            PlayerInventory.Instance.ClearInventory();
            if (PlayerInventory.Instance.autoEquipDefaultLoadout)
            {
                PlayerInventory.Instance.EnsureDefaultLoadout();
            }
        }

        onStorageChanged?.Invoke();
        Debug.Log($"<color=green>[HeistStorage] Операция завершена! Все оставшиеся предметы ({returnedCount} шт.) успешно возвращены в Общее хранилище банды.</color>");
    }
}

