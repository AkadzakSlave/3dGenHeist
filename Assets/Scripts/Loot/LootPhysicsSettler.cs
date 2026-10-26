using UnityEngine;
using System.Collections;

/// <summary>
/// Обеспечивает мягкую физическую посадку предмета лута на поверхность (столы, полки, пол).
/// После успокоения предмет замораживается (isKinematic = true) для нулевой нагрузки на процессор.
/// </summary>
public class LootPhysicsSettler : MonoBehaviour
{
    private Rigidbody rb;
    private float timer = 0f;
    private const float MaxSettleDuration = 0.8f;
    private const float MinSettleDuration = 0.15f;

    public void BeginSettle()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.mass = 1.0f;
        rb.linearDamping = 3.0f;
        rb.angularDamping = 6.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.isKinematic = false;

        StartCoroutine(SettleRoutine());
    }

    private IEnumerator SettleRoutine()
    {
        yield return new WaitForFixedUpdate();

        while (timer < MaxSettleDuration)
        {
            timer += Time.fixedDeltaTime;

            if (timer >= MinSettleDuration && rb != null)
            {
                if (rb.linearVelocity.sqrMagnitude < 0.005f)
                {
                    break;
                }
            }
            yield return new WaitForFixedUpdate();
        }

        FreezePhysics();
    }

    private void FreezePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Destroy(this);
    }
}
