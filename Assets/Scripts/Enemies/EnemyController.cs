using UnityEngine;
using UnityEngine.AI;

public enum EnemyMovementMode
{
    Auto,
    NavMesh,
    DirectToPlayer
}

public class EnemyController : MonoBehaviour
{
    public EnemyData data;

    [Header("Movement")]
    public EnemyMovementMode movementMode = EnemyMovementMode.Auto;
    public float directMovementStopDistance = 2.0f;
    public float targetSearchInterval = 1.0f;
    public bool directMovementIgnoresDetectionRadius = true;

    [Header("Combat")]
    public Transform firePoint;
    public bool canShoot = true;
    public bool requireLineOfSight = true;
    public float aimHeight = 1.2f;

    private NavMeshAgent agent;
    private Transform playerTarget;
    private PlayerHealth playerHealth;
    private float targetSearchTimer = 0f;
    private float fireTimer = 0f;

    private float MoveSpeed => data != null ? data.moveSpeed : 3.5f;
    private float AngularSpeed => data != null ? data.angularSpeed : 120f;
    private float Acceleration => data != null ? data.acceleration : 8f;
    private float DetectionRadius => data != null ? data.detectionRadius : 15f;
    private int DamagePerShot => data != null ? data.damagePerShot : 10;
    private float AttackRange => data != null ? data.attackRange : 12f;
    private float FireInterval => data != null ? data.fireInterval : 1.5f;
    private float HitChance => data != null ? data.hitChance : 0.75f;
    private LayerMask LineOfSightMask => data != null ? data.lineOfSightMask : ~0;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = MoveSpeed;
            agent.angularSpeed = AngularSpeed;
            agent.acceleration = Acceleration;

            if (movementMode == EnemyMovementMode.DirectToPlayer)
            {
                agent.enabled = false;
            }
        }
    }

    private void Start()
    {
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(this);
        }

        FindPlayerTarget();
    }

    private void Update()
    {
        if (playerTarget == null)
        {
            targetSearchTimer -= Time.deltaTime;
            if (targetSearchTimer <= 0f)
            {
                FindPlayerTarget();
            }

            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        bool shouldCheckDetection = movementMode != EnemyMovementMode.DirectToPlayer || !directMovementIgnoresDetectionRadius;
        if (shouldCheckDetection && distanceToPlayer > DetectionRadius)
        {
            StopMoving();
            return;
        }

        bool canAttack = canShoot && distanceToPlayer <= AttackRange && HasLineOfSight();
        if (canAttack)
        {
            StopMoving();
            FaceTarget();
            TryShoot();
            return;
        }

        if (ShouldUseNavMesh())
        {
            MoveWithNavMesh();
        }
        else
        {
            MoveDirectly(distanceToPlayer);
        }
    }

    private void FindPlayerTarget()
    {
        targetSearchTimer = targetSearchInterval;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTarget = player.transform;
            playerHealth = player.GetComponent<PlayerHealth>();
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerTarget = GameManager.Instance.playerTransform;
            playerHealth = playerTarget.GetComponent<PlayerHealth>();
        }
    }

    private bool ShouldUseNavMesh()
    {
        if (movementMode == EnemyMovementMode.DirectToPlayer)
        {
            return false;
        }

        if (agent == null || !agent.enabled)
        {
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            if (movementMode == EnemyMovementMode.Auto)
            {
                agent.enabled = false;
            }

            return false;
        }

        return movementMode == EnemyMovementMode.NavMesh || movementMode == EnemyMovementMode.Auto;
    }

    private void MoveWithNavMesh()
    {
        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        agent.SetDestination(playerTarget.position);
    }

    private void MoveDirectly(float distanceToPlayer)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;

        FaceDirection(direction);

        if (distanceToPlayer <= directMovementStopDistance)
        {
            return;
        }

        transform.position += direction.normalized * MoveSpeed * Time.deltaTime;
    }

    private bool HasLineOfSight()
    {
        if (!requireLineOfSight || playerTarget == null)
        {
            return true;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * aimHeight + transform.forward * 0.25f;
        Vector3 target = playerTarget.position + Vector3.up * aimHeight;
        Vector3 direction = target - origin;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, AttackRange, LineOfSightMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            PlayerHealth hitPlayer = hit.collider.GetComponentInParent<PlayerHealth>();
            return hitPlayer != null && hitPlayer == playerHealth;
        }

        return false;
    }

    private void TryShoot()
    {
        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f)
        {
            return;
        }

        fireTimer = FireInterval;

        if (playerHealth == null || playerHealth.isDead)
        {
            FindPlayerTarget();
            return;
        }

        if (Random.value <= HitChance)
        {
            playerHealth.TakeDamage(DamagePerShot);
            Debug.Log($"[Enemy] {name} hit player for {DamagePerShot}. Player HP: {playerHealth.currentHealth}/{playerHealth.maxHealth}");
        }
        else
        {
            Debug.Log($"[Enemy] {name} missed.");
        }
    }

    private void FaceTarget()
    {
        if (playerTarget == null)
        {
            return;
        }

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;
        FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, AngularSpeed * Time.deltaTime);
        }
    }

    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    private void OnDestroy()
    {
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.UnregisterEnemy(this);
        }
    }
}
