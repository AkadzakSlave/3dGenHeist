using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class HammerTool : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player camera for raycasting")]
    public Transform playerCamera;
    [Tooltip("The actual 3D model of the hammer in hand")]
    public GameObject hammerMesh;
    public Animator animator;

    [Header("Settings")]
    public float hitDistance = 4.0f;
    public int damage = 1;
    public float swingCooldown = 0.6f;
    
    [Header("Targeting")]
    [Tooltip("Layers to hit. EXCLUDE the player layer!")]
    public LayerMask hitMask = ~0;

    [Header("Animation Timings")]
    public float equipDelay = 0.3f;
    public float unequipDelay = 0.4f;

    private float nextSwingTime = 0f;
    [SerializeField] private bool isArmed = true; 
    private bool isTransitioning = false;

    void Start()
    {
        if (animator != null) animator.SetBool("IsArmed", isArmed);
        if (hammerMesh != null) hammerMesh.SetActive(isArmed);
        
        // Маленький хак: сбрасываем статус через секунду, на случай если что-то заглючило при старте
        Invoke("ResetTransition", 1f);
    }

    void ResetTransition() { isTransitioning = false; }

    void Update()
    {
        // Переключение молота на Q
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (!isTransitioning)
            {
                ToggleHammer();
            }
            else
            {
                Debug.Log("[Hammer] Кажется, анимация еще проигрывается...");
            }
        }

        // Удар на ЛКМ
        if (isArmed && !isTransitioning && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextSwingTime)
        {
            Swing();
            nextSwingTime = Time.time + swingCooldown;
        }
    }

    void ToggleHammer()
    {
        isArmed = !isArmed;
        isTransitioning = true;
        Debug.Log($"[Hammer] Нажато Q. Состояние IsArmed теперь: {isArmed}");

        if (animator != null)
        {
            animator.SetBool("IsArmed", isArmed);
        }

        StopAllCoroutines(); // Прерываем прошлые попытки, если они были
        if (isArmed) StartCoroutine(EquipRoutine());
        else StartCoroutine(UnequipRoutine());
    }

    IEnumerator EquipRoutine()
    {
        yield return new WaitForSeconds(equipDelay);
        if (hammerMesh != null) hammerMesh.SetActive(true);
        isTransitioning = false;
        Debug.Log("[Hammer] Молот экипирован.");
    }

    IEnumerator UnequipRoutine()
    {
        yield return new WaitForSeconds(unequipDelay);
        if (hammerMesh != null) hammerMesh.SetActive(false);
        isTransitioning = false;
        Debug.Log("[Hammer] Молот убран.");
    }

    void Swing()
    {
        if (playerCamera == null) return;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, hitDistance, hitMask);

        if (animator != null)
        {
            if (hasHit)
            {
                animator.SetTrigger("Hit");
                DestructibleWall wall = hit.collider.GetComponentInParent<DestructibleWall>();
                if (wall != null) wall.TakeDamage(damage);
            }
            else
            {
                animator.SetTrigger("Miss");
            }
        }
    }
}
