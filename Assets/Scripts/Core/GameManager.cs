using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Global Progression")]
    public int globalBankBalance = 0;
    public int completedQuotas = 0;

    [Header("Operation Settings (Campaign)")]
    public int currentDay = 1;
    public int operationTargetQuota = 3000;
    public int accumulatedOperationMoney = 0;
    public LevelPreset activeOperationPreset;
    
    [Header("Current Heist Data (Read Only)")]
    public int bagMoney = 0;
    public int depositedMoney = 0;
    public int currentWeight = 0;
    public int maxWeight = 40;

    [Header("Timer Settings")]
    public float heistTimer = 300f; // 5 минут
    public bool isHeistActive = false;
    private int reinforcementCount = 0;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip depositSound;

    [Header("Events")]
    public UnityEvent onMoneyChanged;
    public UnityEvent onHeistStarted;

    [Header("State & Teleportation")]
    public bool isInLobby = true;
    public Transform playerTransform;
    public CharacterController playerController;
    public LevelGenerator levelGenerator;
    public HeistUI heistUI;
    
    [Header("Spawn Points")]
    public Transform lobbySpawnPoint;
    public Transform motelSpawnPoint;
    public Transform bossRoomSpawnPoint;

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
            // Для удобства тестов
            activeOperationPreset = levelGenerator.activePreset;
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
    // ЦИКЛ ОПЕРАЦИИ (КАМПАНИЯ 3 ДНЯ)
    // ============================================

    public void StartOperation(LevelPreset preset)
    {
        activeOperationPreset = preset;
        currentDay = 1;
        accumulatedOperationMoney = 0;
        
        // Масштабирование сложности (пример: базовая квота 3000 растет с каждой победой)
        operationTargetQuota = 3000 + (completedQuotas * 1500); 
        
        Debug.Log($"<color=cyan>[GameManager] Операция начата! Штат: {preset.levelName}. Квота: {operationTargetQuota}</color>");
        StartLoadingHeist(preset);
    }

    public void StartNextDay()
    {
        if (activeOperationPreset != null)
        {
            currentDay++;
            StartLoadingHeist(activeOperationPreset);
        }
        else
        {
            Debug.LogError("[GameManager] Нет активного пресета операции!");
        }
    }

    private void StartLoadingHeist(LevelPreset preset)
    {
        if (!isInLobby) return;
        StartCoroutine(HeistLoadingRoutine(preset));
    }

    private IEnumerator HeistLoadingRoutine(LevelPreset preset)
    {
        if (heistUI != null) heistUI.ShowLoadingScreen();

        bagMoney = 0;
        depositedMoney = 0;
        currentWeight = 0;
        heistTimer = 300f; 
        isHeistActive = false;
        reinforcementCount = 0;
        currentZeroDelay = 0f;
        onMoneyChanged?.Invoke();

        if (heistUI != null) heistUI.UpdateUI();

        if (levelGenerator != null)
        {
            levelGenerator.ClearLevel();
            yield return new WaitForEndOfFrame();
            
            bool isGenerationDone = false;
            levelGenerator.GenerateAsync(preset, () => { isGenerationDone = true; });

            yield return new WaitUntil(() => isGenerationDone);
        }

        Vector3 targetPos = new Vector3(0, 1, 0);
        if (levelGenerator != null && levelGenerator.vanSpawnPoint != null)
        {
            targetPos = levelGenerator.vanSpawnPoint.position + Vector3.up * 1.5f;
        }
        
        yield return StartCoroutine(TeleportPlayer(targetPos, Quaternion.identity));

        isInLobby = false;
        if (heistUI != null) heistUI.HideLoadingScreen();
    }

    public void ExtractFromHeist()
    {
        if (isInLobby) return;
        StartCoroutine(ExtractionRoutine());
    }

    private IEnumerator ExtractionRoutine()
    {
        if (heistUI != null) heistUI.ShowLoadingScreen();

        accumulatedOperationMoney += depositedMoney;
        Debug.Log($"<color=orange>[Economy] День {currentDay} завершен. Накоплено: ${accumulatedOperationMoney} / ${operationTargetQuota}</color>");

        if (levelGenerator != null) levelGenerator.ClearLevel();

        isInLobby = true;
        isHeistActive = false;

        if (currentDay < 3)
        {
            // Переход на следующий день (отдых в мотеле)
            if (motelSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(motelSpawnPoint.position, motelSpawnPoint.rotation));
            }
            if (heistUI != null) heistUI.HideLoadingScreen();
        }
        else
        {
            // Переход к боссу
            if (bossRoomSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(bossRoomSpawnPoint.position, bossRoomSpawnPoint.rotation));
            }
            if (heistUI != null) heistUI.HideLoadingScreen();
        }
    }

    // ============================================
    // БОСС И ОПЛАТА
    // ============================================

    public void ProcessBossResult(bool isSuccess)
    {
        StartCoroutine(ProcessBossResultRoutine(isSuccess));
    }

    private IEnumerator ProcessBossResultRoutine(bool isSuccess)
    {
        if (isSuccess)
        {
            int profit = accumulatedOperationMoney - operationTargetQuota;
            globalBankBalance += profit;
            completedQuotas++;
            Debug.Log($"<color=green>[Economy] Прибыль после уплаты квоты: ${profit}. Общий баланс: ${globalBankBalance}</color>");
            
            // Если мы выиграли, покажем черный экран для перехода в Лобби, так как BossRoomManager его не показывал
            if (heistUI != null) heistUI.ShowLoadingScreen();
        }
        else
        {
            // Провал
            globalBankBalance = 0;
            completedQuotas = 0;
            Debug.Log($"<color=red>[Economy] Игровой процесс сброшен. Начинаем заново.</color>");
            // Экран уже черный (BossRoomManager затемнил его перед выстрелом)
        }

        // Возвращаемся в Лобби
        if (lobbySpawnPoint != null)
        {
            yield return StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }

        if (heistUI != null) heistUI.HideLoadingScreen();
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
            onHeistStarted?.Invoke();
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
        onMoneyChanged?.Invoke();
    }

    public void DepositBag()
    {
        if (bagMoney <= 0) return;

        depositedMoney += bagMoney;
        bagMoney = 0;
        currentWeight = 0;
        
        if (audioSource != null && depositSound != null)
        {
            audioSource.PlayOneShot(depositSound);
        }

        onMoneyChanged?.Invoke();
    }

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
    }
}
