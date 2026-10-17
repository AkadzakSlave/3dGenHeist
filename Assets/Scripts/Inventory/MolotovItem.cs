using UnityEngine;

public class MolotovItem : EquipableItem
{
    [Header("References")]
    public Transform playerCamera;
    public GameObject handMesh;
    public LineRenderer trajectoryLineRenderer;
    public LineRenderer landingCircleRenderer;

    [Header("Prefabs")]
    public GameObject molotovProjectilePrefab;

    [Header("Settings")]
    public float throwForce = 16f;
    public float throwUpwardForce = 1.0f;
    public int trajectorySteps = 35;
    public float timeStep = 0.04f;
    public float previewFireRadius = 2.5f;

    private bool isEquipped = false;

    private InputReader inputReader => PlayerInventory.Instance != null ? PlayerInventory.Instance.inputReader : null;

    private Material blueLineMaterial;

    private void Start()
    {
        if (handMesh != null) handMesh.SetActive(false);
        EnsureMaterials();
        SetupLineRenderers();
    }

    private void EnsureMaterials()
    {
        if (blueLineMaterial != null) return;

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

        if (unlitShader != null)
        {
            blueLineMaterial = new Material(unlitShader);
            Color blueColor = new Color(0.2f, 0.75f, 1.0f, 0.92f);
            if (blueLineMaterial.HasProperty("_BaseColor")) blueLineMaterial.SetColor("_BaseColor", blueColor);
            if (blueLineMaterial.HasProperty("_Color")) blueLineMaterial.SetColor("_Color", blueColor);
        }
    }

    private void SetupLineRenderers()
    {
        if (trajectoryLineRenderer != null)
        {
            if (blueLineMaterial != null) trajectoryLineRenderer.sharedMaterial = blueLineMaterial;
            trajectoryLineRenderer.startWidth = 0.045f;
            trajectoryLineRenderer.endWidth = 0.035f;
            trajectoryLineRenderer.widthMultiplier = 1.0f;
            trajectoryLineRenderer.startColor = new Color(0.2f, 0.75f, 1.0f, 0.95f);
            trajectoryLineRenderer.endColor = new Color(0.15f, 0.55f, 1.0f, 0.85f);
            trajectoryLineRenderer.numCornerVertices = 4;
            trajectoryLineRenderer.numCapVertices = 4;
            trajectoryLineRenderer.alignment = LineAlignment.View;
            trajectoryLineRenderer.enabled = false;
        }
        EnsureLandingCircleRenderer();
        if (landingCircleRenderer != null) landingCircleRenderer.enabled = false;
    }

    private void EnsureLandingCircleRenderer()
    {
        if (landingCircleRenderer != null) return;

        EnsureMaterials();

        GameObject circleObj = new GameObject("MolotovLandingCircle");
        circleObj.transform.SetParent(transform, false);
        landingCircleRenderer = circleObj.AddComponent<LineRenderer>();
        landingCircleRenderer.useWorldSpace = true;
        landingCircleRenderer.loop = true;
        landingCircleRenderer.startWidth = 0.055f;
        landingCircleRenderer.endWidth = 0.055f;
        landingCircleRenderer.startColor = new Color(0.2f, 0.8f, 1.0f, 0.95f);
        landingCircleRenderer.endColor = new Color(0.15f, 0.55f, 1.0f, 0.95f);

        if (blueLineMaterial != null)
        {
            landingCircleRenderer.sharedMaterial = blueLineMaterial;
        }
        else if (trajectoryLineRenderer != null && trajectoryLineRenderer.sharedMaterial != null)
        {
            landingCircleRenderer.sharedMaterial = trajectoryLineRenderer.sharedMaterial;
        }
        landingCircleRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (!isEquipped) return;

        HandleTrajectoryPreview();
    }

    public Vector3 GetThrowOrigin(Transform cam)
    {
        if (handMesh != null && handMesh.activeInHierarchy)
        {
            Transform bottleT = handMesh.transform.childCount > 0 ? handMesh.transform.GetChild(0) : handMesh.transform;
            return bottleT.position + (cam != null ? cam.forward * 0.12f : Vector3.zero);
        }

        if (cam != null)
        {
            return cam.position + (cam.right * 0.32f) - (cam.up * 0.22f) + (cam.forward * 0.45f);
        }

        return transform.position;
    }

