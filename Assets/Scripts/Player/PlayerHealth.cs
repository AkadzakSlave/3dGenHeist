using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;
    public bool isDead = false;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;
    public UnityEvent onRevived;
    public UnityEvent onHealed;

    private CharacterController characterController;
    private PlayerMovement playerMovement;
    private PlayerInteraction playerInteraction;
    private PlayerInventory playerInventory;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerInteraction = GetComponent<PlayerInteraction>();
        playerInventory = GetComponent<PlayerInventory>();
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private void OnEnable()
    {
        RegisterWithGameManager();
    }

    private void Start()
    {
        RegisterWithGameManager();
        UpdateHealthUI();
    }

    private void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayerHealth(this);
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayerHealth(this);
        }
    }

    public bool Heal(int amount)
    {
        if (isDead || currentHealth >= maxHealth || amount <= 0)
        {
            return false;
        }

        int prevHp = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        int healed = currentHealth - prevHp;
        onHealed?.Invoke();
        UpdateHealthUI();

        if (HeistUI.Instance != null && healed > 0)
        {
            HeistUI.Instance.ShowNotification($"+{healed} HP", new Color(0.2f, 1.0f, 0.55f, 1f));
        }

        Debug.Log($"[PlayerHealth] Healed +{healed} HP. Current HP: {currentHealth}/{maxHealth}");
        return true;
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        onDamaged?.Invoke();
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Revive(bool restoreFullHealth = true)
    {
        isDead = false;

        if (restoreFullHealth)
        {
            currentHealth = maxHealth;
        }

        SetPlayerControlEnabled(true);
        onRevived?.Invoke();
        UpdateHealthUI();
    }

    public void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0;
        SetPlayerControlEnabled(false);
        onDeath?.Invoke();
        UpdateHealthUI();

        RegisterWithGameManager();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifyPlayerDied(this);
        }
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (playerMovement != null) playerMovement.enabled = isEnabled;
        if (playerInteraction != null) playerInteraction.enabled = isEnabled;
        if (playerInventory != null) playerInventory.enabled = isEnabled;
        if (characterController != null) characterController.enabled = isEnabled;
    }

    private void UpdateHealthUI()
    {
        if (GameManager.Instance != null && GameManager.Instance.heistUI != null)
        {
            GameManager.Instance.heistUI.UpdateHealth(currentHealth, maxHealth);
        }
    }
}
