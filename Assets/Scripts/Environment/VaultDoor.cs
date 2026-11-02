using UnityEngine;
using System.Collections;
using FMODUnity;

[SelectionBase]
public class VaultDoor : MonoBehaviour, IInteractable
{
    public enum WheelAxis
    {
        Auto,
        LocalZ,
        LocalY,
        LocalX
    }

    [Header("Door References")]
    [Tooltip("Объект двери, который будет распахиваться (если не указан, берется этот объект или дочерний SafeDoor)")]
    public Transform doorTransform;

    [Tooltip("Трансформ ручки/вентиля сейфа для анимации вращения при взломе (Cylinder.011)")]
    public Transform wheelTransform;

    [Header("Door State")]
    public bool isLocked = true;
    public bool isOpen = false;

    [Header("Safe Cracking (Hold E)")]
    [Tooltip("Длительность удержания кнопки E для взлома сейфа (в секундах)")]
    public float holdDuration = 2.4f;
    private float currentHoldTimer = 0f;
    private float clickTimer = 0f;

    [Header("Wheel Settings")]
    [Tooltip("Ось вращения вентиля: Auto (определяет по тончайшей грани диска) либо вручную")]
    public WheelAxis wheelRotationAxis = WheelAxis.Auto;
    [Tooltip("Скорость вращения вентиля при удержании (градусов в секунду)")]
    public float wheelSpeed = 380f;

    [Tooltip("Запускать общую тревогу банка при вскрытии хранилища")]
    public bool triggerAlarmOnBreach = true;

    [Header("Animation & Visuals")]
    public float openSpeed = 1.6f;
    private const float targetOpenAngle = -105f;

    [Header("Audio (FMOD & Unity Audio)")]
    public EventReference safeActionEvent;
    private const string DefaultSafeActionPath = "event:/Environment/Furniture/Safe_Action";

    public AudioSource audioSource;
    public AudioClip creakSound;
    public AudioClip unlockSound;

    // Позиционирование и петли двери
    private Vector3 hingeWorldPoint;
    private float currentOpenAngle = 0f;

    private static AudioClip proceduralClickClip;
    private static AudioClip proceduralHeavyThudClip;

    private void Awake()
    {
        AutoConfigureReferences();
    }

    private void Start()
    {
        AutoConfigureReferences();
        EnsureCollider();
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

    private void Reset()
    {
        AutoConfigureReferences();
    }

    private void OnValidate()
    {
        AutoConfigureReferences();
    }

    public void AutoConfigureReferences()
    {
        // 1. Поиск объекта двери (Cylinder.009 / SafeDoor)
        if (doorTransform == null)
        {
            if (gameObject.name.ToLower().Contains("door") || gameObject.name.ToLower().Contains("cylinder.009"))
            {
                doorTransform = transform;
            }
            else
            {
                foreach (Transform c in GetComponentsInChildren<Transform>(true))
                {
                    string n = c.name.ToLower();
                    if (n.Contains("safedoor") || n.Contains("cylinder.009"))
                    {
                        doorTransform = c;
                        break;
                    }
                }
                if (doorTransform == null) doorTransform = transform;
            }
        }

        // 2. Поиск и связывание вентиля (Cylinder.011 / Valve)
        FindWheelTransform();

        // 3. Если вентиль найден на уровне родителя (как в RoomS2 FBX), делаем его дочерним двери,
        // чтобы при распахивании двери вентиль двигался вместе с ней!
        if (wheelTransform != null && doorTransform != null && wheelTransform.parent != doorTransform)
        {
            wheelTransform.SetParent(doorTransform, true);
        }
    }

    public void FindWheelTransform()
    {
        if (wheelTransform != null) return;

        Transform searchRoot = doorTransform != null ? doorTransform : transform;

        // Поиск среди дочерних объектов двери
        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == searchRoot) continue;
            string n = child.name.ToLower();
            if (n.Contains("valve") || n.Contains("cylinder.011") || n.Contains("wheel") || n.Contains("handle") || n.Contains("dial"))
            {
                wheelTransform = child;
                return;
            }
        }

