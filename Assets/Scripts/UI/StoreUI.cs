using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class StoreUI : MonoBehaviour
{
    public static StoreUI Instance { get; private set; }

    [Header("Main Panels")]
    public GameObject mainStoreWindow;
    public GameObject storeTabPanel;
    public GameObject storageTabPanel;

    [Header("Tab Buttons")]
    public Button storeTabButton;
    public Button storageTabButton;
    public Button closeButton;

    [Header("Store Tab References")]
    public Transform catalogContainer;
    public Transform cartContainer;
    public TextMeshProUGUI totalCostText;
    public TextMeshProUGUI availableMoneyText;
    public TextMeshProUGUI remainingMoneyText;
    public Button purchaseButton;
    public Button clearCartButton;

    [Header("Storage Tab References")]
    public Transform storageContainer;
    public TextMeshProUGUI storageStatusText;

    [Header("Loadout Bar (Inspector / Equipment)")]
    public TextMeshProUGUI loadoutSlot1Text;
    public TextMeshProUGUI loadoutSlot2Text;
    public TextMeshProUGUI loadoutSlot3Text;
    public TextMeshProUGUI loadoutAmmoText;
    public TextMeshProUGUI loadoutWeightText;

    [Header("Ammo Quick Purchase Buttons")]
    public Button buyLightAmmoButton;
    public Button buyMediumAmmoButton;
    public Button buyShotgunAmmoButton;

    [Header("Item Card Prefabs")]
    public GameObject itemCardPrefab;
    public GameObject cartCardPrefab;
    public GameObject storageCardPrefab;

    [Header("Input Reader Reference")]
    public InputReader inputReader;

    private readonly Dictionary<ItemData, int> shoppingCart = new Dictionary<ItemData, int>();
    private ShopCategory activeCategory = ShopCategory.All;
    private bool isOpen = false;
    public bool IsStoreOpen => isOpen || (mainStoreWindow != null && mainStoreWindow.activeSelf);

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (mainStoreWindow != null) mainStoreWindow.SetActive(false);

        if (storeTabButton != null) storeTabButton.onClick.AddListener(ShowStoreTab);
        if (storageTabButton != null) storageTabButton.onClick.AddListener(ShowStorageTab);
        if (closeButton != null) closeButton.onClick.AddListener(CloseStore);
        if (purchaseButton != null) purchaseButton.onClick.AddListener(OnPurchaseClicked);
        if (clearCartButton != null) clearCartButton.onClick.AddListener(ClearCart);

        if (buyLightAmmoButton != null) buyLightAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Light, 18, 60));
        if (buyMediumAmmoButton != null) buyMediumAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Medium, 50, 150));
        if (buyShotgunAmmoButton != null) buyShotgunAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Shotgun, 12, 80));

        if (TeamStorageManager.Instance != null)
        {
            TeamStorageManager.Instance.onStorageChanged.AddListener(OnStorageUpdated);
        }
    }

    private void OnDestroy()
    {
        if (TeamStorageManager.Instance != null)
        {
            TeamStorageManager.Instance.onStorageChanged.RemoveListener(OnStorageUpdated);
        }
    }

    private void Update()
    {
        if (isOpen)
        {
            if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame))
            {
                CloseStore();
            }
            return;
        }

        // В Лобби можно быстро открыть оружейную/склад по нажатию [B]
        if (GameManager.Instance != null && GameManager.Instance.isInLobby)
        {
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused)
                {
                    OpenStore();
                }
            }
        }
    }

    private InputReader EffectiveInputReader => inputReader != null ? inputReader : (PlayerInventory.Instance != null ? PlayerInventory.Instance.inputReader : null);

    public void OpenStore()
    {
        isOpen = true;
        if (mainStoreWindow != null) mainStoreWindow.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (EffectiveInputReader != null)
        {
            EffectiveInputReader.DisableAllActions();
        }

        ShowStoreTab();
        RefreshUI();
    }

    public void CloseStore()
    {
        isOpen = false;
        if (mainStoreWindow != null) mainStoreWindow.SetActive(false);

        if (EffectiveInputReader != null)
        {
            EffectiveInputReader.EnableAllActions();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ShowStoreTab()
    {
        if (storeTabPanel != null) storeTabPanel.SetActive(true);
        if (storageTabPanel != null) storageTabPanel.SetActive(false);
        RefreshStoreTab();
        RefreshLoadoutUI();
    }

    public void ShowStorageTab()
    {
        if (storeTabPanel != null) storeTabPanel.SetActive(false);
        if (storageTabPanel != null) storageTabPanel.SetActive(true);
        RefreshStorageTab();
        RefreshLoadoutUI();
    }

    public void SetCategoryFilter(int categoryIndex)
    {
        activeCategory = (ShopCategory)categoryIndex;
        RefreshStoreTab();
    }

    // Shopping Cart Methods
    public void AddToCart(ItemData item)
    {
        if (item == null || TeamStorageManager.Instance == null) return;

        int currentInCart = shoppingCart.TryGetValue(item, out int count) ? count : 0;
        int currentInStorage = TeamStorageManager.Instance.GetItemCount(item);
        int maxCap = TeamStorageManager.Instance.GetMaxCapacity(item);

        if ((currentInStorage + currentInCart + 1) > maxCap)
        {
            Debug.LogWarning($"[StoreUI] Нельзя добавить в корзину: превышен лимит ({currentInStorage + currentInCart}/{maxCap}) для {item.itemName}");
            return;
        }

        shoppingCart[item] = currentInCart + 1;
        RefreshStoreTab();
    }

    public void RemoveFromCart(ItemData item)
    {
        if (item == null) return;
        if (shoppingCart.TryGetValue(item, out int count))
        {
            if (count > 1) shoppingCart[item] = count - 1;
            else shoppingCart.Remove(item);
        }
        RefreshStoreTab();
    }

    public void ClearCart()
    {
        shoppingCart.Clear();
        RefreshStoreTab();
    }

    private void OnPurchaseClicked()
    {
        if (shoppingCart.Count == 0 || TeamStorageManager.Instance == null) return;

        int total = CalculateTotalCost();
        List<KeyValuePair<ItemData, int>> itemsToBuy = new List<KeyValuePair<ItemData, int>>();
        foreach (var kvp in shoppingCart)
        {
            itemsToBuy.Add(new KeyValuePair<ItemData, int>(kvp.Key, kvp.Value));
        }

        if (TeamStorageManager.Instance.PurchaseCart(itemsToBuy, total))
        {
            shoppingCart.Clear();
            RefreshStoreTab();
            RefreshLoadoutUI();
        }
    }

    public void BuyAmmo(AmmoType type, int amount, int cost)
    {
        if (GameManager.Instance == null || PlayerInventory.Instance == null) return;

        if (GameManager.Instance.sessionMoney < cost)
        {
            Debug.LogWarning($"[StoreUI] Недостаточно средств для покупки патронов! Нужно: ${cost}, есть: ${GameManager.Instance.sessionMoney}");
            return;
        }

        if (PlayerInventory.Instance.GetAmmo(type) >= PlayerInventory.Instance.GetMaxAmmo(type))
        {
            Debug.LogWarning("[StoreUI] Боезапас данного типа уже полон!");
            return;
        }

        GameManager.Instance.sessionMoney -= cost;
        PlayerInventory.Instance.AddAmmo(type, amount);

        if (GameManager.Instance.onMoneyChanged != null)
        {
            GameManager.Instance.onMoneyChanged.Invoke();
        }

        RefreshUI();
    }

    private int CalculateTotalCost()
    {
        int total = 0;
        if (TeamStorageManager.Instance == null || TeamStorageManager.Instance.catalog == null) return 0;

        foreach (var kvp in shoppingCart)
        {
            StoreCatalogEntry entry = TeamStorageManager.Instance.catalog.GetEntry(kvp.Key);
            if (entry != null)
            {
                total += entry.price * kvp.Value;
            }
        }
        return total;
    }

    public void RefreshUI()
    {
        RefreshStoreTab();
        RefreshStorageTab();
        RefreshLoadoutUI();
    }

    private void RefreshStoreTab()
    {
        if (!isOpen) return;

        // Заполняем каталог товаров
        if (catalogContainer != null && itemCardPrefab != null && TeamStorageManager.Instance != null && TeamStorageManager.Instance.catalog != null)
        {
            foreach (Transform child in catalogContainer) Destroy(child.gameObject);

            List<StoreCatalogEntry> entries = TeamStorageManager.Instance.catalog.GetCategoryItems(activeCategory);
            foreach (var entry in entries)
            {
                if (entry == null || entry.itemData == null) continue;
                GameObject cardObj = Instantiate(itemCardPrefab, catalogContainer);
                StoreItemCardUI cardScript = cardObj.GetComponent<StoreItemCardUI>();
                if (cardScript != null) cardScript.Setup(entry);
            }
        }

        // Заполняем корзину покупок
        if (cartContainer != null && cartCardPrefab != null && TeamStorageManager.Instance != null && TeamStorageManager.Instance.catalog != null)
        {
            foreach (Transform child in cartContainer) Destroy(child.gameObject);

            foreach (var kvp in shoppingCart)
            {
                StoreCatalogEntry entry = TeamStorageManager.Instance.catalog.GetEntry(kvp.Key);
                int price = entry != null ? entry.price : 0;

                GameObject cartObj = Instantiate(cartCardPrefab, cartContainer);
                StoreCartCardUI cartScript = cartObj.GetComponent<StoreCartCardUI>();
                if (cartScript != null) cartScript.Setup(kvp.Key, kvp.Value, price);
            }
        }

        int totalCost = CalculateTotalCost();
        int playerMoney = GameManager.Instance != null ? GameManager.Instance.sessionMoney : 0;
        int remaining = playerMoney - totalCost;

        if (totalCostText != null) totalCostText.text = $"К оплате: ${totalCost}";
        if (availableMoneyText != null) availableMoneyText.text = $"Бюджет банды: ${playerMoney}";
        if (remainingMoneyText != null)
        {
            remainingMoneyText.text = $"Остаток: ${remaining}";
            remainingMoneyText.color = remaining >= 0 ? Color.white : new Color(1f, 0.4f, 0.4f, 1f);
        }

        if (purchaseButton != null)
        {
            purchaseButton.interactable = shoppingCart.Count > 0 && remaining >= 0;
        }
    }

    private void RefreshStorageTab()
    {
        if (!isOpen || TeamStorageManager.Instance == null) return;

        var owned = TeamStorageManager.Instance.GetAllOwnedItems();

        if (storageContainer != null && storageCardPrefab != null)
        {
            foreach (Transform child in storageContainer) Destroy(child.gameObject);

            foreach (var kvp in owned)
            {
                int maxCap = TeamStorageManager.Instance.GetMaxCapacity(kvp.Key);
                GameObject cardObj = Instantiate(storageCardPrefab, storageContainer);
                StorageItemCardUI cardScript = cardObj.GetComponent<StorageItemCardUI>();
                if (cardScript != null) cardScript.Setup(kvp.Key, kvp.Value, maxCap);
            }
        }

        if (storageStatusText != null)
        {
            if (owned.Count == 0)
            {
                storageStatusText.text = "Склад пуст. Купите снаряжение во вкладке 'Магазин'.";
            }
            else
            {
                string summary = "<b>СКЛАД СНАРЯЖЕНИЯ БАНДЫ:</b>\n";
                foreach (var kvp in owned)
                {
                    int maxCap = TeamStorageManager.Instance.GetMaxCapacity(kvp.Key);
                    summary += $"• {kvp.Key.itemName}: {kvp.Value}/{maxCap}\n";
                }
                storageStatusText.text = summary;
            }
        }
    }

    public void RefreshLoadoutUI()
    {
        if (PlayerInventory.Instance == null) return;

        var inv = PlayerInventory.Instance;

        // Слот 1 (Инструмент)
        if (loadoutSlot1Text != null)
        {
            string s1 = (inv.slots != null && inv.slots.Length > 0 && inv.slots[0] != null && inv.slots[0].itemData != null)
                ? inv.slots[0].itemData.itemName : "[Пусто]";
            loadoutSlot1Text.text = $"[1] Инструмент: <b>{s1}</b>";
        }

        // Слот 2 (Оружие)
        if (loadoutSlot2Text != null)
        {
            string s2 = (inv.slots != null && inv.slots.Length > 1 && inv.slots[1] != null && inv.slots[1].itemData != null)
                ? inv.slots[1].itemData.itemName : "[Пусто]";
            loadoutSlot2Text.text = $"[2] Оружие: <b>{s2}</b>";
        }

        // Слот 3 (Расходник / Сумка)
        if (loadoutSlot3Text != null)
        {
            string s3 = (inv.slots != null && inv.slots.Length > 2 && inv.slots[2] != null && inv.slots[2].itemData != null)
                ? inv.slots[2].itemData.itemName : "[Пусто]";
            loadoutSlot3Text.text = $"[3] Спецсредство: <b>{s3}</b>";
        }

        // Патроны
        if (loadoutAmmoText != null)
        {
            loadoutAmmoText.text = $"Боезапас: Лёгкие {inv.lightAmmo}/{inv.maxLightAmmo} | Средние {inv.mediumAmmo}/{inv.maxMediumAmmo} | Дробь {inv.shotgunAmmo}/{inv.maxShotgunAmmo}";
        }

        // Вес
        if (loadoutWeightText != null && GameManager.Instance != null)
        {
            int curWeight = GameManager.Instance.currentWeight;
            int maxWeight = GameManager.Instance.maxWeight;
            int freeWeight = Mathf.Max(0, maxWeight - curWeight);
            loadoutWeightText.text = $"Вес: <b>{curWeight} / {maxWeight} кг</b> (Свободно под золото: <b>{freeWeight} кг</b>)";
        }
    }

    private void OnStorageUpdated()
    {
        RefreshUI();
    }
}
