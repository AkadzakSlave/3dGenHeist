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
        // Проверка нажатия кнопки E
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (playerCamera == null) return;

        RaycastHit hit;
        // Пускаем луч из камеры
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, interactDistance, interactLayer))
        {
            // Ищем лут
            LootItem loot = hit.collider.GetComponentInParent<LootItem>();
            if (loot != null)
            {
                loot.Collect();
                return;
            }

            // Можно добавить другие взаимодействия (рычаги, двери) в будущем
            Debug.Log($"[Interaction] Посмотрели на {hit.collider.name}, но взаимодействовать не с чем.");
        }
    }
}
