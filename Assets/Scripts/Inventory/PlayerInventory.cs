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
    public List<EquipableItem> allPossibleItems;

    [Header("Inventory Status")]
    [Tooltip("Slot 0: Tool. Slot 1: Weapon.")]
    public EquipableItem[] slots = new EquipableItem[2];
    public int activeSlotIndex = 0;
    public string emptyWeaponText = "Нет оружия";

    [Header("Events")]
    public UnityEvent onInventoryChanged;

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
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = null;
        }

        activeSlotIndex = 0;

        foreach (var item in allPossibleItems)
        {
            if (item == null) continue;

            item.gameObject.SetActive(true);
            item.Unequip();
        }

        onInventoryChanged?.Invoke();
        UpdateWeaponText();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchSlot(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchSlot(1);

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f) SwitchSlot(activeSlotIndex - 1);
        else if (scroll < 0f) SwitchSlot(activeSlotIndex + 1);

        if (Mouse.current.leftButton.wasPressedThisFrame && slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].PrimaryAction();
        }

        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            DropCurrentItem();
        }
    }

    public void SwitchSlot(int newIndex)
    {
        if (slots == null || slots.Length == 0) return;

        if (newIndex < 0) newIndex = slots.Length - 1;
        if (newIndex >= slots.Length) newIndex = 0;

        if (activeSlotIndex == newIndex)
        {
            UpdateWeaponText();
            return;
        }

        if (slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Unequip();
        }

        activeSlotIndex = newIndex;

        if (slots[activeSlotIndex] != null)
        {
            EnsureItemActive(slots[activeSlotIndex]);
            slots[activeSlotIndex].Equip();
        }

        onInventoryChanged?.Invoke();
        UpdateWeaponText();
    }

    public bool PickupItem(ItemData data)
    {
        if (data == null) return false;

        int targetIndex = data.itemType == ItemType.Tool ? 0 : 1;
        if (slots == null || targetIndex >= slots.Length)
        {
            Debug.LogError($"[Inventory] Missing slot for {data.itemType}.");
            return false;
        }

        if (slots[targetIndex] != null)
        {
            string occupiedBy = slots[targetIndex].itemData != null ? slots[targetIndex].itemData.itemName : slots[targetIndex].name;
            Debug.Log($"[Inventory] {data.itemType} slot is already occupied by {occupiedBy}.");
            return false;
        }

        EquipableItem itemToEnable = allPossibleItems.Find(item => item != null && item.itemData == data);
        if (itemToEnable == null)
        {
            Debug.LogError($"[Inventory] Item {data.itemName} is not listed in allPossibleItems.");
            return false;
        }

        if (slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Unequip();
        }

        slots[targetIndex] = itemToEnable;
        activeSlotIndex = targetIndex;
        EnsureItemActive(slots[targetIndex]);
        slots[targetIndex].Equip();

        PlayPickupSound(data);

        onInventoryChanged?.Invoke();
        UpdateWeaponText();
        return true;
    }

    public void DropCurrentItem()
    {
        if (slots == null || activeSlotIndex < 0 || activeSlotIndex >= slots.Length) return;

        EquipableItem currentItem = slots[activeSlotIndex];
        if (currentItem == null) return;

        currentItem.Unequip();

        Vector3 dropPosition;
        Transform cam = Camera.main != null ? Camera.main.transform : transform;

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 2.0f))
        {
            dropPosition = hit.point - cam.forward * 0.2f;
        }
        else
        {
            dropPosition = cam.position + cam.forward * 1.5f;
        }

        if (currentItem.itemData != null && currentItem.itemData.dropPrefab != null)
        {
            GameObject droppedObj = Instantiate(currentItem.itemData.dropPrefab, dropPosition, Quaternion.identity);

            if (currentItem is BagTool bag)
            {
                WorldEquipment worldEq = droppedObj.GetComponent<WorldEquipment>();
                if (worldEq != null)
                {
                    worldEq.storedMoney = bag.storedMoney;
                    worldEq.storedWeight = bag.storedWeight;
                }
            }
        }

        string itemName = currentItem.itemData != null ? currentItem.itemData.itemName : currentItem.name;
        slots[activeSlotIndex] = null;

        onInventoryChanged?.Invoke();
        UpdateWeaponText();

        Debug.Log($"[Inventory] {itemName} dropped.");
    }

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

    private void PlayPickupSound(ItemData data)
    {
        if (pickupSound.IsNull) return;

        EventInstance pickup = RuntimeManager.CreateInstance(pickupSound);
        pickup.setParameterByName("MainType", (float)data.fmodMainType);
        RuntimeManager.AttachInstanceToGameObject(pickup, gameObject);
        pickup.start();
        pickup.release();
    }

    private void EnsureItemActive(EquipableItem item)
    {
        if (item != null && !item.gameObject.activeSelf)
        {
            item.gameObject.SetActive(true);
        }
    }

    private void UpdateWeaponText()
    {
        if (GameManager.Instance == null || GameManager.Instance.heistUI == null) return;

        if (slots != null && slots.Length > 1 && slots[1] is WeaponItem weapon)
        {
            weapon.RefreshWeaponUI();
        }
        else
        {
            GameManager.Instance.heistUI.UpdateWeapon(emptyWeaponText);
        }
    }
}
