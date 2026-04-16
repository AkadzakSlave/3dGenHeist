using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(CharacterController))]
public class FootstepOnMove : MonoBehaviour
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

        // Логика расчета скорости с учетом веса
        float currentMaxSpeed = walkSpeed;
        bool isOverweight = false;

        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.currentWeight > GameManager.Instance.maxWeight)
            {
                isOverweight = true;
                currentMaxSpeed = minWeightSpeed;
            }
        }

        float applySpeed = currentMaxSpeed;
        bool isSprinting = false;

        // Логика Стамины и Спринта
        if (!isOverweight && isSprintPressed && isMoving && currentStamina > 0)
        {
            isSprinting = true;
            applySpeed = sprintSpeed; 
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
                if (currentStamina >= jumpStaminaCost) 
                {
                    currentStamina -= jumpStaminaCost;
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    PlayJumpSound();
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
    }

    private void PlayFootstepSound()
    {
        if (footstepEvent.IsNull) return;
        
        EventInstance footsteps = RuntimeManager.CreateInstance(footstepEvent);
        RuntimeManager.AttachInstanceToGameObject(footsteps, transform, GetComponent<Rigidbody>());
        footsteps.start();
        footsteps.release();
    }

    private void PlayJumpSound()
    {
        if (jumpEvent.IsNull) return;
        
        EventInstance jump = RuntimeManager.CreateInstance(jumpEvent);
        RuntimeManager.AttachInstanceToGameObject(jump, transform, GetComponent<Rigidbody>());
        jump.start();
        jump.release();
    }

    private void PlayLandSound()
    {
        if (landEvent.IsNull) return;
        
        EventInstance land = RuntimeManager.CreateInstance(landEvent);
        RuntimeManager.AttachInstanceToGameObject(land, transform, GetComponent<Rigidbody>());
        land.start();
        land.release();
    }
}