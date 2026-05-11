using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Settings")]
    public Transform playerCamera;
    public float interactDistance = 3.0f;
    public LayerMask interactLayer;

    [Header("UI Message (Optional)")]
    public string interactKey = "E"; // Будет использоваться в логах или UI

    void Update()
    {
        UpdateInteractionUI();

        // Проверка нажатия кнопки E
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void UpdateInteractionUI()
    {
        if (playerCamera == null) return;
        if (GameManager.Instance == null || GameManager.Instance.heistUI == null) return;

        RaycastHit hit;
        string prompt = "";

        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, interactDistance, interactLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                prompt = $"[{interactKey}] {interactable.GetInteractText()}";
            }
        }

        GameManager.Instance.heistUI.SetInteractionText(prompt);
    }

    private void TryInteract()
    {
        if (playerCamera == null) return;

        RaycastHit hit;
        // Пускаем луч из камеры
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, interactDistance, interactLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
            }
        }
    }
}
