using UnityEngine;
using UnityEngine.AI;

public enum EnemyMovementMode
{
    Auto,
    NavMesh,
    DirectToPlayer
}

public enum EnemyState
{
    Patrol,
    Investigate,
    RadioAlert,
    CombatCover,
    CombatAssault,
    Breaching
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    public EnemyData data;

    [Header("State Machine")]
    public EnemyState currentState = EnemyState.Patrol;

    [Header("Movement & Obstacle Avoidance")]
    public EnemyMovementMode movementMode = EnemyMovementMode.Auto;
    public float directMovementStopDistance = 2.0f;
    public float targetSearchInterval = 0.8f;
    public float agentRadius = 0.32f;
    public float agentAngularSpeed = 450f;
    public float agentAcceleration = 22f;

    [Header("Combat")]
    public Transform firePoint;
    public bool canShoot = true;
    public bool requireLineOfSight = true;
    public float aimHeight = 1.2f;

    [Header("Tactical AI & Distance")]
    public float preferredMinDistance = 5.0f;
    public float hearingRadius = 26.0f;

    [Header("Realistic Sensory Perception (Vision)")]
    [Tooltip("Дальность обнаружения, если враг смотрит прямо на игрока (центральный сектор)")]
    public float directViewDistance = 32.0f;
    [Tooltip("Угол прямого взгляда (градусы), где действует максимальная дальность")]
    public float directViewAngle = 35.0f;
    [Tooltip("Дальность периферийного бокового зрения")]
    public float peripheralViewDistance = 18.0f;
    [Tooltip("Широкий угол бокового зрения (градусы)")]
    public float peripheralViewAngle = 110.0f;

    [Header("Auditory Perception (Hearing Rear)")]
    [Tooltip("Дальность, на которой враг слышит бегущего со спины игрока")]
    public float rearSprintHearingDist = 8.5f;
    [Tooltip("Дальность, на которой враг слышит идущего обычным шагом игрока")]
    public float rearWalkHearingDist = 4.0f;
    [Tooltip("Дистанция обнаружения крадущегося игрока в упор")]
    public float rearCrouchHearingDist = 1.2f;

    [Header("Sensory Alert State")]
    public bool isAlerted = false;
    public float alertDuration = 14.0f;
    private float alertTimer = 0f;

    [Header("Tactical Pre-Aiming & Accuracy")]
    [Tooltip("Находится ли враг в засаде/держит угол под прицелом")]
    public bool isPreAiming = false;
    [Tooltip("Максимальный угол прицеливания (градусы), при котором действует бонус точности Pre-Aim")]
    public float directAimThresholdAngle = 20.0f;
    private float preAimTimer = 0f;
    private bool isRetreatingToCorner = false;
    private float retreatTimer = 0f;
    private Vector3? dynamicCoverPoint;
    private bool isPeeking = false;
    private float peekCycleTimer = 0f;

    [Header("Home Room & Living Patrol")]
    public float patrolWanderRadius = 7.5f;
    public float patrolWaitMin = 2.5f;
    public float patrolWaitMax = 5.0f;
    private Vector3 patrolOrigin;
    private Bounds homeRoomBounds;
    private bool hasHomeRoomBounds = false;
    private float patrolWaitTimer = 0f;
    private float patrolLookAroundTimer = 0f;

    [Header("Cover System")]
    public bool useCoverSystem = true;
    public float coverSearchInterval = 2.5f;
    private CoverPoint currentCoverPoint;
    private float coverSearchTimer = 0f;

    [Header("Barricade Breaching")]
    public float breachRange = 2.2f;
    public float breachAttackInterval = 0.65f;
    public int breachDamage = 35;
    private PlacedBarricade targetBarricade;
    private float breachAttackTimer = 0f;

    [Header("Radio Communications")]
    public float radioCallDuration = 0.8f;
    private bool hasCalledRadio = false;
    private float radioTimer = 0f;

    [Header("Weapon & Ammo State")]
    private int currentAmmo = 10;
    private bool isReloading = false;
    private float reloadTimer = 0f;
    private int shotsFiredInBurst = 0;
    private float burstPauseTimer = 0f;
    private float fireTimer = 0f;

        [Header("Colliders and Hitboxes Setup")]
    [Tooltip("Радиус физического коллайдера туловища для движения")]
    public float movementRadius = 0.26f;
    [Tooltip("Высота физического коллайдера туловища для движения")]
    public float movementHeight = 1.70f;
    [Tooltip("Смещение центра коллайдера туловища (Y)")]
    public float movementCenterY = 0.85f;

    [Space(6)]
    [Tooltip("Радиус хитбокса тела (урон 1.0x)")]
    public float bodyHitboxRadius = 0.44f;
    [Tooltip("Высота хитбокса тела")]
    public float bodyHitboxHeight = 1.45f;
    [Tooltip("Смещение центра хитбокса тела (Y)")]
    public float bodyHitboxCenterY = 0.75f;

    [Space(6)]
    [Tooltip("Радиус хитбокса головы/шляпы (хэдшот 1.5x)")]
    public float headHitboxRadius = 0.32f;
    [Tooltip("Высота хитбокса головы")]
    public float headHitboxHeight = 0.60f;
    [Tooltip("Смещение центра хитбокса головы (Y) - охватывает голову и шляпу")]
    public float headHitboxCenterY = 1.62f;

