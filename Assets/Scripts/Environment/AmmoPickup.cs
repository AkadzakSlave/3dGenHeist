using UnityEngine;
using FMODUnity;

public class AmmoPickup : MonoBehaviour, IInteractable
{
    [Header("Ammo Settings")]
    [Tooltip("Количество добавляемых патронов")]
    public int ammoAmount = 30;

    [Header("Audio (FMOD)")]
    [Tooltip("Звук подбора патронов")]
    public EventReference pickupSound;

    public void Interact()
    {
        if (PlayerInventory.Instance == null)
        {
            Debug.LogError("[AmmoPickup] PlayerInventory.Instance не найден!");
            return;
        }

        bool added = false;
        
        // 1. Пытаемся добавить патроны к оружию во втором слоте (слот 1)
        if (PlayerInventory.Instance.slots != null && PlayerInventory.Instance.slots.Length > 1)
        {
            WeaponItem weapon = PlayerInventory.Instance.slots[1] as WeaponItem;
            if (weapon != null)
            {
                weapon.reserveAmmo += ammoAmount;
                weapon.RefreshWeaponUI();
                added = true;
                Debug.Log($"[AmmoPickup] Добавлено {ammoAmount} патронов для {weapon.itemData.itemName}. Всего в запасе: {weapon.reserveAmmo}");
            }
        }

        // 2. Если в слоте нет оружия, ищем в списке всех возможных предметов игрока
        if (!added && PlayerInventory.Instance.allPossibleItems != null)
        {
            foreach (var item in PlayerInventory.Instance.allPossibleItems)
            {
                if (item is WeaponItem weapon)
                {
                    weapon.reserveAmmo += ammoAmount;
                    added = true;
                    Debug.Log($"[AmmoPickup] Добавлено {ammoAmount} патронов для {weapon.itemData.itemName} в резерв. Всего: {weapon.reserveAmmo}");
                    break;
                }
            }
        }

        if (added)
        {
            // Воспроизводим звук подбора
            if (!pickupSound.IsNull)
            {
                RuntimeManager.PlayOneShot(pickupSound, transform.position);
            }

            Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("[AmmoPickup] Не удалось найти подходящее оружие у игрока, чтобы добавить патроны.");
        }
    }

    public string GetInteractText()
    {
        return $"Pick up Ammo (+{ammoAmount})";
    }
}
