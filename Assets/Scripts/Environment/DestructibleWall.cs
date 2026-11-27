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
    [Tooltip("Минимальная задержка между ударами кувалды (синхронизировано со скоростью взмаха молота)")]
    public float hitCooldown = 0.5f;
    private float lastHitTime = -1f;

    public void TakeDamage(int damage)
    {
        // Защита от ударов после разрушения
        if (isDestroyed) return;

        // В мультиплеере клиент перенаправляет запрос на авторитетный сервер
        if (Mirror.NetworkClient.active && !Mirror.NetworkServer.active)
        {
            if (WildBunch.Networking.World.NetworkWorldState.Instance != null)
            {
                WildBunch.Networking.World.NetworkWorldState.Instance.CmdDamageWallAtPosition(transform.position, damage);
            }
            return;
        }

        bool isExplosive = damage >= maxHits;
        
        // Защита от спама ударов кувалды (взрывы динамита проходят без кулдауна)
        if (!isExplosive && Time.time - lastHitTime < hitCooldown)
        {
            return;
        }
        
        lastHitTime = Time.time;
        int damageApplied = Mathf.Max(1, damage);
        currentHits += damageApplied;
        Debug.Log($"[Wall] Удар по стене (+{damageApplied} урона, всего {currentHits}/{maxHits}) в {Time.time:F2}");
        
        // Создаём и запускаем звук
        EventInstance hitInstance = RuntimeManager.CreateInstance(wallHitEvent);
        RuntimeManager.AttachInstanceToGameObject(hitInstance, gameObject);
        hitInstance.setParameterByName("HitNumber", currentHits);
        hitInstance.start();
        hitInstance.release();

        // Оповещаем охрану поблизости об ударах кувалдой по стене
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(transform.position, 20f);
        }
        
        if (currentHits >= maxHits)
        {
            BreakWall();

            if (Mirror.NetworkServer.active && WildBunch.Networking.World.NetworkWorldState.Instance != null)
            {
                WildBunch.Networking.World.NetworkWorldState.Instance.RpcBreakWallAtPosition(transform.position);
            }
        }
    }
    
    public void BreakWall()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Громкий грохот разрушения стены поднимает тревогу у всех охранников сектора
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.NotifyNoise(transform.position, 35f);
        }
        
        // ЗАПУСК ОГРАБЛЕНИЯ: Таймер, Сирены, Полиция
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartHeist();
        }

        Destroy(gameObject);
    }
}
