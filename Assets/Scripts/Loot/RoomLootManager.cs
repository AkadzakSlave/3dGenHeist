using UnityEngine;
using System.Collections.Generic;

public class RoomLootManager : MonoBehaviour
{
    [Header("Database")]
    public LootDatabase lootDB;

    [Header("Spawn Settings")]
    public bool spawnOnStart = true;
    [Range(0, 360)] public float randomRotationJitter = 20f; // На сколько градусов может отклониться предмет

    private void Start()
    {
        if (spawnOnStart) SpawnLoot();
    }

    public void SpawnLoot()
    {
        if (lootDB == null) return;

        LootPoint[] points = GetComponentsInChildren<LootPoint>();
        
        foreach (var point in points)
        {
            if (point.isOccupied) continue;
            if (Random.Range(0f, 100f) > point.spawnChance) continue;

            FillPointCapacity(point);
            point.isOccupied = true;
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

            if (remainingCapacity >= 4 && Random.value > 0.7f) { targetSize = LootPointSize.Large; sizeCost = 4; }
            else if (remainingCapacity >= 2 && Random.value > 0.5f) { targetSize = LootPointSize.Medium; sizeCost = 2; }

            LootData lootData = lootDB.GetRandomLoot(targetSize);
            
            if (lootData != null)
            {
                Vector3 spawnPos;
                Quaternion spawnRot;

                if (point.spawnPositions.Count > 0)
                {
                    if (sizeCost > 1 && (currentPosIndex + 1) < point.spawnPositions.Count)
                    {
                        Vector3 p1 = point.spawnPositions[currentPosIndex].position;
                        Vector3 p2 = point.spawnPositions[currentPosIndex + 1].position;
                        spawnPos = Vector3.Lerp(p1, p2, 0.5f) + Vector3.up * point.verticalOffset;
                        
                        // Сочетаем вращение точки и префаба
                        spawnRot = point.spawnPositions[currentPosIndex].rotation;
                        currentPosIndex += 2;
                    }
                    else
                    {
                        Transform t = point.spawnPositions[Mathf.Min(currentPosIndex, point.spawnPositions.Count - 1)];
                        spawnPos = t.position + Vector3.up * point.verticalOffset;
                        spawnRot = t.rotation;
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

                SpawnAt(lootData, spawnPos, spawnRot);
                remainingCapacity -= sizeCost;
            }
            else
            {
                if (targetSize == LootPointSize.Small) break;
                remainingCapacity -= 1;
            }
        }
    }

    private void SpawnAt(LootData lootData, Vector3 pos, Quaternion rot)
    {
        if (lootData.prefab == null) return;

        // Перемножаем вращение точки на вращение префаба, чтобы сохранить его ориентацию
        Quaternion finalRot = rot * lootData.prefab.transform.rotation;

        GameObject lootObj = Instantiate(lootData.prefab, pos, finalRot);
        lootObj.transform.SetParent(this.transform, true);

        LootItem item = lootObj.GetComponent<LootItem>();
        if (item == null) item = lootObj.AddComponent<LootItem>();
        
        item.data = lootData;
        item.Initialize();
    }
}
