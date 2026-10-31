using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [Header("Settings")]
    public PatrolDatabase patrolDatabase;
    public int mobCap = 20;
    public float patrolTimerInterval = 45f;
    public float startingPatrolLevel = 5f;
    public bool spawnInitialEnemiesOnHeistStart = true;
    public bool usePatrolSpawnChance = false;

    [Header("Patrol Tuning (Inspector Adjustable)")]
    [Range(10f, 120f)]
    [Tooltip("Интервал между проверками патруля в секундах (по умолчанию 45 сек)")]
    public float patrolCheckInterval = 45f;
    [Range(0f, 100f)]
    [Tooltip("Шанс спавна патруля за интервал (в %). По умолчанию 50%!")]
    public float patrolSpawnChance = 50f;
    [Tooltip("Если true, использует patrolSpawnChance из инспектора; если false, берет из пресета сложности")]
    public bool useFixedPatrolChance = true;

    [Header("Prefabs")]
    public GameObject guardPrefab;
    public GameObject assaulterPrefab;
    public GameObject elitePrefab;

    [Header("State")]
    public float currentPatrolLevel = 5f;
    public int currentEnemyCount = 0;
    public int initialSpawnPointCount = 0;
    public int patrolDoorCount = 0;

    [Header("Police Assault Waves")]
    public float policeRespawnTimer = -1f;
    public int currentPoliceWave = 1;
    public int activePoliceCount = 0;

    private float patrolTimer = 0f;
    private bool isHeistInitialized = false;
    public bool isAssaultWaveActive = false;

    private readonly List<EnemySpawnPoint> initialSpawnPoints = new List<EnemySpawnPoint>();
    private readonly List<EnemySpawnPoint> activePatrolDoors = new List<EnemySpawnPoint>();
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly List<EnemyController> activePoliceUnits = new List<EnemyController>();

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

    private void Update()
    {
        // Отладочный вызов штурма полиции клавишей F6 для быстрого тестирования
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f6Key.wasPressedThisFrame)
        {
            TriggerPoliceAssaultWave(1);
        }

        // Отладочный вызов патруля клавишей F7 для быстрого тестирования
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame)
        {
            Debug.Log("<color=cyan>[EnemyManager] F7 нажат — принудительный вызов патруля!</color>");
            SpawnPatrol();
        }

        // Таймер возрождения полиции: через 30 секунд после гибели волны прибывает новая
        if (isAssaultWaveActive && policeRespawnTimer > 0f)
        {
            policeRespawnTimer -= Time.deltaTime;
            if (policeRespawnTimer <= 0f)
            {
                policeRespawnTimer = -1f;
                currentPoliceWave++;
                TriggerPoliceAssaultWave(currentPoliceWave);
            }
        }

        if (GameManager.Instance == null || !GameManager.Instance.isHeistActive)
        {
            return;
        }

        if (!isHeistInitialized)
        {
            BeginHeist();
        }

        float effectiveInterval = patrolCheckInterval > 0f ? patrolCheckInterval : patrolTimerInterval;
        if (patrolDatabase == null || effectiveInterval <= 0f)
        {
            return;
        }

        patrolTimer += Time.deltaTime;
        if (patrolTimer >= effectiveInterval)
        {
            patrolTimer -= effectiveInterval;
            OnPatrolMinutePassed();
        }
    }

    public void ResetForNewHeist()
    {
        ClearActiveEnemies();

        initialSpawnPoints.Clear();
        activePatrolDoors.Clear();
        activePoliceUnits.Clear();
        activePoliceCount = 0;
        policeRespawnTimer = -1f;
        currentPoliceWave = 1;

        currentPatrolLevel = startingPatrolLevel;
        currentEnemyCount = 0;
        initialSpawnPointCount = 0;
        patrolDoorCount = 0;
        patrolTimer = 0f;
        isHeistInitialized = false;
        isAssaultWaveActive = false;
    }

    [ContextMenu("🚨 Trigger Police Assault Wave")]
    public void ContextMenuTriggerPoliceWave()
    {
        TriggerPoliceAssaultWave(1);
    }

    [ContextMenu("👮 Spawn Patrol Now (F7)")]
    public void ContextMenuSpawnPatrol()
    {
        SpawnPatrol();
    }

    public void TriggerPoliceAssaultWave(int waveIndex)
    {
        isAssaultWaveActive = true;
        currentPoliceWave = waveIndex;
        policeRespawnTimer = -1f; // Сбрасываем таймер
        mobCap += 8; // Расширяем лимит врагов на время штурма
        currentPatrolLevel = Mathf.Max(currentPatrolLevel, 20f + waveIndex * 8f);

        // Ищем точку входа с улицы (Van_Room / vanSpawnPoint)
        Transform streetSpawnPoint = null;
        if (LevelGenerator.Instance != null && LevelGenerator.Instance.vanSpawnPoint != null)
        {
            streetSpawnPoint = LevelGenerator.Instance.vanSpawnPoint;
        }
        else if (GameManager.Instance != null && GameManager.Instance.levelGenerator != null && GameManager.Instance.levelGenerator.vanSpawnPoint != null)
        {
            streetSpawnPoint = GameManager.Instance.levelGenerator.vanSpawnPoint;
        }

        // Если точка фургона не найдена, используем резервные двери
        if (streetSpawnPoint == null)
        {
            activePatrolDoors.RemoveAll(point => point == null || !point.gameObject.activeInHierarchy);
            if (activePatrolDoors.Count == 0) CacheSpawnPoints();
        }

        int squadsToSpawn = Mathf.Clamp(waveIndex + 1, 1, 3);

        for (int i = 0; i < squadsToSpawn; i++)
        {
            int assaulters = Random.Range(2, 4); // 2-3 полицейских штурмовика Assaulter
            int elites = waveIndex >= 2 ? 1 : 0; // Командир спецназа на повторных волнах

            Transform spawnTrans = streetSpawnPoint != null ? streetSpawnPoint : (activePatrolDoors.Count > 0 ? activePatrolDoors[Random.Range(0, activePatrolDoors.Count)].transform : transform);
            StartCoroutine(StaggeredPoliceSpawnRoutine(assaulters, elites, spawnTrans));
        }

        // Поднимаем общую тревогу штурма по всей карте и ориентируем врагов на игроков
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            NotifyRadioAlert(player.transform.position, 300f, player.transform);
        }

        if (HeistUI.Instance != null)
        {
            string copMsg = waveIndex == 1
                ? "🚨 ВНИМАНИЕ: Полиция прибыла на вызов! Штурмовики перекрыли улицу у фургона!"
                : $"🚨 ВНИМАНИЕ: Новая волна штурма полиции (#{waveIndex}) наступает с улицы!";
            HeistUI.Instance.ShowNotification(copMsg, new Color(1f, 0.25f, 0.25f, 1f));
        }

        Debug.Log($"<color=red>[EnemyManager] Волна штурма полиции #{waveIndex} активирована! Полиция (Assaulter) наступает со стороны улицы/фургона.</color>");
    }

    public void BeginHeist()
    {
        if (isHeistInitialized)
        {
            return;
        }

        // Apply Bank Difficulty scaling
        int diff = 1;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            diff = Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
        }

        currentPatrolLevel = startingPatrolLevel + (diff - 1) * 3f;
        mobCap = 10 + diff * 2;
        patrolTimerInterval = Mathf.Max(25f, 60f - (diff - 1) * 3.5f);

        CacheSpawnPoints();

        // Расчёт целевого стартового пула врагов в зависимости от количества комнат и сложности
        int roomCount = 8;
        if (LevelGenerator.Instance != null && LevelGenerator.Instance.SpawnedRooms != null && LevelGenerator.Instance.SpawnedRooms.Count > 0)
        {
            roomCount = LevelGenerator.Instance.SpawnedRooms.Count;
        }
        else if (GameManager.Instance != null && GameManager.Instance.levelGenerator != null && GameManager.Instance.levelGenerator.SpawnedRooms != null)
        {
            roomCount = GameManager.Instance.levelGenerator.SpawnedRooms.Count;
        }

        int targetStartCount = Mathf.Clamp(Mathf.RoundToInt(roomCount * 0.6f) + diff, 3, mobCap);

        if (spawnInitialEnemiesOnHeistStart)
        {
            SpawnInitialEnemies(targetStartCount, diff);
        }

        patrolTimer = 0f;
        isHeistInitialized = true;

        Debug.Log($"<color=orange>[EnemyManager] Heist Difficulty: {diff}. Комнат: {roomCount}. Квота старта: {targetStartCount}. Точек: {initialSpawnPointCount}, дверей патрулей: {patrolDoorCount}, кап: {mobCap}</color>");
    }

    public void ClearActiveEnemies()
    {
        foreach (var enemy in new List<EnemyController>(activeEnemies))
        {
            if (enemy != null)
            {
                Destroy(enemy.gameObject);
            }
        }

        activeEnemies.Clear();
        currentEnemyCount = 0;
    }

    private void CacheSpawnPoints()
    {
        initialSpawnPoints.Clear();
        activePatrolDoors.Clear();

        EnemySpawnPoint[] allPoints = FindObjectsByType<EnemySpawnPoint>();
        foreach (var point in allPoints)
        {
            if (point == null || !point.gameObject.activeInHierarchy)
            {
                continue;
            }

            point.isActivePool = true;

            if (point.pointType == SpawnPointType.PatrolDoor || point.spawnRole == EnemySpawnRole.Patrol_Door)
            {
                activePatrolDoors.Add(point);
            }
            else
            {
                initialSpawnPoints.Add(point);
            }
        }

        // Сортируем стартовые точки: приоритетные (Vault_Guard) идут первыми
        initialSpawnPoints.Sort((a, b) => b.priority.CompareTo(a.priority));

        initialSpawnPointCount = initialSpawnPoints.Count;
        patrolDoorCount = activePatrolDoors.Count;
    }

    private void SpawnInitialEnemies(int targetCount, int difficulty)
    {
        if (initialSpawnPoints.Count == 0)
        {
            Debug.Log("[EnemyManager] Точки спавна охраны (EnemySpawnPoint) не найдены на сцене.");
            return;
        }

        int spawnedTotal = 0;
        List<EnemySpawnPoint> pointsPool = new List<EnemySpawnPoint>(initialSpawnPoints);

        // 1. Сначала спавним гарантированные ключевые точки (Vault_Guard, priority >= 10)
        foreach (var point in pointsPool)
        {
            if (spawnedTotal >= targetCount || spawnedTotal >= mobCap) break;
            if (point.priority < 10) continue;

            int count = Random.Range(point.minInitialCount, point.maxInitialCount + 1);
            EnemyType spawnType = DetermineEnemyTypeForPoint(point, difficulty);
            StartCoroutine(SpawnEnemiesOfType(GetPrefabForType(spawnType), count, point.transform));
            spawnedTotal += count;
        }

        // 2. Спавн постов комнат с учетом шанса spawnChance
        foreach (var point in pointsPool)
        {
            if (spawnedTotal >= targetCount || spawnedTotal >= mobCap) break;
            if (point.priority >= 10) continue; // Уже заспавнены

            if (Random.Range(0f, 100f) <= point.spawnChance)
            {
                int count = Random.Range(point.minInitialCount, point.maxInitialCount + 1);
                EnemyType spawnType = DetermineEnemyTypeForPoint(point, difficulty);
                StartCoroutine(SpawnEnemiesOfType(GetPrefabForType(spawnType), count, point.transform));
                spawnedTotal += count;
            }
        }

        // 3. Если квота ещё не заполнена (не повезло с процентами), добираем оставшиеся посты
        if (spawnedTotal < targetCount)
        {
            foreach (var point in pointsPool)
            {
                if (spawnedTotal >= targetCount || spawnedTotal >= mobCap) break;
                int count = 1;
                EnemyType spawnType = DetermineEnemyTypeForPoint(point, difficulty);
                StartCoroutine(SpawnEnemiesOfType(GetPrefabForType(spawnType), count, point.transform));
                spawnedTotal += count;
            }
        }

        Debug.Log($"<color=orange>[EnemyManager] Стартовый спавн завершён: заспавнено {spawnedTotal} врагов (квота: {targetCount}, кап: {mobCap}).</color>");
    }

    private EnemyType DetermineEnemyTypeForPoint(EnemySpawnPoint point, int difficulty)
    {
        // 1. Охрана хранилища/сейфа
        if (point.spawnRole == EnemySpawnRole.Vault_Guard)
        {
            if (difficulty >= 5) return EnemyType.Elite;
            return EnemyType.Guard;
        }

        // 2. Внутренние посты охраны банка (Guard_Post, Corridor_Patrol):
        // Assaulter — это полиция с улицы при тревоге, внутри на старте его НЕТ.
        // На высоких уровнях (> 5) обычный Guard с шансом заменяется на Elite:
        if (difficulty > 5)
        {
            float eliteChance = 0.05f + (difficulty - 5) * 0.05f; // diff 6: 10%, diff 7: 15%, ..., diff 10: 30%
            if (Random.value < eliteChance)
            {
                return EnemyType.Elite;
            }
        }

        return EnemyType.Guard;
    }

    private void OnPatrolMinutePassed()
    {
        currentPatrolLevel += patrolDatabase.patrolLevelIncreasePerMinute;
        Debug.Log($"[EnemyManager] Прошла 1 минута ограбления. Уровень патруля: {currentPatrolLevel}%");

        if (currentEnemyCount >= mobCap)
        {
            Debug.Log("[EnemyManager] Достигнут лимит врагов (Mob Cap). Спавн патруля пропущен.");
            return;
        }

        int diff = 1;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            diff = Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
        }

        // Получаем шанс появления патруля
        float spawnChancePercent = 50f;
        if (useFixedPatrolChance)
        {
            spawnChancePercent = patrolSpawnChance;
        }
        else if (GameManager.Instance != null && GameManager.Instance.difficultyDatabase != null)
        {
            DifficultyPreset preset = GameManager.Instance.difficultyDatabase.GetDifficulty(diff);
            if (preset != null)
            {
                spawnChancePercent = preset.patrolLevelPercent;
            }
        }

        float roll = Random.Range(0f, 100f);
        if (roll < spawnChancePercent)
        {
            Debug.Log($"<color=orange>[EnemyManager] Проверка патруля успешна: бросок {roll:F1}% < {spawnChancePercent}%. Спавн патруля!</color>");
            SpawnPatrol();
        }
        else
        {
            Debug.Log($"[EnemyManager] Патруль в этот интервал не появился: бросок {roll:F1}% >= {spawnChancePercent}%.");
        }
    }

    private void SpawnPatrol()
    {
        activePatrolDoors.RemoveAll(point => point == null || !point.gameObject.activeInHierarchy);
        patrolDoorCount = activePatrolDoors.Count;

        // Патруль спавнится ТОЛЬКО в открытых/исследованных комнатах!
        List<EnemySpawnPoint> openPatrolDoors = activePatrolDoors.FindAll(point => {
            if (point == null) return false;
            RoomTracker tracker = point.GetComponentInParent<RoomTracker>();
            if (tracker != null && !tracker.isExplored)
            {
                return false; // Комната еще не исследована игроками
            }
            return point.IsRoomOpened();
        });
        
        // Фильтр от скопления врагов у двери (> 1 врага в радиусе 5м)
        List<EnemySpawnPoint> uncrowdedDoors = openPatrolDoors.FindAll(door => {
            int nearbyEnemies = 0;
            Collider[] cols = Physics.OverlapSphere(door.transform.position, 5.0f);
            foreach (var col in cols)
            {
                if (col.GetComponentInParent<EnemyController>() != null) nearbyEnemies++;
            }
            return nearbyEnemies <= 1;
        });

        List<EnemySpawnPoint> candidateDoors = uncrowdedDoors.Count > 0 ? uncrowdedDoors : openPatrolDoors;

        if (candidateDoors.Count == 0)
        {
            Debug.Log("[EnemyManager] Спавн патруля пропущен: нет доступных открытых исследованных комнат.");
            return;
        }

        EnemySpawnPoint door = candidateDoors[Random.Range(0, candidateDoors.Count)];

        PlacedBarricade barricade = door != null ? door.GetComponentInChildren<PlacedBarricade>() : null;
        if (barricade != null)
        {
            if (barricade.TryBlockPatrolExit(door.gameObject.name))
            {
                return; // Патруль заблокирован баррикадой
            }
        }

        RoomTracker roomTracker = door.GetComponentInParent<RoomTracker>();
        string sectorName = roomTracker != null ? roomTracker.roomName : (door.transform.parent != null ? door.transform.parent.name : "Банк");

        PatrolTier tier = patrolDatabase.GetTierForLevel(currentPatrolLevel);
        Debug.Log($"[EnemyManager] Патруль выходит из {door.gameObject.name} в секторе [{sectorName}].");
        SpawnTierGroup(tier, door.transform, sectorName);
    }

    private void SpawnTierGroup(PatrolTier tier, Transform spawnPoint, string sectorName = "")
    {
        int diff = 1;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            diff = Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
        }

        int guardsToSpawn = Random.Range(tier.minGuards, tier.maxGuards + 1);
        if (guardsToSpawn < 1) guardsToSpawn = 1;

        // В обычных минутных патрулях полиции (Assaulter) нет — полиция штурмует снаружи при 0 таймера
        int assaultersToSpawn = 0;

        // На высоких уровнях (> 5) появляется шанс Elite в патруле
        int elitesToSpawn = 0;
        if (diff > 5)
        {
            float elitePatrolChance = 0.15f + (diff - 5) * 0.10f; // diff 6 = 25%, diff 10 = 65%
            if (Random.value < elitePatrolChance)
            {
                elitesToSpawn = 1;
            }
        }

        StartCoroutine(StaggeredSpawnRoutine(guardsToSpawn, assaultersToSpawn, elitesToSpawn, spawnPoint));

        if (HeistUI.Instance != null)
        {
            string alertMsg = string.IsNullOrEmpty(sectorName)
                ? "⚠️ Внимание: замечено движение патруля охраны!"
                : $"⚠️ Внимание: в секторе [{sectorName}] замечен патруль охраны!";
            HeistUI.Instance.ShowNotification(alertMsg, new Color(1f, 0.78f, 0.28f, 1f));
        }
    }

    private System.Collections.IEnumerator StaggeredPoliceSpawnRoutine(int assaulters, int elites, Transform spawnPoint)
    {
        yield return SpawnPoliceEnemies(assaulterPrefab, assaulters, spawnPoint);
        yield return SpawnPoliceEnemies(elitePrefab, elites, spawnPoint);
    }

    private System.Collections.IEnumerator SpawnPoliceEnemies(GameObject prefab, int count, Transform spawnPoint)
    {
        if (prefab == null || spawnPoint == null || count <= 0)
        {
            yield break;
        }

        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        Transform target = player != null ? player.transform : (GameManager.Instance != null ? GameManager.Instance.playerTransform : null);

        for (int i = 0; i < count; i++)
        {
            if (currentEnemyCount >= mobCap)
            {
                break;
            }

            Vector3 offset = spawnPoint.forward * (i * 0.9f) + new Vector3(Random.Range(-0.8f, 0.8f), 0f, 0f);
            Vector3 targetPos = spawnPoint.position + offset;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, 8.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            GameObject enemyObj = Instantiate(prefab, targetPos, spawnPoint.rotation, transform);
            EnemyController enemy = enemyObj.GetComponentInChildren<EnemyController>();

            if (enemy != null)
            {
                var agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(targetPos);
                }

                // Переводим полицию в режим активного штурма здания без ограничений по комнатам!
                enemy.SetAssaultBreachMode(target);
                RegisterEnemy(enemy);
                activePoliceUnits.Add(enemy);
                activePoliceCount = activePoliceUnits.Count;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private System.Collections.IEnumerator StaggeredSpawnRoutine(int guards, int assaulters, int elites, Transform spawnPoint)
    {
        yield return SpawnEnemiesOfType(guardPrefab, guards, spawnPoint);
        yield return SpawnEnemiesOfType(assaulterPrefab, assaulters, spawnPoint);
        yield return SpawnEnemiesOfType(elitePrefab, elites, spawnPoint);
    }

    private System.Collections.IEnumerator SpawnEnemiesOfType(GameObject prefab, int count, Transform spawnPoint)
    {
        if (prefab == null || spawnPoint == null || count <= 0)
        {
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (currentEnemyCount >= mobCap)
            {
                break;
            }

            Vector3 offset = spawnPoint.forward * (i * 0.8f) + new Vector3(Random.Range(-0.5f, 0.5f), 0f, 0f);
            Vector3 targetPos = spawnPoint.position + offset;
            if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out UnityEngine.AI.NavMeshHit hit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }

            GameObject enemyObj = Instantiate(prefab, targetPos, spawnPoint.rotation, transform);
            EnemyController enemy = enemyObj.GetComponentInChildren<EnemyController>();

            if (enemy != null)
            {
                var agent = enemyObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(targetPos);
                }
                RegisterEnemy(enemy);
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private GameObject GetPrefabForType(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Assaulter:
                return assaulterPrefab;
            case EnemyType.Elite:
                return elitePrefab;
            default:
                return guardPrefab;
        }
    }

    public void RegisterEnemy(EnemyController enemy)
    {
        if (enemy != null && !activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
            currentEnemyCount = activeEnemies.Count;
        }
    }

    public void UnregisterEnemy(EnemyController enemy)
    {
        if (enemy != null && activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            currentEnemyCount = activeEnemies.Count;
        }

        if (enemy != null && activePoliceUnits.Contains(enemy))
        {
            activePoliceUnits.Remove(enemy);
            activePoliceCount = activePoliceUnits.Count;

            // Если все полицейские из волны ликвидированы — запускаем 30-секундный таймер до следующей волны!
            if (isAssaultWaveActive && activePoliceUnits.Count == 0 && policeRespawnTimer < 0f)
            {
                policeRespawnTimer = 30f;
                Debug.Log("<color=yellow>[Police] Все штурмовики волны ликвидированы! Подкрепление прибудет через 30 секунд.</color>");
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.ShowNotification("⚠ Полиция ликвидирована! Следующая волна прибудет через 30 сек!", new Color(1f, 0.85f, 0.25f, 1f));
                }
            }
        }
    }

    public void NotifyNoise(Vector3 soundPosition, float soundRadius = 22f)
    {
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            var enemy = activeEnemies[i];
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(enemy.transform.position, soundPosition);
                if (dist <= soundRadius)
                {
                    enemy.OnHearSound(soundPosition);
                }
            }
        }
    }

    public void NotifyRadioAlert(Vector3 alertPosition, float alertRadius = 35f, Transform targetPlayer = null)
    {
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            var enemy = activeEnemies[i];
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(enemy.transform.position, alertPosition);
                if (dist <= alertRadius)
                {
                    enemy.OnRadioAlertReceived(alertPosition, targetPlayer);
                }
            }
        }
    }
}
