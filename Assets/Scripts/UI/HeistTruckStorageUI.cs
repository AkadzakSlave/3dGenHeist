using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Автономный интерфейс "Хранилище вылазки" у машины.
/// Построен на стандартном UI Unity (без зависимостей от внешних картинок).
/// Открывается при нажатии [E] на объекте с HeistTruckStorageTerminal.
/// </summary>
public class HeistTruckStorageUI : MonoBehaviour
{
    public static HeistTruckStorageUI Instance { get; private set; }

    public bool IsOpen { get; private set; } = false;

    private GameObject windowRoot;
    private Transform cardsContainer;
    private TextMeshProUGUI statusEmptyText;
    private TextMeshProUGUI loadoutSummaryText;
    private Button closeButton;

    private readonly List<GameObject> activeCards = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();
    }

    public static HeistTruckStorageUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        HeistTruckStorageUI existing = FindAnyObjectByType<HeistTruckStorageUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        Canvas mainCanvas = null;
        if (StoreUI.Instance != null && StoreUI.Instance.transform.parent != null)
        {
            mainCanvas = StoreUI.Instance.transform.parent.GetComponentInParent<Canvas>();
        }
        if (mainCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    mainCanvas = c;
                    break;
                }
            }
            if (mainCanvas == null && canvases.Length > 0) mainCanvas = canvases[0];
        }

        GameObject host = new GameObject("HeistTruckStorage_Manager");
        if (mainCanvas != null) host.transform.SetParent(mainCanvas.transform, false);

        Instance = host.AddComponent<HeistTruckStorageUI>();
        return Instance;
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
            {
                Close();
            }
        }
    }

    public void ToggleOpen()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (windowRoot == null) BuildUI();

        IsOpen = true;
        windowRoot.SetActive(true);
        windowRoot.transform.SetAsLastSibling();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshUI();
    }

    public void Close()
    {
        IsOpen = false;
        if (windowRoot != null) windowRoot.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RefreshUI()
    {
        if (!IsOpen || windowRoot == null) return;

        // Очищаем старые карточки
        foreach (var card in activeCards)
        {
            if (card != null) Destroy(card);
        }
        activeCards.Clear();

        HeistLoadoutStorage heistStorage = HeistLoadoutStorage.EnsureInstance();

        // Собираем все предметы: то, что в машине + то, что надето прямо сейчас в руках
        Dictionary<ItemData, int> itemsToShow = new Dictionary<ItemData, int>();

        var allTruckItems = heistStorage.GetAllItems();
        foreach (var kvp in allTruckItems)
        {
            if (kvp.Key != null && kvp.Value > 0)
            {
                itemsToShow[kvp.Key] = kvp.Value;
            }
        }

        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null)
        {
            foreach (var slot in PlayerInventory.Instance.slots)
            {
                if (slot != null && slot.itemData != null)
                {
                    if (!itemsToShow.ContainsKey(slot.itemData))
                    {
                        itemsToShow[slot.itemData] = 0; // В машине 0, но предмет надет в руках
                    }
                }
            }
        }

        // Спавним карточки предметов
        foreach (var pair in itemsToShow)
        {
            CreateItemCard(pair.Key, pair.Value);
        }

        // Статус пустого хранилища
        if (statusEmptyText != null)
        {
            bool isEmpty = itemsToShow.Count == 0;
            statusEmptyText.gameObject.SetActive(isEmpty);
            if (isEmpty)
            {
                statusEmptyText.text = "В машине нет снаряжения!\nЗагрузите предметы во вкладке 'Склад' перед вылазкой.";
            }
        }

        // Обновляем статус экипировки в руках
        UpdateLoadoutSummary();
    }

    private void CreateItemCard(ItemData item, int truckCount)
    {
        if (item == null || cardsContainer == null) return;

        // Карточка
        GameObject card = new GameObject("TruckCard_" + item.itemName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(cardsContainer, false);
        activeCards.Add(card);

        RectTransform cRt = card.GetComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(235f, 95f);

        Image cBg = card.GetComponent<Image>();
        cBg.color = new Color(0.18f, 0.18f, 0.22f, 0.95f);

        // Иконка
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(card.transform, false);
        RectTransform iRt = iconObj.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0f, 0.5f);
        iRt.anchorMax = new Vector2(0f, 0.5f);
        iRt.pivot = new Vector2(0.5f, 0.5f);
        iRt.anchoredPosition = new Vector2(34f, 4f);
        iRt.sizeDelta = new Vector2(46f, 46f);

        Image iImg = iconObj.GetComponent<Image>();
        iImg.sprite = item.icon;
        iImg.preserveAspect = true;
        iImg.raycastTarget = false;

        // Название предмета
        GameObject nameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(card.transform, false);
        RectTransform nRt = nameObj.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0f, 1f);
        nRt.anchorMax = new Vector2(1f, 1f);
        nRt.pivot = new Vector2(0f, 1f);
        nRt.anchoredPosition = new Vector2(65f, -10f);
        nRt.sizeDelta = new Vector2(-75f, 22f);

        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.text = item.itemName;
        nameTmp.fontSize = 15;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = Color.white;
        nameTmp.raycastTarget = false;

        // Проверяем, надет ли предмет прямо сейчас
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

        // Текст количества в машине / статуса
        GameObject qObj = new GameObject("Quantity", typeof(RectTransform), typeof(TextMeshProUGUI));
        qObj.transform.SetParent(card.transform, false);
        RectTransform qRt = qObj.GetComponent<RectTransform>();
        qRt.anchorMin = new Vector2(0f, 0f);
        qRt.anchorMax = new Vector2(0f, 0f);
        qRt.pivot = new Vector2(0f, 0f);
        qRt.anchoredPosition = new Vector2(65f, 10f);
        qRt.sizeDelta = new Vector2(120f, 36f);

        TextMeshProUGUI qTmp = qObj.GetComponent<TextMeshProUGUI>();
        if (isEquippedInHands)
        {
            qTmp.text = $"<color=#00ff88><b>В руках: ДА</b></color>\n<size=11>В машине: {truckCount} шт.</size>";
        }
        else
        {
            qTmp.text = $"<size=12>В машине:</size>\n<b>{truckCount}</b> шт.";
        }
        qTmp.fontSize = 13;
        qTmp.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        qTmp.raycastTarget = false;

        // Кнопка действия (ВЗЯТЬ / СНЯТЬ)
        GameObject btnObj = new GameObject("ActionBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(card.transform, false);
        RectTransform bRt = btnObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(1f, 0f);
        bRt.anchorMax = new Vector2(1f, 0f);
        bRt.pivot = new Vector2(1f, 0f);
        bRt.anchoredPosition = new Vector2(-8f, 8f);
        bRt.sizeDelta = new Vector2(85f, 32f);

        Image bImg = btnObj.GetComponent<Image>();
        Button btn = btnObj.GetComponent<Button>();

        GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblObj.transform.SetParent(btnObj.transform, false);
        RectTransform lRt = lblObj.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.sizeDelta = Vector2.zero;
        lRt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI bLbl = lblObj.GetComponent<TextMeshProUGUI>();
        bLbl.fontSize = 11;
        bLbl.fontStyle = FontStyles.Bold;
        bLbl.color = Color.white;
        bLbl.alignment = TextAlignmentOptions.Center;
        bLbl.raycastTarget = false;

        if (isEquippedInHands)
        {
            btn.interactable = true;
            bImg.color = new Color(0.85f, 0.45f, 0.2f, 1f); // Оранжевый
            bLbl.text = "СНЯТЬ";
            btn.onClick.AddListener(() => UnequipToTruck(item));
        }
        else if (truckCount > 0)
        {
            btn.interactable = true;
            bImg.color = new Color(0.22f, 0.65f, 0.32f, 1f); // Зеленый
            bLbl.text = "ВЗЯТЬ";
            btn.onClick.AddListener(() => EquipFromTruck(item));
        }
        else
        {
            btn.interactable = false;
            bImg.color = new Color(0.4f, 0.4f, 0.45f, 0.5f);
            bLbl.text = "ПУСТО";
            bLbl.color = Color.gray;
        }
    }

    private void EquipFromTruck(ItemData item)
    {
        if (item == null || PlayerInventory.Instance == null) return;
        HeistLoadoutStorage heistStorage = HeistLoadoutStorage.EnsureInstance();

        int slotIdx = (int)item.itemType;
        if (slotIdx < 0 || slotIdx >= PlayerInventory.Instance.slots.Length) return;

        // Если в слоте уже лежит предмет — аккуратно возвращаем его в машину
        EquipableItem oldItem = PlayerInventory.Instance.slots[slotIdx];
        ItemData oldData = oldItem != null ? oldItem.itemData : null;

        if (oldItem != null)
        {
            oldItem.Unequip();
            PlayerInventory.Instance.slots[slotIdx] = null;
        }

        if (PlayerInventory.Instance.PickupItem(item))
        {
            heistStorage.RemoveItem(item, 1);
            if (oldData != null && oldData != item)
            {
                heistStorage.AddItem(oldData, 1);
            }
        }
        else if (oldItem != null)
        {
            PlayerInventory.Instance.slots[slotIdx] = oldItem;
            oldItem.Equip();
        }

        RefreshUI();
    }

    private void UnequipToTruck(ItemData item)
    {
        if (item == null || PlayerInventory.Instance == null) return;
        HeistLoadoutStorage heistStorage = HeistLoadoutStorage.EnsureInstance();

        int slotIdx = (int)item.itemType;
        if (slotIdx >= 0 && slotIdx < PlayerInventory.Instance.slots.Length)
        {
            var curSlot = PlayerInventory.Instance.slots[slotIdx];
            if (curSlot != null && curSlot.itemData == item)
            {
                curSlot.Unequip();
                PlayerInventory.Instance.slots[slotIdx] = null;
                heistStorage.AddItem(item, 1);
                PlayerInventory.Instance.onInventoryChanged?.Invoke();
                PlayerInventory.Instance.UpdateWeaponText();
            }
        }

        RefreshUI();
    }

    private void UpdateLoadoutSummary()
    {
        if (loadoutSummaryText == null || PlayerInventory.Instance == null) return;

        var inv = PlayerInventory.Instance;
        string t1 = (inv.slots != null && inv.slots.Length > 0 && inv.slots[0] != null && inv.slots[0].itemData != null) ? inv.slots[0].itemData.itemName : "[Пусто]";
        string t2 = (inv.slots != null && inv.slots.Length > 1 && inv.slots[1] != null && inv.slots[1].itemData != null) ? inv.slots[1].itemData.itemName : "[Пусто]";
        string t3 = (inv.slots != null && inv.slots.Length > 2 && inv.slots[2] != null && inv.slots[2].itemData != null) ? inv.slots[2].itemData.itemName : "[Пусто]";

        loadoutSummaryText.text = $"<b>ТЕКУЩИЕ РУКИ:</b>  [1] Инструмент: <color=#ffd700>{t1}</color>  |  [2] Оружие: <color=#ffd700>{t2}</color>  |  [3] Спецсредство: <color=#ffd700>{t3}</color>";
    }

    private void BuildUI()
    {
        if (windowRoot != null) return;

        // Создаем главное окно
        windowRoot = new GameObject("HeistTruckStorage_Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        windowRoot.transform.SetParent(transform, false);

        RectTransform wRt = windowRoot.GetComponent<RectTransform>();
        wRt.anchorMin = new Vector2(0.5f, 0.5f);
        wRt.anchorMax = new Vector2(0.5f, 0.5f);
        wRt.pivot = new Vector2(0.5f, 0.5f);
        wRt.anchoredPosition = Vector2.zero;
        wRt.sizeDelta = new Vector2(820f, 520f);

        Image wBg = windowRoot.GetComponent<Image>();
        wBg.color = new Color(0.10f, 0.10f, 0.13f, 0.98f); // Стильный тёмный металлик

        // Верхняя шапка (Header)
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        header.transform.SetParent(windowRoot.transform, false);
        RectTransform hRt = header.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = Vector2.zero;
        hRt.sizeDelta = new Vector2(0f, 65f);

        Image hBg = header.GetComponent<Image>();
        hBg.color = new Color(0.14f, 0.15f, 0.19f, 1f);

        // Заголовок
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(header.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.5f);
        tRt.anchorMax = new Vector2(1f, 0.5f);
        tRt.pivot = new Vector2(0f, 0.5f);
        tRt.anchoredPosition = new Vector2(25f, 8f);
        tRt.sizeDelta = new Vector2(-100f, 30f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "ХРАНИЛИЩЕ ВЫЛАЗКИ (В МАШИНЕ)";
        titleTmp.fontSize = 20;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(1f, 0.85f, 0.35f, 1f); // Золотистый

        // Подзаголовок
        GameObject subObj = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(header.transform, false);
        RectTransform sRt = subObj.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0.5f);
        sRt.anchorMax = new Vector2(1f, 0.5f);
        sRt.pivot = new Vector2(0f, 0.5f);
        sRt.anchoredPosition = new Vector2(25f, -14f);
        sRt.sizeDelta = new Vector2(-100f, 20f);

        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
        subTmp.text = "Снаряжение, подготовленное к вылазке. Возьмите нужное в руки перед входом в банк.";
        subTmp.fontSize = 12;
        subTmp.color = new Color(0.7f, 0.72f, 0.78f, 1f);

        // Кнопка закрытия [X]
        GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeObj.transform.SetParent(header.transform, false);
        RectTransform cRt = closeObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(1f, 0.5f);
        cRt.anchorMax = new Vector2(1f, 0.5f);
        cRt.pivot = new Vector2(1f, 0.5f);
        cRt.anchoredPosition = new Vector2(-15f, 0f);
        cRt.sizeDelta = new Vector2(36f, 36f);

        Image cImg = closeObj.GetComponent<Image>();
        cImg.color = new Color(0.75f, 0.25f, 0.25f, 1f);

        closeButton = closeObj.GetComponent<Button>();
        closeButton.onClick.AddListener(Close);

        GameObject xObj = new GameObject("XText", typeof(RectTransform), typeof(TextMeshProUGUI));
        xObj.transform.SetParent(closeObj.transform, false);
        RectTransform xRt = xObj.GetComponent<RectTransform>();
        xRt.anchorMin = Vector2.zero;
        xRt.anchorMax = Vector2.one;
        xRt.sizeDelta = Vector2.zero;
        xRt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI xTmp = xObj.GetComponent<TextMeshProUGUI>();
        xTmp.text = "✕";
        xTmp.fontSize = 18;
        xTmp.fontStyle = FontStyles.Bold;
        xTmp.color = Color.white;
        xTmp.alignment = TextAlignmentOptions.Center;
        xTmp.raycastTarget = false;

        // Контейнер сетки карточек
        GameObject gridObj = new GameObject("CardsGrid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObj.transform.SetParent(windowRoot.transform, false);
        cardsContainer = gridObj.transform;

        RectTransform gRt = gridObj.GetComponent<RectTransform>();
        gRt.anchorMin = new Vector2(0.5f, 0.5f);
        gRt.anchorMax = new Vector2(0.5f, 0.5f);
        gRt.pivot = new Vector2(0.5f, 0.5f);
        gRt.anchoredPosition = new Vector2(0f, 10f);
        gRt.sizeDelta = new Vector2(780f, 360f);

        GridLayoutGroup grid = gridObj.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(245f, 100f);
        grid.spacing = new Vector2(14f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperLeft;

        // Текст "В машине пусто"
        GameObject emptyObj = new GameObject("EmptyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObj.transform.SetParent(windowRoot.transform, false);
        RectTransform eRt = emptyObj.GetComponent<RectTransform>();
        eRt.anchorMin = new Vector2(0.5f, 0.5f);
        eRt.anchorMax = new Vector2(0.5f, 0.5f);
        eRt.pivot = new Vector2(0.5f, 0.5f);
        eRt.anchoredPosition = new Vector2(0f, 20f);
        eRt.sizeDelta = new Vector2(650f, 70f);

        statusEmptyText = emptyObj.GetComponent<TextMeshProUGUI>();
        statusEmptyText.fontSize = 16;
        statusEmptyText.color = new Color(0.85f, 0.75f, 0.5f, 1f);
        statusEmptyText.alignment = TextAlignmentOptions.Center;
        statusEmptyText.raycastTarget = false;

        // Нижняя полоса текущей экипировки (Footer)
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        footer.transform.SetParent(windowRoot.transform, false);
        RectTransform fRt = footer.GetComponent<RectTransform>();
        fRt.anchorMin = new Vector2(0f, 0f);
        fRt.anchorMax = new Vector2(1f, 0f);
        fRt.pivot = new Vector2(0.5f, 0f);
        fRt.anchoredPosition = Vector2.zero;
        fRt.sizeDelta = new Vector2(0f, 50f);

        Image fBg = footer.GetComponent<Image>();
        fBg.color = new Color(0.08f, 0.08f, 0.10f, 1f);

        GameObject summaryObj = new GameObject("SummaryText", typeof(RectTransform), typeof(TextMeshProUGUI));
        summaryObj.transform.SetParent(footer.transform, false);
        RectTransform smRt = summaryObj.GetComponent<RectTransform>();
        smRt.anchorMin = Vector2.zero;
        smRt.anchorMax = Vector2.one;
        smRt.sizeDelta = new Vector2(-40f, 0f);
        smRt.anchoredPosition = Vector2.zero;

        loadoutSummaryText = summaryObj.GetComponent<TextMeshProUGUI>();
        loadoutSummaryText.fontSize = 13;
        loadoutSummaryText.color = Color.white;
        loadoutSummaryText.alignment = TextAlignmentOptions.MidlineLeft;
        loadoutSummaryText.raycastTarget = false;

        windowRoot.SetActive(false);
    }
}
