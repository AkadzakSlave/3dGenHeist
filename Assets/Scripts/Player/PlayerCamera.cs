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

    [Header("Procedural Recoil (Arcade Friendly)")]
    public float recoilRecoverySpeed = 14f;

    public static PlayerCamera Instance { get; private set; }

    private float xRotation = 0f;
    private float currentTilt = 0f;
    private Vector2 currentRecoil = Vector2.zero;
    private float bobTimer = 0f;
    private Vector3 initialLocalPos;
    private Vector3 targetBobOffset = Vector3.zero;
    private float currentCrouchOffset = 0f;
    private float landingDipOffset = 0f;

    private void Awake()
    {
        Instance = this;
    }

    public void AddRecoil(float verticalKick, float horizontalKick)
    {
        currentRecoil.x += verticalKick;
        currentRecoil.y += horizontalKick;
    }

    [Header("Visual Atmosphere (Noir Comic & Dust)")]
    public bool enableNoirComicEffect = true;
    public bool enableDustParticles = true;

    [Header("Flashlight [F]")]
    public bool enableFlashlight = true;
    public Light flashlight;
    public bool isFlashlightOn = false; // По умолчанию выключен в игре

    private void OnValidate()
    {
        if (flashlight == null)
        {
            Transform flChild = transform.Find("PlayerFlashlight");
            if (flChild != null)
            {
                flashlight = flChild.GetComponent<Light>();
            }
        }
    }

    void Start()
    {
        initialLocalPos = transform.localPosition;

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        // Автоматически подключаем мягкий полноэкранный нуар/комикс эффект к камере
        if (enableNoirComicEffect && GetComponent<NoirPostProcessEffect>() == null)
        {
            var noir = gameObject.AddComponent<NoirPostProcessEffect>();
            noir.ApplyPresetSubtleComic();
        }

        // Автоматически подключаем парящие пылинки по всей игре
        if (enableDustParticles && GetComponentInChildren<AtmosphericDust>() == null)
        {
            GameObject dustObj = new GameObject("AtmosphericDust");
            dustObj.transform.SetParent(transform, false);
            var dust = dustObj.AddComponent<AtmosphericDust>();
            dust.followTarget = transform;
        }

        // 1. Ищем фонарик, настроенный на камере в префабе или на сцене
        if (flashlight == null)
        {
            Transform flChild = transform.Find("PlayerFlashlight");
            if (flChild != null)
            {
                flashlight = flChild.GetComponent<Light>();
            }
            else
            {
                flashlight = GetComponentInChildren<Light>();
            }
        }

        // 2. Если фонарика не было в префабе, создаем его с аутентичным теплым винтажным светом 1930-х
        if (enableFlashlight && flashlight == null)
        {
            GameObject flObj = new GameObject("PlayerFlashlight");
            flObj.transform.SetParent(transform, false);
            flObj.transform.localPosition = new Vector3(0f, 0f, 1f);

            flashlight = flObj.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 7f;
            flashlight.spotAngle = 70f;
            flashlight.innerSpotAngle = 35f;
            flashlight.useColorTemperature = true;
            flashlight.colorTemperature = 3500f;
            flashlight.color = new Color(1.0f, 0.902f, 0.655f, 1.0f);
            flashlight.intensity = 10f;
            flashlight.shadows = LightShadows.Soft;
            flashlight.shadowStrength = 0.942f;
            flashlight.cookie = null;
        }

        if (flashlight != null)
        {
            flashlight.enabled = isFlashlightOn;
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

    [Header("3rd Person Debug View (F5)")]
    [Tooltip("Нажми F5 в игре, чтобы переключиться на вид от 3-го лица и увидеть своего персонажа так, как его видят другие игроки!")]
    public bool isThirdPerson = false;
    public Vector3 thirdPersonOffset = new Vector3(0.5f, 0.2f, -2.2f);

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame)
        {
            ToggleThirdPersonView();
        }

        // Переключение фонарика по клавише [F]
        if (enableFlashlight && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            ToggleFlashlight();
        }

        // Переключение надписей статуса над головами охраны по [F8] (по умолчанию ВЫКЛ)
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            EnemyController.ToggleOverheadDebugText();
        }

        // Если никакой UI не открыт — автоматически блокируем курсор без необходимости кликать по экрану
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (!IsAnyUiOpen())
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                return;
            }
        }

        HandleMouseLook();
        HandleHeadBobbing();
        HandleCrouchHeight();
        HandleStrafeTilt();
        HandleLandingImpact();

        ApplyCameraPosition();
    }

    public void ToggleFlashlight()
    {
        if (flashlight != null)
        {
            isFlashlightOn = !flashlight.enabled;
            flashlight.enabled = isFlashlightOn;
            Debug.Log($"<color=yellow>[Flashlight] Фонарик {(isFlashlightOn ? "ВКЛЮЧЕН" : "ВЫКЛЮЧЕН")} [F]</color>");
        }
    }

    public static bool IsAnyUiOpen()
    {
        if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsMainMenuActive) return true;
        if (PauseMenuUI.Instance != null && PauseMenuUI.Instance.IsPaused) return true;
        if (StoreUI.Instance != null && StoreUI.Instance.IsStoreOpen) return true;
        if (DonResultUI.Instance != null && DonResultUI.Instance.isOpen) return true;
        if (DossierSelectionUI.Instance != null && DossierSelectionUI.Instance.IsDossierOpen) return true;
        if (MapSelectionUI.Instance != null && MapSelectionUI.Instance.IsMapOpen) return true;
        return false;
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

        // Плавное самовосстановление отдачи к исходной точке прицеливания (игроку не нужно "душиться" и тянуть мышь)
        currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, Time.deltaTime * recoilRecoverySpeed);

        transform.localRotation = Quaternion.Euler(xRotation - currentRecoil.x, currentRecoil.y, currentTilt);
        
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

    public void ToggleThirdPersonView()
    {
        isThirdPerson = !isThirdPerson;
        PlayerBodyVisibility bodyVis = GetComponentInParent<PlayerBodyVisibility>();
        if (bodyVis != null)
        {
            bodyVis.SetLocalPlayer(!isThirdPerson);
        }
        Debug.Log($"<color=yellow>[Camera] Режим перспективы: {(isThirdPerson ? "3-е лицо (Вид других игроков)" : "1-е лицо")}</color>");
    }

    private void ApplyCameraPosition()
    {
        Vector3 basePos = isThirdPerson ? (initialLocalPos + thirdPersonOffset) : initialLocalPos;
        Vector3 targetPos = basePos + new Vector3(0f, currentCrouchOffset - landingDipOffset, 0f) + targetBobOffset;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 14f);
    }
}
