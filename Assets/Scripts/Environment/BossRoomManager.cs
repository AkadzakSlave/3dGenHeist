using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using FMODUnity;
using FMOD.Studio;

public class BossRoomManager : MonoBehaviour
{
    public static BossRoomManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Текстовый экран за спиной босса, где крутятся цифры")]
    public TextMeshProUGUI quotaMonitorText;

    [Header("Audio (FMOD)")]
    public EventReference countEvent;
    public EventReference gunshotEvent;
    public EventReference successEvent;

    private EventInstance countInstance;

    [Header("Animations / Hooks")]
    public float bossShootAnimDuration = 1.5f;
    public UnityEvent onBossShoot;
    public UnityEvent onBossSuccess;

    private AudioSource audioSource;
    private bool isEvaluating = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void CheckConsoleInteraction()
    {
        if (isEvaluating) return;
        
        if (GameManager.Instance != null)
        {
            isEvaluating = true;
            Debug.Log("[BossRoom] Нажата консоль. Начинаем подсчет...");
            StartCoroutine(EvaluateQuotaRoutine());
        }
    }

    private IEnumerator EvaluateQuotaRoutine()
    {
        int targetCollected = GameManager.Instance.accumulatedOperationMoney;
        int requiredQuota = GameManager.Instance.operationTargetQuota;

        float animDuration = 2.5f;
        float timer = 0f;

        // ПУНКТ 1: Анимация счета от 0 до targetCollected
        if (!countEvent.IsNull)
        {
            countInstance = RuntimeManager.CreateInstance(countEvent);
            RuntimeManager.AttachInstanceToGameObject(countInstance, gameObject);
            countInstance.start();
        }

        while (timer < animDuration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / animDuration);
            int currentDisplayAmount = Mathf.RoundToInt(Mathf.Lerp(0, targetCollected, progress));

            if (quotaMonitorText != null)
            {
                quotaMonitorText.text = $"{currentDisplayAmount} / {requiredQuota}";
                quotaMonitorText.color = Color.white;
            }

            yield return null;
        }

        if (countInstance.isValid())
        {
            countInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            countInstance.release();
        }

        // ПУНКТ 2: Анализ результатов (успех/провал)
        if (targetCollected >= requiredQuota)
        {
            // УСПЕХ
            if (quotaMonitorText != null) quotaMonitorText.color = Color.green;
            
            if (!successEvent.IsNull)
            {
                EventInstance success = RuntimeManager.CreateInstance(successEvent);
                RuntimeManager.AttachInstanceToGameObject(success, gameObject);
                success.start();
                success.release();
            }
            onBossSuccess?.Invoke();
            Debug.Log("[BossRoom] Квота выполнена! Текст зеленый. Ждем 3 сек...");

            yield return new WaitForSeconds(3f);

            GameManager.Instance.ProcessBossResult(true);
        }
        else
        {
            // ПРОВАЛ
            if (quotaMonitorText != null) quotaMonitorText.color = Color.red;
            
            // Драматичная пауза 1 секунда (игрок понимает, что денег не хватило)
            yield return new WaitForSeconds(1f);

            // Запуск анимации босса (он достает пушку)
            onBossShoot?.Invoke();
            Debug.Log($"<color=orange>[BossRoom] Босс достает оружие... Ждем {bossShootAnimDuration} сек.</color>");

            // Ждем, пока анимация дойдет до момента выстрела
            yield return new WaitForSeconds(bossShootAnimDuration);

            // Выстрел и моментально черный экран (имитация смерти)
            if (!gunshotEvent.IsNull)
            {
                EventInstance gunshot = RuntimeManager.CreateInstance(gunshotEvent);
                RuntimeManager.AttachInstanceToGameObject(gunshot, gameObject);
                gunshot.start();
                gunshot.release();
            }
            
            if (GameManager.Instance.heistUI != null)
            {
                GameManager.Instance.heistUI.ShowLoadingScreen();
            }

            Debug.Log("<color=red>[BossRoom] ВЫСТРЕЛ! Экран погас.</color>");

            // Экран висит черным 3 секунды
            yield return new WaitForSeconds(3f);

            GameManager.Instance.ProcessBossResult(false);
        }

        isEvaluating = false;
    }
}
