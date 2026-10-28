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
        Initialize(false);
    }

    public void Initialize(bool isHighValue)
    {
        if (data == null) return;
        
        if (isHighValue)
        {
            // Для богатых точек (сейф) генерируем верхний диапазон или бонус к цене
            int highMin = Mathf.RoundToInt(Mathf.Lerp(data.minValue, data.maxValue, 0.5f));
            int highMax = Mathf.RoundToInt(data.maxValue * 1.4f);
            value = Random.Range(highMin, highMax + 1);
        }
        else
        {
            // Рандомим стоимость из диапазона в LootData
            value = Random.Range(data.minValue, data.maxValue + 1);
        }
        
        weight = data.weight;
    }

    public void Interact()
    {
        Collect();
    }

    public string GetInteractText()
    {
        string name = data != null ? data.itemName : "Loot";
        BagTool bag = null;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.GetActiveItem() is BagTool heldBag)
        {
            bag = heldBag;
        }
        else if (GameManager.Instance != null)
        {
            bag = GameManager.Instance.GetHeldBag();
        }

        if (bag == null)
        {
            return $"<color=#FF6666>{name} (${value}) - Нужна сумка!</color>";
        }
        else if (bag.storedWeight + weight > bag.maxCapacity)
        {
            return $"<color=#FF9933>{name} (${value}, {weight}kg) - Сумка полна!</color>";
        }

        return $"Забрать {name} (${value}, {weight}kg)";
    }

    public void Collect()
    {
        if (PlayerInventory.Instance == null) return;

        BagTool bag = null;
        if (PlayerInventory.Instance.GetActiveItem() is BagTool heldBag)
        {
            bag = heldBag;
        }
        else if (GameManager.Instance != null)
        {
            bag = GameManager.Instance.GetHeldBag();
        }
        
        // Лут собирается в сумку (в руках или в слоте инвентаря)
        if (bag != null)
        {
            if (bag.AddLoot(value, weight))
            {
                string itemName = data != null ? data.itemName : "Loot";
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.ShowNotification($"+${value} ({itemName})", new Color(0.2f, 1f, 0.45f, 1f));
                }

                if (GameManager.Instance != null) 
                {
                    Debug.Log($"[Loot] Подобрано: {itemName}. В сумке: ${bag.storedMoney}, Вес: {GameManager.Instance.currentWeight}/{GameManager.Instance.maxWeight}кг");
                    GameManager.Instance.onMoneyChanged?.Invoke();
                    GameManager.Instance.RecalculateMapLoot();
                }

                if (PlayerInventory.Instance != null)
                {
                    PlayerInventory.Instance.RefreshActiveItemUI();
                    PlayerInventory.Instance.onInventoryChanged?.Invoke();
                }
                
                PlayPickUpSound();
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("<color=red>[Loot] СУМКА ЗАПОЛНЕНА!</color>");
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.ShowNotification("Сумка переполнена!", new Color(1f, 0.35f, 0.35f, 1f));
                    HeistUI.Instance.SetInteractionText("<color=red>Сумка переполнена!</color>");
                }
            }
        }
        else
        {
            Debug.Log("[Loot] Нужна сумка в инвентаре!");
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.ShowNotification("Нужна сумка в инвентаре!", new Color(1f, 0.85f, 0.25f, 1f));
                HeistUI.Instance.SetInteractionText("<color=yellow>Нужна сумка в инвентаре!</color>");
            }
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