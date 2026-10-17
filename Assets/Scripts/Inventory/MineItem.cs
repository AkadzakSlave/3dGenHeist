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
    public float placeDistance = 4.0f;
    public float minPlayerDistance = 0.35f;
    public float mineRadius = 0.38f;
    public LayerMask groundLayer = ~0;

    private GameObject previewInstance;
    private bool isEquipped = false;
    private Vector3 placePosition;
    private Quaternion placeRotation;
    private bool isValidPlacement = false;
    private MaterialPropertyBlock propBlock;
    private readonly int colorPropId = Shader.PropertyToID("_BaseColor");
    private readonly int mainColorPropId = Shader.PropertyToID("_Color");

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

        isValidPlacement = false;
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, placeDistance, groundLayer, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit firstHit = default;
            bool hasValidHit = false;

            foreach (var hit in hits)
            {
                if (IsPlayerCollider(hit.collider)) continue;
                firstHit = hit;
                hasValidHit = true;
                break;
            }

            if (hasValidHit)
            {
                float slopeAngle = Vector3.Angle(firstHit.normal, Vector3.up);
                Vector3 candidateFloorPos;
                Vector3 candidateFloorNormal;
                bool foundFloor = false;

                if (slopeAngle <= 35f)
                {
                    // Direct hit on walkable floor
                    candidateFloorPos = firstHit.point;
                    candidateFloorNormal = firstHit.normal;
                    foundFloor = true;
                }
                else
                {
                    // Hit a vertical wall/pillar. A mine cannot attach to walls!
                    // Instead, look for the floor directly below at the base of the wall
                    Vector3 rayDownOrigin = firstHit.point + firstHit.normal * 0.35f + Vector3.up * 0.1f;
                    if (Physics.Raycast(rayDownOrigin, Vector3.down, out RaycastHit floorHit, 4.0f, groundLayer, QueryTriggerInteraction.Ignore))
                    {
                        if (Vector3.Angle(floorHit.normal, Vector3.up) <= 35f && !IsPlayerCollider(floorHit.collider))
                        {
                            candidateFloorPos = floorHit.point;
                            candidateFloorNormal = floorHit.normal;
                            foundFloor = true;
                        }
                        else
                        {
                            candidateFloorPos = Vector3.zero;
                            candidateFloorNormal = Vector3.up;
                        }
                    }
                    else
                    {
                        candidateFloorPos = Vector3.zero;
                        candidateFloorNormal = Vector3.up;
                    }
                }

                if (foundFloor)
                {
                    // Mine preview always rests flat on the floor
                    placePosition = candidateFloorPos + candidateFloorNormal * 0.01f;
                    placeRotation = Quaternion.FromToRotation(Vector3.up, candidateFloorNormal);

                    // Check if anything obstructs the mine
                    isValidPlacement = CheckMineClearance(candidateFloorPos, candidateFloorNormal, cam);

                    EnsurePreviewInstance();
                    if (previewInstance != null)
                    {
                        previewInstance.SetActive(true);
                        previewInstance.transform.position = placePosition;
                        previewInstance.transform.rotation = placeRotation;
                        UpdatePreviewColor(isValidPlacement);
                    }
                    return;
                }
            }
        }

        // Hide preview when not aiming or no valid floor in range
        if (previewInstance != null && previewInstance.activeSelf)
        {
            previewInstance.SetActive(false);
        }
    }

    private bool CheckMineClearance(Vector3 candidatePos, Vector3 floorNormal, Transform cam)
    {
        // 1. Minimum distance from player body
        Vector3 playerPos = PlayerInventory.Instance != null ? PlayerInventory.Instance.transform.position : cam.position;
        float horizDist = Vector2.Distance(new Vector2(candidatePos.x, candidatePos.z), new Vector2(playerPos.x, playerPos.z));
        if (horizDist < minPlayerDistance)
        {
            return false;
        }

        // 2. Overlap check for solid obstacles (pillars, walls, crates, barricades) standing on the floor
        // Centered 10cm above the floor: floor tiles are below it, only tall obstacles intersect!
        Vector3 boxCenter = candidatePos + Vector3.up * 0.10f;
        Vector3 boxHalfExtents = new Vector3(mineRadius * 0.75f, 0.04f, mineRadius * 0.75f);
        Collider[] colliders = Physics.OverlapBox(boxCenter, boxHalfExtents, Quaternion.identity, groundLayer, QueryTriggerInteraction.Ignore);
        foreach (var col in colliders)
        {
            if (col == null || col.isTrigger) continue;
            if (IsPlayerCollider(col)) continue;

            // If collider is just a floor tile beneath the mine, ignore it
            if (col.bounds.max.y <= candidatePos.y + 0.05f) continue;

            // A solid object (pillar, wall, crate, door) sticks up into the mine space!
            return false;
        }

        // 3. Radial raycasts at height +8cm to detect vertical walls (even if part of the same MeshCollider)
        Vector3 centerH = candidatePos + Vector3.up * 0.08f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            if (Physics.Raycast(centerH, dir, out RaycastHit wallHit, mineRadius * 0.85f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                if (!IsPlayerCollider(wallHit.collider))
                {
                    // Vertical surface (wall/pillar) closer than the mine disc radius
                    if (Vector3.Angle(wallHit.normal, Vector3.up) >= 45f)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void EnsurePreviewInstance()
    {
        if (previewInstance == null && previewMinePrefab != null)
        {
            previewInstance = Instantiate(previewMinePrefab);
            foreach (var col in previewInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }
    }

    private void UpdatePreviewColor(bool isValid)
    {
        if (previewInstance == null) return;

        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        // Valid: semi-transparent cyan/blue. Blocked: semi-transparent red.
        Color tint = isValid ? new Color(0.15f, 0.65f, 1.0f, 0.55f) : new Color(1.0f, 0.2f, 0.2f, 0.55f);

        Renderer[] rends = previewInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(colorPropId, tint);
            propBlock.SetColor(mainColorPropId, tint);
            r.SetPropertyBlock(propBlock);
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
            GameObject spawnedMine = Instantiate(placedMinePrefab, placePosition, placeRotation);
            IgnorePlayerCollisions(spawnedMine);

            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
            isValidPlacement = false;

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
