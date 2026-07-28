using UnityEngine;

public class DynamiteItem : EquipableItem
{
    [Header("References")]
    public Transform playerCamera;
    public GameObject handMesh;

    [Header("Prefabs")]
    [Tooltip("The actual placed dynamite prefab with PlacedDynamite script attached")]
    public GameObject placedDynamitePrefab;
    [Tooltip("A visual preview model (semi-transparent, no colliders)")]
    public GameObject previewDynamitePrefab;

    [Header("Settings")]
    public float attachDistance = 3.0f;
    public LayerMask interactLayer = ~0;

    private GameObject previewInstance;
    private DestructibleWall targetWall;
    private bool isEquipped = false;
    private Vector3 placePosition;
    private Quaternion placeRotation;

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

        bool showPreview = false;
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            RaycastHit hit;
            if (Physics.Raycast(cam.position, cam.forward, out hit, attachDistance, interactLayer))
            {
                DestructibleWall wall = hit.collider.GetComponentInParent<DestructibleWall>();
                if (wall != null)
                {
                    targetWall = wall;
                    placePosition = hit.point;
                    // Place it flat against the wall normal
                    placeRotation = Quaternion.LookRotation(hit.normal);
                    showPreview = true;
                }
            }
        }

        if (showPreview)
        {
            if (previewInstance == null && previewDynamitePrefab != null)
            {
                previewInstance = Instantiate(previewDynamitePrefab);
                // Disable colliders on the preview to prevent raycast self-hits
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
            targetWall = null;
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

        // Clean up preview instance
        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }
        targetWall = null;
    }

    public override void PrimaryAction()
    {
        // Only allow placement when projecting on a target wall
        if (isEquipped && targetWall != null && placedDynamitePrefab != null)
        {
            // Spawn the real dynamite
            GameObject spawnedDynamite = Instantiate(placedDynamitePrefab, placePosition, placeRotation);
            // Parent it to the wall so it stays attached if the wall moves/is destroyed
            spawnedDynamite.transform.SetParent(targetWall.transform, true);

            PlacedDynamite placedScript = spawnedDynamite.GetComponent<PlacedDynamite>();
            if (placedScript != null)
            {
                placedScript.Initialize(targetWall);
            }

            // Cleanup preview
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }

            // Consume dynamite item from inventory
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

        // Notify inventory changes
        PlayerInventory.Instance.onInventoryChanged?.Invoke();
        // Force UI/text refresh
        var heistUI = GameManager.Instance != null ? GameManager.Instance.heistUI : null;
        if (heistUI != null)
        {
            heistUI.SetInteractionText("");
        }
    }
}
