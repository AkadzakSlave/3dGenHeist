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

    private EventInstance soundInstance;

    private void Start()
    {
        if (!fireLoopSound.IsNull)
        {
            soundInstance = RuntimeManager.CreateInstance(fireLoopSound);
            soundInstance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            soundInstance.start();
        }

        StartCoroutine(DamageRoutine());
        Destroy(gameObject, duration);
    }

    private void OnDestroy()
    {
        if (soundInstance.isValid())
        {
            soundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            soundInstance.release();
        }
    }

    private IEnumerator DamageRoutine()
    {
        float timer = 0f;
        while (timer < duration)
        {
            yield return new WaitForSeconds(damageInterval);
            timer += damageInterval;

            Collider[] hits = Physics.OverlapSphere(transform.position, fireRadius);
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
