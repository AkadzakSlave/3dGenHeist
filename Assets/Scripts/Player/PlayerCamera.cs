using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("Look Settings")]
    public float mouseSensitivity = 15f;
    public Transform playerBody;
    public InputReader inputReader;

    [Header("Player Movement Link")]
    public PlayerMovement playerMovement;

    [Header("Head Bobbing")]
    public bool enableBobbing = true;
    public float walkBobFrequency = 5.0f;
    public float walkBobAmount = 0.012f;
    public float sprintBobFrequency = 7.5f;
    public float sprintBobAmount = 0.020f;
    public float crouchBobFrequency = 4.0f;
    public float crouchBobAmount = 0.007f;
    public float bobSmoothing = 6.0f;

    [Header("Crouch Eye Height")]
    public float crouchCameraYOffset = -0.45f;
    public float crouchTransitionSpeed = 8.0f;

    [Header("Strafe Tilt (Roll)")]
    public bool enableStrafeTilt = true;
    public float maxTiltAngle = 1.2f;
    public float tiltSmoothing = 8.0f;

    [Header("Landing Impact")]
    public float landingDipMultiplier = 0.008f;
    public float maxLandingDip = 0.05f;
    public float landingRecoverSpeed = 5.0f;

    [Header("Input Protection")]
    public float maxMouseDeltaClamp = 35f;

    private float xRotation = 0f;
    private float currentTilt = 0f;
    private float bobTimer = 0f;
    private Vector3 initialLocalPos;
    private Vector3 targetBobOffset = Vector3.zero;
    private float currentCrouchOffset = 0f;
    private float landingDipOffset = 0f;

    void Start()
    {
        initialLocalPos = transform.localPosition;

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        HandleMouseLook();
        HandleHeadBobbing();
        HandleCrouchHeight();
        HandleStrafeTilt();
        HandleLandingImpact();

        ApplyCameraPosition();
    }

    private void HandleMouseLook()
    {
        Vector2 mouseDelta = Vector2.zero;
        if (inputReader != null)
        {
            mouseDelta = inputReader.LookInput;
        }

        // Защита от системных скачков курсора в Windows (отсекаем спайки больше maxMouseDeltaClamp)
        float clampedX = Mathf.Clamp(mouseDelta.x, -maxMouseDeltaClamp, maxMouseDeltaClamp);
        float clampedY = Mathf.Clamp(mouseDelta.y, -maxMouseDeltaClamp, maxMouseDeltaClamp);

        // Стабильный расчет сенсы (мышь выдает пиксели за кадр, поэтому не множим на скачущий Time.deltaTime)
        // Нормализованный коэффициент масштабирования:
        float sensFactor = (mouseSensitivity > 30f) ? (mouseSensitivity * 0.0008f) : (mouseSensitivity * 0.08f);

        float mouseX = clampedX * sensFactor;
        float mouseY = clampedY * sensFactor;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, currentTilt);
        
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }

    private void HandleHeadBobbing()
    {
        if (!enableBobbing || playerMovement == null)
        {
            targetBobOffset = Vector3.zero;
            return;
        }

        bool isMoving = playerMovement.IsMoving;
        bool isGrounded = playerMovement.IsGrounded;

        if (isMoving && isGrounded)
        {
            float frequency = walkBobFrequency;
            float amount = walkBobAmount;

            if (playerMovement.IsSprinting)
            {
                frequency = sprintBobFrequency;
                amount = sprintBobAmount;
            }
            else if (playerMovement.IsCrouching)
            {
                frequency = crouchBobFrequency;
                amount = crouchBobAmount;
            }

            bobTimer += Time.deltaTime * frequency;

            float bobX = Mathf.Cos(bobTimer * 0.5f) * (amount * 0.5f);
            float bobY = Mathf.Sin(bobTimer) * amount;

            targetBobOffset = new Vector3(bobX, bobY, 0f);
        }
        else
        {
            bobTimer = 0f;
            targetBobOffset = Vector3.MoveTowards(targetBobOffset, Vector3.zero, Time.deltaTime * bobSmoothing);
        }
    }

    private void HandleCrouchHeight()
    {
        float targetOffset = (playerMovement != null && playerMovement.IsCrouching) ? crouchCameraYOffset : 0f;
        currentCrouchOffset = Mathf.Lerp(currentCrouchOffset, targetOffset, Time.deltaTime * crouchTransitionSpeed);
    }

    private void HandleStrafeTilt()
    {
        if (!enableStrafeTilt || playerMovement == null)
        {
            currentTilt = Mathf.MoveTowards(currentTilt, 0f, Time.deltaTime * tiltSmoothing);
            return;
        }

        Vector2 moveInput = playerMovement.MoveInputVector;
        float targetTilt = -moveInput.x * maxTiltAngle;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmoothing);
    }

    private void HandleLandingImpact()
    {
        if (landingDipOffset > 0f)
        {
            landingDipOffset = Mathf.MoveTowards(landingDipOffset, 0f, Time.deltaTime * landingRecoverSpeed);
        }
    }

    public void TriggerLandingImpact(float fallSpeed)
    {
        if (fallSpeed < 1.0f) return;
        landingDipOffset = Mathf.Clamp(fallSpeed * landingDipMultiplier, 0.02f, maxLandingDip);
    }

    private void ApplyCameraPosition()
    {
        Vector3 targetPos = initialLocalPos + new Vector3(0f, currentCrouchOffset - landingDipOffset, 0f) + targetBobOffset;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 14f);
    }
}
