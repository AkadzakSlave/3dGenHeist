using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class PlacedDynamite : MonoBehaviour
{
    [Header("Settings")]
    public float countdownDuration = 3f;
    public float explosionRadius = 5f;
    public int explosionDamage = 50;

    [Header("Effects")]
    public GameObject explosionEffectPrefab;
    public EventReference explosionSound;

    private DestructibleWall targetWall;
    private bool hasExploded = false;

    public void Initialize(DestructibleWall wall)
    {
        targetWall = wall;
    }

    private void Start()
    {
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        yield return new WaitForSeconds(countdownDuration);
        Explode();
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Visual effects
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        // Sound effects (FMOD)
        if (!explosionSound.IsNull)
        {
            EventInstance explosionInstance = RuntimeManager.CreateInstance(explosionSound);
            explosionInstance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            explosionInstance.start();
            explosionInstance.release();
        }

        // Destroy wall
        if (targetWall != null)
        {
            // Deal damage equivalent to maxHits to destroy it
            targetWall.TakeDamage(targetWall.maxHits);
        }

        // Damage nearby players and enemies
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in colliders)
        {
            if (hit == null) continue;

            // Damage Player
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player == null) player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(explosionDamage);
            }

            // Damage Enemy
            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(explosionDamage);
            }
        }

        Destroy(gameObject);
    }
}
