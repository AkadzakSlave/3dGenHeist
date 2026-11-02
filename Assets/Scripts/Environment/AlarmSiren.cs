using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AlarmSiren : MonoBehaviour
{
    [Header("Audio (FMOD)")]
    public EventReference sirenEvent;

    [Header("Visual Alarm (Red Flashing Light)")]
    public Light alarmLight;
    public float pulseSpeed = 4.0f;
    public float minIntensity = 0.5f;
    public float maxIntensity = 3.5f;
    public float lightRange = 16f;

    private EventInstance sirenInstance;
    private bool sirenActive = false;

    void Start()
    {
        // Инициализируем инстанс аудио FMOD
        try
        {
            if (!sirenEvent.IsNull)
            {
                sirenInstance = RuntimeManager.CreateInstance(sirenEvent);
            }
            else
            {
                sirenInstance = RuntimeManager.CreateInstance("event:/Environment/Siren_Bell");
            }
            RuntimeManager.AttachInstanceToGameObject(sirenInstance, gameObject, GetComponent<Rigidbody>());
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[AlarmSiren] FMOD siren initialization warning: {ex.Message}");
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.onHeistStarted.AddListener(StartSiren);
        }

        // Если лампа не назначена вручную — подготавливаем компонент
        if (alarmLight == null)
        {
            alarmLight = GetComponentInChildren<Light>();
        }
        if (alarmLight != null)
        {
            alarmLight.enabled = false;
        }
    }

    void Update()
    {
        if (sirenActive && alarmLight != null && alarmLight.enabled)
        {
            // Пульсация красного аварийного маяка
            float wave = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            alarmLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, wave);
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

        // Активируем визуальный красный свет тревоги
        if (alarmLight == null)
        {
            GameObject lObj = new GameObject("EmergencyAlarmLight");
            lObj.transform.SetParent(transform, false);
            lObj.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            alarmLight = lObj.AddComponent<Light>();
            alarmLight.type = LightType.Point;
            alarmLight.color = Color.red;
            alarmLight.range = lightRange;
            alarmLight.shadows = LightShadows.None; // 0 просадок FPS
        }

        if (alarmLight != null)
        {
            alarmLight.enabled = true;
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
