using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Global Economy")]
    public int globalBankBalance = 0;
    public int currentDay = 1;

    [Header("Heist Settings")]
    public int targetQuota = 1000;
    
    [Header("Current Heist Data (Read Only)")]
    public int bagMoney = 0;
    public int depositedMoney = 0;
    public int currentWeight = 0;
    public int maxWeight = 40;

    [Header("Timer Settings")]
    public float heistTimer = 300f; // 5 минут
    public bool isHeistActive = false;
    private int reinforcementCount = 0;
    private bool timerFinished = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip depositSound;

    [Header("Events")]
    public UnityEvent onMoneyChanged;
    public UnityEvent onQuotaMet;

    [Header("State & Teleportation")]
    public bool isInLobby = true;
    public Transform playerTransform;
    public CharacterController playerController;
    public Transform lobbySpawnPoint;
    public LevelGenerator levelGenerator;
    public HeistUI heistUI;

    private bool quotaMetTriggered = false;
    private float zeroTimerDelay = 3f;
    private float currentZeroDelay = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (isInLobby && lobbySpawnPoint != null && playerController != null)
        {
            StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }
        else if (!isInLobby && levelGenerator != null && levelGenerator.activePreset != null)
        {
            // Для удобства тестов: если стартуем сразу на уровне, генерируем его автоматически
            levelGenerator.GenerateAsync(levelGenerator.activePreset, null);
        }
    }

    private void Update()
    {
        if (isHeistActive && !isInLobby)
        {
            if (heistTimer > 0)
            {
                heistTimer -= Time.deltaTime;
            }
            else
            {
                heistTimer = 0;
                currentZeroDelay += Time.deltaTime;
                
                if (currentZeroDelay >= zeroTimerDelay)
                {
                    currentZeroDelay = 0;
                    OnTimerReachZero();
                }
            }
        }
    }

    // ============================================
    // ЦИКЛ ОГРАБЛЕНИЯ (HEIST LOOP)
    // ============================================

    public void StartLoadingHeist(LevelPreset preset)
    {
        if (!isInLobby) return;
        Debug.Log($"<color=cyan>[GameManager] Начинаем загрузку локации: {preset.levelName}</color>");
        StartCoroutine(HeistLoadingRoutine(preset));
    }

    private IEnumerator HeistLoadingRoutine(LevelPreset preset)
    {
        // 1. Показываем экран загрузки
        if (heistUI != null) heistUI.ShowLoadingScreen();

        // 2. Сбрасываем старые значения
        bagMoney = 0;
        depositedMoney = 0;
        currentWeight = 0;
        heistTimer = 300f; // TODO: Брать из настроек сложности
        isHeistActive = false;
        quotaMetTriggered = false;
        reinforcementCount = 0;
        currentZeroDelay = 0f;
        onMoneyChanged?.Invoke();

        if (heistUI != null) heistUI.UpdateUI();

        // 3. Чистим старый уровень (если был) и строим новый
        if (levelGenerator != null)
        {
            levelGenerator.ClearLevel();
            yield return new WaitForEndOfFrame();
            
            bool isGenerationDone = false;
            levelGenerator.GenerateAsync(preset, () => { isGenerationDone = true; });

            // Ждем пока строится уровень
            yield return new WaitUntil(() => isGenerationDone);
        }

        // 4. Телепортируем игрока на старт уровня (например, координаты (0,0,0) - это всегда старт)
        // В идеале берем из startObj, но пока просто в ноль.
        yield return StartCoroutine(TeleportPlayer(new Vector3(0, 1, 0), Quaternion.identity));

        // 5. Завершаем загрузку
        isInLobby = false;
        if (heistUI != null) heistUI.HideLoadingScreen();
        Debug.Log("<color=green>[GameManager] Ограбление началось (Таймер еще спит)</color>");
    }

    public void ExtractFromHeist()
    {
        if (isInLobby) return;
        Debug.Log("<color=yellow>[GameManager] Эвакуация из ограбления...</color>");
        StartCoroutine(ExtractionRoutine());
    }

    private IEnumerator ExtractionRoutine()
    {
        // 1. Экран загрузки
        if (heistUI != null) heistUI.ShowLoadingScreen();

        if (depositedMoney < targetQuota)
        {
            Debug.Log("<color=red>[Economy] Игрок уехал раньше собранной квоты!</color>");
        }

        globalBankBalance += depositedMoney;
        Debug.Log($"[Economy] Заработано за ограбление: ${depositedMoney}. Общий банк: ${globalBankBalance}");

        // TODO: Логика повышения сложности, если квота выполнена

        // 3. Телепорт в Лобби
        if (lobbySpawnPoint != null)
        {
            yield return StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }

        // 4. Очистка локации из памяти
        if (levelGenerator != null)
        {
            levelGenerator.ClearLevel();
        }

        isInLobby = true;
        isHeistActive = false;
        
        // 5. Убираем экран
        if (heistUI != null) heistUI.HideLoadingScreen();
    }

    // Хелпер для телепорта (отключает контроллер, чтобы физика не мешала)
    private IEnumerator TeleportPlayer(Vector3 pos, Quaternion rot)
    {
        if (playerController != null)
        {
            playerController.enabled = false;
            playerTransform.position = pos;
            playerTransform.rotation = rot;
            yield return new WaitForEndOfFrame();
            playerController.enabled = true;
        }
        else if (playerTransform != null)
        {
            playerTransform.position = pos;
            playerTransform.rotation = rot;
        }
    }


    // ============================================
    // ИГРОВАЯ МЕХАНИКА ОГРАБЛЕНИЯ
    // ============================================

    public void StartHeist()
    {
        if (!isHeistActive && !isInLobby)
        {
            isHeistActive = true;
            Debug.Log("<color=red>[Alarm] Тишина закончилась! Таймер запущен.</color>");
        }
    }

    private void OnTimerReachZero()
    {
        reinforcementCount++;
        Debug.Log($"<color=red>[Backup] Прибыло подкрепление! (#{reinforcementCount})</color>");
        heistTimer = 60f;
    }

    public void AddMoneyToBag(int amount, int weight)
    {
        bagMoney += amount;
        currentWeight += weight;
        Debug.Log($"[Economy] Собрано: ${amount} (Вес: {weight}). Всего в сумке: ${bagMoney} (Вес: {currentWeight}/{maxWeight})");
        onMoneyChanged?.Invoke();
    }

    public void DepositBag()
    {
        if (bagMoney <= 0) return;

        depositedMoney += bagMoney;
        Debug.Log($"[Economy] Деньги сданы! Квота: {depositedMoney} / {targetQuota}");
        bagMoney = 0;
        currentWeight = 0;
        
        if (audioSource != null && depositSound != null)
        {
            audioSource.PlayOneShot(depositSound);
        }

        onMoneyChanged?.Invoke();

        if (depositedMoney >= targetQuota && !quotaMetTriggered)
        {
            quotaMetTriggered = true;
            Debug.Log("<color=green>[Победа] Квота выполнена! Живем!</color>");
            onQuotaMet?.Invoke();
        }
    }

    public bool IsQuotaMet()
    {
        return depositedMoney >= targetQuota;
    }
}
