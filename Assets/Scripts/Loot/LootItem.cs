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
    public EventReference pickUpSound;

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
        return $"{name} (${value}, {weight}kg)";
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
                {
                    string itemName = data != null ? data.itemName : "Loot";
                    Debug.Log($"[Loot] Подобрано: {itemName}. В сумке: ${GameManager.Instance.bagMoney}, Вес: {GameManager.Instance.currentWeight}/{GameManager.Instance.maxWeight}кг");
                    GameManager.Instance.onMoneyChanged?.Invoke();
                }
                
                PlayPickUpSound();
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("<color=red>[Loot] СУМКА ЗАПОЛНЕНА!</color>");
            }
        }
        else
        {
            Debug.Log("[Loot] Нужна сумка в руках!");
        }
    }

    private void PlayPickUpSound()
    {
        if (pickUpSound.IsNull) return;

        FMOD.Studio.EventInstance instance = RuntimeManager.CreateInstance(pickUpSound);
        
        // Устанавливаем параметр LootType для выбора нужного звука в FMOD
        if (data != null)
        {
            instance.setParameterByName("LootType", (float)data.lootType);
        }

        // Исправленный метод (передаем GameObject вместо Transform)
        RuntimeManager.AttachInstanceToGameObject(instance, gameObject, GetComponent<Rigidbody>());
        
        instance.start();
        instance.release();
    }
}