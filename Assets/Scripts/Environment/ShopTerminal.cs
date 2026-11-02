using UnityEngine;

public enum ShopItemType
{
    Weapon = 0,
    Ammo = 1,
    FullStoreUI = 2
}

public class ShopTerminal : MonoBehaviour, IInteractable
{
    [Header("Shop Settings")]
    public ShopItemType itemType = ShopItemType.FullStoreUI;
    
    [Header("Weapon settings (if Type == Weapon)")]
    public ItemData weaponItemData;
    public int weaponCost = 1500;

    [Header("Ammo settings (if Type == Ammo)")]
    public int ammoRefillCost = 300;
    public int ammoAmount = 30;

    public void Interact()
    {
        if (itemType == ShopItemType.FullStoreUI)
        {
            OpenFullStore();
            return;
        }

        if (GameManager.Instance == null || PlayerInventory.Instance == null)
        {
            Debug.LogError("[ShopTerminal] GameManager or PlayerInventory is null.");
            return;
        }

        if (itemType == ShopItemType.Weapon)
        {
            TryBuyWeapon();
        }
        else if (itemType == ShopItemType.Ammo)
        {
            TryBuyAmmo();
        }
    }

    private void OpenFullStore()
    {
        if (StoreUI.Instance != null)
        {
            StoreUI.Instance.OpenStore();
        }
        else
        {
            StoreUI store = FindAnyObjectByType<StoreUI>(FindObjectsInactive.Include);
            if (store != null)
            {
                store.OpenStore();
            }
            else
            {
                Debug.LogError("[ShopTerminal] StoreUI не найден на сцене!");
            }
        }
    }

    private void TryBuyWeapon()
    {
        if (weaponItemData == null)
        {
            Debug.LogWarning("[ShopTerminal] WeaponItemData is not assigned.");
            return;
        }

        // Check if player has slot 1 occupied
        if (PlayerInventory.Instance.slots != null && PlayerInventory.Instance.slots.Length > 1 && PlayerInventory.Instance.slots[1] != null)
        {
            Debug.Log("[ShopTerminal] Ваша кобура занята! Выбросите текущее оружие (G) перед покупкой.");
            return;
        }

        if (GameManager.Instance.sessionMoney < weaponCost)
        {
            Debug.Log($"[ShopTerminal] Недостаточно денег! Нужно: ${weaponCost}, доступно: ${GameManager.Instance.sessionMoney}");
            return;
        }

        // Attempt to pick up
        if (PlayerInventory.Instance.PickupItem(weaponItemData))
        {
            GameManager.Instance.sessionMoney -= weaponCost;
            Debug.Log($"[ShopTerminal] Куплено оружие: {weaponItemData.itemName} за ${weaponCost}. Оставшиеся карманные деньги: ${GameManager.Instance.sessionMoney}");
            
            if (GameManager.Instance.onMoneyChanged != null)
            {
                GameManager.Instance.onMoneyChanged.Invoke();
            }
        }
    }

    private void TryBuyAmmo()
    {
        // Find equipped weapon
        if (PlayerInventory.Instance.slots == null || PlayerInventory.Instance.slots.Length <= 1)
        {
            return;
        }

        WeaponItem equippedWeapon = PlayerInventory.Instance.slots[1] as WeaponItem;

        if (equippedWeapon == null)
        {
            Debug.Log("[ShopTerminal] У вас нет оружия для закупки патронов!");
            return;
        }

        if (GameManager.Instance.sessionMoney < ammoRefillCost)
        {
            Debug.Log($"[ShopTerminal] Недостаточно денег! Нужно: ${ammoRefillCost}, доступно: ${GameManager.Instance.sessionMoney}");
            return;
        }

        // Refill ammo
        equippedWeapon.reserveAmmo += ammoAmount;
        GameManager.Instance.sessionMoney -= ammoRefillCost;

        string weaponName = equippedWeapon.itemData != null ? equippedWeapon.itemData.itemName : equippedWeapon.name;
        Debug.Log($"[ShopTerminal] Куплено {ammoAmount} патронов для {weaponName} за ${ammoRefillCost}. Всего в запасе: {equippedWeapon.reserveAmmo}");
        
        equippedWeapon.RefreshWeaponUI();

        if (GameManager.Instance.onMoneyChanged != null)
        {
            GameManager.Instance.onMoneyChanged.Invoke();
        }
    }

    public string GetInteractText()
    {
        if (itemType == ShopItemType.FullStoreUI)
        {
            return "[E] Оружейная и склад (Магазин / Загрузка)";
        }
        else if (itemType == ShopItemType.Weapon)
        {
            string weaponName = weaponItemData != null ? weaponItemData.itemName : "Weapon";
            return $"Купить {weaponName} (${weaponCost})";
        }
        else
        {
            return $"Пополнить патроны (${ammoRefillCost})";
        }
    }
}