        // Поиск среди дочерних объектов всего префаба / родителя (сиблинги в RoomS2)
        Transform parent = searchRoot.parent != null ? searchRoot.parent : transform.parent;
        if (parent != null)
        {
            foreach (Transform sibling in parent.GetComponentsInChildren<Transform>(true))
            {
                if (sibling == searchRoot || sibling == transform) continue;
                string n = sibling.name.ToLower();
                if (n.Contains("valve") || n.Contains("cylinder.011") || n.Contains("wheel") || n.Contains("handle") || n.Contains("dial"))
                {
                    wheelTransform = sibling;
                    return;
                }
            }
        }
    }

    public Vector3 GetResolvedWheelAxis()
    {
        switch (wheelRotationAxis)
        {
            case WheelAxis.LocalX: return Vector3.right;
            case WheelAxis.LocalY: return Vector3.up;
            case WheelAxis.LocalZ: return Vector3.forward;
            case WheelAxis.Auto:
            default:
                if (wheelTransform != null)
                {
                    MeshFilter mf = wheelTransform.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Vector3 s = mf.sharedMesh.bounds.size;
                        // Ось вентиля — это ось с наименьшим размером (толщина диска/колеса меньше его диаметра)
                        if (s.x < s.y && s.x < s.z) return Vector3.right;
                        if (s.y < s.x && s.y < s.z) return Vector3.up;
                        return Vector3.forward;
                    }
                }
                return Vector3.forward;
        }
    }

    private void EnsureCollider()
    {
        Transform t = doorTransform != null ? doorTransform : transform;

        MeshCollider mc = t.GetComponent<MeshCollider>();
        if (mc != null && !mc.convex)
        {
            Destroy(mc);
        }

        BoxCollider box = t.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = t.gameObject.AddComponent<BoxCollider>();
        }

        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            box.center = mf.sharedMesh.bounds.center;
            box.size = Vector3.Max(mf.sharedMesh.bounds.size, new Vector3(1.4f, 2.2f, 0.6f));
        }
        else
        {
            box.center = Vector3.zero;
            box.size = new Vector3(1.4f, 2.2f, 0.6f);
        }
    }

    private void CalculateHingePoint()
    {
        Transform t = doorTransform != null ? doorTransform : transform;
        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            Vector3 localHinge = b.center + new Vector3(-b.extents.x * 0.95f, 0f, 0f);
            hingeWorldPoint = t.TransformPoint(localHinge);
        }
        else
        {
            hingeWorldPoint = t.position;
        }
    }

    private void Update()
    {
        Transform t = doorTransform != null ? doorTransform : transform;

        // Плавная анимация распахивания двери вокруг ее петли
        float targetAngle = isOpen ? targetOpenAngle : 0f;
        if (Mathf.Abs(currentOpenAngle - targetAngle) > 0.05f)
        {
            float newAngle = Mathf.MoveTowards(currentOpenAngle, targetAngle, Time.deltaTime * 90f * openSpeed);
            float delta = newAngle - currentOpenAngle;
            currentOpenAngle = newAngle;

            t.RotateAround(hingeWorldPoint, t.up, delta);
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            // Для закрытого сейфа требуется зажатие кнопки E
            return;
        }

        // Если замок уже вскрыт - открываем/закрываем дверь
        isOpen = !isOpen;
        PlaySafeActionSound();
        PlaySound(creakSound != null ? creakSound : proceduralClickClip, 1.0f);
    }

    public string GetInteractText()
    {
        if (isOpen) return "Закрыть дверь хранилища";
        if (isLocked) return "Удерживайте [E] чтобы вскрыть замок сейфа";
        return "Открыть дверь хранилища";
    }

    /// <summary>
    /// Вызывается каждый кадр из PlayerInteraction при наведении на сейф
    /// </summary>
    public void ProcessHoldInteraction(float deltaTime, bool isHoldingE, Vector3 playerPos)
    {
        if (!isLocked || isOpen)
        {
            CancelHoldInteraction();
            return;
        }

        Transform targetT = doorTransform != null ? doorTransform : transform;

        // Проверка дистанции: если игрок отошел дальше 4.2м — сбрасываем удержание
        if (Vector3.Distance(targetT.position, playerPos) > 5.0f)
        {
            CancelHoldInteraction();
            return;
        }

        if (isHoldingE)
        {
            currentHoldTimer += deltaTime;

            // Вращение вентиля сейфа во время взлома
            if (wheelTransform != null)
            {
                wheelTransform.Rotate(GetResolvedWheelAxis(), wheelSpeed * deltaTime, Space.Self);
            }

            // Звуковые щелчки трещотки замка
            clickTimer -= deltaTime;
            if (clickTimer <= 0f)
            {
                clickTimer = 0.12f;
                float progressPitch = 0.95f + (currentHoldTimer / holdDuration) * 0.45f;
                PlaySound(proceduralClickClip, progressPitch);
            }

            float progress = Mathf.Clamp01(currentHoldTimer / holdDuration);
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.SetInteractionProgress(progress, "Взлом сейфа...");
            }

            if (currentHoldTimer >= holdDuration)
            {
                currentHoldTimer = 0f;
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.HideInteractionProgress();
                }
                UnlockAndOpen();
            }
        }
        else
        {
            CancelHoldInteraction();
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

    private void PlaySafeActionSound()
    {
        try
        {
            if (!safeActionEvent.IsNull)
            {
                RuntimeManager.PlayOneShot(safeActionEvent, transform.position);
            }
            else
            {
                RuntimeManager.PlayOneShot(DefaultSafeActionPath, transform.position);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[VaultDoor] FMOD Safe Action Sound warning: {ex.Message}");
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
            proceduralClickClip = CreateTone(1250f, 0.045f);
        }
        if (proceduralHeavyThudClip == null)
        {
            proceduralHeavyThudClip = CreateTone(140f, 0.35f);
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

    /// <summary>
    /// Взрыв динамитом или удар кувалдой выбивает замок сейфа
    /// </summary>
    public void TakeDamage(int damage)
    {
        // Выбивание замка пулями или кувалдой отключено (в будущем будет реализовано только от подрыва динамита)
        return;
    }

    public void UnlockAndOpen()
    {
        if (isOpen && !isLocked) return;

        isLocked = false;
        isOpen = true;

        PlaySafeActionSound();
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
