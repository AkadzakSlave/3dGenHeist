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
    public GameObject storeHeaderBanner;

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
    private float lastClosedTime = -1f;
    public bool IsStoreOpen => isOpen || (mainStoreWindow != null && mainStoreWindow.activeSelf) || (Time.unscaledTime - lastClosedTime < 0.35f);

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
        if (storeHeaderBanner == null && mainStoreWindow != null)
        {
            Transform banner = mainStoreWindow.transform.Find("StoreHeader_Banner");
            if (banner != null) storeHeaderBanner = banner.gameObject;
        }

        if (mainStoreWindow != null) mainStoreWindow.SetActive(false);

        if (storeTabButton != null) storeTabButton.onClick.AddListener(ShowStoreTab);
        if (storageTabButton != null) storageTabButton.onClick.AddListener(ShowStorageTab);
        if (closeButton != null) closeButton.onClick.AddListener(CloseStore);
        if (purchaseButton != null) purchaseButton.onClick.AddListener(OnPurchaseClicked);
        if (clearCartButton != null) clearCartButton.onClick.AddListener(ClearCart);

        if (buyLightAmmoButton != null) buyLightAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Light, 18, 30));
        if (buyMediumAmmoButton != null) buyMediumAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Medium, 50, 60));
        if (buyShotgunAmmoButton != null) buyShotgunAmmoButton.onClick.AddListener(() => BuyAmmo(AmmoType.Shotgun, 12, 40));

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
        // Чит-бинд для быстрых тестов в лобби/магазине: [F1], [F2], [Numpad +] или [+] добавляет +$1000
        if (Keyboard.current != null && (Keyboard.current.f1Key.wasPressedThisFrame || Keyboard.current.f2Key.wasPressedThisFrame || Keyboard.current.numpadPlusKey.wasPressedThisFrame || Keyboard.current.equalsKey.wasPressedThisFrame))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.sessionMoney += 1000;
                GameManager.Instance.onMoneyChanged?.Invoke();
                if (isOpen) RefreshUI();
                Debug.Log($"<color=green>[Cheat] Добавлено +$1000! Текущий баланс: ${GameManager.Instance.sessionMoney}</color>");
            }
        }

        if (isOpen)
        {
            if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame))
            {
                CloseStore();
            }
            return;
        }

        // В Лобби можно быстро открыть оружейную/склад по нажатию [B]
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            bool inLobby = GameManager.Instance == null || GameManager.Instance.isInLobby;
            if (inLobby && (PauseMenuUI.Instance == null || !PauseMenuUI.Instance.IsPaused))
            {
                OpenStore();
            }
        }
    }

    private InputReader EffectiveInputReader => inputReader != null ? inputReader : (PlayerInventory.Instance != null ? PlayerInventory.Instance.inputReader : null);

    public bool isTruckStorageMode { get; private set; } = false;

    public void OpenStore()
    {
        OpenStore(false);
    }

    public void OpenStore(bool truckStorageMode)
    {
        if (Instance == null) Instance = this;
        isTruckStorageMode = truckStorageMode;
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (mainStoreWindow != null) mainStoreWindow.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (isTruckStorageMode)
        {
            if (storeTabButton != null) storeTabButton.gameObject.SetActive(false);
            ShowStorageTab();
        }
        else
        {
            if (storeTabButton != null) storeTabButton.gameObject.SetActive(true);
            ShowStoreTab();
        }
        RefreshUI();
    }

    public void CloseStore()
    {
        isOpen = false;
        lastClosedTime = Time.unscaledTime;
        if (mainStoreWindow != null) mainStoreWindow.SetActive(false);
        if (storeTabButton != null) storeTabButton.gameObject.SetActive(true);
        isTruckStorageMode = false;

        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(EnforceCursorLock());
    }

    private System.Collections.IEnumerator EnforceCursorLock()
    {
        for (int i = 0; i < 5; i++)
        {
            if (isOpen) yield break;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            yield return null;
        }
    }

    public void ShowStoreTab()
    {
        if (storeTabPanel != null) storeTabPanel.SetActive(true);
        if (storageTabPanel != null) storageTabPanel.SetActive(false);
        if (storeHeaderBanner != null) storeHeaderBanner.SetActive(true);
        RefreshStoreTab();
        RefreshLoadoutUI();
    }

    public void ShowStorageTab()
    {
        if (storeTabPanel != null) storeTabPanel.SetActive(false);
        if (storageTabPanel != null) storageTabPanel.SetActive(true);
        if (storeHeaderBanner != null) storeHeaderBanner.SetActive(false);
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
        if (item == null) return;

        int currentInCart = shoppingCart.TryGetValue(item, out int count) ? count : 0;
        int currentInStorage = TeamStorageManager.Instance != null ? TeamStorageManager.Instance.GetItemCount(item) : 0;
        int maxCap = TeamStorageManager.Instance != null ? TeamStorageManager.Instance.GetMaxCapacity(item) : 99;

        // Лимит корзины не должен блокировать покупку, если в хранилище свободно
        if ((currentInStorage + currentInCart + 1) > Mathf.Max(maxCap, 6) && currentInCart >= 2)
        {
            Debug.LogWarning($"[StoreUI] Достигнут лимит для {item.itemName} ({currentInCart} уже в корзине)");
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

    /// <summary>
    /// Мгновенная прямая покупка предмета (1 клик по ценнику в стиле старого терминала)
    /// </summary>
    public bool DirectBuyItem(ItemData item)
    {
        if (item == null) return false;

        StoreCatalogEntry entry = (TeamStorageManager.Instance != null && TeamStorageManager.Instance.catalog != null)
            ? TeamStorageManager.Instance.catalog.GetEntry(item) : null;
        int price = entry != null ? entry.price : 50;

        int playerMoney = GameManager.Instance != null ? GameManager.Instance.sessionMoney : 0;
        if (playerMoney < price)
        {
            Debug.LogWarning($"[StoreUI] Недостаточно денег для покупки {item.itemName}! Нужно ${price}, доступно ${playerMoney}");
            return false;
        }

        // Списываем деньги
        if (GameManager.Instance != null)
        {
            GameManager.Instance.sessionMoney -= price;
            if (GameManager.Instance.onMoneyChanged != null)
            {
                GameManager.Instance.onMoneyChanged.Invoke();
            }
        }

        // Если соответствующий слот в руках/кобуре игрока пуст — экипируем сразу в руки!
        bool autoEquipped = false;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
        {
            int slotIdx = (int)item.itemType;
            if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
            {
                if (PlayerInventory.Instance.slots[slotIdx] == null)
                {
                    autoEquipped = PlayerInventory.Instance.PickupItem(item);
                }
            }
        }

        // Иначе отправляем в командное хранилище (Склад банды)
        if (!autoEquipped && TeamStorageManager.Instance != null)
        {
            TeamStorageManager.Instance.AddItem(item, 1);
        }

        RefreshUI();
        Debug.Log($"<color=green>[StoreUI] Успешно куплен {item.itemName} за ${price}! {(autoEquipped ? "Экипирован в руки!" : "Отправлен на склад банды.")}</color>");
        return true;
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

        if (totalCostText != null) totalCostText.text = "";
        if (availableMoneyText != null) availableMoneyText.text = $"<color=#dfd6c6>Balance:</color> <color=#ffd700>${playerMoney}</color>";
        if (remainingMoneyText != null)
        {
            remainingMoneyText.text = $"<color=#dfd6c6>Remaining:</color> <color={(remaining >= 0 ? "#ffffff" : "#ff5555")}>${remaining}</color>";
        }

        if (purchaseButton != null)
        {
            purchaseButton.interactable = shoppingCart.Count > 0 && remaining >= 0;
        }
    }

    private void RefreshStorageTab()
    {
        if (!isOpen) return;

        HeistLoadoutStorage heistStorage = HeistLoadoutStorage.EnsureInstance();

        if (storageContainer != null && storageCardPrefab != null)
        {
            foreach (Transform child in storageContainer) Destroy(child.gameObject);

            if (isTruckStorageMode)
            {
                // ============================================
                // РЕЖИМ 2: ХРАНИЛИЩЕ ВЫЛАЗКИ (В МАШИНЕ)
                // ============================================
                Dictionary<ItemData, int> truckItems = new Dictionary<ItemData, int>();

                var onHeist = heistStorage.GetAllItems();
                foreach (var kvp in onHeist)
                {
                    if (kvp.Key != null && kvp.Value > 0) truckItems[kvp.Key] = kvp.Value;
                }

                // Плюс предметы в руках игрока
                if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
                {
                    foreach (var slot in PlayerInventory.Instance.slots)
                    {
                        if (slot != null && slot.itemData != null)
                        {
                            if (!truckItems.ContainsKey(slot.itemData))
                            {
                                truckItems[slot.itemData] = 0;
                            }
                        }
                    }
                }

                foreach (var kvp in truckItems)
                {
                    int maxCap = TeamStorageManager.Instance != null ? TeamStorageManager.Instance.GetMaxCapacity(kvp.Key) : 10;
                    GameObject cardObj = Instantiate(storageCardPrefab, storageContainer);
                    StorageItemCardUI cardScript = cardObj.GetComponent<StorageItemCardUI>();
                    if (cardScript != null)
                    {
                        cardScript.Setup(kvp.Key, 0, kvp.Value, maxCap, isTruckMode: true);
                    }
                }

                if (storageStatusText != null)
                {
                    if (truckItems.Count == 0)
                    {
                        storageStatusText.gameObject.SetActive(true);
                        storageStatusText.text = "В машине нет снаряжения! Загрузите предметы во вкладке 'Склад' перед вылазкой.";
                    }
                    else
                    {
                        storageStatusText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // ============================================
                // РЕЖИМ 1: ОСНОВНОЙ СКЛАД БАНДЫ (УБЕЖИЩЕ)
                // ============================================
                if (TeamStorageManager.Instance == null) return;

                Dictionary<ItemData, int> mainItems = new Dictionary<ItemData, int>();
                var owned = TeamStorageManager.Instance.GetAllOwnedItems();
                foreach (var kvp in owned)
                {
                    if (kvp.Key != null) mainItems[kvp.Key] = kvp.Value;
                }

                // Также показываем предметы, загруженные в машину
                var onHeist = heistStorage.GetAllItems();
                foreach (var kvp in onHeist)
                {
                    if (kvp.Key != null && !mainItems.ContainsKey(kvp.Key))
                    {
                        mainItems[kvp.Key] = 0;
                    }
                }

                foreach (var kvp in mainItems)
                {
                    int mainCount = kvp.Value;
                    int heistCount = heistStorage.GetItemCount(kvp.Key);
                    int maxCap = TeamStorageManager.Instance.GetMaxCapacity(kvp.Key);

                    GameObject cardObj = Instantiate(storageCardPrefab, storageContainer);
                    StorageItemCardUI cardScript = cardObj.GetComponent<StorageItemCardUI>();
                    if (cardScript != null)
                    {
                        cardScript.Setup(kvp.Key, mainCount, heistCount, maxCap, isTruckMode: false);
                    }
                }

                if (storageStatusText != null)
                {
                    if (mainItems.Count == 0)
                    {
                        storageStatusText.gameObject.SetActive(true);
                        storageStatusText.text = "Склад пуст. Купите снаряжение во вкладке 'Магазин'.";
                    }
                    else
                    {
                        storageStatusText.gameObject.SetActive(false);
                    }
                }
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
