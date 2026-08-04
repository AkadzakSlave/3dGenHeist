using UnityEngine;

public class MolotovItem : EquipableItem
{
    [Header("References")]
    public Transform playerCamera;
    public GameObject handMesh;
    public LineRenderer trajectoryLineRenderer;

    [Header("Prefabs")]
    public GameObject molotovProjectilePrefab;

    [Header("Settings")]
    public float throwForce = 15f;
    public float throwUpwardForce = 2f;
    public int trajectorySteps = 30;
    public float timeStep = 0.05f;

    private bool isEquipped = false;

    private InputReader inputReader => PlayerInventory.Instance != null ? PlayerInventory.Instance.inputReader : null;

    private void Start()
    {
        if (handMesh != null) handMesh.SetActive(false);
        if (trajectoryLineRenderer != null) trajectoryLineRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (!isEquipped) return;

        HandleTrajectoryPreview();
    }

    private void HandleTrajectoryPreview()
    {
        if (inputReader == null || trajectoryLineRenderer == null) return;

        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            trajectoryLineRenderer.enabled = true;

            // Offset start position away from camera lens (towards hand position) to avoid near-clip z-fighting
            Vector3 startPos = cam.position + (cam.forward * 0.8f) + (cam.right * 0.25f) - (cam.up * 0.15f);
            Vector3 startVelocity = (cam.forward * throwForce) + (Vector3.up * throwUpwardForce);

            trajectoryLineRenderer.positionCount = trajectorySteps;
            Vector3 currentPos = startPos;
            Vector3 currentVel = startVelocity;

            for (int i = 0; i < trajectorySteps; i++)
            {
                trajectoryLineRenderer.SetPosition(i, currentPos);
                currentPos += currentVel * timeStep;
                currentVel += Physics.gravity * timeStep;

                if (Physics.Raycast(currentPos - currentVel * timeStep, currentVel.normalized, out RaycastHit hit, currentVel.magnitude * timeStep))
                {
                    trajectoryLineRenderer.positionCount = i + 1;
                    trajectoryLineRenderer.SetPosition(i, hit.point);
                    break;
                }
            }
        }
        else
        {
            if (trajectoryLineRenderer.enabled)
            {
                trajectoryLineRenderer.enabled = false;
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
        if (trajectoryLineRenderer != null) trajectoryLineRenderer.enabled = false;
    }

    public override void PrimaryAction()
    {
        if (isEquipped && molotovProjectilePrefab != null)
        {
            Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;

            Vector3 spawnPos = cam.position + cam.forward * 0.8f;
            GameObject projectileObj = Instantiate(molotovProjectilePrefab, spawnPos, cam.rotation);

            // Ignore collision with player colliders so the bottle doesn't hit the thrower on frame 0
            Collider[] bottleCols = projectileObj.GetComponentsInChildren<Collider>();
            GameObject playerObj = PlayerInventory.Instance != null ? PlayerInventory.Instance.gameObject : (cam.parent != null ? cam.parent.gameObject : null);
            if (playerObj != null)
            {
                Collider[] playerCols = playerObj.GetComponentsInChildren<Collider>();
                foreach (var bCol in bottleCols)
                {
                    foreach (var pCol in playerCols)
                    {
                        if (bCol != null && pCol != null)
                        {
                            Physics.IgnoreCollision(bCol, pCol, true);
                        }
                    }
                }
            }

            Rigidbody rb = projectileObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 throwVel = (cam.forward * throwForce) + (Vector3.up * throwUpwardForce);
                rb.AddForce(throwVel, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }

            if (trajectoryLineRenderer != null) trajectoryLineRenderer.enabled = false;

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
