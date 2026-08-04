using UnityEngine;
using UnityEngine.AI;
using FMODUnity;
using FMOD.Studio;

public class PlacedBarricade : MonoBehaviour
{
    [Header("Effects")]
    public GameObject breakEffectPrefab;
    public EventReference breakSound;

    [Header("NavMesh Blocking")]
    public NavMeshObstacle navObstacle;

    private bool hasBeenAttempted = false;

    private void Awake()
    {
        if (navObstacle == null) navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle != null)
        {
            navObstacle.carving = true;
            navObstacle.enabled = true;
        }
    }

    public bool TryBlockPatrolExit(string doorName = "PatrolDoor")
    {
        if (hasBeenAttempted) return false;

        hasBeenAttempted = true;
        Debug.Log($"[Barricade] 🛡️ Patrol exit at '{doorName}' was BLOCKED by Barricade! Patrol spawn cancelled.");
        BreakBarricade();
        return true; // Successfully blocked this first exit attempt
    }

    public void BreakBarricade()
    {
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

        Debug.Log("[Barricade] Barricade blocked patrol exit and broke into pieces!");
        Destroy(gameObject);
    }
}
