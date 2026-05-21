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

    [HideInInspector] public bool isActivePool = false;

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
