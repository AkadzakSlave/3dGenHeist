using UnityEngine;

public class MineItem : EquipableItem
{
    [Header("References")]
    public Transform playerCamera;
    public GameObject handMesh;

    [Header("Prefabs")]
    public GameObject placedMinePrefab;
    public GameObject previewMinePrefab;

    [Header("Settings")]
    public float placeDistance = 3.5f;
    public LayerMask groundLayer = ~0;

    private GameObject previewInstance;
    private bool isEquipped = false;
    private Vector3 placePosition;
    private Quaternion placeRotation;
    private bool isValidPlacement = false;

    private InputReader inputReader => PlayerInventory.Instance != null ? PlayerInventory.Instance.inputReader : null;

    private void Start()
    {
        if (handMesh != null) handMesh.SetActive(false);
    }

    private void Update()
    {
        if (!isEquipped) return;

        HandlePreviewProjection();
    }

    private void HandlePreviewProjection()
    {
        if (inputReader == null) return;

        isValidPlacement = false;
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, placeDistance, groundLayer))
            {
                // Verify slope is walk-able floor (normal pointing mostly up)
                if (Vector3.Angle(hit.normal, Vector3.up) < 45f)
                {
                    placePosition = hit.point;
                    placeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    isValidPlacement = true;
                }
            }
        }

        if (isValidPlacement)
        {
            if (previewInstance == null && previewMinePrefab != null)
            {
                previewInstance = Instantiate(previewMinePrefab);
                foreach (var col in previewInstance.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
            }

            if (previewInstance != null)
            {
                previewInstance.SetActive(true);
                previewInstance.transform.position = placePosition;
                previewInstance.transform.rotation = placeRotation;
            }
        }
        else
        {
            if (previewInstance != null && previewInstance.activeSelf)
            {
                previewInstance.SetActive(false);
            }
        }
    }

    public override void Equip()
    {
        isEquipped = true;
        if (handMesh != null) handMesh.SetActive(true);
    }

    public override void Unequip()
    {
        isEquipped = false;
        if (handMesh != null) handMesh.SetActive(false);

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        isValidPlacement = false;
    }

    public override void PrimaryAction()
    {
        if (isEquipped && isValidPlacement && placedMinePrefab != null)
        {
            Instantiate(placedMinePrefab, placePosition, placeRotation);

            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            isValidPlacement = false;

            ConsumeFromInventory();
        }
    }

    private void ConsumeFromInventory()
    {
        if (PlayerInventory.Instance == null) return;

        int activeIndex = PlayerInventory.Instance.activeSlotIndex;
        if (activeIndex >= 0 && activeIndex < PlayerInventory.Instance.slots.Length)
        {
            if (PlayerInventory.Instance.slots[activeIndex] == this)
            {
                PlayerInventory.Instance.slots[activeIndex] = null;
            }
        }

        Unequip();

        PlayerInventory.Instance.onInventoryChanged?.Invoke();
        var heistUI = GameManager.Instance != null ? GameManager.Instance.heistUI : null;
        if (heistUI != null)
        {
            heistUI.SetInteractionText("");
        }
    }
}
