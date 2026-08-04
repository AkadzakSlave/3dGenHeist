using UnityEngine;

public class BarricadeItem : EquipableItem
{
    [Header("References")]
    public Transform playerCamera;
    public GameObject handMesh;

    [Header("Prefabs")]
    public GameObject placedBarricadePrefab;
    public GameObject previewBarricadePrefab;

    [Header("Settings")]
    public float placeDistance = 3.5f;
    public LayerMask doorLayer = ~0;

    private GameObject previewInstance;
    private bool isEquipped = false;
    private Vector3 placePosition;
    private Quaternion placeRotation;
    private Transform targetDoorTransform;
    private bool isValidTarget = false;

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

        isValidTarget = false;
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, placeDistance, doorLayer))
            {
                // Must target a valid PatrolDoor spawn point
                EnemySpawnPoint spawnPoint = hit.collider.GetComponentInParent<EnemySpawnPoint>();
                if (spawnPoint != null && spawnPoint.pointType == SpawnPointType.PatrolDoor)
                {
                    targetDoorTransform = spawnPoint.transform;
                    placePosition = hit.point;
                    placeRotation = Quaternion.LookRotation(hit.normal);
                    isValidTarget = true;
                }
            }
        }

        if (isValidTarget)
        {
            if (previewInstance == null && previewBarricadePrefab != null)
            {
                previewInstance = Instantiate(previewBarricadePrefab);
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
            targetDoorTransform = null;
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
        isValidTarget = false;
        targetDoorTransform = null;
    }

    public override void PrimaryAction()
    {
        if (isEquipped && isValidTarget && placedBarricadePrefab != null)
        {
            GameObject spawnedBarricade = Instantiate(placedBarricadePrefab, placePosition, placeRotation);
            if (targetDoorTransform != null)
            {
                spawnedBarricade.transform.SetParent(targetDoorTransform, true);
            }

            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            isValidTarget = false;

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
