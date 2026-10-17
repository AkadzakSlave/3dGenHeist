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
    public float minPlayerDistance = 0.9f;
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

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        if (col.transform.IsChildOf(transform.root)) return true;
        if (col.GetComponentInParent<PlayerHealth>() != null) return true;
        if (col.GetComponentInParent<CharacterController>() != null) return true;
        return false;
    }

    private void HandlePreviewProjection()
    {
        if (inputReader == null) return;

        isValidTarget = false;
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, placeDistance, doorLayer, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (IsPlayerCollider(hit.collider)) continue;

                // Must target a valid PatrolDoor spawn point
                EnemySpawnPoint spawnPoint = hit.collider.GetComponentInParent<EnemySpawnPoint>();
                if (spawnPoint != null && spawnPoint.pointType == SpawnPointType.PatrolDoor)
                {
                    Vector3 playerPos = PlayerInventory.Instance != null ? PlayerInventory.Instance.transform.position : cam.position;
                    float distToDoor = Vector2.Distance(new Vector2(hit.point.x, hit.point.z), new Vector2(playerPos.x, playerPos.z));

                    if (distToDoor < minPlayerDistance)
                    {
                        // Player is standing directly in the doorway opening
                        isValidTarget = false;
                        var ui = GameManager.Instance != null ? GameManager.Instance.heistUI : null;
                        if (ui != null)
                        {
                            ui.SetInteractionText("<color=yellow>Отойдите назад из проёма двери для установки!</color>");
                        }
                        break;
                    }

                    targetDoorTransform = spawnPoint.transform;
                    placePosition = hit.point;
                    placeRotation = Quaternion.LookRotation(hit.normal);
                    isValidTarget = true;
                    break;
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

            IgnorePlayerCollisions(spawnedBarricade);

            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            isValidTarget = false;

            ConsumeFromInventory();
        }
    }

    private void IgnorePlayerCollisions(GameObject placedObj)
    {
        if (placedObj == null) return;
        GameObject playerObj = PlayerInventory.Instance != null ? PlayerInventory.Instance.gameObject : transform.root.gameObject;
        Collider[] placedCols = placedObj.GetComponentsInChildren<Collider>();
        Collider[] playerCols = playerObj.GetComponentsInChildren<Collider>();
        foreach (var pCol in playerCols)
        {
            foreach (var objCol in placedCols)
            {
                if (pCol != null && objCol != null)
                {
                    Physics.IgnoreCollision(pCol, objCol, true);
                }
            }
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
