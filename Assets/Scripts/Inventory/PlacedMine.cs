using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class PlacedMine : MonoBehaviour
{
    [Header("Settings")]
    public float armingDelay = 1.5f;
    public float triggerRadius = 1.5f;
    public float explosionRadius = 4f;
    public int enemyDamage = 100;
    public int playerDamage = 40;
    public LayerMask triggerLayers = ~0;

    [Header("Effects")]
    public GameObject explosionEffectPrefab;
    public EventReference explosionSound;
    public GameObject armedIndicatorLight;

    private bool isArmed = false;
    private bool hasExploded = false;

    private void Start()
    {
        if (armedIndicatorLight != null) armedIndicatorLight.SetActive(false);
        StartCoroutine(ArmingRoutine());
    }

    private IEnumerator ArmingRoutine()
    {
        yield return new WaitForSeconds(armingDelay);
        isArmed = true;
        if (armedIndicatorLight != null) armedIndicatorLight.SetActive(true);
    }

    private void Update()
    {
        if (!isArmed || hasExploded) return;

        // Check for targets stepping into trigger radius
        Collider[] targets = Physics.OverlapSphere(transform.position, triggerRadius, triggerLayers, QueryTriggerInteraction.Ignore);
        foreach (var col in targets)
        {
            if (col == null) continue;

            EnemyHealth enemy = col.GetComponentInParent<EnemyHealth>();
            PlayerHealth player = col.GetComponentInParent<PlayerHealth>();

            if (enemy != null || player != null)
            {
                Explode();
                break;
            }
        }
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        if (!explosionSound.IsNull)
        {
            EventInstance soundInst = RuntimeManager.CreateInstance(explosionSound);
            soundInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            soundInst.start();
            soundInst.release();
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            // Damage Player (reduced damage)
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(playerDamage);
                Debug.Log($"[Mine] 💥 Mine explosion dealt {playerDamage} DMG to Player '{player.name}'.");
            }

            // Damage Enemy (full damage)
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(enemyDamage);
                Debug.Log($"[Mine] 💥 Mine explosion dealt {enemyDamage} DMG to Enemy '{enemy.name}'.");
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
