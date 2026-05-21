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
    }

    public void BeginHeist()
    {
        if (isHeistInitialized)
        {
            return;
        }

        CacheSpawnPoints();

        if (spawnInitialEnemiesOnHeistStart)
        {
            SpawnInitialEnemies();
        }

        patrolTimer = 0f;
        isHeistInitialized = true;

        Debug.Log($"[EnemyManager] Heist enemies ready. Initial points: {initialSpawnPointCount}, patrol doors: {patrolDoorCount}, patrol level: {currentPatrolLevel}%");
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

            if (point.pointType == SpawnPointType.Initial)
            {
                initialSpawnPoints.Add(point);
            }
            else if (point.pointType == SpawnPointType.PatrolDoor)
            {
                activePatrolDoors.Add(point);
            }
        }

        initialSpawnPointCount = initialSpawnPoints.Count;
        patrolDoorCount = activePatrolDoors.Count;
    }

    private void SpawnInitialEnemies()
    {
        foreach (var point in initialSpawnPoints)
        {
            if (point == null)
            {
                continue;
            }

            int count = Random.Range(point.minInitialCount, point.maxInitialCount + 1);
            SpawnEnemiesOfType(GetPrefabForType(point.initialEnemyType), count, point.transform);
        }
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

        if (activePatrolDoors.Count == 0)
        {
            Debug.LogWarning("[EnemyManager] No active patrol doors found.");
            return;
        }

        EnemySpawnPoint door = activePatrolDoors[Random.Range(0, activePatrolDoors.Count)];
        PatrolTier tier = patrolDatabase.GetTierForLevel(currentPatrolLevel);

        Debug.Log($"[EnemyManager] Patrol spawning from {door.gameObject.name}.");
        SpawnTierGroup(tier, door.transform);
    }

    private void SpawnTierGroup(PatrolTier tier, Transform spawnPoint)
    {
        int guardsToSpawn = Random.Range(tier.minGuards, tier.maxGuards + 1);
        int assaultersToSpawn = Random.Range(tier.minAssaulters, tier.maxAssaulters + 1);
        int elitesToSpawn = Random.Range(tier.minElites, tier.maxElites + 1);

        SpawnEnemiesOfType(guardPrefab, guardsToSpawn, spawnPoint);
        SpawnEnemiesOfType(assaulterPrefab, assaultersToSpawn, spawnPoint);
        SpawnEnemiesOfType(elitePrefab, elitesToSpawn, spawnPoint);
    }

    private void SpawnEnemiesOfType(GameObject prefab, int count, Transform spawnPoint)
    {
        if (prefab == null || spawnPoint == null || count <= 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (currentEnemyCount >= mobCap)
            {
                break;
            }

            Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            GameObject enemyObj = Instantiate(prefab, spawnPoint.position + offset, spawnPoint.rotation, transform);
            EnemyController enemy = enemyObj.GetComponentInChildren<EnemyController>();

            if (enemy != null)
            {
                RegisterEnemy(enemy);
            }
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
}
