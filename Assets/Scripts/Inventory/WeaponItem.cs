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

    [Header("Ammo & Category")]
    public AmmoType ammoType = AmmoType.Light;
    public bool autoDetectAmmoType = true;
    public bool infiniteAmmo = false;
    public int magazineSize = 6;
    public int ammoInMagazine = 6;
    [SerializeField]
    private int fallbackReserveAmmo = 24;

    public int reserveAmmo
    {
        get => PlayerInventory.Instance != null ? PlayerInventory.Instance.GetAmmo(ammoType) : fallbackReserveAmmo;
        set
        {
            if (PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.SetAmmo(ammoType, value);
            }
            else
            {
                fallbackReserveAmmo = value;
            }
        }
    }
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

    [Header("Recoil & Shooting Feel (Arcade Friendly)")]
    [Tooltip("Вертикальный подброс камеры (самовосстанавливающийся)")]
    public float recoilVertical = 1.0f;
    [Tooltip("Случайный горизонтальный разброс отдачи")]
    public float recoilHorizontal = 0.25f;

    [Header("Muzzle Flash Light")]
    public bool enableMuzzleLight = true;
    public Color muzzleLightColor = new Color(1f, 0.85f, 0.45f, 1f);
    public float muzzleLightRange = 9f;
    public float muzzleLightIntensity = 2.5f;

    [Header("Debug Laser Tracers")]
    [Tooltip("Тестовый белый лазерный луч выстрела (показывает траекторию и точку попадания)")]
    public bool enableLaserTracer = true;
    public float laserDuration = 0.12f;
    public float laserWidth = 0.02f;

    private static Material sharedLaserMaterial;

    [Header("Input")]
    public InputReader inputReader;

    private Light runtimeMuzzleLight;
    private float muzzleLightTimer = 0f;
    private FMOD.Studio.EventInstance activeReloadInstance;

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
                recoilVertical = 1.35f;
                recoilHorizontal = 0.3f;
            }
            else if (idName.Contains("win") || idName.Contains("1887"))
            {
                weaponProfile = FMODWeaponType.Shotgun_Lever;
                recoilVertical = 2.4f;
                recoilHorizontal = 0.45f;
            }
            else if (idName.Contains("steven"))
            {
                weaponProfile = FMODWeaponType.Shotgun_Double;
                recoilVertical = 2.6f;
                recoilHorizontal = 0.5f;
            }
            else if (idName.Contains("tompson") || idName.Contains("thompson") || isAutomatic)
            {
                weaponProfile = FMODWeaponType.SMG;
                recoilVertical = 0.55f;
                recoilHorizontal = 0.20f;
            }
            else
            {
                weaponProfile = FMODWeaponType.Revolver;
                recoilVertical = 1.5f;
                recoilHorizontal = 0.35f;
            }
        }

        if (autoDetectAmmoType)
        {
            string idName = (itemData != null ? itemData.itemName : name).ToLower();
            if (idName.Contains("win") || idName.Contains("1887") || idName.Contains("steven") || idName.Contains("shotgun") || weaponProfile == FMODWeaponType.Shotgun_Lever || weaponProfile == FMODWeaponType.Shotgun_Double)
            {
                ammoType = AmmoType.Shotgun;
            }
            else if (idName.Contains("tompson") || idName.Contains("thompson") || idName.Contains("smg") || idName.Contains("rifle") || isAutomatic || weaponProfile == FMODWeaponType.SMG || weaponProfile == FMODWeaponType.Rifle)
            {
                ammoType = AmmoType.Medium;
            }
            else
            {
                ammoType = AmmoType.Light;
            }
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.ReloadEvent -= OnReloadAction;
        }
        StopReloadSound();
    }

    private void OnDestroy()
    {
        StopReloadSound();
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

        // Таймер вспышки динамического света дула
        if (muzzleLightTimer > 0f)
        {
            muzzleLightTimer -= Time.deltaTime;
            if (muzzleLightTimer <= 0f && runtimeMuzzleLight != null)
            {
                runtimeMuzzleLight.enabled = false;
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
        StopReloadSound();

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

    private void TriggerMuzzleLight()
    {
        if (!enableMuzzleLight || muzzlePoint == null) return;

        if (runtimeMuzzleLight == null)
        {
            GameObject lightObj = new GameObject("MuzzleFlashLight", typeof(Light));
            lightObj.transform.SetParent(muzzlePoint, false);
            lightObj.transform.localPosition = Vector3.zero;

            runtimeMuzzleLight = lightObj.GetComponent<Light>();
            runtimeMuzzleLight.type = LightType.Point;
            runtimeMuzzleLight.color = muzzleLightColor;
            runtimeMuzzleLight.range = muzzleLightRange;
            runtimeMuzzleLight.intensity = muzzleLightIntensity;
            runtimeMuzzleLight.shadows = LightShadows.None;
            runtimeMuzzleLight.enabled = false;
        }

        runtimeMuzzleLight.enabled = true;
        muzzleLightTimer = 0.055f;
    }

    private void Fire()
    {
        if (!infiniteAmmo)
        {
            ammoInMagazine--;
        }

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 soundPos = muzzlePoint != null ? muzzlePoint.position : (cam != null ? cam.position + cam.forward * 0.45f : transform.position);

        // ==================== 1. ЗВУК ВЫСТРЕЛА СТРОГО ИЗ ДУЛА (FMOD) ====================
        try
        {
            FMOD.Studio.EventInstance fireInst;
            if (!fireEvent.IsNull)
            {
                fireInst = RuntimeManager.CreateInstance(fireEvent);
            }
            else
            {
                fireInst = RuntimeManager.CreateInstance("event:/Weapons/Weapon_Fire");
            }

            // В текущей сборке FMOD банка аудио-клипы добавлены для WeaponType = 0 (Revolver).
            // Чтобы каждый вид оружия звучал всегда из дула и обладал уникальным тембром:
            float pitch = 1.0f;
            float paramType = 0f;

            switch (weaponProfile)
            {
                case FMODWeaponType.SMG:
                    pitch = 1.26f; // Томпсон: звонкая, резкая, скорострельная автоматная дробь
                    paramType = 0f;
                    break;
                case FMODWeaponType.Pistol:
                    pitch = 1.08f; // Кольт: хлесткий одиночный пистолетный выстрел
                    paramType = 0f;
                    break;
                case FMODWeaponType.Shotgun_Lever:
                case FMODWeaponType.Shotgun_Double:
                    pitch = 0.82f; // Дробовики: мощный басовитый залп
                    paramType = 0f;
                    break;
                default:
                    pitch = 1.0f;
                    paramType = (float)weaponProfile;
                    break;
            }

            fireInst.setParameterByName("WeaponType", paramType);
            fireInst.setPitch(pitch);

            // Позиционируем звук строго на дуле (muzzlePoint)
            fireInst.set3DAttributes(RuntimeUtils.To3DAttributes(soundPos));
            if (muzzlePoint != null)
            {
                RuntimeManager.AttachInstanceToGameObject(fireInst, muzzlePoint.gameObject);
            }
            else
            {
                RuntimeManager.AttachInstanceToGameObject(fireInst, gameObject);
            }

            fireInst.start();
            fireInst.release();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Weapon] Fire sound error: {ex.Message}");
        }

        // ==================== 2. ВИЗУАЛЬНЫЕ ЭФФЕКТЫ И ПРОЦЕДУРНАЯ ОТДАЧА ====================
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        TriggerMuzzleLight();

        if (animator != null)
        {
            animator.SetTrigger("Fire");
        }

        // Мягкая камерная отдача (самовосстанавливающаяся)
        if (PlayerCamera.Instance != null)
        {
            float randH = Random.Range(-recoilHorizontal, recoilHorizontal);
            PlayerCamera.Instance.AddRecoil(recoilVertical, randH);
        }

        // ==================== 3. РАСЧЕТ ПОПАДАНИЙ (HITSCAN) ====================
        if (cam == null)
        {
            Debug.LogWarning("[Weapon] No main camera found for hitscan shot.");
            UpdateWeaponUI();
            return;
        }

        Vector3 rayOrigin = cam.position + cam.forward * 0.5f;
        float rayRange = Mathf.Max(0.1f, range - 0.5f);
        int actualPellets = Mathf.Max(1, pelletsPerShot);
        Vector3 tracerOrigin = muzzlePoint != null ? muzzlePoint.position : rayOrigin;

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

            Vector3 hitPoint;
            if (Physics.Raycast(rayOrigin, shootDir, out RaycastHit hit, rayRange, hitLayers, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                PlayBulletImpact(hit);

                EnemyDamageHitbox damageHitbox = hit.collider.GetComponent<EnemyDamageHitbox>();
                EnemyHealth enemyHealth = hit.collider.GetComponentInParent<EnemyHealth>();
                if (damageHitbox != null)
                {
                    bool isHead = damageHitbox.IsHeadshot;
                    int dealt = damageHitbox.TakeDamageWithHitbox(damage);
                    if (HeistUI.Instance != null)
                    {
                        HeistUI.Instance.TriggerHitmarker(isHead);
                    }
                    Debug.Log($"[Weapon] Hit {hit.collider.name} ({damageHitbox.bodyPart}) for {dealt} damage (x{damageHitbox.damageMultiplier}).");
                }
                else if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                    if (HeistUI.Instance != null)
                    {
                        HeistUI.Instance.TriggerHitmarker(false);
                    }
                    Debug.Log($"[Weapon] Pellet hit {enemyHealth.name} for {damage}.");
                }
            }
            else
            {
                hitPoint = rayOrigin + shootDir * rayRange;
            }

            SpawnLaserTracer(tracerOrigin, hitPoint);
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

            // 1. Приоритет: Проверка врагов / живых целей -> Flesh (3)
            if (hit.collider.GetComponentInParent<EnemyHealth>() != null || hit.collider.GetComponentInParent<EnemyController>() != null)
            {
                surfaceType = 3f; // Flesh
            }
            else
            {
                // 2. Явный компонент SurfaceIdentifier на объекте или его предках
                SurfaceIdentifier surf = hit.collider.GetComponentInParent<SurfaceIdentifier>();
                if (surf != null)
                {
                    surfaceType = (float)surf.surfaceType;
                }
                else
                {
                    // 3. Проверка материалов: PhysicMaterial коллайдера + Visual Material рендерера
                    string physMatName = hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.name.ToLower() : "";
                    string visualMatName = "";
                    Renderer rend = hit.collider.GetComponent<Renderer>();
                    if (rend != null && rend.sharedMaterial != null)
                    {
                        visualMatName = rend.sharedMaterial.name.ToLower();
                    }

                    // 4. Имена объекта и родителя в иерархии
                    string cName = hit.collider.name.ToLower();
                    string pName = hit.collider.transform.parent != null ? hit.collider.transform.parent.name.ToLower() : "";
                    string combined = $"{physMatName} {visualMatName} {cName} {pName}";

                    if (combined.Contains("flesh") || combined.Contains("meat") || combined.Contains("body") || combined.Contains("skin"))
                    {
                        surfaceType = 3f; // Flesh
                    }
                    else if (combined.Contains("metal") || combined.Contains("iron") || combined.Contains("steel") || combined.Contains("safe") || combined.Contains("chrome") || combined.Contains("chain") || combined.Contains("grate") || combined.Contains("pipe") || combined.Contains("barrel"))
                    {
                        surfaceType = 2f; // Metal
                    }
                    else if (combined.Contains("wood") || combined.Contains("table") || combined.Contains("door") || combined.Contains("board") || combined.Contains("plank") || combined.Contains("crate") || combined.Contains("cabinet") || combined.Contains("closet") || combined.Contains("shelf"))
                    {
                        surfaceType = 1f; // Wood
                    }
                    else
                    {
                        surfaceType = 0f; // Concrete (бетон, стены, пол, кирпич, камень)
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

    private void SpawnLaserTracer(Vector3 start, Vector3 end)
    {
        if (!enableLaserTracer) return;

        GameObject tracerObj = new GameObject("LaserTracer", typeof(LineRenderer), typeof(LaserTracerEffect));
        LineRenderer lr = tracerObj.GetComponent<LineRenderer>();
        lr.startWidth = laserWidth;
        lr.endWidth = laserWidth * 0.75f;

        if (sharedLaserMaterial == null)
        {
            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit == null) unlit = Shader.Find("Universal Render Pipeline/Unlit");
            sharedLaserMaterial = new Material(unlit != null ? unlit : Shader.Find("Unlit/Color"));
            sharedLaserMaterial.color = Color.white;
        }

        lr.material = sharedLaserMaterial;
        lr.startColor = Color.white;
        lr.endColor = new Color(1f, 1f, 1f, 0.85f);
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        LaserTracerEffect effect = tracerObj.GetComponent<LaserTracerEffect>();
        if (effect != null)
        {
            effect.lineRenderer = lr;
            effect.duration = laserDuration;
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

        StopReloadSound();

        try
        {
            activeReloadInstance = !reloadEvent.IsNull 
                ? RuntimeManager.CreateInstance(reloadEvent) 
                : RuntimeManager.CreateInstance("event:/Weapons/Weapon_Reload");

            if (activeReloadInstance.isValid())
            {
                float reloadParamType = (float)weaponProfile;
                float reloadPitch = 1.0f;

                // В FMOD банке для SMG (4) звуковик ещё не выделил отдельную дорожку.
                // Для Томпсона используем сочную магазинную перезарядку (Pistol = 1) с чуть ускоренным темпом
                if (weaponProfile == FMODWeaponType.SMG)
                {
                    reloadParamType = 1.0f;
                    reloadPitch = 1.12f;
                }

                activeReloadInstance.setParameterByName("WeaponType", reloadParamType);
                activeReloadInstance.setParameterByName("ReloadPhase", 0f);
                activeReloadInstance.setPitch(reloadPitch);
                RuntimeManager.AttachInstanceToGameObject(activeReloadInstance, gameObject);
                activeReloadInstance.start();
            }
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
        int ammoToLoad = PlayerInventory.Instance != null 
            ? PlayerInventory.Instance.ConsumeAmmo(ammoType, missingAmmo) 
            : Mathf.Min(missingAmmo, fallbackReserveAmmo);

        if (PlayerInventory.Instance == null)
        {
            fallbackReserveAmmo -= ammoToLoad;
        }

        ammoInMagazine += ammoToLoad;
        isReloading = false;

        if (activeReloadInstance.isValid())
        {
            try
            {
                activeReloadInstance.setParameterByName("ReloadPhase", 1f);
                activeReloadInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                activeReloadInstance.release();
            }
            catch { }
            activeReloadInstance.clearHandle();
        }

        Debug.Log($"[Weapon] Reloaded {GetDisplayName()}: {ammoInMagazine}/{reserveAmmo}");
        UpdateWeaponUI();
    }

    private void StopReloadSound()
    {
        if (activeReloadInstance.isValid())
        {
            try
            {
                activeReloadInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                activeReloadInstance.release();
            }
            catch { }
            activeReloadInstance.clearHandle();
        }
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

/// <summary>
/// Автономный контроллер затухания и удаления тестового лазера.
/// Не зависит от оружия и корутин — гарантированно самоуничтожается,
/// даже если в момент выстрела игрок выбросил или сменил оружие.
/// </summary>
public class LaserTracerEffect : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public float duration = 0.12f;
    private float timer = 0f;

    private void Start()
    {
        // Безусловная подстраховка на уровне движка Unity
        Destroy(gameObject, duration + 0.05f);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float alpha = Mathf.Clamp01(1f - (timer / duration));
        if (lineRenderer != null)
        {
            lineRenderer.startColor = new Color(1f, 1f, 1f, alpha);
            lineRenderer.endColor = new Color(1f, 1f, 1f, alpha * 0.75f);
        }

        if (timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}
