using UnityEngine;
using UnityEngine.AI;

public enum EnemyMovementMode
{
    Auto,
    NavMesh,
    DirectToPlayer
}

[RequireComponent(typeof(NavMeshAgent))]
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
    private readonly RaycastHit[] losHits = new RaycastHit[5];



    [Header("Tactical AI & Distance")]
    public float preferredMinDistance = 6.0f;
    public float hearingRadius = 22.0f;

    [Header("Cover System")]
    public bool useCoverSystem = true;
    public float coverSearchInterval = 3.0f;
    private CoverPoint currentCoverPoint;
    private float coverSearchTimer = 0f;

    private Vector3? lastHeardSoundPos;
    private float soundAlertTimer = 0f;

    [Header("Weapon & Ammo State")]
    private int currentAmmo = 10;
    private bool isReloading = false;
    private float reloadTimer = 0f;
    private int shotsFiredInBurst = 0;
    private float burstPauseTimer = 0f;

    private LineRenderer tracerLineRenderer;

    private int DifficultyLevel
    {
        get
        {
            if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
            {
                return Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
            }
            return 1;
        }
    }

    private float MoveSpeed => (data != null ? data.moveSpeed : 3.5f) * (1f + (DifficultyLevel - 1) * 0.05f);
    private float AngularSpeed => data != null ? data.angularSpeed : 120f;
    private float Acceleration => data != null ? data.acceleration : 8f;
    private float DetectionRadius => data != null ? data.detectionRadius : 15f;
    private int DamagePerShot => Mathf.RoundToInt((data != null ? data.damagePerShot : 10) * (1f + (DifficultyLevel - 1) * 0.12f));
    private float AttackRange => data != null ? data.attackRange : 12f;
    private float FireInterval => Mathf.Max(0.5f, (data != null ? data.fireInterval : 1.5f) / (1f + (DifficultyLevel - 1) * 0.08f));
    private float HitChance => Mathf.Min(0.95f, (data != null ? data.hitChance : 0.75f) + (DifficultyLevel - 1) * 0.02f);
    private LayerMask LineOfSightMask => data != null ? (data.lineOfSightMask & ~(1 << LayerMask.NameToLayer("Enemy"))) : ~0;

    [Header("Debug Visualizer")]
    public bool showOverheadDebugText = true;
    private TextMesh debugTextMesh;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = MoveSpeed;
            agent.angularSpeed = AngularSpeed;
            agent.acceleration = Acceleration;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(20, 70);
            agent.stoppingDistance = preferredMinDistance;
            agent.radius = 0.42f;

            if (movementMode == EnemyMovementMode.DirectToPlayer)
            {
                agent.enabled = false;
            }
        }

        // Create 3D Overhead TextMesh Debugger visible in Game & Scene View
        GameObject textObj = new GameObject("OverheadDebugText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = Vector3.up * 2.3f;
        textObj.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face camera direction
        debugTextMesh = textObj.AddComponent<TextMesh>();
        debugTextMesh.characterSize = 0.15f;
        debugTextMesh.fontSize = 24;
        debugTextMesh.alignment = TextAlignment.Center;
        debugTextMesh.anchor = TextAnchor.MiddleCenter;
        debugTextMesh.text = "";

        // Initialize LineRenderer for Bullet Tracers
        tracerLineRenderer = gameObject.AddComponent<LineRenderer>();
        tracerLineRenderer.startWidth = 0.04f;
        tracerLineRenderer.endWidth = 0.01f;
        tracerLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        tracerLineRenderer.startColor = new Color(1f, 0.8f, 0.3f, 0.9f);
        tracerLineRenderer.endColor = new Color(1f, 0.3f, 0.1f, 0.0f);
        tracerLineRenderer.enabled = false;
        tracerLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void Start()
    {
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(this);
        }

        currentAmmo = data != null ? data.magazineSize : 10;
        FindPlayerTarget();
    }

    public void OnHearSound(Vector3 soundPosition)
    {
        lastHeardSoundPos = soundPosition;
        soundAlertTimer = 10.0f;

        if (currentCoverPoint == null && useCoverSystem)
        {
            CoverPoint bestPoint = CoverPoint.FindBestCoverPoint(transform.position, soundPosition, 20f);
            if (bestPoint != null && bestPoint.Claim(this))
            {
                currentCoverPoint = bestPoint;
            }
        }
    }

    private void UpdateOverheadDebugText(string status, Color color)
    {
        if (debugTextMesh != null)
        {
            debugTextMesh.gameObject.SetActive(showOverheadDebugText);
            if (showOverheadDebugText)
            {
                debugTextMesh.text = $"[{status}]\nHP:{ (playerHealth != null ? playerHealth.currentHealth : 0) } | Ammo:{currentAmmo}";
                debugTextMesh.color = color;
                if (Camera.main != null)
                {
                    debugTextMesh.transform.rotation = Quaternion.LookRotation(debugTextMesh.transform.position - Camera.main.transform.position);
                }
            }
        }
    }

    private void Update()
    {
        if (soundAlertTimer > 0f)
        {
            soundAlertTimer -= Time.deltaTime;
        }

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            UpdateOverheadDebugText($"RELOAD ({reloadTimer:F1}s)", Color.magenta);
            if (reloadTimer <= 0f)
            {
                isReloading = false;
                currentAmmo = data != null ? data.magazineSize : 10;
            }
        }

        if (burstPauseTimer > 0f)
        {
            burstPauseTimer -= Time.deltaTime;
        }

        if (playerTarget == null || playerHealth == null || playerHealth.isDead)
        {
            playerTarget = null;
            playerHealth = null;

            ReleaseCover();

            targetSearchTimer -= Time.deltaTime;
            if (targetSearchTimer <= 0f)
            {
                FindPlayerTarget();
            }

            StopMoving();
            UpdateOverheadDebugText("IDLE / PATROL", Color.gray);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

        // Tactical Cover Management
        if (useCoverSystem)
        {
            UpdateCoverLogic(distanceToPlayer);
        }

        bool canAttack = canShoot && distanceToPlayer <= AttackRange && HasLineOfSight();
        if (canAttack)
        {
            StopMoving();
            FaceTarget();
            UpdateOverheadDebugText("COMBAT FIRING", Color.red);

            if (!isReloading && burstPauseTimer <= 0f)
            {
                TryShoot();
            }
            return;
        }

        // Ambush / Hold Angle Stance if player is behind cover but heard or targeted
        if (!canAttack && soundAlertTimer > 0f && lastHeardSoundPos.HasValue)
        {
            if (currentCoverPoint != null)
            {
                StopMoving();
                Vector3 dirToSound = lastHeardSoundPos.Value - transform.position;
                FaceDirection(dirToSound);
                UpdateOverheadDebugText("HOLDING ANGLE", Color.yellow);
                return;
            }
        }

        // Patrol Room Sweep: Move towards cover point or player room (no ramming)
        UpdateOverheadDebugText("ROOM SWEEP", Color.cyan);

        if (currentCoverPoint != null && currentCoverPoint.gameObject.activeInHierarchy)
        {
            if (ShouldUseNavMesh())
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(currentCoverPoint.transform.position);
                }
            }
        }
        else if (ShouldUseNavMesh())
        {
            MoveWithNavMesh();
        }
        else
        {
            if (distanceToPlayer > preferredMinDistance)
            {
                MoveDirectly(distanceToPlayer);
            }
            else
            {
                StopMoving();
            }
        }
    }

    private void UpdateCoverLogic(float distanceToPlayer)
    {
        coverSearchTimer -= Time.deltaTime;

        // Release cover if player rushed too close (flanked)
        if (currentCoverPoint != null && distanceToPlayer < 3.5f)
        {
            ReleaseCover();
        }

        if (currentCoverPoint == null && coverSearchTimer <= 0f)
        {
            coverSearchTimer = coverSearchInterval;
            CoverPoint bestPoint = CoverPoint.FindBestCoverPoint(transform.position, playerTarget.position, 20f);
            if (bestPoint != null && bestPoint.Claim(this))
            {
                currentCoverPoint = bestPoint;
            }
        }
    }

    private void ReleaseCover()
    {
        if (currentCoverPoint != null)
        {
            currentCoverPoint.Release(this);
            currentCoverPoint = null;
        }
    }

    private void FindPlayerTarget()
    {
        targetSearchTimer = targetSearchInterval;

        // Try to find the closest living player in GameManager
        if (GameManager.Instance != null && GameManager.Instance.activePlayers.Count > 0)
        {
            PlayerHealth closestPlayer = null;
            float closestDistance = float.MaxValue;

            foreach (var player in GameManager.Instance.activePlayers)
            {
                if (player != null && !player.isDead)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestPlayer = player;
                    }
                }
            }

            if (closestPlayer != null)
            {
                playerTarget = closestPlayer.transform;
                playerHealth = closestPlayer;
                return;
            }
        }

        // Fallback: Find by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlayerHealth ph = playerObj.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                playerTarget = playerObj.transform;
                playerHealth = ph;
                return;
            }
        }

        // Fallback: GameManager playerTransform
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            PlayerHealth ph = GameManager.Instance.playerTransform.GetComponent<PlayerHealth>();
            if (ph != null && !ph.isDead)
            {
                playerTarget = GameManager.Instance.playerTransform;
                playerHealth = ph;
            }
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
            // Try to warp agent back to the nearest NavMesh position
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            return agent.isOnNavMesh;
        }

        return movementMode == EnemyMovementMode.NavMesh || movementMode == EnemyMovementMode.Auto;
    }

    private void MoveWithNavMesh()
    {
        if (playerTarget == null) return;

        float distance = Vector3.Distance(transform.position, playerTarget.position);
        agent.stoppingDistance = preferredMinDistance;

        if (distance <= preferredMinDistance)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
            }
            FaceTarget();
            return;
        }

        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        agent.SetDestination(playerTarget.position);
    }

    private void MoveDirectly(float distanceToPlayer)
    {
        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;

        FaceDirection(direction);

        if (distanceToPlayer <= directMovementStopDistance)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            return;
        }

        Vector3 translation = direction.normalized * MoveSpeed * Time.deltaTime;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (agent.isStopped)
            {
                agent.isStopped = false;
            }
            agent.Move(translation);
        }
        else
        {
            transform.position += translation;
        }
    }

    private bool HasLineOfSight()
    {
        if (!requireLineOfSight || playerTarget == null)
        {
            return true;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * aimHeight + transform.forward * 0.35f;
        Vector3 target = playerTarget.position + Vector3.up * aimHeight;
        Vector3 direction = target - origin;
        float distance = Mathf.Min(direction.magnitude, AttackRange);

        int hitCount = Physics.RaycastNonAlloc(origin, direction.normalized, losHits, distance, LineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hitCount > 0)
        {
            float closestDist = float.MaxValue;
            RaycastHit closestHit = default;
            bool foundHit = false;

            for (int i = 0; i < hitCount; i++)
            {
                if (losHits[i].collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (losHits[i].distance < closestDist)
                {
                    closestDist = losHits[i].distance;
                    closestHit = losHits[i];
                    foundHit = true;
                }
            }

            if (foundHit)
            {
                PlayerHealth hitPlayer = closestHit.collider.GetComponentInParent<PlayerHealth>();
                return hitPlayer != null && hitPlayer == playerHealth;
            }
        }

        return false;
    }

    private void TryShoot()
    {
        if (isReloading || burstPauseTimer > 0f) return;

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

        // Check Magazine Ammo
        if (currentAmmo <= 0)
        {
            isReloading = true;
            reloadTimer = data != null ? data.reloadTime : 2.5f;
            Debug.Log($"[Enemy] {name} magazine empty! Reloading for {reloadTimer}s...");
            return;
        }

        currentAmmo--;
        shotsFiredInBurst++;

        int burstLimit = data != null ? data.burstCount : 3;
        if (shotsFiredInBurst >= burstLimit)
        {
            shotsFiredInBurst = 0;
            burstPauseTimer = data != null ? data.burstPause : 1.2f;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * aimHeight + transform.forward * 0.35f;
        Vector3 targetPoint = playerTarget.position + Vector3.up * aimHeight;

        // Broadcast gunshot noise to alert nearby guards
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(origin, hearingRadius);
        }

        // Play gunshot audio
        if (data != null && !data.fireEvent.IsNull)
        {
            FMODUnity.RuntimeManager.PlayOneShot(data.fireEvent, origin);
        }

        bool isHit = Random.value <= HitChance;
        if (isHit)
        {
            playerHealth.TakeDamage(DamagePerShot);
            Debug.Log($"[Enemy] {name} hit player for {DamagePerShot}. Player HP: {playerHealth.currentHealth}/{playerHealth.maxHealth}");
            TriggerBulletTracer(origin, targetPoint);
        }
        else
        {
            // Missed shot offset
            Vector3 missOffset = Random.insideUnitSphere * 1.5f;
            TriggerBulletTracer(origin, targetPoint + missOffset);
            Debug.Log($"[Enemy] {name} missed.");
        }
    }

    private void TriggerBulletTracer(Vector3 startPos, Vector3 endPos)
    {
        if (tracerLineRenderer != null && gameObject.activeInHierarchy)
        {
            StartCoroutine(DrawTracerRoutine(startPos, endPos));
        }
    }

    private System.Collections.IEnumerator DrawTracerRoutine(Vector3 startPos, Vector3 endPos)
    {
        tracerLineRenderer.SetPosition(0, startPos);
        tracerLineRenderer.SetPosition(1, endPos);
        tracerLineRenderer.enabled = true;

        yield return new WaitForSeconds(0.08f);

        tracerLineRenderer.enabled = false;
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

    private void OnDrawGizmos()
    {
        Vector3 headPos = transform.position + Vector3.up * 2.2f;

        if (isReloading)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(headPos, 0.35f);
        }
        else if (playerTarget != null && HasLineOfSight())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(headPos, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * aimHeight, playerTarget.position + Vector3.up * aimHeight);
        }
        else if (currentCoverPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(headPos, Vector3.one * 0.4f);
            Gizmos.DrawLine(transform.position, currentCoverPoint.transform.position);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(headPos, 0.25f);
        }
    }
}
