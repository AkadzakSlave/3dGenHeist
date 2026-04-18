using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AlarmSiren : MonoBehaviour
{
    private AudioSource audioSource;
    private bool sirenActive = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // Настраиваем AudioSource для 3D звука по умолчанию
        audioSource.spatialBlend = 1.0f; 
        audioSource.loop = true;
        audioSource.playOnAwake = false;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onHeistStarted.AddListener(StartSiren);
        }
    }

    void StartSiren()
    {
        if (audioSource != null && !sirenActive)
        {
            sirenActive = true;
            audioSource.Play();
            Debug.Log($"[Siren] Сирена на объекте {gameObject.name} запущена!");
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onHeistStarted.RemoveListener(StartSiren);
        }
    }
}
