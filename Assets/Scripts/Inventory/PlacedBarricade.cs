using UnityEngine;
using UnityEngine.AI;
using FMODUnity;
using FMOD.Studio;

public class PlacedBarricade : MonoBehaviour
{
    [Header("Barricade Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Effects")]
    public GameObject breakEffectPrefab;
    public EventReference breakSound;
    public EventReference hitSound;

    [Header("NavMesh Blocking")]
    public NavMeshObstacle navObstacle;

    private bool hasBeenAttempted = false;
    private bool isBroken = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        if (navObstacle == null) navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle != null)
        {
            navObstacle.carving = true;
            navObstacle.enabled = true;
        }

        // Ensure collider is present for enemy raycast / melee detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(1.5f, 2f, 0.4f);
            box.center = new Vector3(0f, 1f, 0f);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isBroken) return;

        currentHealth -= damage;

        if (!hitSound.IsNull)
        {
            RuntimeManager.PlayOneShot(hitSound, transform.position);
        }

        Debug.Log($"[Barricade] Barricade hit for {damage} damage! Remaining HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            BreakBarricade();
        }
    }

    public bool TryBlockPatrolExit(string doorName = "PatrolDoor")
    {
        if (hasBeenAttempted || isBroken) return false;

        hasBeenAttempted = true;
        Debug.Log($"[Barricade] 🛡️ Patrol exit at '{doorName}' was BLOCKED by Barricade! Patrol spawn cancelled.");
        BreakBarricade();
        return true; // Successfully blocked this first exit attempt
    }

    public void BreakBarricade()
    {
        if (isBroken) return;
        isBroken = true;

        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        if (!breakSound.IsNull)
        {
            EventInstance soundInst = RuntimeManager.CreateInstance(breakSound);
            soundInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            soundInst.start();
            soundInst.release();
        }

        Debug.Log("[Barricade] Barricade broken into pieces!");
        Destroy(gameObject);
    }
}
