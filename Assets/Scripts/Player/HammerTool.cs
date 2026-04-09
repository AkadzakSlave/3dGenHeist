using UnityEngine;
using UnityEngine.InputSystem;

public class HammerTool : MonoBehaviour
{
    [Tooltip("Player camera for raycasting")]
    public Transform playerCamera;
    
    public float hitDistance = 2.5f;
    public int damage = 1;
    public float swingCooldown = 0.5f;
    
    [Header("Targeting")]
    [Tooltip("Layers to hit. EXCLUDE the player layer!")]
    public LayerMask hitMask = ~0;

    private float nextSwingTime = 0f;

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextSwingTime)
        {
            Swing();
            nextSwingTime = Time.time + swingCooldown;
        }
    }

    void Swing()
    {
        Debug.Log("Удар молотом!");
        
        if (playerCamera == null) return;

        RaycastHit hit;
        // Пускаем луч, игнорируя слои, не отмеченные в hitMask
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, hitDistance, hitMask))
        {
            DestructibleWall wall = hit.collider.GetComponentInParent<DestructibleWall>();
            if (wall != null)
            {
                wall.TakeDamage(damage);
            }
            else
            {
                Debug.Log($"Попали по: {hit.collider.name}");
            }
        }
    }
}
