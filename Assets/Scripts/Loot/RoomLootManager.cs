using UnityEngine;
using System.Collections.Generic;

public class RoomLootManager : MonoBehaviour
{
    [Header("Database")]
    public LootDatabase lootDB;

    [Header("Spawn Settings")]
    [Tooltip("Если включено, лут спавнится в Start(). Обычно запускается централизованно LevelGenerator'ом")]
    public bool spawnOnStart = false;
    [Range(0, 360)] public float randomRotationJitter = 20f; // На сколько градусов может отклониться предмет

    private Transform lootContainer;

    private void Start()
    {
        // Если запущена отдельная тестовая комната без LevelGenerator
        if (spawnOnStart || FindAnyObjectByType<LevelGenerator>() == null)
        {
            SpawnLoot();
        }
    }

    public void SpawnLoot()
    {
        if (lootDB == null)
        {
            Debug.LogWarning($"[RoomLootManager] LootDatabase не назначена на {gameObject.name}");
            return;
        }

        LootPoint[] points = GetComponentsInChildren<LootPoint>();
        if (points == null || points.Length == 0) return;

        if (lootContainer == null)
        {
            var existing = transform.Find("_SpawnedLoot");
            if (existing != null) lootContainer = existing;
            else
            {
                var go = new GameObject("_SpawnedLoot");
                go.transform.SetParent(this.transform, false);
                lootContainer = go.transform;
            }
        }
        
        foreach (var point in points)
        {
            if (point == null || point.isOccupied) continue;
            if (Random.Range(0f, 100f) > point.spawnChance) continue;

            FillPointCapacity(point);
            point.isOccupied = true;
        }
    }

    public void ClearLoot()
    {
        if (lootContainer != null)
        {
            Destroy(lootContainer.gameObject);
            lootContainer = null;
        }

        LootPoint[] points = GetComponentsInChildren<LootPoint>();
        if (points != null)
        {
            foreach (var p in points)
            {
                if (p != null) p.isOccupied = false;
            }
        }
    }

    private void FillPointCapacity(LootPoint point)
    {
        int remainingCapacity = point.capacityUnits;
        int currentPosIndex = 0;

        while (remainingCapacity > 0)
        {
            LootPointSize targetSize = LootPointSize.Small;
            int sizeCost = 1;

            if (point.sizeFilter == LootPointSizeFilter.LargeOnly)
            {
                targetSize = LootPointSize.Large;
                sizeCost = Mathf.Max(1, remainingCapacity);
            }
            else if (point.sizeFilter == LootPointSizeFilter.MediumOnly)
            {
                targetSize = LootPointSize.Medium;
                sizeCost = Mathf.Min(2, remainingCapacity);
            }
            else if (point.sizeFilter == LootPointSizeFilter.SmallOnly)
            {
                targetSize = LootPointSize.Small;
                sizeCost = 1;
            }
            else // Any
            {
                if (remainingCapacity >= 4 && (point.isHighValue || Random.value > 0.4f))
                {
                    targetSize = LootPointSize.Large;
                    sizeCost = 4;
                }
                else if (remainingCapacity >= 2 && (point.isHighValue || Random.value > 0.5f))
                {
                    targetSize = LootPointSize.Medium;
                    sizeCost = 2;
                }
                else
                {
                    targetSize = LootPointSize.Small;
                    sizeCost = 1;
                }
            }

            LootData lootData = lootDB.GetRandomLoot(targetSize, point.isHighValue);
            
            if (lootData != null)
            {
                Vector3 spawnPos;
                Quaternion spawnRot;

                if (point.spawnPositions != null && point.spawnPositions.Count > 0)
                {
                    if (sizeCost > 1 && (currentPosIndex + 1) < point.spawnPositions.Count)
                    {
                        Vector3 p1 = point.spawnPositions[currentPosIndex].position;
                        Vector3 p2 = point.spawnPositions[currentPosIndex + 1].position;
                        spawnPos = Vector3.Lerp(p1, p2, 0.5f) + Vector3.up * point.verticalOffset;
                        
                        spawnRot = point.spawnPositions[currentPosIndex].rotation;
                        currentPosIndex += 2;
                    }
                    else
                    {
                        Transform t = point.spawnPositions[Mathf.Min(currentPosIndex, point.spawnPositions.Count - 1)];
                        spawnPos = (t != null ? t.position : point.transform.position) + Vector3.up * point.verticalOffset;
                        spawnRot = t != null ? t.rotation : point.transform.rotation;
                        currentPosIndex++;
                    }
                }
                else
                {
                    spawnPos = point.transform.position + Vector3.up * point.verticalOffset;
                    spawnRot = point.transform.rotation;
                }

                // Добавляем случайный поворот по Y
                float jitter = Random.Range(-randomRotationJitter, randomRotationJitter);
                spawnRot *= Quaternion.Euler(0, jitter, 0);

                SpawnAt(lootData, spawnPos, spawnRot, point.isHighValue);
                remainingCapacity -= sizeCost;
            }
            else
            {
                if (targetSize == LootPointSize.Small) break;
                remainingCapacity -= 1;
            }
        }
    }

    private void SpawnAt(LootData lootData, Vector3 pos, Quaternion rot, bool isHighValue)
    {
        if (lootData.prefab == null) return;

        Quaternion finalRot = rot * lootData.prefab.transform.rotation;

        Transform parentTransform = lootContainer != null ? lootContainer : this.transform;
        GameObject lootObj = Instantiate(lootData.prefab, pos, finalRot, parentTransform);

        LootItem item = lootObj.GetComponent<LootItem>();
        if (item == null) item = lootObj.AddComponent<LootItem>();
        
        item.data = lootData;
        item.Initialize(isHighValue);
    }
}
