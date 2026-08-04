using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MolotovProjectile : MonoBehaviour
{
    [Header("Prefabs & Effects")]
    public GameObject fireZonePrefab;
    public GameObject shatterVfxPrefab;
    public EventReference shatterSound;

    private bool hasImpacted = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasImpacted) return;

        // Ignore triggers or player collisions
        if (collision.collider == null || collision.collider.isTrigger) return;
        if (collision.gameObject.GetComponentInParent<PlayerHealth>() != null) return;

        hasImpacted = true;

        ContactPoint contact = collision.contacts.Length > 0 ? collision.contacts[0] : default;
        Vector3 impactPoint = contact.point != Vector3.zero ? contact.point : transform.position;
        Vector3 impactNormal = contact.normal != Vector3.zero ? contact.normal : Vector3.up;

        if (shatterVfxPrefab != null)
        {
            Instantiate(shatterVfxPrefab, impactPoint, Quaternion.LookRotation(impactNormal));
        }

        if (!shatterSound.IsNull)
        {
            EventInstance soundInst = RuntimeManager.CreateInstance(shatterSound);
            soundInst.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
            soundInst.start();
            soundInst.release();
        }

        if (fireZonePrefab != null)
        {
            // Raycast down to locate the floor surface
            Vector3 floorPos = impactPoint;
            Quaternion floorRot = Quaternion.identity;

            if (Physics.Raycast(impactPoint + Vector3.up * 0.2f, Vector3.down, out RaycastHit groundHit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                floorPos = groundHit.point;
                floorRot = Quaternion.FromToRotation(Vector3.up, groundHit.normal);
            }
            else if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit altGroundHit, 5.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                floorPos = altGroundHit.point;
                floorRot = Quaternion.FromToRotation(Vector3.up, altGroundHit.normal);
            }

            Instantiate(fireZonePrefab, floorPos, floorRot);
        }

        Destroy(gameObject);
    }
}
