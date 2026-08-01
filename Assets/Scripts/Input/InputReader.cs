using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "InputReader", menuName = "Heist/Input Reader")]
public class InputReader : ScriptableObject
{
    // Events
    public event UnityAction<Vector2> MoveEvent;
    public event UnityAction<Vector2> LookEvent;
    public event UnityAction JumpEvent;
    public event UnityAction JumpCanceledEvent;
    public event UnityAction SprintEvent;
    public event UnityAction SprintCanceledEvent;
    public event UnityAction InteractEvent;
    public event UnityAction PrimaryActionEvent;
    public event UnityAction PrimaryActionCanceledEvent;
    public event UnityAction SecondaryActionEvent;
    public event UnityAction SecondaryActionCanceledEvent;
    public event UnityAction ReloadEvent;
    public event UnityAction DropEvent;
    public event UnityAction<int> SwitchSlotEvent;
    public event UnityAction<float> ScrollSlotEvent;
    public event UnityAction ToggleDebugMenuEvent;

    [Header("Input Asset Reference")]
    public InputActionAsset inputActionsAsset;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction interactAction;
    private InputAction attackAction;
    private InputAction secondaryAction;
    private InputAction reloadAction;
    private InputAction dropAction;
    private InputAction digit1Action;
    private InputAction digit2Action;
    private InputAction digit3Action; // prepared placeholder
    private InputAction scrollAction;
    private InputAction debugAction;

    private void OnEnable()
    {
        InitializeActions();
    }

    private void OnDisable()
    {
        DisableAllActions();
    }

    public void InitializeActions()
    {
        if (inputActionsAsset == null)
        {
            Debug.LogWarning("[InputReader] InputActionAsset is null. Creating actions programmatically.");
            // Fallback: Create actions dynamically
            CreateFallbackActions();
            return;
        }

        playerMap = inputActionsAsset.FindActionMap("Player");
        if (playerMap == null)
        {
            Debug.LogWarning("[InputReader] Player action map not found. Creating fallback actions.");
            CreateFallbackActions();
            return;
        }

        // Move
        moveAction = playerMap.FindAction("Move");
        if (moveAction != null)
        {
            moveAction.performed += ctx => MoveEvent?.Invoke(ctx.ReadValue<Vector2>());
            moveAction.canceled += ctx => MoveEvent?.Invoke(Vector2.zero);
        }

        // Look
        lookAction = playerMap.FindAction("Look");
        if (lookAction != null)
        {
            lookAction.performed += ctx => LookEvent?.Invoke(ctx.ReadValue<Vector2>());
            lookAction.canceled += ctx => LookEvent?.Invoke(Vector2.zero);
        }

        // Jump
        jumpAction = playerMap.FindAction("Jump");
        if (jumpAction != null)
        {
            jumpAction.performed += ctx => JumpEvent?.Invoke();
            jumpAction.canceled += ctx => JumpCanceledEvent?.Invoke();
        }

        // Sprint
        sprintAction = playerMap.FindAction("Sprint");
        if (sprintAction != null)
        {
            sprintAction.performed += ctx => SprintEvent?.Invoke();
            sprintAction.canceled += ctx => SprintCanceledEvent?.Invoke();
        }

        // Interact
        interactAction = playerMap.FindAction("Interact");
        if (interactAction != null)
        {
            interactAction.performed += ctx => InteractEvent?.Invoke();
        }

        // Attack (Primary Action)
        attackAction = playerMap.FindAction("Attack");
        if (attackAction == null) attackAction = playerMap.FindAction("PrimaryAction");
        if (attackAction != null)
        {
            attackAction.performed += ctx => PrimaryActionEvent?.Invoke();
            attackAction.canceled += ctx => PrimaryActionCanceledEvent?.Invoke();
        }

        // Secondary Action (RMB)
        secondaryAction = playerMap.FindAction("SecondaryAction");
        if (secondaryAction == null) secondaryAction = playerMap.FindAction("Aim");
        if (secondaryAction == null)
        {
            secondaryAction = new InputAction("SecondaryAction", binding: "<Mouse>/rightButton");
        }
        secondaryAction.performed += ctx => SecondaryActionEvent?.Invoke();
        secondaryAction.canceled += ctx => SecondaryActionCanceledEvent?.Invoke();

        // Reload, Drop, digits and scroll - check asset or bind to hardware
        reloadAction = playerMap.FindAction("Reload");
        if (reloadAction == null)
        {
            reloadAction = new InputAction("Reload", binding: "<Keyboard>/r");
        }
        reloadAction.performed += ctx => ReloadEvent?.Invoke();

        dropAction = playerMap.FindAction("Drop");
        if (dropAction == null)
        {
            dropAction = new InputAction("Drop", binding: "<Keyboard>/g");
        }
        dropAction.performed += ctx => DropEvent?.Invoke();

        digit1Action = playerMap.FindAction("Digit1");
        if (digit1Action == null)
        {
            digit1Action = new InputAction("Digit1", binding: "<Keyboard>/1");
        }
        digit1Action.performed += ctx => SwitchSlotEvent?.Invoke(0);

        digit2Action = playerMap.FindAction("Digit2");
        if (digit2Action == null)
        {
            digit2Action = new InputAction("Digit2", binding: "<Keyboard>/2");
        }
        digit2Action.performed += ctx => SwitchSlotEvent?.Invoke(1);

        digit3Action = playerMap.FindAction("Digit3");
        if (digit3Action == null)
        {
            digit3Action = new InputAction("Digit3", binding: "<Keyboard>/3");
        }
        digit3Action.performed += ctx => SwitchSlotEvent?.Invoke(2);

        scrollAction = playerMap.FindAction("Scroll");
        if (scrollAction == null)
        {
            scrollAction = new InputAction("Scroll", binding: "<Mouse>/scroll/y");
        }
        scrollAction.performed += ctx => ScrollSlotEvent?.Invoke(ctx.ReadValue<float>());

        debugAction = playerMap.FindAction("Debug");
        if (debugAction == null)
        {
            debugAction = new InputAction("Debug", binding: "<Keyboard>/f1");
        }
        debugAction.performed += ctx => ToggleDebugMenuEvent?.Invoke();

        EnableAllActions();
    }

