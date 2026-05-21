using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class WeaponItem : EquipableItem
{
    [Header("Combat")]
    public float fireRate = 0.4f;
    public int damage = 25;
    public float range = 60f;
    public LayerMask hitLayers = ~0;

    [Header("Ammo")]
    public bool infiniteAmmo = false;
    public int magazineSize = 6;
    public int ammoInMagazine = 6;
    public int reserveAmmo = 24;
    public float reloadTime = 1.5f;

    [Header("Visuals & Animation")]
    public GameObject weaponMesh;
    public Animator animator;
    public Transform muzzlePoint;
    public string animatorLayerName = "Weapon Layer";

    [Header("Audio (FMOD)")]
    public EventReference fireEvent;

    private float nextFireTime = 0f;
    private float reloadFinishTime = 0f;
    private bool isEquipped = false;
    private bool isReloading = false;

    private void Awake()
    {
        ammoInMagazine = Mathf.Clamp(ammoInMagazine, 0, magazineSize);
    }

    private void Update()
    {
        if (!isEquipped)
        {
            return;
        }

        if (isReloading && Time.time >= reloadFinishTime)
        {
            FinishReload();
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
        }
    }

    public override void Equip()
    {
        isEquipped = true;

        if (weaponMesh != null) weaponMesh.SetActive(true);
        if (animator != null)
        {
            animator.SetBool("IsEquipped", true);
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 1f);
        }

        Debug.Log($"[Weapon] {GetDisplayName()} equipped.");
    }

    public override void Unequip()
    {
        isEquipped = false;

        if (weaponMesh != null) weaponMesh.SetActive(false);
        if (animator != null)
        {
            animator.SetBool("IsEquipped", false);
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 0f);
        }

        Debug.Log($"[Weapon] {GetDisplayName()} hidden.");
    }

    public override void PrimaryAction()
    {
        if (!isEquipped || isReloading || Time.time < nextFireTime)
        {
            return;
        }

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            StartReload();
            return;
        }

        Fire();
        nextFireTime = Time.time + fireRate;
    }

    private void Fire()
    {
        if (!infiniteAmmo)
        {
            ammoInMagazine--;
        }

        if (!fireEvent.IsNull)
        {
            RuntimeManager.PlayOneShot(fireEvent, muzzlePoint != null ? muzzlePoint.position : transform.position);
        }

        if (animator != null)
        {
            animator.SetTrigger("Fire");
        }

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null)
        {
            Debug.LogWarning("[Weapon] No main camera found for hitscan shot.");
            UpdateWeaponUI();
            return;
        }

        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, range, hitLayers, QueryTriggerInteraction.Ignore))
        {
            EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                Debug.Log($"[Weapon] Hit {enemyHealth.name} for {damage}.");
            }
            else
            {
                Debug.Log($"[Weapon] Hit {hit.collider.name}.");
            }
        }
        else
        {
            Debug.Log("[Weapon] Shot missed.");
        }

        UpdateWeaponUI();

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            StartReload();
        }
    }

    private void StartReload()
    {
        if (isReloading || infiniteAmmo || ammoInMagazine >= magazineSize || reserveAmmo <= 0)
        {
            return;
        }

        isReloading = true;
        reloadFinishTime = Time.time + reloadTime;

        if (animator != null)
        {
            animator.SetTrigger("Reload");
        }

        Debug.Log($"[Weapon] Reloading {GetDisplayName()}...");
        UpdateWeaponUI();
    }

    private void FinishReload()
    {
        int missingAmmo = magazineSize - ammoInMagazine;
        int ammoToLoad = Mathf.Min(missingAmmo, reserveAmmo);

        ammoInMagazine += ammoToLoad;
        reserveAmmo -= ammoToLoad;
        isReloading = false;

        Debug.Log($"[Weapon] Reloaded {GetDisplayName()}: {ammoInMagazine}/{reserveAmmo}");
        UpdateWeaponUI();
    }

    private void UpdateWeaponUI()
    {
        if (GameManager.Instance != null && GameManager.Instance.heistUI != null)
        {
            string label = $"{GetDisplayName()}: {ammoInMagazine}/{reserveAmmo}";
            if (isReloading)
            {
                label = $"{GetDisplayName()}: Reloading";
            }

            GameManager.Instance.heistUI.UpdateWeapon(label);
        }
    }

    public void RefreshWeaponUI()
    {
        UpdateWeaponUI();
    }

    private string GetDisplayName()
    {
        return itemData != null ? itemData.itemName : name;
    }
}
