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

    [Header("Item Card Prefabs")]
    public GameObject itemCardPrefab;
    public GameObject cartCardPrefab;
    public GameObject storageCardPrefab;

    [Header("Input Reader Reference")]
    public InputReader inputReader;

    private Dictionary<ItemData, int> shoppingCart = new Dictionary<ItemData, int>();
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
        if (!isOpen) return;

        // New Input System check for ESC
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseStore();
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
    }

    public void ShowStorageTab()
    {
        if (storeTabPanel != null) storeTabPanel.SetActive(false);
        if (storageTabPanel != null) storageTabPanel.SetActive(true);
        RefreshStorageTab();
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
        }
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
    }

    private void RefreshStoreTab()
    {
        if (!isOpen) return;

        // Re-populate catalog grid
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
        else
        {
            if (TeamStorageManager.Instance == null) Debug.LogWarning("[StoreUI] TeamStorageManager.Instance is null on scene!");
            else if (TeamStorageManager.Instance.catalog == null) Debug.LogWarning("[StoreUI] TeamStorageManager.Instance.catalog is not assigned in Inspector!");
            else if (catalogContainer == null) Debug.LogWarning("[StoreUI] catalogContainer is not assigned in Inspector!");
            else if (itemCardPrefab == null) Debug.LogWarning("[StoreUI] itemCardPrefab is not assigned in Inspector!");
        }

        // Re-populate cart list
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

        if (totalCostText != null) totalCostText.text = $"Total: ${totalCost}";
        if (availableMoneyText != null) availableMoneyText.text = $"Balance: ${playerMoney}";
        if (remainingMoneyText != null)
        {
            remainingMoneyText.text = $"Remaining: ${remaining}";
            remainingMoneyText.color = remaining >= 0 ? Color.white : Color.red;
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
                storageStatusText.text = "Storage is empty.";
            }
            else
            {
                string summary = "<b>TEAM STORAGE CONTENTS:</b>\n";
                foreach (var kvp in owned)
                {
                    int maxCap = TeamStorageManager.Instance.GetMaxCapacity(kvp.Key);
                    summary += $"• {kvp.Key.itemName}: {kvp.Value}/{maxCap}\n";
                }
                storageStatusText.text = summary;
            }
        }
    }

    private void OnStorageUpdated()
    {
        RefreshUI();
    }
}
