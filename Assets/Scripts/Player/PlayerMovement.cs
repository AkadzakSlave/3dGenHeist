using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed & Movement")]
    public float walkSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;

    [Header("Crouch Settings")]
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionSpeed = 10f;
    public float crouchFootstepInterval = 0.65f;
    public LayerMask ceilingCheckMask = ~0;

    [Header("Inertia & Physics")]
    public float acceleration = 28f;
    public float deceleration = 65f;
    public float airAcceleration = 10f;
    [Tooltip("Сила сцепления с поверхностью при резких поворотах (устраняет занос на спринте)")]
    public float turnGripSharpness = 3.5f;
    
    [Header("Stamina System")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 18f;
    public float staminaRegenRate = 12f;
    public float jumpStaminaCost = 15f;
    private float currentStamina;

    [Header("Weight Penalties")]
    public float weightSpeedPenalty = 0.05f;
    public float minWeightSpeed = 2.0f;

    [Header("FMOD Audio")]
    public EventReference footstepEvent;
    public EventReference landEvent;
    public EventReference jumpEvent;
    
    [Header("Step Timing")]
    public float walkFootstepInterval = 0.5f;
    public float sprintFootstepInterval = 0.3f;

    [Header("Animation")]
    public Animator animator;

    [Header("UI Reference")]
    public HeistUI uiRef;

    [Header("Camera Reference")]
    public PlayerCamera playerCamera;

    [Header("Input")]
    public InputReader inputReader;

    private CharacterController controller;
    [Header("Debug Status (Read Only)")]
    [SerializeField] private float currentSpeedDisplay;
    [SerializeField] private float currentWeightDisplay;
    [SerializeField] private bool isGroundedDisplay;
    [SerializeField] private bool isCrouchingDisplay;

    private Vector3 velocity;
    private Vector3 currentHorizontalVelocity;
    private float previousVerticalVelocity = 0f;
    private float footstepTimer = 0f;
    private bool wasGrounded = true;
    private bool jumpRequested = false;
    private bool isCrouching = false;
    private bool isMoving = false;
    private bool isSprinting = false;

    // Public properties for camera and interaction feedback
    public bool IsCrouching => isCrouching;
    public bool IsMoving => isMoving;
    public bool IsSprinting => isSprinting;
    public bool IsGrounded => controller != null && controller.isGrounded;
    public float CurrentSpeed => currentHorizontalVelocity.magnitude;
    public Vector3 Velocity => controller != null ? controller.velocity : (currentHorizontalVelocity + Vector3.up * velocity.y);
    public Vector2 MoveInputVector => inputReader != null ? inputReader.MoveInput : Vector2.zero;

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.JumpEvent += OnJump;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.JumpEvent -= OnJump;
        }
    }

    private void OnJump()
    {
        jumpRequested = true;
    }

    public static PlayerMovement Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<PlayerCamera>();
        }

        if (controller != null)
        {
            standingHeight = controller.height;
            // Устанавливаем высоту шага 0.5м для легкого преодоления любых ступеней
            controller.stepOffset = 0.5f;
            controller.slopeLimit = 55f;
        }
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return;

        bool isGrounded = controller.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            // Фиксируем игрока на ступенях при спуске вниз, предотвращая отрыв от лестницы
            velocity.y = -6f;
        }

        float x = 0f;
        float z = 0f;
        bool isSprintPressed = false;
        isMoving = false;

        if (inputReader != null)
        {
            Vector2 moveInput = inputReader.MoveInput;
            x = moveInput.x;
            z = moveInput.y;
            isSprintPressed = inputReader.IsSprintPressed;

            // Логика приседания
            bool isCrouchPressed = inputReader.IsCrouchPressed;
            if (isCrouchPressed)
            {
                isCrouching = true;
            }
            else if (isCrouching)
            {
                // Проверяем, свободен ли потолок для вставания
                Vector3 castOrigin = transform.position + Vector3.up * (crouchHeight * 0.5f);
                float checkDist = standingHeight - (crouchHeight * 0.5f);
                bool ceilingBlocked = Physics.SphereCast(castOrigin, controller.radius * 0.85f, Vector3.up, out RaycastHit hit, checkDist, ceilingCheckMask, QueryTriggerInteraction.Ignore);
                if (!ceilingBlocked)
                {
                    isCrouching = false;
                }
            }
        }

        // Плавная регулировка высоты коллайдера при приседании
        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.magnitude > 0.1f)
        {
            isMoving = true;
            if (move.magnitude > 1f) move.Normalize();
        }

        // Логика расчета скорости с учетом веса (Система из Дизайн 2.0)
        float currentMaxSpeed = walkSpeed;
        float currentMaxSprint = sprintSpeed;
        float weightFactor = 1.0f; // 1.0 = 100% скорости

        if (GameManager.Instance != null)
        {
            float weight = GameManager.Instance.currentWeight;
            
            // Таблица замедления из Дизайн 2.0
            if (weight < 20) weightFactor = 1.0f;
            else if (weight < 25) weightFactor = 0.9f;
            else if (weight < 30) weightFactor = 0.8f;
            else if (weight < 40) weightFactor = 0.6f;
            else if (weight < 50) weightFactor = 0.4f;
            else if (weight < 70) weightFactor = 0.2f;
            else if (weight < 80) weightFactor = 0.1f;
            else weightFactor = 0.0f; // 80+ кг - не может ходить

            currentMaxSpeed = walkSpeed * weightFactor;
            
            // Спринт тоже замедляется, но на 80+ кг остается минимальная скорость (5%)
            float sprintFactor = Mathf.Max(weightFactor, 0.05f);
            currentMaxSprint = sprintSpeed * sprintFactor;
        }

        if (isCrouching)
        {
            currentMaxSpeed = crouchSpeed * weightFactor;
            isSprintPressed = false; // Приседание отменяет спринт
        }

        float applySpeed = currentMaxSpeed;
        isSprinting = false;

        // Логика Стамины и Спринта
        if (isSprintPressed && isMoving && !isCrouching && currentStamina > 0)
        {
            isSprinting = true;
            applySpeed = currentMaxSprint; 
            currentStamina -= staminaDrainRate * Time.deltaTime;
        }
        else
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
            }
        }
        
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        if (uiRef != null)
        {
            uiRef.UpdateStamina(currentStamina, maxStamina);
        }

        // Звуки шагов (FMOD)
        float currentStepInterval = isCrouching ? crouchFootstepInterval : (isSprinting ? sprintFootstepInterval : walkFootstepInterval);
        
        if (isMoving && isGrounded)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                PlayFootstepSound();
                footstepTimer = currentStepInterval;
            }
        }
        else
        {
            footstepTimer = 0f;
        }

        // Логика Прыжка
        if (jumpRequested)
        {
            jumpRequested = false;
            if (isGrounded)
            {
                if (isCrouching)
                {
                    // Проверяем, можно ли встать перед прыжком
                    Vector3 castOrigin = transform.position + Vector3.up * (crouchHeight * 0.5f);
                    float checkDist = standingHeight - (crouchHeight * 0.5f);
                    bool ceilingBlocked = Physics.SphereCast(castOrigin, controller.radius * 0.85f, Vector3.up, out RaycastHit hit, checkDist, ceilingCheckMask, QueryTriggerInteraction.Ignore);
                    if (!ceilingBlocked)
                    {
                        isCrouching = false;
                    }
                }

                if (!isCrouching)
                {
                    float weight = GameManager.Instance != null ? GameManager.Instance.currentWeight : 0;
                    
                    // Прыжок выкл при весе 70+ кг
                    if (weight < 70)
                    {
                        if (currentStamina >= jumpStaminaCost) 
                        {
                            currentStamina -= jumpStaminaCost;
                            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                            PlayJumpSound();
                        }
                    }
                    else
                    {
                        Debug.Log("[Movement] Слишком тяжело для прыжка!");
                    }
                }
            }
        }
        else
        {
            if (isGrounded)
            {
                jumpRequested = false;
            }
        }

        // Плавная инерция разгона и торможения (Smooth Horizontal Inertia)
        Vector3 targetHorizontalVelocity = move * applySpeed;

        // Устранение скольжения/заноса при резких поворотах на бегу и спринте (Turn Grip)
        float turnGripMultiplier = 1f;
        if (isGrounded && isMoving && currentHorizontalVelocity.sqrMagnitude > 0.5f && targetHorizontalVelocity.sqrMagnitude > 0.5f)
        {
            float directionAlignment = Vector3.Dot(currentHorizontalVelocity.normalized, targetHorizontalVelocity.normalized);
            if (directionAlignment < 0.9f)
            {
                // Чем сильнее угол поворота, тем активнее гасится боковой снос
                float turnFactor = Mathf.Clamp01((0.9f - directionAlignment) / 1.9f);
                turnGripMultiplier = Mathf.Lerp(1f, turnGripSharpness, turnFactor);
            }
        }

        float baseAccel = isMoving ? (isSprinting ? acceleration * 1.25f : acceleration) : deceleration;
        float accelRate = isGrounded ? (baseAccel * turnGripMultiplier) : airAcceleration;
        currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetHorizontalVelocity, accelRate * Time.deltaTime);

        // Применяем гравитацию
        velocity.y += gravity * Time.deltaTime;

        // Разделяем горизонтальное и вертикальное перемещение:
        // В Unity CharacterController требует горизонтального вектора без прижимания вниз,
        // чтобы stepOffset мог беспрепятственно поднимать капсулу на ступени лестниц без застреваний и прыжков.
        controller.Move(currentHorizontalVelocity * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);

        // --- ПРИЗЕМЛЕНИЕ ---
        if (isGrounded && !wasGrounded)
        {
            PlayLandSound();
            if (playerCamera != null)
            {
                playerCamera.TriggerLandingImpact(Mathf.Abs(previousVerticalVelocity));
            }
        }

        previousVerticalVelocity = velocity.y;

        // --- Анимации ---
        if (animator != null)
        {
            float animSpeed = isMoving ? (isSprinting ? 2f : (isCrouching ? 0.5f : 1f)) : 0f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("IsGrounded", isGrounded);
        }

        wasGrounded = isGrounded;

        // Обновляем дебаг-поля для Инспектора
        currentSpeedDisplay = currentHorizontalVelocity.magnitude;
        currentWeightDisplay = GameManager.Instance != null ? GameManager.Instance.currentWeight : 0;
        isGroundedDisplay = isGrounded;
        isCrouchingDisplay = isCrouching;
    }

    private void PlayFootstepSound()
    {
        if (footstepEvent.IsNull) return;
        
        EventInstance footsteps = RuntimeManager.CreateInstance(footstepEvent);

        // 1. Проверяем наличие сумки (слот 0)
        float hasBag = 0f;
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.slots != null && PlayerInventory.Instance.slots.Length > 0 && PlayerInventory.Instance.slots[0] != null)
        {
            if (PlayerInventory.Instance.slots[0].itemData != null && PlayerInventory.Instance.slots[0].itemData.fmodMainType == MainEquipmentType.Bag)
            {
                hasBag = 1f;
            }
        }
        footsteps.setParameterByName("HasBag", hasBag);

        // 2. Вес и стадия перегруза для FMOD WeightStage (0: Легкий, 1: Средний, 2: Тяжелый/Перегруз)
        float playerWeight = GameManager.Instance != null ? GameManager.Instance.currentWeight : 0f;
        footsteps.setParameterByName("PlayerWeight", playerWeight);

        float weightStage = 0f;
        if (playerWeight > 35f || (GameManager.Instance != null && GameManager.Instance.currentWeight > GameManager.Instance.maxWeight))
        {
            weightStage = 2f;
        }
        else if (playerWeight > 15f)
        {
            weightStage = 1f;
        }
        footsteps.setParameterByName("WeightStage", weightStage);

        // 3. Параметры спринта и активного предмета в руках
        footsteps.setParameterByName("IsSprinting", isSprinting ? 1f : 0f);
        float hasActiveItem = (PlayerInventory.Instance != null && PlayerInventory.Instance.GetActiveItem() != null) ? 1f : 0f;
        footsteps.setParameterByName("ActiveItem", hasActiveItem);

        RuntimeManager.AttachInstanceToGameObject(footsteps, gameObject);
        footsteps.start();
        footsteps.release();
    }

    private void PlayJumpSound()
    {
        if (jumpEvent.IsNull) return;
        
        EventInstance jump = RuntimeManager.CreateInstance(jumpEvent);
        RuntimeManager.AttachInstanceToGameObject(jump, gameObject);
        jump.start();
        jump.release();
    }

    private void PlayLandSound()
    {
        if (landEvent.IsNull) return;
        
        EventInstance land = RuntimeManager.CreateInstance(landEvent);
        RuntimeManager.AttachInstanceToGameObject(land, gameObject);
        land.start();
        land.release();
    }

    public void ResetVelocity()
    {
        velocity = Vector3.zero;
        currentHorizontalVelocity = Vector3.zero;
        previousVerticalVelocity = 0f;
    }
}