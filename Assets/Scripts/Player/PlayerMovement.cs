using UnityEngine;
using UnityEngine.InputSystem;

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
    [Tooltip("Speed penalty per $100 in bag")]
    public float weightSpeedPenalty = 0.05f;
    [Tooltip("Minimum speed limit regardless of weight")]
    public float minWeightSpeed = 2.0f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip jumpSound;
    public AudioClip[] footstepSounds;
    private float footstepTimer = 0f;
    public float walkFootstepInterval = 0.5f;
    public float sprintFootstepInterval = 0.3f;

    [Header("Animation")]
    public Animator animator;

    [Header("UI Reference")]
    public HeistUI uiRef;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;
    }

    void Update()
    {
        bool isGrounded = controller.isGrounded || Physics.Raycast(transform.position, Vector3.down, (controller.height / 2f) + 0.2f);
        
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

        // Звуки шагов
        if (isMoving && isGrounded)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                PlayFootstepSound();
                footstepTimer = isSprinting ? sprintFootstepInterval : walkFootstepInterval;
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
                    
                    if (audioSource != null && jumpSound != null)
                    {
                        audioSource.PlayOneShot(jumpSound);
                    }

                    if (animator != null)
                    {
                        animator.SetTrigger("Jump");
                    }
                }
                else
                {
                    Debug.Log("[Player] Недостаточно стамины для прыжка!");
                }
            }
            else
            {
                Debug.Log("[Player] Прыжок отменен: Игрок не на земле (isGrounded = false).");
            }
        }

        // Применяем гравитацию
        velocity.y += gravity * Time.deltaTime;

        // ЕДИНЫЙ вызов Move (решает проблему мерцания isGrounded)
        Vector3 finalVelocity = (move * applySpeed) + velocity;
        controller.Move(finalVelocity * Time.deltaTime);

        // --- Анимации ---
        if (animator != null)
        {
            // Передаем скорость: 0 - стоит, 1 - идет, 2 - бежит. 
            // 0.1f - это время сглаживания перехода.
            float animSpeed = isMoving ? (isSprinting ? 2f : 1f) : 0f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
            animator.SetBool("IsGrounded", isGrounded);
        }
    }

    private void PlayFootstepSound()
    {
        if (audioSource != null && footstepSounds != null && footstepSounds.Length > 0)
        {
            int index = Random.Range(0, footstepSounds.Length);
            audioSource.PlayOneShot(footstepSounds[index], 0.3f);
        }
    }
}
