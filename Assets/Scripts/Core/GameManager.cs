using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Global Progression")]
    public int globalBankBalance = 0;
    public int completedQuotas = 0;

    [Header("Operation Settings (Campaign)")]
    [SerializeField] private int _curDay = 1; public int currentDay { get => _curDay; set => _curDay = value; }
    public int operationTargetQuota = 3000;
    public int accumulatedOperationMoney = 0;
    public int sessionMoney = 0; // Деньги для закупки внутри текущего забега
    public LevelPreset activeOperationPreset;
    public List<LevelPreset> unlockedPresets = new List<LevelPreset>();
    
    [Header("Current Heist Session (Live)")]
    public int depositedMoney = 0;
    public int maxWeight = 80;
    public int allPlayersDeadPenalty = 100;

    [Header("Players")]
    public List<PlayerHealth> activePlayers = new List<PlayerHealth>();

    public int bagMoney
    {
        get
        {
            BagTool bag = GetHeldBag();
            return bag != null ? bag.storedMoney : 0;
        }
    }

    public int currentWeight
    {
        get
        {
            return PlayerInventory.Instance != null ? PlayerInventory.Instance.GetTotalWeight() : 0;
        }
    }

    public BagTool GetHeldBag()
    {
        if (PlayerInventory.Instance == null) return null;
        foreach (var item in PlayerInventory.Instance.slots)
        {
            if (item is BagTool bag) return bag;
        }
        return null;
    }

    [Header("UI References")]
    public HeistUI heistUI;
    public DonResultUI donResultUI;

    [Header("Difficulty & Dossiers")]
    public DifficultyDatabase difficultyDatabase;
    public BankDossier[] bankDossiers; // 2 объекта в грузовике
    public BankDossier selectedDossier;

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
    public UnityEvent onGlobalBalanceChanged;

    [Header("State & Teleportation")]
    public bool isInLobby = true;
    public Transform playerTransform;
    public CharacterController playerController;
    public LevelGenerator levelGenerator;
    
    [Header("Spawn Points")]
    public Transform lobbySpawnPoint;
    public Transform motelSpawnPoint;
    public Transform bossRoomSpawnPoint;

    private float zeroTimerDelay = 3f;
    private float currentZeroDelay = 0f;
    private bool isProcessingPlayerWipe = false;
    private bool isSurrendering = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Guard against duplicate instances running Start before Unity finishes Destroy
        if (Instance != null && Instance != this) return;

        // Register all players present in scene
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include);
        foreach (var p in players)
        {
            RegisterPlayerHealth(p);
        }

        onGlobalBalanceChanged?.Invoke();
        onMoneyChanged?.Invoke();

        if (isInLobby && lobbySpawnPoint != null)
        {
            StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }
        else if (!isInLobby && levelGenerator != null && levelGenerator.activePreset != null)
        {
            // Для удобства тестов
            activeOperationPreset = levelGenerator.activePreset;
            if (EnemyManager.Instance != null) EnemyManager.Instance.ResetForNewHeist();
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
        // ПРОВЕРКА ДЕНЕГ (Входной билет)
        if (globalBankBalance < preset.entryFee)
        {
            Debug.LogError($"[Economy] Недостаточно денег для перелета! Нужно: ${preset.entryFee}, у вас: ${globalBankBalance}");
            return;
        }

        globalBankBalance -= preset.entryFee;
        activeOperationPreset = preset;
        currentDay = 1;
        accumulatedOperationMoney = 0;
        
        // Масштабирование сложности
        operationTargetQuota = 3000 + (completedQuotas * 1500); 
        
        Debug.Log($"<color=cyan>[GameManager] Операция начата! Штат: {preset.levelName}. Платный вход: ${preset.entryFee}. Квота: {operationTargetQuota}</color>");
        
        isInLobby = false;
        SetupDossiers();

        onGlobalBalanceChanged?.Invoke();
        onMoneyChanged?.Invoke();

        if (motelSpawnPoint != null)
        {
            StartCoroutine(TeleportPlayer(motelSpawnPoint.position, motelSpawnPoint.rotation));
        }
    }

    public void SetupDossiers()
    {
        if (activeOperationPreset == null || bankDossiers == null || bankDossiers.Length == 0) return;

        // Определяем город по текущему дню
        int cityIndex = Mathf.Clamp(currentDay - 1, 0, activeOperationPreset.cities.Count - 1);
        CityConfig currentCity = activeOperationPreset.cities[cityIndex];

        foreach (var dossier in bankDossiers)
        {
            dossier.Setup(currentCity, difficultyDatabase, activeOperationPreset.bankNamesPool);
        }
        selectedDossier = null;
    }

    public void SelectDossier(BankDossier dossier)
    {
        foreach (var d in bankDossiers) d.Deselect();
        dossier.Select();
        selectedDossier = dossier;
        Debug.Log($"<color=cyan>[Truck] Выбран банк: {selectedDossier.bankName} (Сложность: {selectedDossier.difficultyLevel})</color>");
        onMoneyChanged?.Invoke();
    }

    public void ProceedToHeist()
    {
        if (selectedDossier == null)
        {
            Debug.LogWarning("[Truck] Сначала выберите досье!");
            return;
        }

        // Применяем параметры из досье
        heistTimer = selectedDossier.timeLimit;
        
        // Запускаем генерацию и телепортацию
        if (activeOperationPreset != null)
        {
            StartCoroutine(HeistLoadingRoutine(activeOperationPreset));
        }
    }

    public void StartNextDay()
    {
        if (activeOperationPreset != null)
        {
            currentDay++;
            SetupDossiers(); // Готовим новые досье для нового дня
            
            if (motelSpawnPoint != null)
            {
                StartCoroutine(TeleportPlayer(motelSpawnPoint.position, motelSpawnPoint.rotation));
            }
        }
    }

    private IEnumerator HeistLoadingRoutine(LevelPreset preset)
    {
        if (heistUI != null) heistUI.ShowLoadingScreen();

        BagTool bag = GetHeldBag();
        if (bag != null)
        {
            bag.storedMoney = 0;
            bag.storedWeight = 0;
        }
        depositedMoney = 0;
        // heistTimer теперь устанавливается в ProceedToHeist() перед запуском этой рутины
        isHeistActive = false;
        reinforcementCount = 0;
        currentZeroDelay = 0f;
        if (EnemyManager.Instance != null) EnemyManager.Instance.ResetForNewHeist();
        ReviveAllPlayers();
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

        RecalculateMapLoot();

        isInLobby = false;
        if (heistUI != null) heistUI.HideLoadingScreen();
    }

    [Header("Loot Scan Info")]
    public int totalMapLootValue;
    public int totalMapLootCount;

    public void RecalculateMapLoot()
    {
        LootItem[] items = FindObjectsByType<LootItem>(FindObjectsInactive.Exclude);
        totalMapLootValue = 0;
        totalMapLootCount = items != null ? items.Length : 0;

        foreach (var item in items)
        {
            if (item != null)
            {
                totalMapLootValue += item.value;
            }
        }

        Debug.Log($"<color=yellow>[LootScan] Всего предметов на локации: {totalMapLootCount} шт. На сумму: ${totalMapLootValue}</color>");

        if (heistUI != null)
        {
            heistUI.UpdateMapLootInfo(totalMapLootValue, totalMapLootCount);
        }
    }

    public void ExtractFromHeist()
    {
        if (isInLobby) return;
        StartCoroutine(ExtractionRoutine());
    }

    public void RegisterPlayerHealth(PlayerHealth playerHealth)
    {
        if (playerHealth != null && !activePlayers.Contains(playerHealth))
        {
            activePlayers.Add(playerHealth);
        }
    }

    public void UnregisterPlayerHealth(PlayerHealth playerHealth)
    {
        if (playerHealth != null && activePlayers.Contains(playerHealth))
        {
            activePlayers.Remove(playerHealth);
        }
    }

    public void NotifyPlayerDied(PlayerHealth deadPlayer)
    {
        if (isSurrendering || isProcessingPlayerWipe)
        {
            return;
        }

        if (deadPlayer != null && !activePlayers.Contains(deadPlayer))
        {
            activePlayers.Add(deadPlayer);
        }

        activePlayers.RemoveAll(player => player == null);
        if (activePlayers.Count == 0)
        {
            return;
        }

        foreach (var player in activePlayers)
        {
            if (player != null && !player.isDead)
            {
                return;
            }
        }

        if (isInLobby)
        {
            StartCoroutine(LobbyRespawnRoutine());
            return;
        }

        StartCoroutine(AllPlayersDeadRoutine());
    }

    private IEnumerator LobbyRespawnRoutine()
    {
        isProcessingPlayerWipe = true;
        if (heistUI != null) heistUI.ShowLoadingScreen();
        yield return new WaitForSecondsRealtime(0.5f);
        if (lobbySpawnPoint != null)
        {
            yield return StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }
        ReviveAllPlayers();
        if (heistUI != null) heistUI.HideLoadingScreen();
        isProcessingPlayerWipe = false;
    }

    public void SurrenderToLobby()
    {
        StartCoroutine(SurrenderToLobbyRoutine());
    }

    private IEnumerator SurrenderToLobbyRoutine()
    {
        isSurrendering = true;
        Time.timeScale = 1f;

        // Player dies
        PlayerHealth health = FindAnyObjectByType<PlayerHealth>();
        if (health != null && !health.isDead)
        {
            health.Die();
        }

        yield return new WaitForSecondsRealtime(0.8f);

        if (heistUI != null) heistUI.ShowLoadingScreen();

        isHeistActive = false;
        isInLobby = true;
        isProcessingPlayerWipe = false;

        ApplyPlayerWipePenalty();
        accumulatedOperationMoney = 0;
        depositedMoney = 0;
        currentDay = 1;
        activeOperationPreset = null;
        selectedDossier = null;
        onMoneyChanged?.Invoke();

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearInventory();
        }

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.ResetForNewHeist();
        }

        if (levelGenerator != null)
        {
            levelGenerator.ClearLevel();
        }

        if (lobbySpawnPoint != null)
        {
            yield return StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
        }

        ReviveAllPlayers();

        yield return new WaitForSecondsRealtime(0.2f);

        if (heistUI != null)
        {
            heistUI.HideLoadingScreen();
            heistUI.UpdateUI();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isSurrendering = false;
    }

    private IEnumerator ExtractionRoutine()
    {
        if (heistUI != null) heistUI.ShowLoadingScreen();

        accumulatedOperationMoney += depositedMoney;
        depositedMoney = 0;
        onMoneyChanged?.Invoke();
        Debug.Log($"<color=orange>[Economy] День {currentDay} завершен. Накоплено: ${accumulatedOperationMoney} / ${operationTargetQuota}</color>");

        if (EnemyManager.Instance != null) EnemyManager.Instance.ResetForNewHeist();
        if (levelGenerator != null) levelGenerator.ClearLevel();

        BagTool bag = GetHeldBag();
        if (bag != null)
        {
            bag.storedMoney = 0;
            bag.storedWeight = 0;
        }

        isHeistActive = false;
        ReviveAllPlayers();

        if (currentDay < 3)
        {
            // Переход на следующий день (отдых в мотеле/грузовике)
            currentDay++;
            SetupDossiers(); // Генерируем досье для нового дня/города

            if (motelSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(motelSpawnPoint.position, motelSpawnPoint.rotation));
            }
            isInLobby = false;
            if (heistUI != null) heistUI.HideLoadingScreen();
        }
        else
        {
            // Переход к боссу
            if (bossRoomSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(bossRoomSpawnPoint.position, bossRoomSpawnPoint.rotation));
            }
            isInLobby = false;
            if (heistUI != null) heistUI.HideLoadingScreen();
        }
    }

    private IEnumerator AllPlayersDeadRoutine()
    {
        isProcessingPlayerWipe = true;

        if (heistUI != null) heistUI.ShowLoadingScreen();

        ApplyPlayerWipePenalty();
        accumulatedOperationMoney += depositedMoney;
        depositedMoney = 0;

        Debug.Log($"<color=red>[Heist] All players died. Day {currentDay} ended. Penalty: ${allPlayersDeadPenalty}. Total: ${accumulatedOperationMoney} / ${operationTargetQuota}</color>");

        if (EnemyManager.Instance != null) EnemyManager.Instance.ResetForNewHeist();
        if (levelGenerator != null) levelGenerator.ClearLevel();
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearInventory();
            if (PlayerInventory.Instance.autoEquipDefaultLoadout)
            {
                PlayerInventory.Instance.EnsureDefaultLoadout();
            }
        }

        isHeistActive = false;

        yield return new WaitForSecondsRealtime(1.0f);

        if (currentDay < 3)
        {
            // Начинаем следующий день заезда!
            currentDay++;
            SetupDossiers();

            if (motelSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(motelSpawnPoint.position, motelSpawnPoint.rotation));
            }

            ReviveAllPlayers();
            isInLobby = false; // Игрок в мотеле планирует следующий день
        }
        else
        {
            if (bossRoomSpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(bossRoomSpawnPoint.position, bossRoomSpawnPoint.rotation));
                ReviveAllPlayers();
                isInLobby = false;
            }
            else if (lobbySpawnPoint != null)
            {
                yield return StartCoroutine(TeleportPlayer(lobbySpawnPoint.position, lobbySpawnPoint.rotation));
                ReviveAllPlayers();
                isInLobby = true;
            }
        }

        if (heistUI != null)
        {
            heistUI.HideLoadingScreen();
            heistUI.UpdateUI();
        }

        isProcessingPlayerWipe = false;
    }

    private void ApplyPlayerWipePenalty()
    {
        int penaltyLeft = allPlayersDeadPenalty;
        int depositedPenalty = Mathf.Min(depositedMoney, penaltyLeft);
        depositedMoney -= depositedPenalty;
        penaltyLeft -= depositedPenalty;

        if (penaltyLeft > 0)
        {
            accumulatedOperationMoney = Mathf.Max(0, accumulatedOperationMoney - penaltyLeft);
        }

        onMoneyChanged?.Invoke();
    }

    private void ReviveAllPlayers()
    {
        activePlayers.RemoveAll(player => player == null);
        foreach (var player in activePlayers)
        {
            player.Revive(true);
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
            int totalEarned = accumulatedOperationMoney;
            int profit = totalEarned - operationTargetQuota;
            int cleanMoney = 0;
            int sessionShare = 0;

            if (profit > 0)
            {
                cleanMoney = Mathf.FloorToInt(profit * 0.1f);
                sessionShare = profit - cleanMoney;

                globalBankBalance += cleanMoney;
                sessionMoney += sessionShare;
            }

            completedQuotas++;
            Debug.Log($"<color=green>[Don] Profit: ${profit}. Bank: ${cleanMoney}. Session: ${sessionShare}</color>");
            onGlobalBalanceChanged?.Invoke();

            // РАЗБЛОКИРОВКА НОВЫХ ШТАТОВ
            if (activeOperationPreset != null && activeOperationPreset.unlockPresets != null)
            {
                foreach (var nextPreset in activeOperationPreset.unlockPresets)
                {
                    if (!unlockedPresets.Contains(nextPreset))
                    {
                        unlockedPresets.Add(nextPreset);
                        Debug.Log($"<color=yellow>[Progression] Unlocked: {nextPreset.levelName}!</color>");
                    }
                }
            }
            
            // ПОКАЗЫВАЕМ ЭКРАН РЕЗУЛЬТАТОВ
            if (donResultUI != null)
            {
                donResultUI.ShowResults(totalEarned, operationTargetQuota, cleanMoney, sessionShare);
            }

            if (heistUI != null) heistUI.ShowLoadingScreen();
        }
        else
        {
            // Провал
            globalBankBalance = 0;
            completedQuotas = 0;
            Debug.Log($"<color=red>[Economy] Игровой процесс сброшен. Начинаем заново.</color>");
            onGlobalBalanceChanged?.Invoke();
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
            if (EnemyManager.Instance != null) EnemyManager.Instance.BeginHeist();
            onHeistStarted?.Invoke();
        }
    }

    private void OnTimerReachZero()
    {
        reinforcementCount++;
        Debug.Log($"<color=red>[Backup] Прибыло подкрепление! (#{reinforcementCount})</color>");
        heistTimer = 60f;
    }

    public void DepositBag()
    {
        BagTool bag = GetHeldBag();
        if (bag == null || bag.storedMoney <= 0) return;

        int added = bag.storedMoney;
        depositedMoney += added;
        bag.storedMoney = 0;
        bag.storedWeight = 0;
        
        if (audioSource != null && depositSound != null)
        {
            audioSource.PlayOneShot(depositSound);
        }

        onMoneyChanged?.Invoke();

        if (HeistUI.Instance != null && added > 0)
        {
            HeistUI.Instance.ShowNotification($"+${added}", new Color(0.35f, 0.95f, 0.45f, 1f));
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        // Reset session money & operation run
        sessionMoney = 0;
        depositedMoney = 0;
        accumulatedOperationMoney = 0;
        currentDay = 1;
        activeOperationPreset = null;
        selectedDossier = null;
        isHeistActive = false;
        isInLobby = true;
        isProcessingPlayerWipe = false;

        // Cleanly reload the scene to reset all game state, entities, and rooms
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator TeleportPlayer(Vector3 pos, Quaternion rot)
    {
        if (Instance != null && Instance != this) yield break;

        if (playerTransform == null && playerController != null)
        {
            playerTransform = playerController.transform;
        }

        if (playerController == null && playerTransform != null)
        {
            playerController = playerTransform.GetComponent<CharacterController>();
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<CharacterController>();
            if (playerController != null && playerTransform == null)
            {
                playerTransform = playerController.transform;
            }
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (playerTransform != null)
        {
            playerTransform.position = pos;
            playerTransform.rotation = rot;
        }

        yield return new WaitForEndOfFrame();

        // Check if playerController is still valid after the frame (in case a duplicate GameObject was destroyed)
        if (playerController != null)
        {
            playerController.enabled = true;
        }
        else
        {
            CharacterController cc = FindAnyObjectByType<CharacterController>();
            if (cc != null)
            {
                cc.enabled = true;
                playerController = cc;
                playerTransform = cc.transform;
            }
        }

        PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            pm.ResetVelocity();
        }
    }
}
