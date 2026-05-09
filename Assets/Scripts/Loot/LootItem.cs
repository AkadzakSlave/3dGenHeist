using UnityEngine;
using FMODUnity;

// Типы лута для FMOD параметра LootType (0=Cash, 1=Gold, 2=Case, 3=Brilliant)
public enum LootType { Cash = 0, Gold = 1, Case = 2, Brilliant = 3 }

public class LootItem : MonoBehaviour, IInteractable
{
    [Header("Data Reference")]
    public LootData data;
    
    [Header("Runtime Info (Calculated)")]
    public int value;
    public int weight;

    [Header("Audio")]
    [EventRef] public string pickUpSound = "event:/SFX/Loot/Loot_PickUp";

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (data == null) return;
        
        // Рандомим стоимость из диапазона в LootData
        value = Random.Range(data.minValue, data.maxValue + 1);
        weight = data.weight;
    }

    public void Interact()
    {
        Collect();
    }

    public string GetInteractText()
    {
        string name = data != null ? data.itemName : "Loot";
        return $"Collect {name} (${value})";
    }

    public void Collect()
    {
        if (PlayerInventory.Instance == null) return;

        EquipableItem activeItem = PlayerInventory.Instance.GetActiveItem();
        
        // Лут можно собирать только когда в руках Сумка
        if (activeItem is BagTool bag)
        {
            if (bag.AddLoot(value, weight))
            {
                if (GameManager.Instance != null) 
                    GameManager.Instance.onMoneyChanged?.Invoke();
                
                PlayPickUpSound();
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("[Loot] Сумка полна!");
            }
        }
        else
        {
            Debug.Log("[Loot] Нужна сумка в руках!");
        }
    }

    private void PlayPickUpSound()
    {
        if (string.IsNullOrEmpty(pickUpSound)) return;

        FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(pickUpSound);
        
        // Устанавливаем параметр LootType для выбора нужного звука в FMOD
        if (data != null)
        {
            instance.setParameterByName("LootType", (float)data.lootType);
        }

        // 3D позиционирование
        RuntimeManager.AttachInstanceToGameObject(instance, transform, GetComponent<Rigidbody>());
        
        instance.start();
        instance.release();
    }
}