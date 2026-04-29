using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class WeaponItem : EquipableItem
{
    [Header("Combat Settings")]
    public float fireRate = 0.2f;
    public int damage = 20;
    public float range = 50f;
    public LayerMask hitLayers;

    [Header("Visuals & Animation")]
    public GameObject weaponMesh;
    public Animator animator;
    public Transform muzzlePoint;
    public string animatorLayerName = "Weapon Layer";

    [Header("Audio (FMOD)")]
    public EventReference fireEvent;

    private float lastFireTime;

    public override void Equip()
    {
        if (weaponMesh != null) weaponMesh.SetActive(true);
        if (animator != null) 
        {
            animator.SetBool("IsEquipped", true);
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 1f);
        }
        Debug.Log($"[Weapon] {itemData.itemName} экипирован.");
    }

    public override void Unequip()
    {
        if (weaponMesh != null) weaponMesh.SetActive(false);
        if (animator != null) 
        {
            animator.SetBool("IsEquipped", false);
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 0f);
        }
        Debug.Log($"[Weapon] {itemData.itemName} убран.");
    }

    public override void PrimaryAction()
    {
        if (Time.time - lastFireTime >= fireRate)
        {
            Fire();
            lastFireTime = Time.time;
        }
    }

    private void Fire()
    {
        // Звук выстрела
        if (!fireEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(fireEvent, transform.position);
        }

        // Анимация выстрела
        if (animator != null) animator.SetTrigger("Fire");

        // Рейкаст урона
        RaycastHit hit;
        Transform cam = Camera.main.transform; // В идеале передавать камеру игрока
        if (Physics.Raycast(cam.position, cam.forward, out hit, range, hitLayers))
        {
            Debug.Log($"[Weapon] Попадание в {hit.collider.name}!");
            // Здесь будет логика урона врагам
        }
    }
}
