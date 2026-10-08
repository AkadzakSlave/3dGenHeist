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

    [Header("Input")]
    public InputReader inputReader;

    [Header("Events")]
    public UnityEvent onInventoryChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (slots == null || slots.Length < 3)
            {
                System.Array.Resize(ref slots, 3);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.SwitchSlotEvent += SwitchSlot;
            inputReader.ScrollSlotEvent += OnScrollSlot;
            inputReader.DropEvent += DropCurrentItem;
            inputReader.PrimaryActionEvent += OnPrimaryActionTriggered;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.SwitchSlotEvent -= SwitchSlot;
            inputReader.ScrollSlotEvent -= OnScrollSlot;
            inputReader.DropEvent -= DropCurrentItem;
            inputReader.PrimaryActionEvent -= OnPrimaryActionTriggered;
        }
    }

    private void OnScrollSlot(float scrollValue)
    {
        if (scrollValue > 0.1f) SwitchSlot(activeSlotIndex - 1);
        else if (scrollValue < -0.1f) SwitchSlot(activeSlotIndex + 1);
    }

    private void OnPrimaryActionTriggered()
    {
        if (slots[activeSlotIndex] != null)
        {
            if (slots[activeSlotIndex] is WeaponItem weapon && weapon.isAutomatic)
            {
                // Automatic weapons are polled in Update
                return;
            }
            slots[activeSlotIndex].PrimaryAction();
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
        HandlePrimaryAction();
    }

    private void HandlePrimaryAction()
    {
        if (slots[activeSlotIndex] != null)
        {
            bool isHeld = inputReader != null && inputReader.IsPrimaryActionPressed;

            if (slots[activeSlotIndex] is WeaponItem weapon && weapon.isAutomatic)
            {
                if (isHeld)
                {
                    slots[activeSlotIndex].PrimaryAction();
                }
            }
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

        int targetIndex = (int)data.itemType;
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

        Vector3 customPosOffset = Vector3.zero;
        Vector3 customRotOffset = Vector3.zero;
        if (currentItem.itemData != null)
        {
            customPosOffset = currentItem.itemData.dropPositionOffset;
            customRotOffset = currentItem.itemData.dropRotationOffset;
        }

        // Base drop position: 1.2m forward and 0.4m down (waist height)
        Vector3 spawnOffset = cam.forward * 1.2f - Vector3.up * 0.4f;
        Vector3 cameraRelativeOffset = cam.right * customPosOffset.x + cam.up * customPosOffset.y + cam.forward * customPosOffset.z;
        spawnOffset += cameraRelativeOffset;

        if (Physics.Raycast(cam.position, spawnOffset.normalized, out RaycastHit hit, spawnOffset.magnitude))
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                dropPosition = cam.position + spawnOffset;
            }
            else
            {
                dropPosition = hit.point - spawnOffset.normalized * 0.15f;
            }
        }
        else
        {
            dropPosition = cam.position + spawnOffset;
        }

        Vector3 camForwardFlat = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        Quaternion baseRotation = camForwardFlat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(camForwardFlat) : Quaternion.identity;
        Quaternion dropRotation = baseRotation * Quaternion.Euler(customRotOffset);

        if (currentItem.itemData != null && currentItem.itemData.dropPrefab != null)
        {
            GameObject droppedObj = Instantiate(currentItem.itemData.dropPrefab, dropPosition, dropRotation);

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

    public void ClearInventory()
    {
        if (allPossibleItems != null)
        {
            foreach (var item in allPossibleItems)
            {
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = null;
            }
        }

        activeSlotIndex = 0;
        onInventoryChanged?.Invoke();
        UpdateWeaponText();
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
        if (item != null)
        {
            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
            }

            PlayerInteraction interaction = GetComponentInParent<PlayerInteraction>();
            if (interaction != null)
            {
                interaction.ApplyHoldingMode();
            }
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
