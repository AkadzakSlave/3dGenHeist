using UnityEngine;
using System.Collections.Generic;

public class RoomLootManager : MonoBehaviour
{
    [Header("Database")]
    public LootDatabase lootDB;

    [Header("Spawn Settings")]
    public bool spawnOnStart = true;

    private void Start()
    {
        if (spawnOnStart) SpawnLoot();
    }

    public void SpawnLoot()
    {
        if (lootDB == null) return;

        // Находим все точки лута в этой комнате
        LootPoint[] points = GetComponentsInChildren<LootPoint>();
        
        foreach (var point in points)
        {
            if (point.isOccupied) continue;

            // Проверка шанса спавна
            if (Random.Range(0f, 100f) > point.spawnChance) continue;

            // Если у точки есть конкретные позиции, спавним в каждую
            if (point.spawnPositions.Count > 0)
            {
                foreach (var pos in point.spawnPositions)
                {
                    if (pos != null) SpawnAt(point.pointSize, pos);
                }
            }
            else
            {
                // Иначе просто в центре точки
                SpawnAt(point.pointSize, point.transform);
            }

            point.isOccupied = true;
        }
    }

    private void SpawnAt(LootPointSize size, Transform targetTransform)
    {
        LootData lootData = lootDB.GetRandomLoot(size);
        if (lootData == null || lootData.prefab == null) return;

        // Спавним префаб
        GameObject lootObj = Instantiate(lootData.prefab, targetTransform.position, targetTransform.rotation, targetTransform);
        
        // Добавляем/настраиваем компонент LootItem
        LootItem item = lootObj.GetComponent<LootItem>();
        if (item == null) item = lootObj.AddComponent<LootItem>();
        
        item.data = lootData;
        item.Initialize();
    }
}
