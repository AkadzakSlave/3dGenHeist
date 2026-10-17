using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MolotovFireZone : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 10f;
    public float fireRadius = 3f;
    public float damageInterval = 0.5f;
    public int enemyDamagePerSec = 15;
    public int playerDamagePerSec = 5;

    [Header("Effects")]
    public EventReference fireLoopSound;

    [Header("Visual Spreading & Polish")]
    public float spreadDuration = 0.45f;
    public bool enableFireLight = true;
    public Color fireLightColor = new Color(1.0f, 0.42f, 0.08f);

    private EventInstance soundInstance;
    private Light fireLight;
    private Vector3 targetScale;
    private float currentRadius;
    private float baseLightIntensity = 2.2f;

    private void Start()
    {
        targetScale = transform.localScale;
        if (targetScale.sqrMagnitude < 0.1f)
        {
            targetScale = new Vector3(fireRadius * 2f, 0.05f, fireRadius * 2f);
        }

        // Start small for spreading expansion
        transform.localScale = new Vector3(0.2f, targetScale.y, 0.2f);
        currentRadius = 0.3f;

        if (enableFireLight)
        {
            GameObject lightObj = new GameObject("MolotovFireLight");
            lightObj.transform.SetParent(transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = fireLightColor;
            fireLight.range = fireRadius * 2.5f;
            fireLight.intensity = 0f;
            fireLight.shadows = LightShadows.None;
        }

        if (!fireLoopSound.IsNull)
        {
            soundInstance = RuntimeManager.CreateInstance(fireLoopSound);
            soundInstance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            soundInstance.start();
        }

        StartCoroutine(SpreadRoutine());
        StartCoroutine(DamageRoutine());
        StartCoroutine(LifecycleRoutine());
    }

    private void Update()
    {
        if (fireLight != null)
        {
            // Organic fire flicker
            float flicker = Mathf.PerlinNoise(Time.time * 9f, 0.0f) * 0.6f + 0.7f;
            fireLight.intensity = Mathf.Lerp(fireLight.intensity, baseLightIntensity * flicker, Time.deltaTime * 12f);
        }
    }

    private void OnDestroy()
    {
        if (soundInstance.isValid())
        {
            soundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            soundInstance.release();
        }
    }

    private IEnumerator SpreadRoutine()
    {
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;

        while (elapsed < spreadDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spreadDuration);
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f); // Smooth ease out

            transform.localScale = Vector3.Lerp(initialScale, targetScale, easeT);
            currentRadius = Mathf.Lerp(0.3f, fireRadius, easeT);
            baseLightIntensity = Mathf.Lerp(0.5f, 2.2f, easeT);

            yield return null;
        }

        transform.localScale = targetScale;
        currentRadius = fireRadius;
    }

    private IEnumerator LifecycleRoutine()
    {
        float fadeTime = 1.2f;
        float activeTime = Mathf.Max(0.1f, duration - fadeTime);
        yield return new WaitForSeconds(activeTime);

        // Smooth fade out & shrink before destruction
        float elapsed = 0f;
        Vector3 activeScale = transform.localScale;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeTime);

            transform.localScale = Vector3.Lerp(activeScale, Vector3.zero, t);
            baseLightIntensity = Mathf.Lerp(2.2f, 0f, t);

            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator DamageRoutine()
    {
        float timer = 0f;
        while (timer < duration)
        {
            yield return new WaitForSeconds(damageInterval);
            timer += damageInterval;

            Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius);
            foreach (var hit in hits)
            {
                if (hit == null) continue;

                // Damage Enemy
                EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    int enemyTickDmg = Mathf.Max(1, Mathf.RoundToInt(enemyDamagePerSec * damageInterval));
                    enemy.TakeDamage(enemyTickDmg);
                    Debug.Log($"[Molotov] 🔥 Fire burned Enemy '{enemy.name}' for {enemyTickDmg} DMG.");
                }

                // Damage Player (reduced fire DMG)
                PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    int playerTickDmg = Mathf.Max(1, Mathf.RoundToInt(playerDamagePerSec * damageInterval));
                    player.TakeDamage(playerTickDmg);
                    Debug.Log($"[Molotov] 🔥 Fire burned Player '{player.name}' for {playerTickDmg} DMG.");
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, fireRadius);
    }
}