    public void GetThrowPhysics(Transform cam, out Vector3 startPos, out Vector3 initialVelocity)
    {
        startPos = GetThrowOrigin(cam);

        // Raycast forward from camera to find exact crosshair aim convergence target
        Ray aimRay = new Ray(cam.position, cam.forward);
        Vector3 aimPoint;
        if (Physics.Raycast(aimRay, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (PlayerInventory.Instance != null && hit.collider.transform.IsChildOf(PlayerInventory.Instance.transform))
            {
                aimPoint = cam.position + cam.forward * 20f;
            }
            else
            {
                aimPoint = hit.point;
            }
        }
        else
        {
            aimPoint = cam.position + cam.forward * 25f;
        }

        Vector3 throwDir = (aimPoint - startPos).normalized;
        initialVelocity = (throwDir * throwForce) + (Vector3.up * throwUpwardForce);
    }

    private void HandleTrajectoryPreview()
    {
        if (inputReader == null || trajectoryLineRenderer == null) return;

        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);

        if (cam != null && inputReader.IsSecondaryActionPressed)
        {
            EnsureLandingCircleRenderer();
            
            if (blueLineMaterial != null && trajectoryLineRenderer.sharedMaterial != blueLineMaterial)
            {
                trajectoryLineRenderer.sharedMaterial = blueLineMaterial;
            }
            trajectoryLineRenderer.startWidth = 0.045f;
            trajectoryLineRenderer.endWidth = 0.035f;
            trajectoryLineRenderer.widthMultiplier = 1.0f;
            trajectoryLineRenderer.startColor = new Color(0.2f, 0.75f, 1.0f, 0.95f);
            trajectoryLineRenderer.endColor = new Color(0.15f, 0.55f, 1.0f, 0.85f);
            trajectoryLineRenderer.numCornerVertices = 4;
            trajectoryLineRenderer.numCapVertices = 4;
            trajectoryLineRenderer.alignment = LineAlignment.View;
            trajectoryLineRenderer.enabled = true;

            GetThrowPhysics(cam, out Vector3 startPos, out Vector3 startVelocity);

            trajectoryLineRenderer.positionCount = trajectorySteps;
            Vector3 currentPos = startPos;
            Vector3 currentVel = startVelocity;
            bool hitDetected = false;

            for (int i = 0; i < trajectorySteps; i++)
            {
                trajectoryLineRenderer.SetPosition(i, currentPos);
                Vector3 nextPos = currentPos + currentVel * timeStep;
                Vector3 seg = nextPos - currentPos;

                if (Physics.Raycast(currentPos, seg.normalized, out RaycastHit hit, seg.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (PlayerInventory.Instance != null && hit.collider.transform.IsChildOf(PlayerInventory.Instance.transform))
                    {
                        currentPos = nextPos;
                        currentVel += Physics.gravity * timeStep;
                        continue;
                    }

                    trajectoryLineRenderer.positionCount = i + 1;
                    trajectoryLineRenderer.SetPosition(i, hit.point);
                    hitDetected = true;

                    // Draw landing fire circle indicator on the hit floor/wall
                    if (landingCircleRenderer != null)
                    {
                        landingCircleRenderer.enabled = true;
                        int circleSegments = 24;
                        landingCircleRenderer.positionCount = circleSegments + 1;

                        Vector3 normal = hit.normal;
                        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
                        if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(normal, Vector3.right);
                        tangent.Normalize();
                        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

                        Vector3 center = hit.point + normal * 0.04f;
                        for (int c = 0; c <= circleSegments; c++)
                        {
                            float angle = c * (Mathf.PI * 2f / circleSegments);
                            Vector3 pt = center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * previewFireRadius;
                            landingCircleRenderer.SetPosition(c, pt);
                        }
                    }
                    break;
                }

                currentPos = nextPos;
                currentVel += Physics.gravity * timeStep;
            }

            if (!hitDetected && landingCircleRenderer != null)
            {
                landingCircleRenderer.enabled = false;
            }
        }
        else
        {
            if (trajectoryLineRenderer.enabled) trajectoryLineRenderer.enabled = false;
            if (landingCircleRenderer != null && landingCircleRenderer.enabled) landingCircleRenderer.enabled = false;
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
        if (landingCircleRenderer != null) landingCircleRenderer.enabled = false;
    }

    public override void PrimaryAction()
    {
        if (isEquipped && molotovProjectilePrefab != null)
        {
            Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null) return;

            GetThrowPhysics(cam, out Vector3 spawnPos, out Vector3 throwVel);
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
                rb.AddForce(throwVel, ForceMode.VelocityChange);
                // Calm, natural bottle tumble forward in flight (no crazy high-speed spinning)
                rb.angularDamping = 1.0f;
                rb.maxAngularVelocity = 5.0f;
                rb.angularVelocity = cam.right * 2.2f;
            }

            if (trajectoryLineRenderer != null) trajectoryLineRenderer.enabled = false;
            if (landingCircleRenderer != null) landingCircleRenderer.enabled = false;

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
