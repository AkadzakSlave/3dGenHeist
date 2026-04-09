using System.Collections.Generic;
using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    [Header("Settings")]
    public List<GameObject> lootPrefabs;
    [Tooltip("Spawn chance for loot at each point (0-1)")]
    public float spawnChance = 0.5f;

    [Header("Spawn Points")]
    public List<Transform> spawnPoints;

    void Start()
    {
        SpawnLoot();
    }

    public void SpawnLoot()
    {
        if (lootPrefabs == null || lootPrefabs.Count == 0 || spawnPoints == null) return;

        foreach (var point in spawnPoints)
        {
            if (Random.value <= spawnChance)
            {
                int randomIndex = Random.Range(0, lootPrefabs.Count);
                Instantiate(lootPrefabs[randomIndex], point.position, point.rotation, transform);
            }
        }
    }
}
