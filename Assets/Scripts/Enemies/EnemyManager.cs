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

        // Apply Bank Difficulty scaling
        int diff = 1;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            diff = Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
        }

        currentPatrolLevel = startingPatrolLevel + (diff - 1) * 3f;
        mobCap = 12 + diff * 3;
        patrolTimerInterval = Mathf.Max(25f, 60f - (diff - 1) * 3.5f);

        CacheSpawnPoints();

        if (spawnInitialEnemiesOnHeistStart)
        {
            SpawnInitialEnemies();
        }

        patrolTimer = 0f;
        isHeistInitialized = true;

        Debug.Log($"<color=orange>[EnemyManager] Heist Difficulty Level: {diff}. Initial points: {initialSpawnPointCount}, patrol doors: {patrolDoorCount}, mob cap: {mobCap}, patrol level: {currentPatrolLevel}%</color>");
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
            StartCoroutine(SpawnEnemiesOfType(GetPrefabForType(point.initialEnemyType), count, point.transform));
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
}
