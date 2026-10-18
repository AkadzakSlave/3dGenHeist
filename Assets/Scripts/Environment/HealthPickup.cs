using UnityEngine;
using FMODUnity;

public class HealthPickup : MonoBehaviour, IInteractable
{
    [Header("Heal Settings")]
    [Tooltip("Количество восстанавливаемого здоровья")]
    public int healAmount = 40;

    [Tooltip("Разрешить автоматический подбор при касании коллайдера игроком")]
    public bool allowTouchPickup = true;

    [Header("Audio (FMOD)")]
    [Tooltip("Звук подбора аптечки")]
    public EventReference pickupSound;

    public void Interact()
    {
        TryHeal(GetTargetPlayerHealth());
    }

    public string GetInteractText()
    {
        PlayerHealth health = GetTargetPlayerHealth();
        if (health != null && health.currentHealth >= health.maxHealth)
        {
            return "Health is full";
        }
        return $"Pick up Medkit (+{healAmount} HP)";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!allowTouchPickup) return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
        {
            health = other.GetComponent<PlayerHealth>();
        }

        if (health != null)
        {
            TryHeal(health);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!allowTouchPickup) return;

        PlayerHealth health = collision.gameObject.GetComponentInParent<PlayerHealth>();
        if (health == null)
        {
            health = collision.gameObject.GetComponent<PlayerHealth>();
        }

        if (health != null)
        {
            TryHeal(health);
        }
    }

    private bool TryHeal(PlayerHealth health)
    {
        if (health == null || health.isDead)
        {
            return false;
        }

        if (health.currentHealth >= health.maxHealth)
        {
            return false;
        }

        if (health.Heal(healAmount))
        {
            PlayPickupSound();
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    private void PlayPickupSound()
    {
        if (!pickupSound.IsNull)
        {
            RuntimeManager.PlayOneShot(pickupSound, transform.position);
        }
        else
        {
            // Качественный звуковой фидбек подбора предмета
            try
            {
                RuntimeManager.PlayOneShot("event:/Environment/Items/Secondary/Secondary_SwitchPickUp", transform.position);
            }
            catch { }
        }
    }

    private PlayerHealth GetTargetPlayerHealth()
    {
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            PlayerHealth health = GameManager.Instance.playerTransform.GetComponent<PlayerHealth>();
            if (health != null) return health;
        }

        if (GameManager.Instance != null && GameManager.Instance.activePlayers.Count > 0)
        {
            return GameManager.Instance.activePlayers[0];
        }

        return Object.FindAnyObjectByType<PlayerHealth>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.55f);
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
