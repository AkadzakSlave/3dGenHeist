using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public bool useEnemyDataHealth = true;
    public int maxHealth = 100;
    public int currentHealth = 100;
    public bool isDead = false;
    public bool destroyOnDeath = true;
    public float destroyDelay = 0f;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    private EnemyController enemyController;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = GetComponentInParent<EnemyController>();
        }
    }

    private void Start()
    {
        if (useEnemyDataHealth && enemyController != null && enemyController.data != null)
        {
            maxHealth = enemyController.data.maxHealth;
        }

        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        onDamaged?.Invoke();

        Debug.Log($"[EnemyHealth] {name} took {damage}. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        onDeath?.Invoke();

        if (EnemyManager.Instance != null && enemyController != null)
        {
            EnemyManager.Instance.UnregisterEnemy(enemyController);
        }

        if (destroyOnDeath)
        {
            Destroy(enemyController != null ? enemyController.gameObject : gameObject, destroyDelay);
        }
    }
}