    [Header("Animation")]
    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsAlertedHash = Animator.StringToHash("IsAlerted");
    private static readonly int ShootTriggerHash = Animator.StringToHash("Shoot");
    private static readonly int BreachTriggerHash = Animator.StringToHash("BreachAttack");
    private static readonly int InCoverHash = Animator.StringToHash("InCover");

    [Header("Debug Visualizer")]
    public bool showOverheadDebugText = true;
    private TextMesh debugTextMesh;

    private NavMeshAgent agent;
    private Transform playerTarget;
    private PlayerHealth playerHealth;
    private float targetSearchTimer = 0f;
    private Vector3? lastHeardSoundPos;
    private float soundAlertTimer = 0f;
    private LineRenderer tracerLineRenderer;
    private readonly RaycastHit[] losHits = new RaycastHit[16];
    private readonly Collider[] overlapCols = new Collider[8];

    // Tactical strafing during assault
    private float strafeTimer = 0f;
    private Vector3 currentStrafeOffset = Vector3.zero;

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
    private float AngularSpeed => data != null ? data.angularSpeed : agentAngularSpeed;
    private float Acceleration => data != null ? data.acceleration : agentAcceleration;
    private float DetectionRadius => (data != null ? data.detectionRadius : directViewDistance) * (IsAssaultActive ? 1.4f : 1f);
    private int DamagePerShot => Mathf.RoundToInt((data != null ? data.damagePerShot : 10) * (1f + (DifficultyLevel - 1) * 0.12f));
    private float AttackRange => data != null ? data.attackRange : 28f;
    private float FireInterval => Mathf.Max(0.12f, (data != null ? data.fireInterval : 1.5f) / (1f + (DifficultyLevel - 1) * 0.08f));
    private float HitChance => Mathf.Min(0.95f, (data != null ? data.hitChance : 0.75f) + (DifficultyLevel - 1) * 0.02f);
    private LayerMask LineOfSightMask => data != null ? data.lineOfSightMask : ~0;
    public bool IsAssaultActive => EnemyManager.Instance != null && EnemyManager.Instance.isAssaultWaveActive;


    private void OnValidate()
    {
        // Не выполняем для ассетов префабов на диске (вне сцены), только для экземпляров на сцене
        if (!Application.isPlaying && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            EnsureDualColliders();
        }
    }
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Настройка раздельных коллайдеров: компактный для перемещения и широкий для стрельбы игрока
        EnsureDualColliders();

        if (agent != null)
        {
            agent.speed = MoveSpeed;
            agent.angularSpeed = AngularSpeed;
            agent.acceleration = Acceleration;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(20, 70);
            agent.stoppingDistance = preferredMinDistance;
            agent.radius = agentRadius;

            if (movementMode == EnemyMovementMode.DirectToPlayer)
            {
                agent.enabled = false;
            }
        }

        animator = GetComponentInChildren<Animator>();

        GameObject textObj = new GameObject("OverheadDebugText");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = Vector3.up * 2.3f;
        textObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
        debugTextMesh = textObj.AddComponent<TextMesh>();
        debugTextMesh.characterSize = 0.15f;
        debugTextMesh.fontSize = 24;
        debugTextMesh.alignment = TextAlignment.Center;
        debugTextMesh.anchor = TextAnchor.MiddleCenter;
        debugTextMesh.text = "";

