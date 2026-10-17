using UnityEngine;
using UnityEngine.InputSystem;

public enum ItemHoldingMode
{
    BodyBound,   // Режим 1: Предмет привязан к туловищу игрока
    CameraBound  // Режим 2: Предмет перемещается в пространстве вместе с камерой (Как в CS)
}

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerCamera;
    public Transform playerBody;
    public float interactDistance = 5.5f;
    public float sphereRadius = 0.22f; // Объемный луч для легкого подбора предметов
    public LayerMask interactLayer = ~0;

    [Header("Highlighter")]
    public InteractableHighlighter highlighter;

    [Header("Item Holding & Motion Settings")]
    public ItemHoldingMode holdingMode = ItemHoldingMode.CameraBound;
    public Transform itemHolderRoot; // Контейнер предметов в руках (ItemsInHand)
    public PlayerMovement playerMovement;

    [Header("Viewmodel Offset (Точная подстройка в инспекторе)")]
    [Tooltip("Дополнительное смещение предметов от 1-го лица (X: влево/вправо, Y: выше/ниже, Z: вперед/назад)")]
    public Vector3 viewmodelOffset = Vector3.zero;

    [Header("Mouse Sway (CS-Style)")]
    public bool enableCSStyleSway = true; // Плавный покачивающийся свей в стиле CS
    public float swayAmount = 0.025f;
    public float maxSwayClamp = 0.05f;

    [Header("Movement Bobbing (Покачивание при шагах)")]
    public bool enableBobbing = true;
    public float walkBobFrequency = 5.5f;
    public float walkBobAmountX = 0.035f;
    public float walkBobAmountY = 0.022f;
    public float sprintBobFrequency = 7.5f;
    public float sprintBobAmountX = 0.055f;
    public float sprintBobAmountY = 0.035f;
    public float stepRollAngle = 2.0f;
    public float idleBreathFrequency = 2.0f;
    public float idleBreathAmount = 0.003f;
    public float motionSmoothing = 12f;

    [Header("Obstacle Soft Pullback (Отвод у стен/столов)")]
    public bool enableSoftPullback = true;
    public float wallCheckDistance = 0.6f;
    public float maxPullbackAmount = 0.15f;
    public LayerMask obstacleMask = ~0;
    public float pullbackSmoothing = 8f;

    [Header("Input")]
    public InputReader inputReader;
    public string interactKey = "E"; // Будет использоваться в логах или UI

    private Collider lastHitCollider;
    private IInteractable cachedInteractable;
    private float bobTimer = 0f;
    private float currentPullback = 0f;

    // Кэш исходных локальных координат предметов (чтобы ничего не смещалось вверх/вбок)
    private readonly System.Collections.Generic.Dictionary<Transform, Vector3> baseItemPositions = new System.Collections.Generic.Dictionary<Transform, Vector3>();
    private readonly System.Collections.Generic.Dictionary<Transform, Quaternion> baseItemRotations = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
    private Transform lastActiveItemTransform = null;

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        ApplyHoldingMode();

        if (highlighter == null)
        {
            highlighter = GetComponent<InteractableHighlighter>();
            if (highlighter == null)
            {
                highlighter = gameObject.AddComponent<InteractableHighlighter>();
            }
        }

        // Автоматически настраиваем видимость тела (локальный игрок не видит своё тело, но отбрасывает тень)
        PlayerBodyVisibility bodyVis = GetComponent<PlayerBodyVisibility>();
        if (bodyVis == null)
        {
            bodyVis = gameObject.AddComponent<PlayerBodyVisibility>();
        }
        bodyVis.itemsInHandRoot = itemHolderRoot;
        bodyVis.CacheAndApply();
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.InteractEvent += TryInteract;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.InteractEvent -= TryInteract;
        }

        if (highlighter != null)
        {
            highlighter.ClearHighlight();
        }

        if (GameManager.Instance != null && GameManager.Instance.heistUI != null)
        {
            GameManager.Instance.heistUI.SetInteractionText("");
            GameManager.Instance.heistUI.SetReticleFocus(false);
        }
    }

    void Update()
    {
        UpdateInteractionUI();

        // Переключение режимов по горячей клавише V для удобства теста
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.vKey.wasPressedThisFrame)
        {
            ToggleHoldingMode();
        }
    }

    private void LateUpdate()
    {
        UpdateViewmodelMotion();
    }

    public void ToggleHoldingMode()
    {
        holdingMode = holdingMode == ItemHoldingMode.BodyBound ? ItemHoldingMode.CameraBound : ItemHoldingMode.BodyBound;
        ApplyHoldingMode();
        Debug.Log($"<color=cyan>[PlayerInteraction] Режим удержания предметов изменен на: {holdingMode}</color>");
    }

    public void SetHoldingMode(ItemHoldingMode mode)
    {
        holdingMode = mode;
        ApplyHoldingMode();
        Debug.Log($"<color=cyan>[PlayerInteraction] Режим удержания предметов установлен: {holdingMode}</color>");
    }

    public void ApplyHoldingMode()
    {
        Transform targetParent = null;

        if (holdingMode == ItemHoldingMode.CameraBound)
        {
            targetParent = playerCamera;
        }
        else
        {
            targetParent = playerBody != null ? playerBody : transform;
        }

        if (itemHolderRoot != null && targetParent != null)
        {
            itemHolderRoot.SetParent(targetParent, true);
        }

        // Переключаем родительский объект для всех экипируемых предметов инвентаря
        // Сохраняем мировые координаты (true), чтобы каждый предмет остался на своей авторской позиции
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.allPossibleItems != null)
        {
            foreach (var item in PlayerInventory.Instance.allPossibleItems)
            {
                if (item == null) continue;
                if (targetParent != null && item.transform.parent != targetParent)
                {
                    item.transform.SetParent(targetParent, true);
                }

                // Кэшируем исходную позицию и поворот под камерой
                baseItemPositions[item.transform] = item.transform.localPosition;
                baseItemRotations[item.transform] = item.transform.localRotation;
            }
        }
    }

    private void UpdateViewmodelMotion()
    {
        if (holdingMode != ItemHoldingMode.CameraBound) return;

        EquipableItem activeItem = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetActiveItem() : null;
        Transform activeT = activeItem != null ? activeItem.transform : null;

        // Если сменился предмет — восстанавливаем позицию предыдущего
        if (lastActiveItemTransform != null && lastActiveItemTransform != activeT)
        {
            if (baseItemPositions.TryGetValue(lastActiveItemTransform, out Vector3 prevBasePos))
            {
                lastActiveItemTransform.localPosition = prevBasePos;
            }
            if (baseItemRotations.TryGetValue(lastActiveItemTransform, out Quaternion prevBaseRot))
            {
                lastActiveItemTransform.localRotation = prevBaseRot;
            }
        }
        lastActiveItemTransform = activeT;

        if (activeT == null) return;

        if (!baseItemPositions.ContainsKey(activeT))
        {
            baseItemPositions[activeT] = activeT.localPosition;
            baseItemRotations[activeT] = activeT.localRotation;
        }

        Vector3 basePos = baseItemPositions[activeT];
        Quaternion baseRot = baseItemRotations[activeT];

        // 1. Мышиный свей (Sway)
        Vector3 swayOffset = Vector3.zero;
        if (enableCSStyleSway && inputReader != null)
        {
            Vector2 lookDelta = inputReader.LookInput;
            float moveX = Mathf.Clamp(-lookDelta.x * swayAmount, -maxSwayClamp, maxSwayClamp);
            float moveY = Mathf.Clamp(-lookDelta.y * swayAmount, -maxSwayClamp, maxSwayClamp);
            swayOffset = new Vector3(moveX, moveY, 0f);
        }

        // 2. Покачивание при шагах (Bobbing)
        Vector3 bobOffset = Vector3.zero;
        float rollAngle = 0f;
        if (enableBobbing)
        {
            bool isMoving = playerMovement != null && playerMovement.IsMoving && playerMovement.IsGrounded;
            if (isMoving)
            {
                bool isSprinting = playerMovement.IsSprinting;
                float freq = isSprinting ? sprintBobFrequency : walkBobFrequency;
                float ampX = isSprinting ? sprintBobAmountX : walkBobAmountX;
                float ampY = isSprinting ? sprintBobAmountY : walkBobAmountY;

                bobTimer += Time.deltaTime * freq;
                float x = Mathf.Sin(bobTimer) * ampX;
                float y = (Mathf.Cos(bobTimer * 2f) * 0.5f + 0.5f) * ampY;
                bobOffset = new Vector3(x, -y, 0f);
                rollAngle = Mathf.Sin(bobTimer) * (isSprinting ? stepRollAngle * 1.5f : stepRollAngle);
            }
            else
            {
                // Плавное дыхание в покое (Idle breath)
                bobTimer += Time.deltaTime * idleBreathFrequency;
                float x = Mathf.Sin(bobTimer * 0.5f) * idleBreathAmount;
                float y = Mathf.Sin(bobTimer) * idleBreathAmount;
                bobOffset = new Vector3(x, y, 0f);
            }
        }

        // 3. Мягкий отвод предмета при приближении к стене/столу (Soft Pullback)
        Vector3 pullbackOffset = Vector3.zero;
        if (enableSoftPullback && playerCamera != null)
        {
            if (Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit, wallCheckDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(transform))
                {
                    float factor = Mathf.Clamp01(1f - (hit.distance / wallCheckDistance));
                    currentPullback = Mathf.Lerp(currentPullback, factor * maxPullbackAmount, Time.deltaTime * pullbackSmoothing);
                }
                else
                {
                    currentPullback = Mathf.Lerp(currentPullback, 0f, Time.deltaTime * pullbackSmoothing);
                }
            }
            else
            {
                currentPullback = Mathf.Lerp(currentPullback, 0f, Time.deltaTime * pullbackSmoothing);
            }

            pullbackOffset = new Vector3(0f, -currentPullback * 0.35f, -currentPullback);
        }

        Vector3 targetPos = basePos + viewmodelOffset + swayOffset + bobOffset + pullbackOffset;
        Quaternion targetRot = baseRot * Quaternion.Euler(0f, 0f, rollAngle);

        activeT.localPosition = Vector3.Lerp(activeT.localPosition, targetPos, Time.deltaTime * motionSmoothing);
        activeT.localRotation = Quaternion.Slerp(activeT.localRotation, targetRot, Time.deltaTime * motionSmoothing);
    }

    private void UpdateInteractionUI()
    {
        if (playerCamera == null) return;
        if (GameManager.Instance == null || GameManager.Instance.heistUI == null) return;

        cachedInteractable = FindBestInteractable(out lastHitCollider);

        string prompt = "";
        if (cachedInteractable != null)
        {
            prompt = $"[{interactKey}] {cachedInteractable.GetInteractText()}";
        }

        GameManager.Instance.heistUI.SetInteractionText(prompt);

        // Highlight focused interactable (Variant B - subtle outline)
        if (highlighter != null)
        {
            if (cachedInteractable != null && cachedInteractable is Component comp)
            {
                highlighter.SetHighlight(comp.gameObject);
            }
            else
            {
                highlighter.ClearHighlight();
            }
        }
    }

    private IInteractable FindBestInteractable(out Collider bestCollider)
    {
        bestCollider = null;
        if (playerCamera == null) return null;

        Vector3 origin = playerCamera.position;
        Vector3 forward = playerCamera.forward;

        // 1. Прямой точный луч имеет наивысший приоритет
        if (Physics.Raycast(origin, forward, out RaycastHit directHit, interactDistance, interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable direct = directHit.collider.GetComponentInParent<IInteractable>();
            if (direct != null && IsInteractableAvailable(direct))
            {
                bestCollider = directHit.collider;
                return direct;
            }
        }

        // 2. Умный конусный поиск с взвешиванием по центру экрана (Dot Product Scoring)
        // Решает проблему выбора нужного предмета в плотных кучках/кластерах
        Collider[] overlapCols = Physics.OverlapSphere(origin, interactDistance, interactLayer, QueryTriggerInteraction.Ignore);
        if (overlapCols == null || overlapCols.Length == 0) return null;

        var evaluated = new System.Collections.Generic.HashSet<IInteractable>();
        IInteractable bestCandidate = null;
        float bestScore = -1f;
        Collider bestCol = null;

        foreach (var col in overlapCols)
        {
            if (col == null) continue;
            if (col.transform.IsChildOf(transform)) continue;

            IInteractable candidate = col.GetComponentInParent<IInteractable>();
            if (candidate == null || !IsInteractableAvailable(candidate)) continue;
            if (evaluated.Contains(candidate)) continue;
            evaluated.Add(candidate);

            Vector3 targetPoint = col.bounds.center;
            Vector3 toTarget = targetPoint - origin;
            float dist = toTarget.magnitude;
            if (dist > interactDistance || dist < 0.001f) continue;

            Vector3 dir = toTarget / dist;
            float dot = Vector3.Dot(forward, dir);

            // Конус видимости: предмет должен быть перед игроком (~35 градусов от центра)
            if (dot < 0.82f) continue;

            // Проверка прямой видимости (чтобы не подбирать сквозь стены или столы)
            if (Physics.Raycast(origin, dir, out RaycastHit losHit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                if (losHit.collider != col && !losHit.collider.transform.IsChildOf(((Component)candidate).transform))
                {
                    continue;
                }
            }

            // Формула: высокая степень dot^6 даёт подавляющее преимущество тому,
            // на что перекрестие направлено прямо в центр, даже если рядом лежит предмет ближе к ногам
            float angleScore = Mathf.Pow(Mathf.Clamp01(dot), 6f);
            float distScore = 1f / Mathf.Max(0.5f, dist);
            float score = angleScore * distScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
                bestCol = col;
            }
        }

        bestCollider = bestCol;
        return bestCandidate;
    }

    private bool IsInteractableAvailable(IInteractable interactable)
    {
        if (interactable is WorldEquipment we && !we.CanPickup)
        {
            return false;
        }
        return true;
    }

    private void TryInteract()
    {
        if (cachedInteractable != null)
        {
            cachedInteractable.Interact();
        }
    }
}
