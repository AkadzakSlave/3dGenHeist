using UnityEngine;
using FMODUnity;

public class AmmoPickup : MonoBehaviour, IInteractable
{
    [Header("Ammo Settings")]
    [Tooltip("Процент пополнения боезапаса от максимума для каждого вида оружия (15% = 0.15)")]
    public float ammoPercent = 0.15f;

    [Tooltip("Разрешить автоматический подбор при касании/наступании на коробку")]
    public bool allowTouchPickup = true;

    [Header("Audio (FMOD)")]
    [Tooltip("Звук подбора патронов")]
    public EventReference pickupSound;

    public void Interact()
    {
        TryPickupAmmo(GetTargetPlayerInventory());
    }

    public string GetInteractText()
    {
        PlayerInventory inv = GetTargetPlayerInventory();
        if (inv != null && inv.IsAmmoFull())
        {
            return "Патроны полны";
        }
        int percentDisplay = Mathf.RoundToInt(ammoPercent * 100f);
        return $"Подобрать патроны (+{percentDisplay}%)";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!allowTouchPickup) return;
        PlayerInventory inv = other.GetComponentInParent<PlayerInventory>();
        if (inv == null) inv = other.GetComponent<PlayerInventory>();
        if (inv != null) TryPickupAmmo(inv);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!allowTouchPickup) return;
        PlayerInventory inv = collision.gameObject.GetComponentInParent<PlayerInventory>();
        if (inv == null) inv = collision.gameObject.GetComponent<PlayerInventory>();
        if (inv != null) TryPickupAmmo(inv);
    }

    private bool TryPickupAmmo(PlayerInventory inv)
    {
        if (inv == null)
        {
            return false;
        }

        if (inv.AddAmmoBox(ammoPercent))
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
            try
            {
                RuntimeManager.PlayOneShot("event:/Environment/Items/Secondary/Secondary_SwitchPickUp", transform.position);
            }
            catch { }
        }
    }

    private PlayerInventory GetTargetPlayerInventory()
    {
        if (PlayerInventory.Instance != null) return PlayerInventory.Instance;
        return Object.FindAnyObjectByType<PlayerInventory>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.55f);
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
