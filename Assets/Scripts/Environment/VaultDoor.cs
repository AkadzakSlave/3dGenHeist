using UnityEngine;
using System.Collections;

public class VaultDoor : MonoBehaviour, IInteractable
{
    [Header("Door State")]
    public bool isLocked = true;
    public bool isOpen = false;

    [Header("Safe Cracking Mini-Game Settings")]
    [Tooltip("Количество штифтов/тумблеров для успешного взлома сейфа")]
    public int totalPins = 3;
    public int currentPin = 1;

    [Tooltip("Базовая скорость вращения стрелки (оборотов в секунду)")]
    public float needleSpeed = 0.75f;

    [Tooltip("Ширина зеленой зоны успеха (в долях окружности, 0.15 = 15% круга)")]
    public float sweetSpotSize = 0.20f;

    [Tooltip("Запускать общую тревогу банка при вскрытии хранилища")]
    public bool triggerAlarmOnBreach = true;

    [Header("Animation & Visuals")]
    public float openSpeed = 1.6f;
    private const float targetOpenAngle = -105f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip creakSound;
    public AudioClip unlockSound;

    // Внутреннее состояние мини-игры
    private bool isMiniGameActive = false;
    private float needleAngle = 0f;
    private float sweetSpotStart = 0.35f;
    private float needleDirection = 1f;
    private float flashTimer = 0f;
    private bool isSuccessFlash = false;
    private bool isErrorFlash = false;

    // Позиционирование и петли двери
    private Vector3 hingeWorldPoint;
    private float currentOpenAngle = 0f;

    private static AudioClip proceduralClickClip;
    private static AudioClip proceduralHeavyThudClip;
    private static AudioClip proceduralErrorClip;

    public bool IsLockpicking => isMiniGameActive;

