using UnityEngine;

public enum SpawnPointType
{
    Initial,
    PatrolDoor
}

public class EnemySpawnPoint : MonoBehaviour
{
    public SpawnPointType pointType;

    [Header("Initial Spawn")]
    public EnemyType initialEnemyType = EnemyType.Guard;
    public int minInitialCount = 1;
    public int maxInitialCount = 1;

    [Header("Patrol Spawn Condition")]
    [Tooltip("Optional destructible wall for this room. Patrols spawn here only if wall is destroyed.")]
    public DestructibleWall requiredWall;

    [HideInInspector] public bool isActivePool = false;

    public bool IsRoomOpened()
    {
        if (pointType != SpawnPointType.PatrolDoor) return true;

        // 1. Direct explicit wall reference
        if (requiredWall != null)
        {
            return false; // Specific wall is still intact
        }

        // 2. Check room sockets in parent room prefab for spawned intact DestructibleWall instances
        Transform roomTransform = transform.parent;
        if (roomTransform != null)
        {
            RoomSocket[] sockets = roomTransform.GetComponentsInChildren<RoomSocket>();
            foreach (var sock in sockets)
            {
                if (sock == null) continue;
                Collider[] cols = Physics.OverlapSphere(sock.transform.position, 1.2f);
                foreach (var col in cols)
                {
                    if (col != null && col.GetComponentInParent<DestructibleWall>() != null)
                    {
                        return false; // Intact wall found on socket!
                    }
                }
            }
        }

        // 3. If no intact wall is blocking this room's sockets, check if heist is active
        return GameManager.Instance != null && GameManager.Instance.isHeistActive;
    }

    private void OnValidate()
    {
        minInitialCount = Mathf.Max(0, minInitialCount);
        maxInitialCount = Mathf.Max(minInitialCount, maxInitialCount);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = pointType == SpawnPointType.Initial ? Color.red : Color.blue;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);
    }
}
