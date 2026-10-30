using UnityEngine;

public enum SpawnPointType
{
    Initial,
    PatrolDoor
}

public enum EnemySpawnRole
{
    Guard_Post,      // Обычный пост/патруль в комнате (50-70% шанс)
    Vault_Guard,     // Охрана сейфа/хранилища (100% гарантирован, высокий приоритет)
    Corridor_Patrol, // Патрульный в коридоре (25-35% шанс)
    Patrol_Door      // Дверь подкреплений/штурма
}

public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Role & Preset")]
    public EnemySpawnRole spawnRole = EnemySpawnRole.Guard_Post;
    public SpawnPointType pointType = SpawnPointType.Initial;

    [Header("Spawn Settings")]
    [Range(0f, 100f)] public float spawnChance = 65f;
    public int priority = 5; // Чем выше, тем раньше заполняется стартовая квота
    public EnemyType initialEnemyType = EnemyType.Guard;
    public int minInitialCount = 1;
    public int maxInitialCount = 1;

    [Header("Patrol Spawn Condition (For Doors)")]
    [Tooltip("Необязательная разрушаемая стена для комнаты. Патруль спавнится только если проход свободен.")]
    public DestructibleWall requiredWall;

    [HideInInspector] public bool isActivePool = false;

    public bool IsRoomOpened()
    {
        if (pointType != SpawnPointType.PatrolDoor && spawnRole != EnemySpawnRole.Patrol_Door) 
            return true;

        // 1. Прямая ссылка на стену
        if (requiredWall != null)
        {
            return false;
        }

        // 2. Проверка сокетов разрушаемых стен в комнате
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
                        return false;
                    }
                }
            }
        }

        return GameManager.Instance != null && GameManager.Instance.isHeistActive;
    }

    private void OnValidate()
    {
        if (spawnRole == EnemySpawnRole.Patrol_Door)
        {
            pointType = SpawnPointType.PatrolDoor;
        }
        else if (pointType == SpawnPointType.PatrolDoor && spawnRole != EnemySpawnRole.Patrol_Door)
        {
            spawnRole = EnemySpawnRole.Patrol_Door;
        }

        if (spawnRole == EnemySpawnRole.Vault_Guard)
        {
            spawnChance = 100f;
            priority = 10;
        }
        else if (spawnRole == EnemySpawnRole.Corridor_Patrol && priority == 5)
        {
            priority = 2;
            spawnChance = 35f;
        }

        minInitialCount = Mathf.Max(0, minInitialCount);
        maxInitialCount = Mathf.Max(minInitialCount, maxInitialCount);
    }

    private void OnDrawGizmos()
    {
        bool isDoor = pointType == SpawnPointType.PatrolDoor || spawnRole == EnemySpawnRole.Patrol_Door;

        if (isDoor)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(1f, 2f, 0.4f));
            Gizmos.DrawLine(transform.position + Vector3.up * 1f, transform.position + Vector3.up * 1f + transform.forward * 1.5f);
        }
        else
        {
            if (spawnRole == EnemySpawnRole.Vault_Guard)
            {
                Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f); // Золотой для охраны сейфа
            }
            else if (spawnRole == EnemySpawnRole.Corridor_Patrol)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f); // Зелёный для патруля коридоров
            }
            else
            {
                Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f); // Красный для постов охраны
            }

            // Капсула охранника
            Vector3 center = transform.position + Vector3.up * 0.9f;
            Gizmos.DrawWireSphere(center + Vector3.up * 0.45f, 0.35f);
            Gizmos.DrawWireSphere(center - Vector3.up * 0.45f, 0.35f);
            Gizmos.DrawLine(center + Vector3.forward * 0.35f, center + Vector3.forward * 0.35f + transform.forward * 1.2f);
        }
    }
}
