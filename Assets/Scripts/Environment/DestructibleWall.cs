using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class DestructibleWall : MonoBehaviour
{
    public int maxHits = 3;
    private int currentHits = 0;
    private bool isDestroyed = false;
    
    [Header("FMOD")]
    public EventReference wallHitEvent;
    
    [Header("Cooldown Settings")]
    public float hitCooldown = 2.2f;   // 2.2 секунды между ударами (под анимацию молота)
    private float lastHitTime = -1f;

    public void TakeDamage(int damage)
    {
        // Защита от ударов после разрушения
        if (isDestroyed) return;
        
        // Защита от слишком частых ударов (ждём 2.2 секунды)
        if (Time.time - lastHitTime < hitCooldown)
        {
            Debug.Log($"Удар проигнорирован: прошло всего {Time.time - lastHitTime:F2} секунд");
            return;
        }
        
        lastHitTime = Time.time;
        currentHits++;
        Debug.Log($"Удар #{currentHits} по стене в {Time.time:F2}");
        
        // Создаём и запускаем звук
        EventInstance hitInstance = RuntimeManager.CreateInstance(wallHitEvent);
        RuntimeManager.AttachInstanceToGameObject(hitInstance, transform, GetComponent<Rigidbody>());
        hitInstance.setParameterByName("HitNumber", currentHits);
        hitInstance.start();
        hitInstance.release();
        
        if (currentHits >= maxHits)
        {
            BreakWall();
        }
    }
    
    private void BreakWall()
    {
        isDestroyed = true;
        Destroy(gameObject);
    }
}