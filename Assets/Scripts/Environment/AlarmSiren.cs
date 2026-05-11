using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AlarmSiren : MonoBehaviour
{
    [Header("Audio (FMOD)")]
    public EventReference sirenEvent;

    private EventInstance sirenInstance;
    private bool sirenActive = false;

    void Awake()
    {
        // Настройка будет происходить при создании инстанса
    }

    void Start()
    {
        // Инициализируем инстанс, но не запускаем
        if (!sirenEvent.IsNull)
        {
            sirenInstance = RuntimeManager.CreateInstance(sirenEvent);
            RuntimeManager.AttachInstanceToGameObject(sirenInstance, gameObject, GetComponent<Rigidbody>());
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onHeistStarted.AddListener(StartSiren);
        }
    }

    void StartSiren()
    {
        if (sirenInstance.isValid() && !sirenActive)
        {
            sirenActive = true;
            sirenInstance.start();
            Debug.Log($"[Siren] Сирена на объекте {gameObject.name} запущена через FMOD!");
        }
    }

    void OnDestroy()
    {
        if (sirenInstance.isValid())
        {
            sirenInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            sirenInstance.release();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onHeistStarted.RemoveListener(StartSiren);
        }
    }
}
