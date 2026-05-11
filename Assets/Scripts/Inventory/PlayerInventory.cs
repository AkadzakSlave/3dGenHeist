using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using FMODUnity;
using FMOD.Studio;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Audio (FMOD)")]
    public EventReference pickupSound;

    [Header("Available Items (Attached to Player)")]
    [Tooltip("Все предметы, которые есть у игрока 'в теле' (Молот, Сумка, разные пушки). Они должны быть выключены.")]
    public List<EquipableItem> allPossibleItems;

    [Header("Inventory Status")]
    [Tooltip("Слот 0: Инструмент. Слот 1: Оружие.")]
    public EquipableItem[] slots = new EquipableItem[2];
    public int activeSlotIndex = 0;

    [Header("Events")]
    public UnityEvent onInventoryChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // У игрока на старте ничего
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = null;
        }

        // Вместо выключения объектов, вызываем Unequip, который теперь прячет только визуал
        foreach (var item in allPossibleItems)
        {
            if (item != null)
            {
                item.gameObject.SetActive(true); // Объект должен быть ВСЕГДА активен для Аниматора
                item.Unequip();
            }
        }
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // --- Переключение слотов (1 и 2) ---
        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchSlot(1);

        // --- Переключение на колесико мыши ---
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f) SwitchSlot(activeSlotIndex - 1);
        else if (scroll < 0f) SwitchSlot(activeSlotIndex + 1);

        // --- Основное действие (ЛКМ) ---
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (slots[activeSlotIndex] != null)
            {
                slots[activeSlotIndex].PrimaryAction();
            }
        }

        // --- Выкинуть предмет (G) ---
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropCurrentItem();
        }
    }

    public void SwitchSlot(int newIndex)
    {
        if (slots == null || slots.Length == 0) return;

        // Закольцовываем индексы
        if (newIndex < 0) newIndex = slots.Length - 1;
        if (newIndex >= slots.Length) newIndex = 0;

        if (activeSlotIndex == newIndex) return;

        // Прячем старый
        if (slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Unequip();
        }

        // Берем новый
        activeSlotIndex = newIndex;
        if (slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Equip();
        }

        onInventoryChanged?.Invoke();
    }

    public bool PickupItem(ItemData data)
    {
        if (data == null) return false;

        // Находим нужный слот в зависимости от типа
        int targetIndex = data.itemType == ItemType.Tool ? 0 : 1;

        if (slots == null || targetIndex >= slots.Length)
        {
            Debug.LogError($"[Inventory] Ошибка: Слот №{targetIndex} для типа {data.itemType} отсутствует в массиве slots!");
            return false;
        }

        // Если слот уже занят - выводим конкретное сообщение
        if (slots[targetIndex] != null)
        {
            string typeName = data.itemType.ToString();
            string occupiedBy = slots[targetIndex].itemData.itemName;
            Debug.Log($"[Inventory] Ячейка {typeName} занята предметом {occupiedBy}");
            return false;
        }

        // Ищем физический компонент этого предмета на теле игрока
        EquipableItem itemToEnable = allPossibleItems.Find(item => item.itemData == data);
        
        if (itemToEnable != null)
        {
            slots[targetIndex] = itemToEnable;
            
            // Если подобрали предмет в активный слот, сразу берем в руки
            if (activeSlotIndex == targetIndex)
            {
                slots[targetIndex].Equip();
            }

            if (!pickupSound.IsNull)
            {
                EventInstance pickup = RuntimeManager.CreateInstance(pickupSound);
                // Передаем тип предмета в FMOD
                pickup.setParameterByName("MainType", (float)data.fmodMainType);
                RuntimeManager.AttachInstanceToGameObject(pickup, gameObject);
                pickup.start();
                pickup.release();
            }

            onInventoryChanged?.Invoke();
            return true;
        }
        else
        {
            Debug.LogError($"[Inventory] Предмет {data.itemName} не найден на теле игрока в allPossibleItems!");
            return false;
        }
    }

    public void DropCurrentItem()
    {
        if (slots == null || activeSlotIndex < 0 || activeSlotIndex >= slots.Length) return;

        EquipableItem currentItem = slots[activeSlotIndex];
        if (currentItem == null) return;

        // 1. Сначала убираем из рук (Мгновенно скрываем визуал для чистоты дропа)
        currentItem.Unequip();
        
        // 2. Рассчитываем точку спавна перед лицом
        Vector3 dropPosition;
        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        
        RaycastHit hit;
        // Пускаем луч, чтобы предмет не спавнился ЗА стеной
        if (Physics.Raycast(cam.position, cam.forward, out hit, 2.0f))
        {
            dropPosition = hit.point - cam.forward * 0.2f; // Чуть ближе к игроку от точки удара
        }
        else
        {
            dropPosition = cam.position + cam.forward * 1.5f;
        }

        // 3. Спавним префаб
        if (currentItem.itemData != null && currentItem.itemData.dropPrefab != null)
        {
            GameObject droppedObj = Instantiate(currentItem.itemData.dropPrefab, dropPosition, Quaternion.identity);
            
            // ПЕРЕНОС ДАННЫХ: Если мы выкинули сумку, данные о деньгах должны сохраниться в префабе на полу
            if (currentItem is BagTool bag)
            {
                WorldEquipment worldEq = droppedObj.GetComponent<WorldEquipment>();
                if (worldEq != null)
                {
                    worldEq.storedMoney = bag.storedMoney;
                    worldEq.storedWeight = bag.storedWeight;
                    Debug.Log($"[Persistent] Данные сумки (${bag.storedMoney}) перенесены на пол.");
                }
            }
        }

        // 4. Очищаем слот
        slots[activeSlotIndex] = null;
        onInventoryChanged?.Invoke();

        Debug.Log($"[Inventory] {currentItem.itemData.itemName} выброшен.");
    }

    // Возвращает весь вес, который сейчас несет игрок (Инструмент + Оружие)
    public int GetTotalWeight()
    {
        int weight = 0;
        foreach (var slot in slots)
        {
            if (slot != null) weight += slot.GetTotalWeight();
        }
        return weight;
    }

    public EquipableItem GetActiveItem()
    {
        return slots[activeSlotIndex];
    }

    // DevSwapTool удален по запросу
}
