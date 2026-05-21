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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayerHealth(this);
        }
    }

    private void Start()
    {
        UpdateHealthUI();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayerHealth(this);
        }
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

    private void Die()
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