        tracerLineRenderer = gameObject.AddComponent<LineRenderer>();
        tracerLineRenderer.startWidth = 0.04f;
        tracerLineRenderer.endWidth = 0.01f;
        tracerLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        tracerLineRenderer.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
        tracerLineRenderer.endColor = new Color(1f, 0.3f, 0.1f, 0.0f);
        tracerLineRenderer.enabled = false;
        tracerLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Настраивает систему раздельных хитбоксов:
    /// 1. Физический коллайдер туловища на корне (для движения, навигации и отталкивания от стен/проходов)
    /// 2. Широкий хитбокс туловища (Body Hitbox, x1.0 урона)
    /// 3. Хитбокс головы (Head Hitbox, x1.5 урона при попадании)
    /// </summary>
    private void EnsureDualColliders()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) Destroy(box);

        // 1. Физический коллайдер туловища для перемещения (аккуратный по телу персонажа)
        CapsuleCollider moveCap = GetComponent<CapsuleCollider>();
        if (moveCap == null) moveCap = gameObject.AddComponent<CapsuleCollider>();
        moveCap.radius = movementRadius;
        moveCap.height = movementHeight;
        moveCap.center = new Vector3(0f, movementCenterY, 0f);

        // 2. Дочерний хитбокс ТЕЛА (шире видимой модели для комфортной стрельбы)
        Transform bodyHitboxTrans = transform.Find("BodyDamageHitbox");
        if (bodyHitboxTrans == null)
        {
            // Совместимость со старым именем если было
            bodyHitboxTrans = transform.Find("DamageHitbox");
        }

        GameObject bodyHitboxObj;
        if (bodyHitboxTrans == null)
        {
            bodyHitboxObj = new GameObject("BodyDamageHitbox");
            bodyHitboxObj.transform.SetParent(transform, false);
            bodyHitboxObj.transform.localPosition = Vector3.zero;
            bodyHitboxObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            bodyHitboxObj = bodyHitboxTrans.gameObject;
            bodyHitboxObj.name = "BodyDamageHitbox";
        }

        CapsuleCollider bodyCap = bodyHitboxObj.GetComponent<CapsuleCollider>();
        if (bodyCap == null) bodyCap = bodyHitboxObj.AddComponent<CapsuleCollider>();
                bodyCap.radius = bodyHitboxRadius;
        bodyCap.height = bodyHitboxHeight;
        bodyCap.center = new Vector3(0f, bodyHitboxCenterY, 0f);
        bodyCap.isTrigger = false;

        EnemyDamageHitbox bodyHitbox = bodyHitboxObj.GetComponent<EnemyDamageHitbox>();
        if (bodyHitbox == null) bodyHitbox = bodyHitboxObj.AddComponent<EnemyDamageHitbox>();
        bodyHitbox.bodyPart = EnemyBodyPart.Body;
        bodyHitbox.damageMultiplier = 1.0f;

        // 3. Дочерний хитбокс ГОЛОВЫ (для хэдшотов с множителем x1.5)
        Transform headHitboxTrans = transform.Find("HeadDamageHitbox");
        GameObject headHitboxObj;
        if (headHitboxTrans == null)
        {
            headHitboxObj = new GameObject("HeadDamageHitbox");
            headHitboxObj.transform.SetParent(transform, false);
            headHitboxObj.transform.localPosition = Vector3.zero;
            headHitboxObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            headHitboxObj = headHitboxTrans.gameObject;
        }

        CapsuleCollider headCap = headHitboxObj.GetComponent<CapsuleCollider>();
        if (headCap == null) headCap = headHitboxObj.AddComponent<CapsuleCollider>();
                headCap.radius = headHitboxRadius;
        headCap.height = headHitboxHeight;
        headCap.center = new Vector3(0f, headHitboxCenterY, 0f);
        headCap.isTrigger = false;

        EnemyDamageHitbox headHitbox = headHitboxObj.GetComponent<EnemyDamageHitbox>();
        if (headHitbox == null) headHitbox = headHitboxObj.AddComponent<EnemyDamageHitbox>();
        headHitbox.bodyPart = EnemyBodyPart.Head;
        headHitbox.damageMultiplier = 1.5f;

        // Отключаем физическое столкновение только если объект находится в загруженной сцене
        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
        {
            if (moveCap != null && bodyCap != null) Physics.IgnoreCollision(moveCap, bodyCap, true);
            if (moveCap != null && headCap != null) Physics.IgnoreCollision(moveCap, headCap, true);
            if (bodyCap != null && headCap != null) Physics.IgnoreCollision(bodyCap, headCap, true);
        }
    }

    private void Start()
    {
        patrolOrigin = transform.position;
        patrolWaitTimer = Random.Range(0.5f, 2.0f);

        InitHomeRoom();

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.RegisterEnemy(this);
        }

        currentAmmo = data != null ? data.magazineSize : 10;
        FindPlayerTarget();

        if (data != null && (data.type == EnemyType.Assaulter || data.type == EnemyType.Elite))
        {
            useCoverSystem = false;
            preferredMinDistance = data.type == EnemyType.Elite ? 4.0f : 2.5f;
        }
    }

    private void InitHomeRoom()
    {
        if (Physics.Raycast(patrolOrigin + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null)
            {
                Bounds b = hit.collider.bounds;
                if (b.size.x >= 3.0f && b.size.z >= 3.0f)
                {
                    homeRoomBounds = b;
                    homeRoomBounds.Expand(new Vector3(-1.2f, 0f, -1.2f));
                    hasHomeRoomBounds = true;
                    return;
                }
            }
        }

        homeRoomBounds = new Bounds(patrolOrigin, new Vector3(patrolWanderRadius * 2f, 4f, patrolWanderRadius * 2f));
        hasHomeRoomBounds = true;
    }

    public void OnHearSound(Vector3 soundPosition)
    {
        lastHeardSoundPos = soundPosition;
        soundAlertTimer = 14.0f;
        isAlerted = true;
        alertTimer = alertDuration;

        if (data == null || data.type == EnemyType.Guard)
        {
            isPreAiming = true;
            preAimTimer = 8.0f;
            currentState = EnemyState.Investigate;
        }
        else if (currentState == EnemyState.Patrol)
        {
            currentState = EnemyState.Investigate;
        }
    }

    public void OnRadioAlertReceived(Vector3 alertPosition, Transform target)
    {
        lastHeardSoundPos = alertPosition;
        soundAlertTimer = 18.0f;
        isAlerted = true;
        alertTimer = alertDuration;

        if (target != null)
        {
            playerTarget = target;
            playerHealth = target.GetComponent<PlayerHealth>();
        }

        if (currentState == EnemyState.Patrol || currentState == EnemyState.Investigate)
        {
            if (IsAssaultActive || (data != null && data.type != EnemyType.Guard))
            {
                currentState = EnemyState.CombatAssault;
            }
            else
            {
                isPreAiming = true;
                preAimTimer = 6.0f;
                currentState = EnemyState.Investigate;
            }
        }
    }

    public void OnDamagedAlert(Vector3 attackerPosition)
    {
        lastHeardSoundPos = attackerPosition;
        soundAlertTimer = 15.0f;
        isAlerted = true;
        alertTimer = alertDuration;

        if (!hasCalledRadio)
        {
            TriggerRadioAlert();
        }

        if (data == null || data.type == EnemyType.Guard)
        {
            if (TryRetreatToCorner(attackerPosition))
            {
                Debug.Log($"[Enemy] {name} попал под огонь! Совершает тактический рывок в укрытие.");
                return;
            }
        }

        if (currentState == EnemyState.Patrol)
        {
            currentState = EnemyState.Investigate;
        }
    }

    private void TriggerRadioAlert()
    {
        hasCalledRadio = true;
        currentState = EnemyState.RadioAlert;
        radioTimer = radioCallDuration;

        if (data != null)
        {
            if (!data.radioAlertEvent.IsNull)
            {
                FMODUnity.RuntimeManager.PlayOneShot(data.radioAlertEvent, transform.position);
            }
            if (!data.alertVoiceEvent.IsNull)
            {
                FMODUnity.RuntimeManager.PlayOneShot(data.alertVoiceEvent, transform.position);
            }
        }

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyRadioAlert(transform.position, 38.0f, playerTarget);
        }
    }

    /// <summary>
    /// Реалистичное восприятие: прямое зрение вдаль, боковое периферийное и обнаружение сзади только по звуку шагов.
    /// </summary>
    private bool IsPlayerSpotted(float distanceToPlayer, out bool heardBehind)
    {
        heardBehind = false;
        if (playerTarget == null) return false;

        // 1. Во время штурма полиции — круговой обзор на полную дистанцию
        if (IsAssaultActive)
        {
            return distanceToPlayer <= directViewDistance && HasLineOfSight();
        }

        Vector3 dirToPlayer = (playerTarget.position - transform.position).normalized;
        dirToPlayer.y = 0f;
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);

        // 2. Зрительное восприятие спереди (Vision)
        // 2.1 Прямой фокусный сектор (<= directViewAngle): видит далеко (до 32м)
        if (angleToPlayer <= directViewAngle * 0.5f)
        {
            if (distanceToPlayer <= directViewDistance && HasLineOfSight())
            {
                return true;
            }
        }
        // 2.2 Периферийное зрение (<= peripheralViewAngle): видит на средней дистанции (до 18м)
        else if (angleToPlayer <= peripheralViewAngle * 0.5f)
        {
            if (distanceToPlayer <= peripheralViewDistance && HasLineOfSight())
            {
                return true;
            }
        }

        // 3. Слепая зона со спины (Blind Zone > 110 град): глазами НЕ видит, реагирует ТОЛЬКО на шаги
        if (angleToPlayer > peripheralViewAngle * 0.5f)
        {
            PlayerMovement pm = playerHealth != null ? playerHealth.GetComponent<PlayerMovement>() : null;
            if (pm == null) pm = PlayerMovement.Instance;

            float hearingThreshold = rearWalkHearingDist;

            if (pm != null)
            {
                if (pm.IsCrouching)
                {
                    hearingThreshold = rearCrouchHearingDist;
                }
                else if (pm.IsSprinting && pm.IsMoving)
                {
                    hearingThreshold = rearSprintHearingDist;
                }
                else if (pm.IsMoving)
                {
                    hearingThreshold = rearWalkHearingDist;
                }
                else
                {
                    hearingThreshold = rearCrouchHearingDist;
                }
            }

            if (distanceToPlayer <= hearingThreshold)
            {
                heardBehind = true;
                return true;
            }
        }

        return false;
    }

    private void UpdateOverheadDebugText(string status, Color color)
    {
        if (debugTextMesh != null)
        {
            debugTextMesh.gameObject.SetActive(showOverheadDebugText);
            if (showOverheadDebugText)
            {
                string typeStr = data != null ? data.type.ToString() : "Enemy";
                string preAimStr = isPreAiming ? " [PRE-AIM]" : "";
                debugTextMesh.text = $"[{typeStr}] {status}{preAimStr}\nAmmo: {currentAmmo}/{ (data != null ? data.magazineSize : 10) }";
                debugTextMesh.color = color;
                if (Camera.main != null)
                {
                    debugTextMesh.transform.rotation = Quaternion.LookRotation(debugTextMesh.transform.position - Camera.main.transform.position);
                }
            }
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null || !animator.enabled) return;

        float currentSpeed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
        animator.SetFloat(SpeedHash, currentSpeed / Mathf.Max(0.1f, MoveSpeed));
        animator.SetBool(IsAlertedHash, isAlerted);
        animator.SetBool(InCoverHash, currentCoverPoint != null || dynamicCoverPoint.HasValue);
    }

    private void Update()
    {
        if (soundAlertTimer > 0f) soundAlertTimer -= Time.deltaTime;

        if (alertTimer > 0f)
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer <= 0f)
            {
                isAlerted = false;
                hasCalledRadio = false;
                isPreAiming = false;
            }
        }

        if (retreatTimer > 0f)
        {
            retreatTimer -= Time.deltaTime;
            if (retreatTimer <= 0f)
            {
                isRetreatingToCorner = false;
            }
        }

        if (preAimTimer > 0f)
        {
            preAimTimer -= Time.deltaTime;
            if (preAimTimer <= 0f)
            {
                isPreAiming = false;
            }
        }

        if (isReloading)
        {
            reloadTimer -= Time.deltaTime;
            if (reloadTimer <= 0f)
            {
                isReloading = false;
                currentAmmo = data != null ? data.magazineSize : 10;
            }
        }

        if (burstPauseTimer > 0f) burstPauseTimer -= Time.deltaTime;
        if (fireTimer > 0f) fireTimer -= Time.deltaTime;

        targetSearchTimer -= Time.deltaTime;
        if (targetSearchTimer <= 0f || playerTarget == null || (playerHealth != null && playerHealth.isDead))
        {
            targetSearchTimer = targetSearchInterval;
            FindPlayerTarget();
        }

        CheckBarricadeObstruction();

        switch (currentState)
        {
            case EnemyState.Breaching:
                ExecuteBreachingState();
                break;

            case EnemyState.RadioAlert:
                ExecuteRadioAlertState();
                break;

            case EnemyState.Patrol:
                ExecutePatrolState();
                break;

            case EnemyState.Investigate:
                ExecuteInvestigateState();
                break;

            case EnemyState.CombatCover:
                ExecuteCombatCoverState();
                break;

            case EnemyState.CombatAssault:
                ExecuteCombatAssaultState();
                break;
        }

        UpdateAnimator();
    }

    #region State Executions

    private void ExecutePatrolState()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = MoveSpeed * 0.65f;
            agent.stoppingDistance = 0.5f;
        }

        UpdateOverheadDebugText("PATROL (ROOM)", Color.gray);

        if (playerTarget != null && !playerHealth.isDead)
        {
            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (IsPlayerSpotted(dist, out bool heardBehind))
            {
                if (heardBehind)
                {
                    Vector3 dirToPlayer = playerTarget.position - transform.position;
                    dirToPlayer.y = 0f;
                    FaceDirection(dirToPlayer);
                    lastHeardSoundPos = playerTarget.position;
                    soundAlertTimer = 10f;
                    isPreAiming = true;
                    preAimTimer = 5.0f;
                    currentState = EnemyState.Investigate;
                    UpdateOverheadDebugText("HEARD BEHIND!", Color.yellow);
                    return;
                }

                OnPlayerSpottedTransition();
                return;
            }
        }

        if (soundAlertTimer > 0f && lastHeardSoundPos.HasValue)
        {
            currentState = EnemyState.Investigate;
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (hasHomeRoomBounds && !homeRoomBounds.Contains(transform.position))
            {
                agent.isStopped = false;
                agent.SetDestination(patrolOrigin);
                return;
            }

            if (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath)
            {
                patrolWaitTimer -= Time.deltaTime;
                patrolLookAroundTimer -= Time.deltaTime;

                if (patrolLookAroundTimer <= 0f)
                {
                    patrolLookAroundTimer = Random.Range(1.5f, 2.8f);
                    Vector3 glanceDir = Quaternion.Euler(0, Random.Range(-65f, 65f), 0) * transform.forward;
                    FaceDirection(glanceDir);
                }

                if (patrolWaitTimer <= 0f)
                {
                    patrolWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
                    PickNewPatrolDestination();
                }
            }
        }
    }

    private void PickNewPatrolDestination()
    {
        Vector3 targetCandidate;

        if (hasHomeRoomBounds)
        {
            float rx = Random.Range(homeRoomBounds.min.x, homeRoomBounds.max.x);
            float rz = Random.Range(homeRoomBounds.min.z, homeRoomBounds.max.z);
            targetCandidate = new Vector3(rx, transform.position.y, rz);
        }
        else
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolWanderRadius;
            targetCandidate = patrolOrigin + randomDir;
            targetCandidate.y = transform.position.y;
        }

        if (NavMesh.SamplePosition(targetCandidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
    }

    private void ExecuteInvestigateState()
    {
        // Если враг держит угол в режиме засады (Pre-Aiming)
        if (isPreAiming)
        {
            StopMoving();

            // Проверяем: если игрок появился в поле зрения — НЕМЕДЛЕННО СТРЕЛЯЕМ!
            if (playerTarget != null && !playerHealth.isDead)
            {
                float dist = Vector3.Distance(transform.position, playerTarget.position);
                if (IsPlayerSpotted(dist, out bool heardBehind))
                {
                    FaceTarget();

                    // Охранник в засаде стреляет без раздумий!
                    fireTimer = 0f;
                    TryShoot();

                    isPreAiming = false;
                    OnPlayerSpottedTransition();
                    return;
                }
            }

            // Пока игрок скрыт, держим ствол строго в направлении последнего звука
            if (lastHeardSoundPos.HasValue)
            {
                Vector3 dirToSound = lastHeardSoundPos.Value - transform.position;
                dirToSound.y = 0f;
                FaceDirection(dirToSound);
            }

            UpdateOverheadDebugText("HOLDING ANGLE (PRE-AIM)", Color.yellow);

            if (preAimTimer <= 0f)
            {
                isPreAiming = false;
            }
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = MoveSpeed * 0.85f;
            agent.stoppingDistance = 1.0f;
        }

        UpdateOverheadDebugText("INVESTIGATE", Color.yellow);

        if (playerTarget != null && !playerHealth.isDead)
        {
            float dist = Vector3.Distance(transform.position, playerTarget.position);
            if (IsPlayerSpotted(dist, out bool heardBehind))
            {
                if (heardBehind)
                {
                    FaceDirection(playerTarget.position - transform.position);
                }

                OnPlayerSpottedTransition();
                return;
            }
        }

        if (lastHeardSoundPos.HasValue)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(lastHeardSoundPos.Value);
            }

            if (Vector3.Distance(transform.position, lastHeardSoundPos.Value) <= 2.2f || soundAlertTimer <= 0f)
            {
                currentState = EnemyState.Patrol;
                patrolWaitTimer = 3.0f;
                lastHeardSoundPos = null;
            }
        }
        else
        {
            currentState = EnemyState.Patrol;
        }
    }

    private void ExecuteRadioAlertState()
    {
        StopMoving();
        UpdateOverheadDebugText("RADIO BACKUP!", Color.cyan);

        if (playerTarget != null)
        {
            FaceTarget();
        }

        radioTimer -= Time.deltaTime;
        if (radioTimer <= 0f)
        {
            if (data != null && (data.type == EnemyType.Assaulter || data.type == EnemyType.Elite) || IsAssaultActive)
            {
                currentState = EnemyState.CombatAssault;
            }
            else
            {
                currentState = EnemyState.CombatCover;
            }
        }
    }

    private void ExecuteCombatCoverState()
    {
        if (playerTarget == null || playerHealth == null || playerHealth.isDead)
        {
            ReleaseCover();
            currentState = EnemyState.Patrol;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTarget.position);

        if (IsAssaultActive)
        {
            ReleaseCover();
            currentState = EnemyState.CombatAssault;
            return;
        }

        if (dist < 3.8f)
        {
            TryRetreatToCorner(playerTarget.position);
        }

        if (isRetreatingToCorner && dynamicCoverPoint.HasValue)
        {
            UpdateOverheadDebugText("RETREATING TO CORNER!", Color.magenta);

            if (Vector3.Distance(transform.position, dynamicCoverPoint.Value) <= 0.6f || agent.remainingDistance <= 0.4f)
            {
                isRetreatingToCorner = false;
                isPeeking = false;
                peekCycleTimer = Random.Range(1.0f, 1.8f);

                if (currentAmmo <= 3 && !isReloading)
                {
                    isReloading = true;
                    reloadTimer = data != null ? data.reloadTime : 2.5f;
                }
            }
            return;
        }

        if (useCoverSystem && !dynamicCoverPoint.HasValue)
        {
            UpdateCoverLogic(dist);
        }

        peekCycleTimer -= Time.deltaTime;
        if (peekCycleTimer <= 0f)
        {
            isPeeking = !isPeeking;
            peekCycleTimer = isPeeking ? Random.Range(1.4f, 2.2f) : Random.Range(1.0f, 1.6f);
        }

        bool hasLOS = HasLineOfSight();
        bool inRange = dist <= AttackRange;

        if (isPeeking && inRange && hasLOS)
        {
            FaceTarget();
            UpdateOverheadDebugText("PEEKING & FIRING!", Color.red);

            if (!isReloading && burstPauseTimer <= 0f)
            {
                TryShoot();
            }
        }
        else if (!isPeeking)
        {
            UpdateOverheadDebugText("TAKING COVER", Color.magenta);

            if (hasLOS)
            {
                TryRetreatToCorner(playerTarget.position);
            }
            else
            {
                FaceDirection(playerTarget.position - transform.position);
            }
        }
        else
        {
            UpdateOverheadDebugText("PEEKING ANGLE", Color.yellow);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = MoveSpeed * 0.75f;
                agent.isStopped = false;
                agent.SetDestination(playerTarget.position);
            }
        }
    }

    private void ExecuteCombatAssaultState()
    {
        if (playerTarget == null || playerHealth == null || playerHealth.isDead)
        {
            currentState = EnemyState.Patrol;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTarget.position);
        float closeStopDist = data != null && data.type == EnemyType.Elite ? 4.0f : 2.5f;

        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeTimer = Random.Range(1.2f, 2.5f);
            currentStrafeOffset = transform.right * Random.Range(-1.8f, 1.8f);
        }

        Vector3 destination = playerTarget.position + currentStrafeOffset;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = MoveSpeed;
            agent.stoppingDistance = closeStopDist;

            if (dist > closeStopDist)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
            else
            {
                agent.isStopped = true;
            }
        }
        else if (movementMode == EnemyMovementMode.DirectToPlayer)
        {
            MoveDirectly(dist);
        }

        FaceTarget();

        bool inRange = dist <= AttackRange;
        bool hasLOS = HasLineOfSight();

        if (inRange && hasLOS)
        {
            UpdateOverheadDebugText("ASSAULT PUSH!", Color.red);
            if (!isReloading && burstPauseTimer <= 0f)
            {
                TryShoot();
            }
        }
        else
        {
            UpdateOverheadDebugText("RUSHING IN!", new Color(1f, 0.4f, 0f));
        }
    }

    private void ExecuteBreachingState()
    {
        StopMoving();
        UpdateOverheadDebugText("BREACHING BARRICADE!", Color.red);

        if (targetBarricade == null || targetBarricade.gameObject == null)
        {
            targetBarricade = null;
            currentState = (IsAssaultActive || (data != null && data.type != EnemyType.Guard)) ? EnemyState.CombatAssault : EnemyState.Investigate;
            return;
        }

        Vector3 dirToBarricade = targetBarricade.transform.position - transform.position;
        FaceDirection(dirToBarricade);

        breachAttackTimer -= Time.deltaTime;
        if (breachAttackTimer <= 0f)
        {
            breachAttackTimer = breachAttackInterval;

            if (animator != null && animator.enabled)
            {
                animator.SetTrigger(BreachTriggerHash);
            }

            targetBarricade.TakeDamage(breachDamage);

            Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * aimHeight;
            if (data != null && !data.breachHitEvent.IsNull)
            {
                FMODUnity.RuntimeManager.PlayOneShot(data.breachHitEvent, targetBarricade.transform.position);
            }
            else if (data != null && !data.fireEvent.IsNull)
            {
                FMODUnity.RuntimeManager.PlayOneShot(data.fireEvent, origin);
            }

            TriggerBulletTracer(origin, targetBarricade.transform.position + Vector3.up * 1f);
        }
    }

    #endregion

    #region Tactical Corner Retreat Finding

    public bool TryRetreatToCorner(Vector3 threatPos)
    {
        Vector3? cornerPoint = FindCornerRetreatPoint(threatPos, 6.5f);
        if (cornerPoint.HasValue)
        {
            dynamicCoverPoint = cornerPoint.Value;
            isRetreatingToCorner = true;
            retreatTimer = 3.0f;
            isPeeking = false;
            peekCycleTimer = 1.5f;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.speed = MoveSpeed * 1.25f;
                agent.stoppingDistance = 0.3f;
                agent.isStopped = false;
                agent.SetDestination(cornerPoint.Value);
            }

            currentState = EnemyState.CombatCover;
            return true;
        }
        return false;
    }

    private Vector3? FindCornerRetreatPoint(Vector3 threatPos, float searchRadius = 6.5f)
    {
        Vector3 dirFromThreat = (transform.position - threatPos).normalized;
        dirFromThreat.y = 0f;

        Vector3[] testDirs = new Vector3[]
        {
            dirFromThreat,
            Quaternion.Euler(0, 45, 0) * dirFromThreat,
            Quaternion.Euler(0, -45, 0) * dirFromThreat,
            Quaternion.Euler(0, 90, 0) * dirFromThreat,
            Quaternion.Euler(0, -90, 0) * dirFromThreat,
            -transform.forward
        };

        Vector3 bestPoint = Vector3.zero;
        bool found = false;
        float bestDist = float.MaxValue;

        for (int i = 0; i < testDirs.Length; i++)
        {
            Vector3 testPos = transform.position + testDirs[i] * searchRadius;
            if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                Vector3 checkOrigin = hit.position + Vector3.up * aimHeight;
                Vector3 checkTarget = threatPos + Vector3.up * aimHeight;
                Vector3 toThreat = checkTarget - checkOrigin;

                if (Physics.Raycast(checkOrigin, toThreat.normalized, toThreat.magnitude, LineOfSightMask, QueryTriggerInteraction.Ignore))
                {
                    float dist = Vector3.Distance(transform.position, hit.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestPoint = hit.position;
                        found = true;
                    }
                }
            }
        }

        return found ? bestPoint : (Vector3?)null;
    }

    #endregion

    #region Barricade Detection

    private void CheckBarricadeObstruction()
    {
        if (currentState == EnemyState.Breaching) return;

        Vector3 forwardRayOrigin = transform.position + Vector3.up * 1.0f;
        int hitCount = Physics.OverlapSphereNonAlloc(forwardRayOrigin + transform.forward * 1.2f, 1.1f, overlapCols);

        for (int i = 0; i < hitCount; i++)
        {
            PlacedBarricade barricade = overlapCols[i].GetComponentInParent<PlacedBarricade>();
            if (barricade != null)
            {
                targetBarricade = barricade;
                breachAttackTimer = 0.2f;
                currentState = EnemyState.Breaching;
                return;
            }
        }
    }

    #endregion

    private void OnPlayerSpottedTransition()
    {
        isAlerted = true;
        alertTimer = alertDuration;

        if (data != null && data.type == EnemyType.Guard && !hasCalledRadio)
        {
            TriggerRadioAlert();
        }
        else if (data != null && (data.type == EnemyType.Assaulter || data.type == EnemyType.Elite) || IsAssaultActive)
        {
            currentState = EnemyState.CombatAssault;
        }
        else
        {
            currentState = EnemyState.CombatCover;
        }
    }

    private void UpdateCoverLogic(float distanceToPlayer)
    {
        coverSearchTimer -= Time.deltaTime;

        if (currentCoverPoint != null && distanceToPlayer < 3.8f)
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

    public void ReleaseCover()
    {
        if (currentCoverPoint != null)
        {
            currentCoverPoint.Release(this);
            currentCoverPoint = null;
        }
        dynamicCoverPoint = null;
        isRetreatingToCorner = false;
    }

    private void FindPlayerTarget()
    {
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

    private void MoveDirectly(float distanceToPlayer)
    {
        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;

        FaceDirection(direction);

        if (distanceToPlayer <= directMovementStopDistance)
        {
            return;
        }

        Vector3 translation = direction.normalized * MoveSpeed * Time.deltaTime;
        transform.position += translation;
    }

    /// <summary>
    /// Проверяет прямую видимость до игрока многоточечным лучом (голова и грудь).
    /// Игнорирует других союзных врагов, чтобы стоящие рядом охранники не закрывали обзор друг другу.
    /// </summary>
    public bool HasLineOfSight()
    {
        if (!requireLineOfSight || playerTarget == null)
        {
            return true;
        }

        Vector3 origin = transform.position + Vector3.up * aimHeight;
        float distToPlayer = Vector3.Distance(origin, playerTarget.position + Vector3.up * aimHeight);

        float maxCheckDist = Mathf.Max(AttackRange, directViewDistance);
        if (distToPlayer > maxCheckDist)
        {
            return false;
        }

        Vector3 headTarget = playerTarget.position + Vector3.up * 1.6f;
        Vector3 chestTarget = playerTarget.position + Vector3.up * 1.05f;

        return CheckSingleRayLOS(origin, headTarget) || CheckSingleRayLOS(origin, chestTarget);
    }

    private bool CheckSingleRayLOS(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;

        int hitCount = Physics.RaycastNonAlloc(origin, dir.normalized, losHits, dist, LineOfSightMask, QueryTriggerInteraction.Ignore);
        if (hitCount > 0)
        {
            float closestDist = float.MaxValue;
            bool foundObstacle = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = losHits[i].collider;
                if (col == null) continue;

                // 1. Игнорируем себя и свои дочерние коллайдеры
                if (col.transform.IsChildOf(transform)) continue;

                // 2. Игнорируем ДРУГИХ ВРАГОВ (союзники не блокируют видимость!)
                if (col.GetComponentInParent<EnemyController>() != null || col.GetComponentInParent<EnemyDamageHitbox>() != null) continue;

                // 3. Игнорируем самого игрока (мы же в него целимся)
                if (col.GetComponentInParent<PlayerHealth>() != null) continue;

                if (losHits[i].distance < closestDist)
                {
                    closestDist = losHits[i].distance;
                    foundObstacle = true;
                }
            }

            // Твердое препятствие (стена/шкаф) ближе позиции игрока
            if (foundObstacle && closestDist < dist - 0.25f)
            {
                return false;
            }
        }

        return true;
    }

    private void TryShoot()
    {
        if (isReloading || burstPauseTimer > 0f) return;

        if (fireTimer > 0f) return;
        fireTimer = FireInterval;

        if (playerHealth == null || playerHealth.isDead)
        {
            FindPlayerTarget();
            return;
        }

        if (currentAmmo <= 0)
        {
            isReloading = true;
            reloadTimer = data != null ? data.reloadTime : 2.5f;

            if (data == null || data.type == EnemyType.Guard)
            {
                TryRetreatToCorner(playerTarget.position);
            }
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

        if (animator != null && animator.enabled)
        {
            animator.SetTrigger(ShootTriggerHash);
        }

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(origin, hearingRadius);
        }

        if (data != null && !data.fireEvent.IsNull)
        {
            FMODUnity.RuntimeManager.PlayOneShot(data.fireEvent, origin);
        }

        // Проверка: направлен ли ствол врага прямо на игрока
        Vector3 dirToTarget = (playerTarget.position - transform.position).normalized;
        dirToTarget.y = 0f;
        float aimAngle = Vector3.Angle(transform.forward, dirToTarget);
        bool isDirectlyAimed = aimAngle <= directAimThresholdAngle;

        float effectiveHitChance = HitChance;
        if (isPreAiming && isDirectlyAimed)
        {
            effectiveHitChance = Mathf.Min(0.95f, HitChance + 0.35f);
            Debug.Log($"[Enemy] {name} стреляет из засады прямой наводкой (угол отклонения: {aimAngle:F1}°)! Точность: {effectiveHitChance * 100:F0}%");
        }

        bool isHit = Random.value <= effectiveHitChance;

        if (isHit)
        {
            playerHealth.TakeDamage(DamagePerShot);
            TriggerBulletTracer(origin, targetPoint);
        }
        else
        {
            Vector3 missOffset = Random.insideUnitSphere * 1.4f;
            TriggerBulletTracer(origin, targetPoint + missOffset);
        }

        if (isPreAiming && shotsFiredInBurst >= 2)
        {
            isPreAiming = false;
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
        if (playerTarget == null) return;
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

        switch (currentState)
        {
            case EnemyState.Breaching:
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(headPos, Vector3.one * 0.6f);
                break;
            case EnemyState.CombatAssault:
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(headPos, 0.4f);
                break;
            case EnemyState.CombatCover:
                Gizmos.color = isPeeking ? Color.red : Color.magenta;
                Gizmos.DrawWireSphere(headPos, 0.4f);
                break;
            case EnemyState.Investigate:
                Gizmos.color = isPreAiming ? Color.yellow : new Color(1f, 0.8f, 0.2f);
                Gizmos.DrawSphere(headPos, 0.3f);
                break;
            default:
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(headPos, 0.25f);
                break;
        }

        // Конусы реалистичного зрения
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
        Vector3 leftDirect = Quaternion.Euler(0, -directViewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightDirect = Quaternion.Euler(0, directViewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + leftDirect * directViewDistance);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + rightDirect * directViewDistance);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.2f);
        Vector3 leftPeriph = Quaternion.Euler(0, -peripheralViewAngle * 0.5f, 0) * transform.forward;
        Vector3 rightPeriph = Quaternion.Euler(0, peripheralViewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + leftPeriph * peripheralViewDistance);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + rightPeriph * peripheralViewDistance);

        if (dynamicCoverPoint.HasValue)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dynamicCoverPoint.Value, 0.5f);
            Gizmos.DrawLine(transform.position, dynamicCoverPoint.Value);
        }

        if (hasHomeRoomBounds)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireCube(homeRoomBounds.center, homeRoomBounds.size);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 1. Зелёный контур: Коллайдер перемещения туловища
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (movementCenterY + movementHeight * 0.5f - movementRadius), movementRadius);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (movementCenterY - movementHeight * 0.5f + movementRadius), movementRadius);

        // 2. Жёлтый контур: Хитбокс туловища (1.0x урон)
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.85f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (bodyHitboxCenterY + bodyHitboxHeight * 0.5f - bodyHitboxRadius), bodyHitboxRadius);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (bodyHitboxCenterY - bodyHitboxHeight * 0.5f + bodyHitboxRadius), bodyHitboxRadius);

        // 3. Красный контур: Хитбокс головы и шляпы (1.5x Хэдшот)
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * headHitboxCenterY, headHitboxRadius);
    }

}