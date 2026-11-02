using UnityEngine;

/// <summary>
/// Терминал "Хранилище вылазки", расположенный рядом с машиной / фургоном на улице.
/// Игроки взаимодействуют с ним по кнопке [E], чтобы брать снаряжение в руки прямо из кузова машины.
/// </summary>
public class HeistTruckStorageTerminal : MonoBehaviour, IInteractable
{
    [Header("Terminal Settings")]
    public string promptTitle = "Хранилище вылазки (Снаряжение в машине)";

    public void Interact()
    {
        StoreUI store = StoreUI.Instance != null ? StoreUI.Instance : FindAnyObjectByType<StoreUI>(FindObjectsInactive.Include);
        if (store != null)
        {
            if (store.IsStoreOpen)
            {
                store.CloseStore();
            }
            else
            {
                store.OpenStore(truckStorageMode: true);
            }
        }
        else
        {
            Debug.LogError("[HeistTruckStorageTerminal] StoreUI не найден на сцене!");
        }
    }

    public string GetInteractText()
    {
        int count = HeistLoadoutStorage.Instance != null ? HeistLoadoutStorage.Instance.GetTotalCount() : 0;
        return $"[E] {promptTitle} [{count} шт.]";
    }
}