    private void Start()
    {
        CalculateHingePoint();

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

    private void CalculateHingePoint()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            // Рассчитываем край двери в локальных координатах и переводим в мировые
            Vector3 localHinge = b.center + new Vector3(-b.extents.x * 0.95f, 0f, 0f);
            hingeWorldPoint = transform.TransformPoint(localHinge);
        }
        else
        {
            hingeWorldPoint = transform.position;
        }
    }

    private void Update()
    {
        // Плавная анимация распахивания двери вокруг ее петли
        float targetAngle = isOpen ? targetOpenAngle : 0f;
        if (Mathf.Abs(currentOpenAngle - targetAngle) > 0.05f)
        {
            float newAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, Time.deltaTime * 90f * openSpeed);
            float delta = newAngle - currentOpenAngle;
            currentOpenAngle = newAngle;

            transform.RotateAround(hingeWorldPoint, transform.up, delta);
        }

        // Таймер вспышки в UI мини-игры
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
            {
                isSuccessFlash = false;
                isErrorFlash = false;
            }
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            if (!isMiniGameActive)
            {
                StartLockpick();
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
            if (isMiniGameActive)
            {
                return $"Взлом сейфа: Штифт {currentPin}/{totalPins} (Нажмите [E] или ЛКМ в зеленой зоне)";
            }
            return "[E] Вскрыть замок сейфа";
        }
        return "Открыть дверь хранилища";
    }

    public void StartLockpick()
    {
        if (!isLocked || isOpen) return;

        isMiniGameActive = true;
        currentPin = 1;
        needleAngle = 0f;
        needleDirection = 1f;
        RandomizeSweetSpot();

        PlaySound(proceduralClickClip, 1.1f);
        Debug.Log("<color=yellow>[Vault] Мини-игра взлома сейфа запущена!</color>");
    }

    public void CancelLockpick()
    {
        if (!isMiniGameActive) return;

        isMiniGameActive = false;
        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.HideInteractionProgress();
        }
    }

    private void RandomizeSweetSpot()
    {
        // Располагаем зеленую зону в случайном месте циферблата
        sweetSpotStart = Random.Range(0.12f, 0.88f - sweetSpotSize);
    }

    /// <summary>
    /// Вызывается каждый кадр из PlayerInteraction при наведении на дверь хранилища
    /// </summary>
    public void ProcessLockpick(float deltaTime, bool isHoldingE, bool eWasPressed, bool lmbPressed, Vector3 playerPos)
    {
        if (!isLocked || isOpen)
        {
            if (isMiniGameActive) CancelLockpick();
            return;
        }

        // Проверка дистанции: если игрок отошел дальше 3.8м — прерываем взлом
        if (Vector3.Distance(transform.position, playerPos) > 3.8f)
        {
            if (isMiniGameActive) CancelLockpick();
            return;
        }

        if (!isMiniGameActive)
        {
            return;
        }

        // 1. Движение стрелки
        float speed = needleSpeed * (0.85f + (currentPin * 0.2f)); // С каждым штифтом стрелка чуть быстрее
        needleAngle = (needleAngle + speed * needleDirection * deltaTime) % 1.0f;
        if (needleAngle < 0f) needleAngle += 1.0f;

        // 2. Обработка клика игрока ([E] или ЛКМ)
        if (eWasPressed || lmbPressed)
        {
            EvaluateLockpickAttempt();
        }

        // 3. Обновление визуального циферблата в интерфейсе
        if (HeistUI.Instance != null && isMiniGameActive)
        {
            string prompt = $"Взлом сейфа: Штифт {currentPin}/{totalPins} (Нажмите [E] в зеленой зоне)";
            HeistUI.Instance.SetLockpickMode(true, needleAngle, sweetSpotStart, sweetSpotSize, prompt, isSuccessFlash, isErrorFlash);
        }
    }

    /// <summary>
    /// Для обратной совместимости
    /// </summary>
    public void ProcessWheelTurn(float deltaTime, bool isHoldingE, Vector3 playerPos)
    {
        bool ePressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
        bool lmbPressed = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
        ProcessLockpick(deltaTime, isHoldingE, ePressed, lmbPressed, playerPos);
    }

    private void EvaluateLockpickAttempt()
    {
        // Проверяем, попала ли стрелка в зеленую зону
        float spotEnd = sweetSpotStart + sweetSpotSize;
        bool isHit = (needleAngle >= sweetSpotStart && needleAngle <= spotEnd);

        if (isHit)
        {
            // УСПЕШНОЕ ПОПАДАНИЕ В ШТИФТ
            isSuccessFlash = true;
            flashTimer = 0.22f;
            float pitch = 1.0f + (currentPin * 0.3f);
            PlaySound(proceduralClickClip, pitch);

            if (currentPin >= totalPins)
            {
                // Все штифты взломаны — сейф открывается!
                CancelLockpick();
                UnlockAndOpen();
            }
            else
            {
                // Переходим к следующему штифту
                currentPin++;
                RandomizeSweetSpot();
                needleDirection = -needleDirection; // Меняем направление вращения для разнообразия
            }
        }
        else
        {
            // ПРОМАХ
            isErrorFlash = true;
            flashTimer = 0.28f;
            PlaySound(proceduralErrorClip != null ? proceduralErrorClip : proceduralHeavyThudClip, 0.8f);

            // Штраф: если игрок был на 2-м или 3-м штифте, откат назад
            if (currentPin > 1)
            {
                currentPin--;
                RandomizeSweetSpot();
            }
        }
    }

    public void CancelWheelTurn()
    {
        CancelLockpick();
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
            proceduralClickClip = CreateTone(1250f, 0.045f);
        }
        if (proceduralHeavyThudClip == null)
        {
            proceduralHeavyThudClip = CreateTone(140f, 0.35f);
        }
        if (proceduralErrorClip == null)
        {
            proceduralErrorClip = CreateBuzzTone(220f, 0.15f);
        }
    }

    private static AudioClip CreateTone(float frequency, float duration)
    {
        int sampleRate = 22050;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 32f);
            data[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * envelope;
        }

        AudioClip clip = AudioClip.Create("RatchetTone", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip CreateBuzzTone(float frequency, float duration)
    {
        int sampleRate = 22050;
        int samples = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 12f);
            float square = Mathf.Sin(2 * Mathf.PI * frequency * t) > 0 ? 0.35f : -0.35f;
            data[i] = square * envelope;
        }

        AudioClip clip = AudioClip.Create("BuzzTone", samples, 1, sampleRate, false);
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
        CancelLockpick();
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
