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
    public bool isAutomatic = false;

    [Header("Shotgun / Spread Tuning")]
    public int pelletsPerShot = 1;
    public float spreadAngle = 0f;

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
    public ParticleSystem muzzleFlash;
    public string animatorLayerName = "Weapon Layer";

    [Header("Audio (FMOD)")]
    public EventReference fireEvent;
    public EventReference reloadEvent;
    public EventReference dryFireEvent;
    public EventReference inspectEvent;

    public enum FMODWeaponType
    {
        Revolver = 0,
        Pistol = 1,
        Shotgun_Lever = 2,
        Shotgun_Double = 3,
        SMG = 4,
        Rifle = 5
    }

    [Header("FMOD Weapon Audio & Profile")]
    public FMODWeaponType weaponProfile = FMODWeaponType.Revolver;
    public bool autoDetectProfile = true;

    [Header("Input")]
    public InputReader inputReader;

    private float nextFireTime = 0f;
    private float reloadFinishTime = 0f;
    private bool isEquipped = false;
    private bool isReloading = false;

    private void Awake()
    {
        ammoInMagazine = Mathf.Clamp(ammoInMagazine, 0, magazineSize);
        if (animator == null)
        {
            animator = GetComponentInParent<Animator>();
        }

        if (autoDetectProfile)
        {
            string idName = (itemData != null ? itemData.itemName : name).ToLower();
            if (idName.Contains("colt"))
            {
                weaponProfile = FMODWeaponType.Pistol;
            }
            else if (idName.Contains("win") || idName.Contains("1887"))
            {
                weaponProfile = FMODWeaponType.Shotgun_Lever;
            }
            else if (idName.Contains("steven"))
            {
                weaponProfile = FMODWeaponType.Shotgun_Double;
            }
            else if (idName.Contains("tompson") || idName.Contains("thompson") || isAutomatic)
            {
                weaponProfile = FMODWeaponType.SMG;
            }
            else
            {
                weaponProfile = FMODWeaponType.Revolver;
            }
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.ReloadEvent -= OnReloadAction;
        }
    }

    private void OnReloadAction()
    {
        if (isEquipped && !isReloading)
        {
            StartReload();
        }
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

        if (!isReloading && Keyboard.current != null)
        {
            if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.hKey.wasPressedThisFrame)
            {
                InspectWeapon();
            }
        }
    }

    public override void Equip()
    {
        isEquipped = true;

        if (inputReader != null)
        {
            inputReader.ReloadEvent += OnReloadAction;
        }

        if (weaponMesh != null) weaponMesh.SetActive(true);
        if (animator != null)
        {
            animator.SetBool("IsEquipped", true);
            string layer = GetTargetLayerName();
            int idx = animator.GetLayerIndex(layer);
            if (idx != -1)
            {
                animator.SetLayerWeight(idx, 1f);
            }
            else
            {
                idx = animator.GetLayerIndex("Upper Layer");
                if (idx != -1) animator.SetLayerWeight(idx, 1f);
            }
        }

        Debug.Log($"[Weapon] {GetDisplayName()} equipped.");
    }

    public override void Unequip()
    {
        isEquipped = false;
        isReloading = false; // Cancel reload when unequipping

        if (inputReader != null)
        {
            inputReader.ReloadEvent -= OnReloadAction;
        }

        if (weaponMesh != null) weaponMesh.SetActive(false);
        if (animator != null)
        {
            animator.SetBool("IsEquipped", false);
            string layer = GetTargetLayerName();
            int idx = animator.GetLayerIndex(layer);
            if (idx != -1)
            {
                animator.SetLayerWeight(idx, 0f);
            }
            else
            {
                idx = animator.GetLayerIndex("Upper Layer");
                if (idx != -1) animator.SetLayerWeight(idx, 0f);
            }
        }

        Debug.Log($"[Weapon] {GetDisplayName()} hidden.");
    }

    public string GetTargetLayerName()
    {
        if (!string.IsNullOrEmpty(animatorLayerName) && animatorLayerName != "Weapon Layer")
        {
            return animatorLayerName;
        }

        string idName = itemData != null ? itemData.itemName : name;
        if (!string.IsNullOrEmpty(idName))
        {
            if (idName.IndexOf("Colt", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Colt Layer";
            if (idName.IndexOf("Tompson", System.StringComparison.OrdinalIgnoreCase) >= 0 || idName.IndexOf("Thompson", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Thompson Layer";
            if (idName.IndexOf("Steven", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "Shotgun Layer";
        }

        return animatorLayerName;
    }

    public override void PrimaryAction()
    {
        if (!isEquipped || isReloading || Time.time < nextFireTime)
        {
            return;
        }

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            PlayDryFire();
            if (reserveAmmo > 0)
            {
                StartReload();
            }
            return;
        }

        Fire();
        nextFireTime = Time.time + fireRate;
    }

    private void PlayDryFire()
    {
        Vector3 pos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        try
        {
            FMOD.Studio.EventInstance dryInst = !dryFireEvent.IsNull 
                ? RuntimeManager.CreateInstance(dryFireEvent) 
                : RuntimeManager.CreateInstance("event:/Weapons/Weapon_DryFire");
            dryInst.setParameterByName("WeaponType", (float)weaponProfile);
            dryInst.set3DAttributes(RuntimeUtils.To3DAttributes(pos));
            dryInst.start();
            dryInst.release();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Weapon] PlayDryFire fallback error: {ex.Message}");
        }
        nextFireTime = Time.time + 0.25f;
    }

    public void InspectWeapon()
    {
        if (animator != null)
        {
            animator.SetTrigger("Inspect");
        }

        try
        {
            FMOD.Studio.EventInstance inspectInst = !inspectEvent.IsNull 
                ? RuntimeManager.CreateInstance(inspectEvent) 
                : RuntimeManager.CreateInstance("event:/Weapons/Weapon_Inspect");
            inspectInst.setParameterByName("WeaponType", (float)weaponProfile);
            inspectInst.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
            inspectInst.start();
            inspectInst.release();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Weapon] Inspect fallback error: {ex.Message}");
        }
    }

    private void Fire()
    {
        if (!infiniteAmmo)
        {
            ammoInMagazine--;
        }

        Vector3 soundPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        try
        {
            FMOD.Studio.EventInstance fireInst = !fireEvent.IsNull 
                ? RuntimeManager.CreateInstance(fireEvent) 
                : RuntimeManager.CreateInstance("event:/Weapons/Weapon_Fire");
            fireInst.setParameterByName("WeaponType", (float)weaponProfile);
            fireInst.set3DAttributes(RuntimeUtils.To3DAttributes(soundPos));
            fireInst.start();
            fireInst.release();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Weapon] Fire sound error: {ex.Message}");
        }

        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
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

        Vector3 rayOrigin = cam.position + cam.forward * 0.5f;
        float rayRange = Mathf.Max(0.1f, range - 0.5f);
        int actualPellets = Mathf.Max(1, pelletsPerShot);

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(rayOrigin, 25f);
        }

        for (int i = 0; i < actualPellets; i++)
        {
            Vector3 shootDir = cam.forward;
            if (spreadAngle > 0f)
            {
                float randomX = Random.Range(-spreadAngle, spreadAngle);
                float randomY = Random.Range(-spreadAngle, spreadAngle);
                shootDir = Quaternion.Euler(randomX, randomY, 0f) * cam.forward;
            }

            if (Physics.Raycast(rayOrigin, shootDir, out RaycastHit hit, rayRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                PlayBulletImpact(hit);

                EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                    Debug.Log($"[Weapon] Pellet hit {enemyHealth.name} for {damage}.");
                }
            }
        }

        UpdateWeaponUI();

        if (!infiniteAmmo && ammoInMagazine <= 0)
        {
            StartReload();
        }
    }

    private void PlayBulletImpact(RaycastHit hit)
    {
        try
        {
            FMOD.Studio.EventInstance impactInst = RuntimeManager.CreateInstance("event:/Weapons/Bullet_Impact");
            float surfaceType = 0f; // 0: Concrete, 1: Wood, 2: Metal, 3: Flesh

            // 1. Проверяем врагов
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null)
            {
                surfaceType = 3f; // Flesh
            }
            else
            {
                // 2. Проверяем явный компонент SurfaceIdentifier
                SurfaceIdentifier surf = hit.collider.GetComponentInParent<SurfaceIdentifier>();
                if (surf != null)
                {
                    surfaceType = (float)surf.surfaceType;
                }
                else
                {
                    // 3. Проверяем PhysicMaterial, если назначен на коллайдере
                    string matName = hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.name.ToLower() : "";
                    string cName = hit.collider.name.ToLower();
                    string parentName = hit.collider.transform.parent != null ? hit.collider.transform.parent.name.ToLower() : "";

                    if (matName.Contains("wood") || cName.Contains("wood") || cName.Contains("table") || cName.Contains("door") || parentName.Contains("door") || parentName.Contains("table"))
                    {
                        surfaceType = 1f; // Wood
                    }
                    else if (matName.Contains("metal") || matName.Contains("iron") || cName.Contains("iron") || cName.Contains("safe") || cName.Contains("metal") || cName.Contains("steel") || parentName.Contains("safe") || parentName.Contains("metal"))
                    {
                        surfaceType = 2f; // Metal
                    }
                    else if (matName.Contains("flesh") || cName.Contains("body") || cName.Contains("enemy"))
                    {
                        surfaceType = 3f; // Flesh
                    }
                }
            }

            impactInst.setParameterByName("SurfaceType", surfaceType);
            impactInst.set3DAttributes(RuntimeUtils.To3DAttributes(hit.point));
            impactInst.start();
            impactInst.release();
        }
        catch { }
    }

    private void StartReload()
    {
        if (isReloading || infiniteAmmo || ammoInMagazine >= magazineSize || reserveAmmo <= 0)
        {
            return;
        }

        isReloading = true;
        reloadFinishTime = Time.time + reloadTime;

        try
        {
            FMOD.Studio.EventInstance reloadInst = !reloadEvent.IsNull 
                ? RuntimeManager.CreateInstance(reloadEvent) 
                : RuntimeManager.CreateInstance("event:/Weapons/Weapon_Reload");
            reloadInst.setParameterByName("WeaponType", (float)weaponProfile);
            reloadInst.setParameterByName("ReloadPhase", 0f);
            reloadInst.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
            reloadInst.start();
            reloadInst.release();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Weapon] Reload sound error: {ex.Message}");
        }

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

            Sprite icon = itemData != null ? itemData.icon : null;
            GameManager.Instance.heistUI.UpdateWeapon(label, icon);
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
