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
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    
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

    private CharacterController controller;
    [Header("Debug Status (Read Only)")]
    [SerializeField] private float currentSpeedDisplay;
    [SerializeField] private float currentWeightDisplay;
    [SerializeField] private bool isGroundedDisplay;
    private Vector3 velocity;
    private float footstepTimer = 0f;
    private bool wasGrounded = true;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return;

        bool isGrounded = controller.isGrounded;
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = 0f;
        float z = 0f;
        bool isSprintPressed = false;
        bool isMoving = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.leftShiftKey.isPressed) isSprintPressed = true;
        }

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

        float applySpeed = currentMaxSpeed;
        bool isSprinting = false;

        // Логика Стамины и Спринта
        if (isSprintPressed && isMoving && currentStamina > 0)
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
        float currentStepInterval = isSprinting ? sprintFootstepInterval : walkFootstepInterval;
        
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
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (isGrounded)
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

        // Применяем гравитацию
        velocity.y += gravity * Time.deltaTime;

        // Движение
        Vector3 finalVelocity = (move * applySpeed) + velocity;
        controller.Move(finalVelocity * Time.deltaTime);

        // --- ПРИЗЕМЛЕНИЕ ---
        if (isGrounded && !wasGrounded)
        {
            PlayLandSound();
        }

        // --- Анимации ---
        if (animator != null)
        {
            float animSpeed = isMoving ? (isSprinting ? 2f : 1f) : 0f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("IsGrounded", isGrounded);
        }

        wasGrounded = isGrounded;

        // Обновляем дебаг-поля для Инспектора
        currentSpeedDisplay = applySpeed;
        currentWeightDisplay = GameManager.Instance != null ? GameManager.Instance.currentWeight : 0;
        isGroundedDisplay = isGrounded;
    }

    private void PlayFootstepSound()
    {
        if (footstepEvent.IsNull) return;
        
        EventInstance footsteps = RuntimeManager.CreateInstance(footstepEvent);
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
}