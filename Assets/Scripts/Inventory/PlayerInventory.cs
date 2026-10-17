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
    [Tooltip("Slot 0: Tool. Slot 1: Weapon. Slot 2: Utility.")]
    public EquipableItem[] slots = new EquipableItem[3];
    public int activeSlotIndex = 0;
    public string emptyWeaponText = "Нет оружия";

    [Header("Physical Toss Settings (CS2-Style)")]
    public float dropThrowForce = 4.2f;
    public float dropUpwardForce = 1.0f;
    public float dropTorqueForce = 1.2f;
    public PlayerMovement playerMovement;

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
        if (slots == null || slots.Length == 0) return;
        int currentIndex = activeSlotIndex < 0 ? 0 : activeSlotIndex;
        if (scrollValue > 0.1f) SwitchSlot((currentIndex - 1 + slots.Length) % slots.Length);
        else if (scrollValue < -0.1f) SwitchSlot((currentIndex + 1) % slots.Length);
    }

    private void OnPrimaryActionTriggered()
    {
        if (activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
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
        if (activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
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

        // Повторное нажатие на активный слот — убираем предмет (пустые руки)
        if (activeSlotIndex == newIndex && activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Unequip();
            activeSlotIndex = -1;
            onInventoryChanged?.Invoke();
            UpdateWeaponText();
            return;
        }

        if (newIndex < 0) newIndex = slots.Length - 1;
        if (newIndex >= slots.Length) newIndex = 0;

        if (activeSlotIndex == newIndex)
        {
            UpdateWeaponText();
            return;
        }

        if (activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
        {
            slots[activeSlotIndex].Unequip();
        }

        activeSlotIndex = newIndex;

        if (activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
        {
            EnsureItemActive(slots[activeSlotIndex]);
            slots[activeSlotIndex].Equip();
            PlaySwitchSound(slots[activeSlotIndex]);
        }

        onInventoryChanged?.Invoke();
        UpdateWeaponText();
    }

    private void PlaySwitchSound(EquipableItem item)
    {
        if (item == null || item.itemData == null) return;

        string eventPath = null;
        switch (item.itemData.itemType)
        {
            case ItemType.Weapon:
                eventPath = "event:/Weapons/Weapon_SwitchPickUp";
                break;
            case ItemType.Tool:
                eventPath = "event:/Environment/Items/Main/Main_SwitchPickUp";
                break;
            case ItemType.Utility:
                eventPath = "event:/Environment/Items/Secondary/Secondary_SwitchPickUp";
                break;
        }

        if (!string.IsNullOrEmpty(eventPath))
        {
            try
            {
                EventInstance switchInst = RuntimeManager.CreateInstance(eventPath);
                if (item.itemData.itemType == ItemType.Tool)
                {
                    switchInst.setParameterByName("MainType", (float)item.itemData.fmodMainType);
                }
                switchInst.setParameterByName("ActiveItem", 1f);
                RuntimeManager.AttachInstanceToGameObject(switchInst, gameObject);
                switchInst.start();
                switchInst.release();
            }
            catch { }
        }
    }

    public bool PickupItem(ItemData data)
    {
        if (data == null) return false;

        if (slots == null || slots.Length < 3)
        {
            System.Array.Resize(ref slots, 3);
        }

        int targetIndex = (int)data.itemType;
        if (targetIndex >= slots.Length)
        {
            System.Array.Resize(ref slots, targetIndex + 1);
        }

        EquipableItem itemToEnable = allPossibleItems.Find(item => item != null && item.itemData == data);
        if (itemToEnable == null)
        {
            Debug.LogError($"[Inventory] Item {data.itemName} is not listed in allPossibleItems.");
            return false;
        }

        // АВТО-СВАП: если слот для предмета уже занят, выбрасываем старый предмет на пол!
        if (slots[targetIndex] != null)
        {
            string oldName = slots[targetIndex].itemData != null ? slots[targetIndex].itemData.itemName : slots[targetIndex].name;
            Debug.Log($"[Inventory] Слот {data.itemType} занят ({oldName}). Выполняем авто-свап!");
            DropItemAtSlot(targetIndex);
        }

        // Убираем предмет, который сейчас был в руках (если это не целевой слот)
        if (activeSlotIndex >= 0 && activeSlotIndex < slots.Length && slots[activeSlotIndex] != null)
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
        DropItemAtSlot(activeSlotIndex);
        onInventoryChanged?.Invoke();
        UpdateWeaponText();
    }

    public void DropItemAtSlot(int slotIndex)
    {
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return;

        EquipableItem itemToDrop = slots[slotIndex];
        if (itemToDrop == null) return;

        itemToDrop.Unequip();

        Transform cam = Camera.main != null ? Camera.main.transform : transform;
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        Vector3 customPosOffset = Vector3.zero;
        Vector3 customRotOffset = Vector3.zero;
        if (itemToDrop.itemData != null)
        {
            customPosOffset = itemToDrop.itemData.dropPositionOffset;
            customRotOffset = itemToDrop.itemData.dropRotationOffset;
        }

        // Natural hand-level drop origin: lower-right from camera, where character holds weapons
        Vector3 handOffset = cam.right * 0.14f - cam.up * 0.22f;
        float targetDistance = 0.45f;
        Vector3 originPos = cam.position + handOffset;
        Vector3 forwardDir = cam.forward;

        // Wall safety check: don't spawn inside or through walls if player is right against a wall
        Vector3 targetDropPos;
        LayerMask wallCheckMask = ~0;
        if (Physics.SphereCast(originPos, 0.12f, forwardDir, out RaycastHit wallHit, targetDistance, wallCheckMask, QueryTriggerInteraction.Ignore))
        {
            if (!wallHit.collider.transform.IsChildOf(transform))
            {
                targetDropPos = originPos + forwardDir * Mathf.Max(0.15f, wallHit.distance - 0.08f);
            }
            else
            {
                targetDropPos = originPos + forwardDir * targetDistance;
            }
        }
        else
        {
            targetDropPos = originPos + forwardDir * targetDistance;
        }

        // Apply optional extra offset if specified
        if (customPosOffset.sqrMagnitude > 0.001f)
        {
            targetDropPos += cam.right * customPosOffset.x + cam.up * customPosOffset.y + cam.forward * customPosOffset.z;
        }

        Vector3 camForwardFlat = new Vector3(cam.forward.x, 0f, cam.forward.z).normalized;
        Quaternion baseRotation = camForwardFlat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(camForwardFlat) : Quaternion.identity;
        Quaternion dropRotation = baseRotation * Quaternion.Euler(customRotOffset);

        if (itemToDrop.itemData != null && itemToDrop.itemData.dropPrefab != null)
        {
            GameObject droppedObj = Instantiate(itemToDrop.itemData.dropPrefab, targetDropPos, dropRotation);

            // Center the physical collider precisely at targetDropPos, regardless of FBX pivot eccentricity
            Collider dropCollider = droppedObj.GetComponentInChildren<Collider>();
            if (dropCollider != null)
            {
                Vector3 centerWorldOffset = dropCollider.bounds.center - droppedObj.transform.position;
                droppedObj.transform.position = targetDropPos - centerWorldOffset;
            }

            // Ignore collisions between dropped item and player body + CharacterController
            Collider[] playerColliders = GetComponentsInChildren<Collider>();
            Collider[] dropColliders = droppedObj.GetComponentsInChildren<Collider>();
            CharacterController cc = GetComponent<CharacterController>();
            if (cc == null && playerMovement != null) cc = playerMovement.GetComponent<CharacterController>();

            if (dropColliders != null)
            {
                foreach (var dCol in dropColliders)
                {
                    if (dCol == null) continue;
                    if (cc != null) Physics.IgnoreCollision(dCol, cc, true);
                    if (playerColliders != null)
                    {
                        foreach (var pCol in playerColliders)
                        {
                            if (pCol == null) continue;
                            Physics.IgnoreCollision(dCol, pCol, true);
                        }
                    }
                }
            }

            // Carry over bag contents
            if (itemToDrop is BagTool bag)
            {
                WorldEquipment worldEq = droppedObj.GetComponent<WorldEquipment>();
                if (worldEq != null)
                {
                    worldEq.storedMoney = bag.storedMoney;
                    worldEq.storedWeight = bag.storedWeight;
                }
            }

            // Setup pickup cooldown so player doesn't instantly re-pickup on the run
            WorldEquipment we = droppedObj.GetComponent<WorldEquipment>();
            if (we != null)
            {
                we.SetPickupCooldown(0.4f);
            }

            // CS2 physics toss: inherit player momentum, clean arc forward, stable rotation
            Rigidbody rb = droppedObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Set center of mass at the physical center of the collider (prevents wild spinning)
                if (dropCollider is BoxCollider bc)
                {
                    rb.centerOfMass = bc.center;
                }
                else if (dropCollider != null)
                {
                    rb.centerOfMass = droppedObj.transform.InverseTransformPoint(dropCollider.bounds.center);
                }

                // Damping for realistic weapon landing
                rb.linearDamping = 0.4f;
                rb.angularDamping = 1.2f;

                // Inherit player velocity
                Vector3 playerVel = playerMovement != null ? playerMovement.Velocity : Vector3.zero;
                if (playerMovement != null && playerMovement.IsGrounded && playerVel.y < 0)
                {
                    playerVel.y = 0f;
                }

                Vector3 throwVel = playerVel + cam.forward * dropThrowForce + Vector3.up * dropUpwardForce;
                rb.linearVelocity = throwVel;

                // Controlled, subtle pitch tilt around camera right (like CS2), no chaotic multi-axis spin
                rb.angularVelocity = cam.right * dropTorqueForce;
            }
        }

        string itemName = itemToDrop.itemData != null ? itemToDrop.itemData.itemName : itemToDrop.name;
        slots[slotIndex] = null;
        Debug.Log($"[Inventory] {itemName} dropped from slot {slotIndex}.");
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
        if (slots == null || activeSlotIndex < 0 || activeSlotIndex >= slots.Length) return null;
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

        EquipableItem activeItem = (slots != null && activeSlotIndex >= 0 && activeSlotIndex < slots.Length) ? slots[activeSlotIndex] : null;

        if (activeItem is WeaponItem weapon)
        {
            weapon.RefreshWeaponUI();
        }
        else if (activeItem is BagTool bag)
        {
            Sprite icon = bag.itemData != null ? bag.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon($"${bag.storedMoney} ({bag.storedWeight}/{bag.maxCapacity} кг)", icon);
        }
        else if (activeItem is HammerTool hammer)
        {
            Sprite icon = hammer.itemData != null ? hammer.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon("[ЛКМ] Проломить стену", icon);
        }
        else if (activeItem is DynamiteItem dynamite)
        {
            Sprite icon = dynamite.itemData != null ? dynamite.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon("[ПКМ] Примерить  [ЛКМ] Закрепить", icon);
        }
        else if (activeItem is MolotovItem molotov)
        {
            Sprite icon = molotov.itemData != null ? molotov.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon("[ПКМ] Траектория  [ЛКМ] Бросить", icon);
        }
        else if (activeItem is MineItem mine)
        {
            Sprite icon = mine.itemData != null ? mine.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon("[ПКМ] Примерить  [ЛКМ] Поставить", icon);
        }
        else if (activeItem is BarricadeItem barricade)
        {
            Sprite icon = barricade.itemData != null ? barricade.itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon("[ПКМ] Примерить  [ЛКМ] Поставить", icon);
        }
        else
        {
            // Руки пусты (кобура)
            GameManager.Instance.heistUI.UpdateWeapon("", null);
        }
    }
}
