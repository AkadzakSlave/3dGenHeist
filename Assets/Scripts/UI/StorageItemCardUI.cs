using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class StorageItemCardUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Elements")]
    public Image itemIcon;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemCapacityText;
    public Image capacityFillBar;
    public Button equipButton;
    public TextMeshProUGUI equipButtonText;
    public Button returnButton; // Опциональная кнопка возврата на склад

    private ItemData currentItem;
    private int currentMainCount;
    private int currentHeistCount;
    private int currentMaxCapacity;
    private bool isTruckStorageMode = false;

    private void Awake()
    {
        // Отключаем старый зеленый прямоугольник прогресса, чтобы он не закрывал текст
        if (capacityFillBar != null) capacityFillBar.gameObject.SetActive(false);
        Transform c1 = transform.Find("capacityImg");
        if (c1 != null) c1.gameObject.SetActive(false);
        Transform c2 = transform.Find("capacityImg (1)");
        if (c2 != null) c2.gameObject.SetActive(false);

        if (equipButton == null)
        {
            equipButton = GetComponentInChildren<Button>();
        }

        if (equipButton == null)
        {
            GameObject btnObj = new GameObject("EquipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-8f, 8f);
            btnRt.sizeDelta = new Vector2(95f, 32f);

            Image img = btnObj.GetComponent<Image>();
            img.color = new Color(0.22f, 0.65f, 0.32f, 1f);

            equipButton = btnObj.GetComponent<Button>();

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(btnObj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.sizeDelta = Vector2.zero;
            lblRt.anchoredPosition = Vector2.zero;

            equipButtonText = lblObj.GetComponent<TextMeshProUGUI>();
            equipButtonText.text = "ВЗЯТЬ";
            equipButtonText.fontSize = 11;
            equipButtonText.fontStyle = FontStyles.Bold;
            equipButtonText.color = Color.white;
            equipButtonText.alignment = TextAlignmentOptions.Center;
            equipButtonText.raycastTarget = false;
            if (itemNameText != null && itemNameText.font != null)
            {
                equipButtonText.font = itemNameText.font;
            }
        }

        if (equipButton != null && equipButtonText == null)
        {
            equipButtonText = equipButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(OnMainButtonClicked);
        }

        // Создаем кнопку возврата [-], если её нет
        EnsureReturnButton();
    }

    private void EnsureReturnButton()
    {
        if (returnButton != null) return;

        Transform existing = transform.Find("ReturnButton");
        if (existing != null)
        {
            returnButton = existing.GetComponent<Button>();
        }
        else if (equipButton != null)
        {
            GameObject retObj = new GameObject("ReturnButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            retObj.transform.SetParent(transform, false);
            RectTransform rRt = retObj.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(1f, 0f);
            rRt.anchorMax = new Vector2(1f, 0f);
            rRt.pivot = new Vector2(1f, 0f);
            rRt.anchoredPosition = new Vector2(-107f, 8f);
            rRt.sizeDelta = new Vector2(28f, 32f);

            Image img = retObj.GetComponent<Image>();
            img.color = new Color(0.75f, 0.35f, 0.2f, 1f);

            returnButton = retObj.GetComponent<Button>();

            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(retObj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.sizeDelta = Vector2.zero;
            lblRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI lbl = lblObj.GetComponent<TextMeshProUGUI>();
            lbl.text = "-";
            lbl.fontSize = 18;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.raycastTarget = false;
            if (itemNameText != null && itemNameText.font != null) lbl.font = itemNameText.font;
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveAllListeners();
            returnButton.onClick.AddListener(OnReturnButtonClicked);
        }
    }

    public void Setup(ItemData item, int mainStorageCount, int maxCapacity)
    {
        int heistCount = HeistLoadoutStorage.Instance != null ? HeistLoadoutStorage.Instance.GetItemCount(item) : 0;
        Setup(item, mainStorageCount, heistCount, maxCapacity, isTruckMode: false);
    }

    public void Setup(ItemData item, int mainStorageCount, int heistCount, int maxCapacity, bool isTruckMode)
    {
        currentItem = item;
        currentMainCount = mainStorageCount;
        currentHeistCount = heistCount;
        currentMaxCapacity = maxCapacity;
        isTruckStorageMode = isTruckMode;

        if (item == null) return;

        // Отключаем зеленый прямоугольник
        if (capacityFillBar != null) capacityFillBar.gameObject.SetActive(false);

        if (itemIcon != null) itemIcon.sprite = item.icon;

        if (itemNameText != null)
        {
            itemNameText.text = item.itemName;
            itemNameText.color = new Color(0.12f, 0.08f, 0.05f, 1f);
        }

        // Проверяем, надет ли этот предмет прямо сейчас в руках
        bool isEquippedInHands = false;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
        {
            int slotIdx = (int)item.itemType;
            if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
            {
                var curSlot = PlayerInventory.Instance.slots[slotIdx];
                if (curSlot != null && curSlot.itemData == item)
                {
                    isEquippedInHands = true;
                }
            }
        }

        Image btnImg = equipButton != null ? equipButton.GetComponent<Image>() : null;

        if (isTruckStorageMode)
        {
            // ============================================
            // РЕЖИМ 2: ХРАНИЛИЩЕ ВЫЛАЗКИ (У МАШИНЫ)
            // ============================================
            if (returnButton != null) returnButton.gameObject.SetActive(false);

            if (itemCapacityText != null)
            {
                if (isEquippedInHands)
                {
                    itemCapacityText.text = $"<color=#007700><b>В руках</b></color>\nВ машине: <b>{heistCount}</b>";
                }
                else
                {
                    itemCapacityText.text = $"В машине: <b>{heistCount}</b> шт.";
                }
                itemCapacityText.color = new Color(0.18f, 0.14f, 0.10f, 1f);
            }

            if (equipButton != null)
            {
                if (isEquippedInHands)
                {
                    equipButton.interactable = true;
                    if (btnImg != null) btnImg.color = new Color(0.85f, 0.42f, 0.2f, 1f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "СНЯТЬ";
                        equipButtonText.color = Color.white;
                    }
                }
                else if (heistCount > 0)
                {
                    equipButton.interactable = true;
                    if (btnImg != null) btnImg.color = new Color(0.22f, 0.65f, 0.32f, 1f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "ВЗЯТЬ";
                        equipButtonText.color = Color.white;
                    }
                }
                else
                {
                    equipButton.interactable = false;
                    if (btnImg != null) btnImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "ПУСТО";
                        equipButtonText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    }
                }
            }
        }
        else
        {
            // ============================================
            // РЕЖИМ 1: ОСНОВНОЙ СКЛАД БАНДЫ (УБЕЖИЩЕ)
            // ============================================
            if (itemCapacityText != null)
            {
                itemCapacityText.text = $"Склад: <b>{mainStorageCount}/{maxCapacity}</b>\nВ машине: <b>{heistCount}</b>";
                itemCapacityText.color = new Color(0.18f, 0.14f, 0.10f, 1f);
            }

            // Кнопка возврата [-] активна, если в машине есть хотя бы 1 штука
            if (returnButton != null)
            {
                returnButton.gameObject.SetActive(heistCount > 0);
            }

            if (equipButton != null)
            {
                if (mainStorageCount > 0)
                {
                    equipButton.interactable = true;
                    if (btnImg != null) btnImg.color = new Color(0.22f, 0.65f, 0.32f, 1f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "НА ВЫЛАЗКУ";
                        equipButtonText.color = Color.white;
                    }
                }
                else if (heistCount > 0)
                {
                    // Если на складе 0, но в машине есть — главная кнопка позволяет вернуть
                    equipButton.interactable = true;
                    if (btnImg != null) btnImg.color = new Color(0.85f, 0.42f, 0.2f, 1f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "ВЕРНУТЬ";
                        equipButtonText.color = Color.white;
                    }
                }
                else
                {
                    equipButton.interactable = false;
                    if (btnImg != null) btnImg.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    if (equipButtonText != null)
                    {
                        equipButtonText.text = "ПУСТО";
                        equipButtonText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
                    }
                }
            }
        }
    }

    private void OnMainButtonClicked()
    {
        if (currentItem == null) return;

        if (isTruckStorageMode)
        {
            // В машине: взять в руки или положить обратно в машину
            HandleTruckEquipToggle();
        }
        else
        {
            // В основном хранилище: "Взять на вылазку" (загрузить в машину)
            if (currentMainCount > 0)
            {
                HeistLoadoutStorage.EnsureInstance().LoadFromMainStorage(currentItem, 1);
            }
            else if (currentHeistCount > 0)
            {
                HeistLoadoutStorage.EnsureInstance().UnloadToMainStorage(currentItem, 1);
            }

            if (StoreUI.Instance != null) StoreUI.Instance.RefreshUI();
        }
    }

    private void OnReturnButtonClicked()
    {
        if (currentItem == null) return;

        // Возврат 1 штуки из машины обратно в основной склад
        if (currentHeistCount > 0)
        {
            HeistLoadoutStorage.EnsureInstance().UnloadToMainStorage(currentItem, 1);
            if (StoreUI.Instance != null) StoreUI.Instance.RefreshUI();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // ПКМ по карточке возвращает предмет из машины на склад
        if (eventData.button == PointerEventData.InputButton.Right && !isTruckStorageMode)
        {
            OnReturnButtonClicked();
        }
    }

    private void HandleTruckEquipToggle()
    {
        if (PlayerInventory.Instance == null || HeistLoadoutStorage.Instance == null) return;

        int slotIdx = (int)currentItem.itemType;
        bool isCurrentlyEquipped = false;
        if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
        {
            var cur = PlayerInventory.Instance.slots[slotIdx];
            if (cur != null && cur.itemData == currentItem)
            {
                isCurrentlyEquipped = true;
            }
        }

        if (isCurrentlyEquipped)
        {
            // Снимаем из рук и возвращаем в машину
            var oldEquipped = PlayerInventory.Instance.slots[slotIdx];
            if (oldEquipped != null)
            {
                oldEquipped.Unequip();
                PlayerInventory.Instance.slots[slotIdx] = null;
                HeistLoadoutStorage.Instance.AddItem(currentItem, 1);
                PlayerInventory.Instance.onInventoryChanged?.Invoke();
                PlayerInventory.Instance.UpdateWeaponText();
            }
        }
        else
        {
            // Берем из машины в руки
            int onHeist = HeistLoadoutStorage.Instance.GetItemCount(currentItem);
            if (onHeist <= 0) return;

            // Если в слоте уже лежит другой предмет — возвращаем его в машину
            EquipableItem replacedItem = (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length) ? PlayerInventory.Instance.slots[slotIdx] : null;
            ItemData replacedData = replacedItem != null ? replacedItem.itemData : null;

            if (replacedItem != null)
            {
                replacedItem.Unequip();
                PlayerInventory.Instance.slots[slotIdx] = null;
            }

            if (PlayerInventory.Instance.PickupItem(currentItem))
            {
                HeistLoadoutStorage.Instance.RemoveItem(currentItem, 1);
                if (replacedData != null && replacedData != currentItem)
                {
                    HeistLoadoutStorage.Instance.AddItem(replacedData, 1);
                }
            }
            else if (replacedItem != null)
            {
                PlayerInventory.Instance.slots[slotIdx] = replacedItem;
                replacedItem.Equip();
            }
        }

        if (StoreUI.Instance != null)
        {
            StoreUI.Instance.RefreshUI();
        }
    }
}
