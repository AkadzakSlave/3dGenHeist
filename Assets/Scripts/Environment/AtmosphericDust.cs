using UnityEngine;

/// <summary>
/// Создаёт и контролирует парящие в воздухе частицы пыли (Dust Motes),
/// как в Lethal Company / рендерах нуар-ограбления.
/// Автоматически настраивает ParticleSystem с органической турбулентностью и мягким затуханием.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
[ExecuteAlways]
public class AtmosphericDust : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("Камера или объект, вокруг которого парит пыль (по умолчанию Camera.main)")]
    public Transform followTarget;
    public Vector3 targetOffset = new Vector3(0, 0, 3f);

    [Header("Dust Density & Appearance")]
    public Material dustMaterial;
    [Range(10, 300)]
    public int maxParticles = 180;
    [Range(5f, 80f)]
    public float emissionRate = 35f;
    [Range(0.01f, 0.15f)]
    public float particleSize = 0.035f;
    [Range(0.1f, 1f)]
    public float maxAlpha = 0.45f;

    [Header("Box Zone Dimensions")]
    public Vector3 boxSize = new Vector3(14f, 6f, 14f);

    private ParticleSystem ps;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        SetupDustSystem();
    }

    private void Start()
    {
        if (followTarget == null && Camera.main != null)
        {
            followTarget = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (followTarget == null)
        {
            if (Camera.main != null) followTarget = Camera.main.transform;
            else return;
        }

        // Перемещаем генератор вслед за камерой.
        // Так как Simulation Space = World, уже летающие пылинки остаются на своих местах в мире!
        transform.position = followTarget.position + followTarget.forward * targetOffset.z + Vector3.up * targetOffset.y;
    }

    [ContextMenu("Regenerate Dust Setup")]
    public void SetupDustSystem()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        if (ps == null) return;

        // 1. Основные параметры (Main)
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.7f, particleSize * 1.3f);
        main.startColor = new Color(1f, 1f, 1f, maxAlpha);
        main.gravityModifier = 0.005f; // Едва заметное медленное оседание
        main.playOnAwake = true;
        main.loop = true;

        // 2. Зона спавна (Shape: Box вокруг камеры)
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        // 3. Эмиссия (Emission)
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        // 4. Плавное появление и растворение (Color Over Lifetime - без резких вспышек)
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.95f, 0.95f, 1f), 1f) },
            new GradientAlphaKey[] { 
                new GradientAlphaKey(0f, 0f), 
                new GradientAlphaKey(maxAlpha, 0.2f), 
                new GradientAlphaKey(maxAlpha, 0.75f), 
                new GradientAlphaKey(0f, 1f) 
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 5. Турбулентность воздуха (Noise / Brownian motion)
        var noise = ps.noise;
        noise.enabled = true;
        noise.frequency = 0.2f;
        noise.strength = 0.15f;
        noise.damping = true;

        // 6. Рендерер
        var renderer = GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0.005f;
            renderer.maxParticleSize = 0.08f;
            if (dustMaterial == null)
            {
                var dustShader = Shader.Find("Heist/SoftDustParticle");
                if (dustShader != null)
                {
                    dustMaterial = new Material(dustShader);
                    dustMaterial.hideFlags = HideFlags.DontSave;
                }
            }
            if (dustMaterial != null)
            {
                renderer.sharedMaterial = dustMaterial;
            }
        }

        if (Application.isPlaying && !ps.isPlaying)
        {
            ps.Play();
        }
    }
}
