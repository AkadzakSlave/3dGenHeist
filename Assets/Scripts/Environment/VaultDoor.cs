using UnityEngine;
using System.Collections;

public class VaultDoor : MonoBehaviour, IInteractable
{
    [Header("Door State")]
    public bool isLocked = true;
    public bool isOpen = false;

    [Header("Crack & Unlock Settings")]
    [Tooltip("Время удержания E для взлома замка (в секундах)")]
    public float unlockHoldDuration = 4.0f;
    private float currentHoldTimer = 0f;

    [Tooltip("Запускать общую тревогу банка при вскрытии хранилища")]
    public bool triggerAlarmOnBreach = true;

    [Header("Animation & Visuals")]
    [Tooltip("Объект петли/двери для поворота")]
    public Transform doorHinge;
    public Vector3 openEulerOffset = new Vector3(0f, -105f, 0f);
    public float openSpeed = 1.5f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip creakSound;
    public AudioClip unlockSound;

    private Quaternion closedRotation;
    private Quaternion targetOpenRotation;

    private void Start()
    {
        if (doorHinge == null)
        {
            doorHinge = transform;
        }

        closedRotation = doorHinge.localRotation;
        targetOpenRotation = closedRotation * Quaternion.Euler(openEulerOffset);
    }

    private void Update()
    {
        if (isOpen && doorHinge != null)
        {
            doorHinge.localRotation = Quaternion.Slerp(doorHinge.localRotation, targetOpenRotation, Time.deltaTime * openSpeed);
        }
    }

    public void Interact()
    {
        if (!isLocked) return;

        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.ShowNotification("Удерживайте [E] 4 сек для взлома хранилища (или используйте динамит)!", new Color(1f, 0.85f, 0.2f, 1f));
        }
    }

    public string GetInteractText()
    {
        if (isOpen) return "Хранилище открыто";
        if (isLocked) return "[Удерживайте E] Вскрыть замок хранилища (4 сек)";
        return "[E] Открыть дверь";
    }

    /// <summary>
    /// Вызывается системой взаимодействия, когда игрок удерживает E, глядя на замок
    /// </summary>
    public void ProcessHoldInteraction(float deltaTime)
    {
        if (!isLocked || isOpen) return;

        currentHoldTimer += deltaTime;
        float progress = Mathf.Clamp01(currentHoldTimer / unlockHoldDuration);

        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.SetInteractionProgress(progress, "Взлом замка хранилища...");
        }

        if (currentHoldTimer >= unlockHoldDuration)
        {
            currentHoldTimer = 0f;
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.HideInteractionProgress();
            }
            UnlockAndOpen();
        }
    }

    public void CancelHoldInteraction()
    {
        if (currentHoldTimer > 0f)
        {
            currentHoldTimer = 0f;
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.HideInteractionProgress();
            }
        }
    }

    /// <summary>
    /// Взрыв динамитом или молотом мгновенно выбивает дверь хранилища
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isOpen) return;

        Debug.Log($"<color=orange>[Vault] Дверь хранилища получила взрывной урон ({damage})!</color>");
        UnlockAndOpen();
    }

    public void UnlockAndOpen()
    {
        if (isOpen) return;

        isLocked = false;
        isOpen = true;

        if (audioSource != null && unlockSound != null)
        {
            audioSource.PlayOneShot(unlockSound);
        }
        else if (audioSource != null && creakSound != null)
        {
            audioSource.PlayOneShot(creakSound);
        }

        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.ShowNotification("ДВЕРЬ ХРАНИЛИЩА ВСКРЫТА!", new Color(1f, 0.85f, 0.1f, 1f));
        }

        // Включение тревоги при вскрытии хранилища
        if (triggerAlarmOnBreach && GameManager.Instance != null)
        {
            GameManager.Instance.StartHeist();
        }

        Debug.Log("<color=yellow>[Vault] Хранилище успешно открыто! Лут внутри доступен.</color>");
    }
}
