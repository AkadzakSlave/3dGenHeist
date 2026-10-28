using UnityEngine;
using System.Collections;

public class VaultDoor : MonoBehaviour, IInteractable
{
    [Header("Door State")]
    public bool isLocked = true;
    public bool isOpen = false;

    [Header("Valve Wheel & Unlock Settings")]
    [Tooltip("Время удержания клавиши E для полного проворота вентиля (в секундах)")]
    public float holdTimeToUnlock = 2.5f;

    [Tooltip("Объект вентиля / штурвала сейфа для вращения")]
    public Transform doorWheel;

    [Tooltip("Скорость вращения вентиля при удержании (градусов в секунду)")]
    public float wheelSpinSpeed = 360f;

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

    // Состояние проворота вентиля
    private float currentHoldProgress = 0f;
    private float clickTimer = 0f;
    private static AudioClip proceduralClickClip;
    private static AudioClip proceduralHeavyThudClip;

    public bool IsLockpicking => currentHoldProgress > 0f;

    private void Start()
    {
        if (doorHinge == null)
        {
            doorHinge = transform;
        }

        closedRotation = doorHinge.localRotation;
        targetOpenRotation = closedRotation * Quaternion.Euler(openEulerOffset);

        // Автопоиск штурвала / вентиля среди дочерних объектов
        if (doorWheel == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform || child == doorHinge) continue;
                string lower = child.name.ToLower();
                if (lower.Contains("wheel") || lower.Contains("valve") || lower.Contains("handle") || lower.Contains("cylinder"))
                {
                    doorWheel = child;
                    break;
                }
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.85f;
                audioSource.maxDistance = 15f;
            }
        }

        EnsureAudioClips();
    }

    private void Update()
    {
        if (doorHinge != null)
        {
            Quaternion target = isOpen ? targetOpenRotation : closedRotation;
            doorHinge.localRotation = Quaternion.Slerp(doorHinge.localRotation, target, Time.deltaTime * openSpeed);
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.ShowNotification("Удерживайте [E], чтобы провернуть вентиль сейфа!", new Color(1f, 0.85f, 0.2f, 1f));
            }
            return;
        }

        // Если замок уже вскрыт - открываем/закрываем дверь
        isOpen = !isOpen;
        PlaySound(creakSound != null ? creakSound : proceduralClickClip, 1.0f);
    }

    public string GetInteractText()
    {
        if (isOpen) return "Закрыть дверь хранилища";
        if (isLocked)
        {
            return "Удерживайте [E] - Провернуть запорный вентиль";
        }
        return "Открыть дверь хранилища";
    }

    /// <summary>
    /// Вызывается каждый кадр из PlayerInteraction при наведении на дверь хранилища
    /// </summary>
    public void ProcessWheelTurn(float deltaTime, bool isHoldingE, Vector3 playerPos)
    {
        if (!isLocked || isOpen)
        {
            if (currentHoldProgress > 0f)
            {
                CancelWheelTurn();
            }
            return;
        }

        // Проверка дистанции: если игрок отошел дальше 4.0м - сбрасываем прогресс
        if (Vector3.Distance(transform.position, playerPos) > 4.0f)
        {
            if (currentHoldProgress > 0f)
            {
                CancelWheelTurn();
            }
            return;
        }

        if (isHoldingE)
        {
            // 1. Накопление прогресса
            currentHoldProgress += deltaTime / Mathf.Max(0.5f, holdTimeToUnlock);

            // 2. Вращение вентиля / штурвала
            if (doorWheel != null)
            {
                doorWheel.Rotate(Vector3.forward, wheelSpinSpeed * deltaTime, Space.Self);
            }

            // 3. Звук механических щелчков с нарастающей тональностью
            clickTimer += deltaTime;
            if (clickTimer >= 0.12f)
            {
                clickTimer = 0f;
                float pitch = 0.95f + (currentHoldProgress * 0.65f);
                PlaySound(proceduralClickClip, pitch);
            }

            // 4. Отрисовка кольца прогресса
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.SetInteractionProgress(currentHoldProgress, "Проворот запорного вентиля");
            }

            // 5. Успех: 100% проворота
            if (currentHoldProgress >= 1.0f)
            {
                CancelWheelTurn();
                UnlockAndOpen();
            }
        }
        else
        {
            // Клавиша отпущена - плавный откат назад
            if (currentHoldProgress > 0f)
            {
                currentHoldProgress = Mathf.Max(0f, currentHoldProgress - deltaTime * 2.0f);
                if (currentHoldProgress <= 0.01f)
                {
                    CancelWheelTurn();
                }
                else if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.SetInteractionProgress(currentHoldProgress, "Проворот запорного вентиля");
                }
            }
        }
    }

    /// <summary>
    /// Для обратной совместимости вызовов
    /// </summary>
    public void ProcessLockpick(float deltaTime, bool isHoldingE, bool eWasPressed, bool lmbPressed, Vector3 playerPos)
    {
        ProcessWheelTurn(deltaTime, isHoldingE, playerPos);
    }

    public void StartLockpick()
    {
        // Не требуется для удержания вентиля
    }

    public void CancelLockpick()
    {
        CancelWheelTurn();
    }

    public void CancelWheelTurn()
    {
        currentHoldProgress = 0f;
        clickTimer = 0f;
        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.HideInteractionProgress();
        }
    }

    private void PlaySound(AudioClip clip, float pitch = 1.0f)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip);
        }
    }

    private void EnsureAudioClips()
    {
        if (proceduralClickClip == null)
        {
            proceduralClickClip = CreateClickTone(1100f, 0.05f);
        }
        if (proceduralHeavyThudClip == null)
        {
            proceduralHeavyThudClip = CreateClickTone(160f, 0.35f);
        }
    }

    private static AudioClip CreateClickTone(float frequency, float duration)
    {
        int sampleRate = 22050;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 28f);
            data[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope;
        }

        AudioClip clip = AudioClip.Create("RatchetTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Взрыв динамитом мгновенно выбивает дверь хранилища
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isOpen) return;

        Debug.Log($"<color=orange>[Vault] Дверь хранилища получила взрывной урон ({damage})!</color>");
        UnlockAndOpen();
    }

    public void UnlockAndOpen()
    {
        if (isOpen && !isLocked) return;

        isLocked = false;
        isOpen = true;

        PlaySound(unlockSound != null ? unlockSound : proceduralHeavyThudClip, 1.0f);

        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.ShowNotification("ХРАНИЛИЩЕ ВСКРЫТО!", new Color(1f, 0.85f, 0.1f, 1f));
        }

        // Включение тревоги при вскрытии хранилища
        if (triggerAlarmOnBreach && GameManager.Instance != null)
        {
            GameManager.Instance.StartHeist();
        }

        Debug.Log("<color=yellow>[Vault] Хранилище успешно открыто! Лут внутри доступен.</color>");
    }
}
