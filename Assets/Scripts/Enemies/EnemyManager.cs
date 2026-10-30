using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance { get; private set; }

    [Header("Settings")]
    public PatrolDatabase patrolDatabase;
    public int mobCap = 20;
    public float patrolTimerInterval = 60f;
    public float startingPatrolLevel = 5f;
    public bool spawnInitialEnemiesOnHeistStart = true;
    public bool usePatrolSpawnChance = false;

    [Header("Prefabs")]
    public GameObject guardPrefab;
    public GameObject assaulterPrefab;
    public GameObject elitePrefab;

    [Header("State")]
    public float currentPatrolLevel = 5f;
    public int currentEnemyCount = 0;
    public int initialSpawnPointCount = 0;
    public int patrolDoorCount = 0;

    private float patrolTimer = 0f;
    private bool isHeistInitialized = false;
    public bool isAssaultWaveActive = false;

    private readonly List<EnemySpawnPoint> initialSpawnPoints = new List<EnemySpawnPoint>();
    private readonly List<EnemySpawnPoint> activePatrolDoors = new List<EnemySpawnPoint>();
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();

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
        if (GameManager.Instance == null || !GameManager.Instance.isHeistActive)
        {
            return;
        }

        if (!isHeistInitialized)
        {
            BeginHeist();
        }

        if (patrolDatabase == null || patrolTimerInterval <= 0f)
        {
            return;
        }

        patrolTimer += Time.deltaTime;
        if (patrolTimer >= patrolTimerInterval)
        {
            patrolTimer -= patrolTimerInterval;
            OnPatrolMinutePassed();
        }
    }

    public void ResetForNewHeist()
    {
        ClearActiveEnemies();

        initialSpawnPoints.Clear();
        activePatrolDoors.Clear();

        currentPatrolLevel = startingPatrolLevel;
        currentEnemyCount = 0;
        initialSpawnPointCount = 0;
        patrolDoorCount = 0;
        patrolTimer = 0f;
        isHeistInitialized = false;
        isAssaultWaveActive = false;
    }

    public void TriggerPoliceAssaultWave(int waveIndex)
    {
        isAssaultWaveActive = true;
        mobCap += 6; // Расширяем лимит врагов на время штурма
        currentPatrolLevel = Mathf.Max(currentPatrolLevel, 15f + waveIndex * 5f);

        activePatrolDoors.RemoveAll(point => point == null || !point.gameObject.activeInHierarchy);
        if (activePatrolDoors.Count == 0)
        {
            CacheSpawnPoints();
        }

        int squadsToSpawn = Mathf.Clamp(activePatrolDoors.Count, 1, 3);
        List<EnemySpawnPoint> doors = new List<EnemySpawnPoint>(activePatrolDoors);

        for (int i = 0; i < squadsToSpawn && doors.Count > 0; i++)
        {
            int idx = Random.Range(0, doors.Count);
            EnemySpawnPoint door = doors[idx];
            doors.RemoveAt(idx);

            int assaulters = Random.Range(2, 4);
            int elites = waveIndex >= 2 ? 1 : 0;
            int guards = 1;

            StartCoroutine(StaggeredSpawnRoutine(guards, assaulters, elites, door.transform));
        }

        // Поднимаем общую тревогу штурма по всей карте и ориентируем врагов на игроков
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            NotifyRadioAlert(player.transform.position, 250f, player.transform);
        }

        Debug.Log($"<color=red>[EnemyManager] Волна штурма полиции #{waveIndex} активирована! Спавн штурмовых отрядов спецназа.</color>");
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
        if (point.spawnRole == EnemySpawnRole.Vault_Guard)
        {
            if (difficulty >= 5) return EnemyType.Elite;
            if (difficulty >= 3) return EnemyType.Assaulter;
            return EnemyType.Guard;
        }

        if (difficulty >= 4 && Random.value < (difficulty - 2) * 0.10f)
        {
            return EnemyType.Assaulter;
        }

        return point.initialEnemyType;
    }

    private void OnPatrolMinutePassed()
    {
        currentPatrolLevel += patrolDatabase.patrolLevelIncreasePerMinute;
        Debug.Log($"[EnemyManager] Patrol minute passed. Patrol level is now {currentPatrolLevel}%");

        if (currentEnemyCount >= mobCap)
        {
            Debug.Log("[EnemyManager] Mob cap reached. Patrol spawn skipped.");
            return;
        }

        if (!usePatrolSpawnChance || Random.value <= patrolDatabase.baseSpawnChance)
        {
            SpawnPatrol();
        }
        else
        {
            Debug.Log("[EnemyManager] Patrol did not spawn this minute.");
        }
    }

    private void SpawnPatrol()
    {
        activePatrolDoors.RemoveAll(point => point == null || !point.gameObject.activeInHierarchy);
        patrolDoorCount = activePatrolDoors.Count;

        List<EnemySpawnPoint> openPatrolDoors = activePatrolDoors.FindAll(point => point != null && point.IsRoomOpened());
        
        // Filter out doors that already have enemies crowding around them (> 1 enemy within 5m)
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
            Debug.Log("[EnemyManager] Patrol skipped: No valid uncrowded rooms available.");
            return;
        }

        EnemySpawnPoint door = candidateDoors[Random.Range(0, candidateDoors.Count)];

        PlacedBarricade barricade = door != null ? door.GetComponentInChildren<PlacedBarricade>() : null;
        if (barricade != null)
        {
            if (barricade.TryBlockPatrolExit(door.gameObject.name))
            {
                return; // Patrol exit blocked by barricade
            }
        }

        PatrolTier tier = patrolDatabase.GetTierForLevel(currentPatrolLevel);

        Debug.Log($"[EnemyManager] Patrol spawning from {door.gameObject.name}.");
        SpawnTierGroup(tier, door.transform);
    }

    private void SpawnTierGroup(PatrolTier tier, Transform spawnPoint)
    {
        int guardsToSpawn = Random.Range(tier.minGuards, tier.maxGuards + 1);
        int assaultersToSpawn = Random.Range(tier.minAssaulters, tier.maxAssaulters + 1);
        int elitesToSpawn = Random.Range(tier.minElites, tier.maxElites + 1);

        StartCoroutine(StaggeredSpawnRoutine(guardsToSpawn, assaultersToSpawn, elitesToSpawn, spawnPoint));
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

            Vector3 offset = spawnPoint.forward * (i * 0.4f) + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);
            GameObject enemyObj = Instantiate(prefab, spawnPoint.position + offset, spawnPoint.rotation, transform);
            EnemyController enemy = enemyObj.GetComponentInChildren<EnemyController>();

            if (enemy != null)
            {
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
