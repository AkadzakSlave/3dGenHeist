using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerCamera;
    public float interactDistance = 3.0f;
    public LayerMask interactLayer;

    [Header("Input")]
    public InputReader inputReader;
    public string interactKey = "E"; // Будет использоваться в логах или UI

    private Collider lastHitCollider;
    private IInteractable cachedInteractable;

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
