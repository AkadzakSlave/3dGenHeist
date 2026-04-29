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
                Debug.Log($"[Hammer] Слой {animatorLayerName} (индекс {layerIndex}) активирован. Вес: {animator.GetLayerWeight(layerIndex)}");
            }
            else
            {
                Debug.LogWarning($"[Hammer] ОШИБКА: Слой с именем '{animatorLayerName}' не найден в Аниматоре!");
            }
        }
        else
        {
            Debug.LogError("[Hammer] ОШИБКА: Ссылка на Animator не назначена в инспекторе!");
        }
        
        if (hammerMesh != null) hammerMesh.SetActive(true); 
        
        StopAllCoroutines();
        StartCoroutine(EquipRoutine());
    }

    public override void Unequip()
    {
        isTransitioning = true;
        if (animator != null) 
        {
            animator.SetBool("IsArmed", false);
        }
        
        // Модель скрываем после задержки или сразу, если нужно быстро. 
        // Если есть анимация убирания 'remove', лучше скрыть в корутине.
        StopAllCoroutines();
        StartCoroutine(UnequipRoutine());
    }

    public override void PrimaryAction()
    {
        if (!isTransitioning && Time.time >= nextSwingTime)
        {
            Swing();
            nextSwingTime = Time.time + swingCooldown;
        }
    }

    // Старый ToggleHammer удален, так как этим теперь занимается PlayerInventory

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
        
        // Сбрасываем вес слоя в 0, чтобы ходьба из Base Layer снова стала видна полностью
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
        if (playerCamera == null) return;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, hitDistance, hitMask);

        if (animator != null)
        {
            if (hasHit)
            {
                animator.SetTrigger("Hit");
                Debug.Log("[Hammer] Триггер Hit отправлен.");
                DestructibleWall wall = hit.collider.GetComponentInParent<DestructibleWall>();
                if (wall != null) wall.TakeDamage(damage);
            }
            else
            {
                animator.SetTrigger("Miss");
                Debug.Log("[Hammer] Триггер Miss отправлен.");
            }
        }
    }
}
