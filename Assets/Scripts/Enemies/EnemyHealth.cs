using System.Collections;
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
    private Renderer[] renderers;
    private Color[] originalColors;
    private Coroutine flashCoroutine;

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

        // Cache all child renderers and their original colors
        renderers = GetComponentsInChildren<Renderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.color;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        // Trigger red flash visual feedback
        FlashRed();

        onDamaged?.Invoke();

        Debug.Log($"[EnemyHealth] {name} took {damage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void FlashRed()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        
        if (gameObject.activeInHierarchy)
        {
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        // Set all renderer colors to red
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = Color.red;
            }
        }

        yield return new WaitForSeconds(0.15f);

        // Restore original colors
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = originalColors[i];
            }
        }

        flashCoroutine = null;
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
