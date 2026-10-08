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
    public float interactDistance = 3.0f;
    public LayerMask interactLayer;

    [Header("Item Holding Mode Settings")]
    public ItemHoldingMode holdingMode = ItemHoldingMode.CameraBound;
    public Transform itemHolderRoot; // Контейнер рук/оружия (если есть)
    public bool enableCSStyleSway = true; // Плавный покачивающийся свей в стиле CS
    public float swayAmount = 0.02f;
    public float swaySmoothing = 6f;

    [Header("Input")]
    public InputReader inputReader;
    public string interactKey = "E"; // Будет использоваться в логах или UI

    private Collider lastHitCollider;
    private IInteractable cachedInteractable;
    private Vector3 initialHolderPos = Vector3.zero;

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (itemHolderRoot != null)
        {
            initialHolderPos = itemHolderRoot.localPosition;
        }

        ApplyHoldingMode();
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
        UpdateSway();
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

        // Также переключаем родительский объект для всех экипируемых предметов инвентаря
        if (PlayerInventory.Instance != null && PlayerInventory.Instance.allPossibleItems != null)
        {
            foreach (var item in PlayerInventory.Instance.allPossibleItems)
            {
                if (item == null) continue;
                if (targetParent != null && item.transform.parent != targetParent)
                {
                    item.transform.SetParent(targetParent, true);
                }
            }
        }
    }

    private void UpdateSway()
    {
        if (!enableCSStyleSway || holdingMode != ItemHoldingMode.CameraBound) return;
        if (inputReader == null || itemHolderRoot == null) return;

        Vector2 lookDelta = inputReader.LookInput;
        float moveX = -lookDelta.x * swayAmount;
        float moveY = -lookDelta.y * swayAmount;

        Vector3 targetPos = initialHolderPos + new Vector3(moveX, moveY, 0f);
        itemHolderRoot.localPosition = Vector3.Lerp(itemHolderRoot.localPosition, targetPos, Time.deltaTime * swaySmoothing);
    }

    private void UpdateInteractionUI()
    {
        if (playerCamera == null) return;
        if (GameManager.Instance == null || GameManager.Instance.heistUI == null) return;

        RaycastHit hit;
        string prompt = "";

        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, interactDistance, interactLayer))
        {
            if (hit.collider != lastHitCollider)
            {
                lastHitCollider = hit.collider;
                cachedInteractable = hit.collider.GetComponentInParent<IInteractable>();
            }

            if (cachedInteractable != null)
            {
                prompt = $"[{interactKey}] {cachedInteractable.GetInteractText()}";
            }
        }
        else
        {
            lastHitCollider = null;
            cachedInteractable = null;
        }

        GameManager.Instance.heistUI.SetInteractionText(prompt);
    }

    private void TryInteract()
    {
        if (cachedInteractable != null)
        {
            cachedInteractable.Interact();
        }
    }
}
