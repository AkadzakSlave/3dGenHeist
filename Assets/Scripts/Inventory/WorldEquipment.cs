using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class WorldEquipment : MonoBehaviour, IInteractable
{
    [Header("Audio (FMOD)")]
    public EventReference dropSound;

    [Tooltip("Какие данные добавить к игроку в слоты при поднятии")]
    public ItemData itemData;

    [Header("Persistent Bag Data (For Drops)")]
    public int storedMoney = 0;
    public int storedWeight = 0;

    [Header("Storage Rack Association")]
    public bool isFromTeamStorage = false;

    private float lastSoundTime = 0f;

    private void OnCollisionEnter(Collision collision)
    {
        if (dropSound.IsNull) return;
        
        // Защита от спама звуком (задержка 0.5с между ударами)
        if (Time.time - lastSoundTime < 0.5f) return;
        
        // Звук играет только при достаточно сильном ударе
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce < 1.0f) return;

        lastSoundTime = Time.time;

        EventInstance drop = RuntimeManager.CreateInstance(dropSound);
        
        if (itemData != null)
        {
            // 1. Тип предмета
            drop.setParameterByName("MainType", (float)itemData.fmodMainType);
            
            // 2. Вес предмета (Базовый вес + вес лута внутри)
            float totalWeight = itemData.baseWeight + storedWeight;
            drop.setParameterByName("ItemWeight", totalWeight);
        }

        // 3. Сила удара
        drop.setParameterByName("ImpactForce", impactForce);
        
        RuntimeManager.AttachInstanceToGameObject(drop, gameObject, GetComponent<Rigidbody>());
        drop.start();
        drop.release();
    }

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

                if (isFromTeamStorage && TeamStorageManager.Instance != null)
                {
                    TeamStorageManager.Instance.RemoveItem(itemData, 1);
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