    private void CreateFallbackActions()
    {
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        moveAction.performed += ctx => MoveEvent?.Invoke(ctx.ReadValue<Vector2>());
        moveAction.canceled += ctx => MoveEvent?.Invoke(Vector2.zero);

        lookAction = new InputAction("Look", binding: "<Pointer>/delta");
        lookAction.performed += ctx => LookEvent?.Invoke(ctx.ReadValue<Vector2>());
        lookAction.canceled += ctx => LookEvent?.Invoke(Vector2.zero);

        jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
        jumpAction.performed += ctx => JumpEvent?.Invoke();
        jumpAction.canceled += ctx => JumpCanceledEvent?.Invoke();

        sprintAction = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
        sprintAction.performed += ctx => SprintEvent?.Invoke();
        sprintAction.canceled += ctx => SprintCanceledEvent?.Invoke();

        interactAction = new InputAction("Interact", binding: "<Keyboard>/e");
        interactAction.performed += ctx => InteractEvent?.Invoke();

        attackAction = new InputAction("Attack", binding: "<Mouse>/leftButton");
        attackAction.performed += ctx => PrimaryActionEvent?.Invoke();
        attackAction.canceled += ctx => PrimaryActionCanceledEvent?.Invoke();

        secondaryAction = new InputAction("SecondaryAction", binding: "<Mouse>/rightButton");
        secondaryAction.performed += ctx => SecondaryActionEvent?.Invoke();
        secondaryAction.canceled += ctx => SecondaryActionCanceledEvent?.Invoke();

        reloadAction = new InputAction("Reload", binding: "<Keyboard>/r");
        reloadAction.performed += ctx => ReloadEvent?.Invoke();

        dropAction = new InputAction("Drop", binding: "<Keyboard>/g");
        dropAction.performed += ctx => DropEvent?.Invoke();

        digit1Action = new InputAction("Digit1", binding: "<Keyboard>/1");
        digit1Action.performed += ctx => SwitchSlotEvent?.Invoke(0);

        digit2Action = new InputAction("Digit2", binding: "<Keyboard>/2");
        digit2Action.performed += ctx => SwitchSlotEvent?.Invoke(1);

        digit3Action = new InputAction("Digit3", binding: "<Keyboard>/3");
        digit3Action.performed += ctx => SwitchSlotEvent?.Invoke(2);

        scrollAction = new InputAction("Scroll", binding: "<Mouse>/scroll/y");
        scrollAction.performed += ctx => ScrollSlotEvent?.Invoke(ctx.ReadValue<float>());

        debugAction = new InputAction("Debug", binding: "<Keyboard>/f1");
        debugAction.performed += ctx => ToggleDebugMenuEvent?.Invoke();

        EnableAllActions();
    }

    public void EnableAllActions()
    {
        if (playerMap != null) playerMap.Enable();
        moveAction?.Enable();
        lookAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
        interactAction?.Enable();
        attackAction?.Enable();
        secondaryAction?.Enable();
        reloadAction?.Enable();
        dropAction?.Enable();
        digit1Action?.Enable();
        digit2Action?.Enable();
        digit3Action?.Enable();
        scrollAction?.Enable();
        debugAction?.Enable();
    }

    public void DisableAllActions()
    {
        if (playerMap != null) playerMap.Disable();
        moveAction?.Disable();
        lookAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
        interactAction?.Disable();
        attackAction?.Disable();
        secondaryAction?.Disable();
        reloadAction?.Disable();
        dropAction?.Disable();
        digit1Action?.Disable();
        digit2Action?.Disable();
        digit3Action?.Disable();
        scrollAction?.Disable();
        debugAction?.Disable();
    }

    // Helper properties for direct value polling where necessary
    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    public bool IsSprintPressed => sprintAction != null && sprintAction.IsPressed();
    public bool IsPrimaryActionPressed => attackAction != null && attackAction.IsPressed();
    public bool IsSecondaryActionPressed => secondaryAction != null && secondaryAction.IsPressed();
}
