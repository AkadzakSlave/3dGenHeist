using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakableLight : MonoBehaviour
{
    public static readonly List<BreakableLight> ActiveLights = new List<BreakableLight>();

    [Header("Components")]
    public Light targetLight;
    public Renderer bulbRenderer;

    [Header("State")]
    public bool isBroken = false;

    [Header("Atmospheric Flicker (Мерцание)")]
    public bool enableFlicker = true;
    public float minFlickerInterval = 12f;
    public float maxFlickerInterval = 34f;

    [Header("Audio & Effects")]
    public float breakNoiseRadius = 14f;

    private float baseIntensity = 2f;
    private Color baseEmissionColor = new Color(2.8f, 2.4f, 1.6f);
    private Coroutine flickerCoroutine;

    private void OnEnable()
    {
        if (!ActiveLights.Contains(this))
        {
            ActiveLights.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveLights.Remove(this);
    }

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        EnsureCollider();

        if (enableFlicker && !isBroken)
        {
            flickerCoroutine = StartCoroutine(FlickerRoutine());
        }
    }

    public void InitializeComponents()
    {
        if (targetLight == null)
        {
            targetLight = GetComponentInChildren<Light>();
            if (targetLight == null) targetLight = GetComponent<Light>();
            if (targetLight == null) targetLight = GetComponentInParent<Light>();
        }

        if (targetLight != null)
        {
            baseIntensity = targetLight.intensity;
        }

        FindBulbRenderer();

        if (bulbRenderer != null && IsValidBulbRenderer(bulbRenderer))
        {
            Material mat = bulbRenderer.sharedMaterial;
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                Color c = mat.GetColor("_EmissionColor");
                if (c.maxColorComponent > 0.05f)
                {
                    baseEmissionColor = c;
                }
            }
        }
    }

    public static bool IsValidBulbRenderer(Renderer r)
    {
        if (r == null) return false;

        string objName = r.gameObject.name.ToLower();

        // 1. СТРОГИЙ ЗАПРЕТ: Стены, полы, потолки, двери, проёмы никогда не являются плафонами!
        if (objName.Contains("wall") || objName.Contains("floor") || objName.Contains("ceiling") ||
            objName.Contains("roof") || objName.Contains("door") || objName.Contains("room") ||
            objName.Contains("corridor") || objName.Contains("pillar") || objName.Contains("column") ||
            objName.Contains("cage") || objName.Contains("ground") || objName.Contains("collider"))
        {
            return false;
        }

        Transform p = r.transform.parent;
        while (p != null)
        {
            string pName = p.name.ToLower();
            if (pName.Contains("room") || pName.Contains("walls"))
            {
                // Если предок - рут комнаты или стен, проверяем размер объекта
                break;
            }
            if (pName.Contains("door") || pName.Contains("gate"))
            {
                return false;
            }
            p = p.parent;
        }

        // 2. Ограничение по размеру: плафон лампы никогда не превышает 1.4м
        Bounds b = r.bounds;
        if (b.size.x > 1.4f || b.size.y > 1.4f || b.size.z > 1.4f || b.size.magnitude > 2.0f)
        {
            return false;
        }

        // 3. Проверка по материалу (M_LampBulb_Emission)
        if (r.sharedMaterials != null)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string mName = m.name.ToLower();
                if (mName.Contains("lampbulb") || mName.Contains("bulb") || mName.Contains("emission"))
                {
                    return true;
                }
            }
        }

        // 4. Проверка по имени объекта или меша (Cube.153, Bulb, Plafon)
        if (objName.Contains("cube.153") || objName.Contains("bulb") || objName.Contains("lampbulb") ||
            objName.Contains("plafon") || objName.Contains("lantern"))
        {
            return true;
        }

        return false;
    }

    public void FindBulbRenderer()
    {
        if (bulbRenderer != null && IsValidBulbRenderer(bulbRenderer)) return;
        bulbRenderer = null;

        // 1. Проверяем сам объект
        Renderer mr = GetComponent<Renderer>();
        if (IsValidBulbRenderer(mr))
        {
            bulbRenderer = mr;
            return;
        }

        // 2. Ищем среди родительских объектов (плафон / Cube.153)
        Transform curr = transform.parent;
        while (curr != null)
        {
            Renderer pRend = curr.GetComponent<Renderer>();
            if (IsValidBulbRenderer(pRend))
            {
                bulbRenderer = pRend;
                return;
            }
            // Не поднимаемся выше 2 уровней, чтобы случайно не уйти в иерархию комнаты
            if (curr.parent != null && curr.parent.parent != null) break;
            curr = curr.parent;
        }

        // 3. Ищем среди дочерних объектов
        Renderer[] childRends = GetComponentsInChildren<Renderer>(true);
        foreach (var cr in childRends)
        {
            if (IsValidBulbRenderer(cr))
            {
                bulbRenderer = cr;
                return;
            }
        }
    }

    private void EnsureCollider()
    {
        // Всегда гарантируем сферический коллайдер радиусом 0.45м на самой лампе,
        // чтобы пуля или взмах кувалдой гарантированно регистрировали попадание
        SphereCollider sc = GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.45f;
            sc.center = Vector3.zero;
            sc.isTrigger = false;
        }

        // Если есть отдельный меш плафона (Cube.153), также убеждаемся в наличии коллайдера на нем
        if (bulbRenderer != null && bulbRenderer.gameObject != gameObject)
        {
            Collider bCol = bulbRenderer.GetComponent<Collider>();
            if (bCol == null)
            {
                SphereCollider bsc = bulbRenderer.gameObject.AddComponent<SphereCollider>();
                bsc.radius = 0.35f;
                bsc.center = Vector3.zero;
                bsc.isTrigger = false;
            }
        }
    }

    private IEnumerator FlickerRoutine()
    {
        // Случайная начальная фаза, чтобы лампы не мигали все одновременно
        yield return new WaitForSeconds(Random.Range(2f, 8f));

        while (!isBroken)
        {
            float waitTime = Random.Range(minFlickerInterval, maxFlickerInterval);
            yield return new WaitForSeconds(waitTime);

            if (isBroken || targetLight == null) yield break;

            // Серия глубоких скачков напряжения (3-6 миганий почти до полной темноты)
            int flickerCount = Random.Range(3, 7);
            for (int i = 0; i < flickerCount; i++)
            {
                if (isBroken || targetLight == null) yield break;

                // 1. Полная темнота: свет тухнет в ноль, эмиссия полностью выключается
                targetLight.intensity = 0f;
                SetEmission(false);

                // Ощутимая пауза темноты (комната явно погружается во тьму)
                yield return new WaitForSeconds(Random.Range(0.06f, 0.16f));

                if (isBroken || targetLight == null) yield break;

                // 2. Короткая вспышка восстановления
                targetLight.intensity = baseIntensity;
                SetEmission(true, 1f);

                yield return new WaitForSeconds(Random.Range(0.04f, 0.10f));
            }

            // В 28% случаев — глубокое затяжное падение напряжения (на 0.6 - 1.0 сек темноты)
            if (Random.value < 0.28f)
            {
                targetLight.intensity = 0f;
                SetEmission(false);
                yield return new WaitForSeconds(Random.Range(0.6f, 1.0f));
            }

            // Возврат к нормальному состоянию
            if (!isBroken && targetLight != null)
            {
                targetLight.intensity = baseIntensity;
                SetEmission(true, 1f);
            }
        }
    }

    public void SetEmission(bool active, float intensityFactor = 1f)
    {
        if (bulbRenderer == null || !IsValidBulbRenderer(bulbRenderer)) return;

        Material[] mats = bulbRenderer.materials;
        if (mats == null) return;

        foreach (Material m in mats)
        {
            if (m == null) continue;
            if (active)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", baseEmissionColor * intensityFactor);
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        try
        {
            DynamicGI.SetEmissive(bulbRenderer, active ? (baseEmissionColor * intensityFactor) : Color.black);
        }
        catch {}
    }

    /// <summary>
    /// Вызывается при попадании пули или ударе кувалдой
    /// </summary>
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;

        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }

        // 1. Полное отключение источника света (выключаем компонент и обнуляем яркость)
        if (targetLight != null)
        {
            targetLight.intensity = 0f;
            targetLight.enabled = false;
        }

        // 2. Полное выключение свечения материала плафона
        SetEmission(false);

        // 3. Звук разбития лампочки (удар стекла)
        PlayBreakSound();

        // 4. Искры и осколки
        SpawnBreakSparks();

        // 5. Оповещение охраны о шуме разбитого стекла
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(transform.position, breakNoiseRadius);
        }

        Debug.Log($"<color=orange>[Light] Лампочка {gameObject.name} РАЗБИТА! PointLight и свечение выключены.</color>");
    }

    private void PlayBreakSound()
    {
        try
        {
            FMODUnity.RuntimeManager.PlayOneShot("event:/Weapons/Bullet_Impact", transform.position);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BreakableLight] Sound warning: {ex.Message}");
        }
    }

    private void SpawnBreakSparks()
    {
        GameObject sparkObj = new GameObject("LightSparks");
        sparkObj.transform.position = transform.position;

        ParticleSystem ps = sparkObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.45f;
        main.startSpeed = Random.Range(1.8f, 3.2f);
        main.startSize = 0.04f;
        main.startColor = new Color(1f, 0.85f, 0.4f, 1f);
        main.gravityModifier = 1.6f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, Random.Range(8, 14)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var renderer = sparkObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        ps.Play();
    }
}
