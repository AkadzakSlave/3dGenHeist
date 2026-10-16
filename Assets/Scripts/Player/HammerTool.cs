using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class HammerTool : EquipableItem
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
    public float equipDelay = 0.1f;
    public float unequipDelay = 0.2f;

    [Header("Animator Layer Settings")]
    [Tooltip("Название слоя в Аниматоре, которым управляет этот предмет (например, Hammer Layer)")]
    public string animatorLayerName = "Hammer Layer";

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
        // Убрали старую логику Q и ЛКМ. Инвентарь теперь рулит!
    }

    public override void Equip()
    {
        // gameObject.SetActive(true); // БОЛЬШЕ НЕ ВЫКЛЮЧАЕМ ВЕСЬ ОБЪЕКТ
        isTransitioning = true;
        
        if (animator != null) 
        {
            animator.SetBool("IsArmed", true);
            int layerIndex = animator.GetLayerIndex(animatorLayerName);
            if (layerIndex != -1) 
            {
                animator.SetLayerWeight(layerIndex, 1f);
            }
        }
        
        if (hammerMesh != null) hammerMesh.SetActive(true); 
        
        StopAllCoroutines();
        if (isActiveAndEnabled) StartCoroutine(EquipRoutine());
        else isTransitioning = false;
    }

    public override void Unequip()
    {
        isTransitioning = true;
        if (animator != null) 
        {
            animator.SetBool("IsArmed", false);
        }
        
        StopAllCoroutines();
        if (isActiveAndEnabled) StartCoroutine(UnequipRoutine());
        else
        {
            if (hammerMesh != null) hammerMesh.SetActive(false); 
            isTransitioning = false;
        }
    }

    public override void PrimaryAction()
    {
        if (!isTransitioning && Time.time >= nextSwingTime)
        {
            Swing();
            nextSwingTime = Time.time + swingCooldown;
        }
    }

    IEnumerator EquipRoutine()
    {
        yield return new WaitForSeconds(equipDelay);
        isTransitioning = false;
        Debug.Log("[Hammer] Молот экипирован.");
    }

    IEnumerator UnequipRoutine()
    {
        yield return new WaitForSeconds(unequipDelay);
        if (hammerMesh != null) hammerMesh.SetActive(false); 
        
        if (animator != null)
        {
            int layerIndex = animator.GetLayerIndex(animatorLayerName);
            if (layerIndex != -1) animator.SetLayerWeight(layerIndex, 0f);
        }

        isTransitioning = false;
        Debug.Log("[Hammer] Молот убран.");
    }

    void Swing()
    {
        Transform cam = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(cam.position, cam.forward, out hit, hitDistance, hitMask);

        if (hasHit)
        {
            if (animator != null) animator.SetTrigger("Hit");

            DestructibleWall wall = hit.collider.GetComponentInParent<DestructibleWall>();
            if (wall != null)
            {
                wall.TakeDamage(damage);
                Debug.Log($"[Hammer] Нанесен урон {damage} по стене!");
            }
        }
        else
        {
            if (animator != null) animator.SetTrigger("Miss");
        }
    }
}
