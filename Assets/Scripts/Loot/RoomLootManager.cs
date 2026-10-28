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

    public int GetTotalSpawnedValue()
    {
        if (lootContainer == null) return 0;
        int sum = 0;
        LootItem[] items = lootContainer.GetComponentsInChildren<LootItem>();
        if (items != null)
        {
            foreach (var it in items)
            {
                if (it != null) sum += it.value;
            }
        }
        return sum;
    }

    /// <summary>
    /// Пытается заспавнить лут в свободной точке комнаты (для системы гарантированного бюджета)
    /// </summary>
    public bool TrySpawnInUnoccupiedPoint(bool forceHighValue)
    {
        LootPoint[] points = GetComponentsInChildren<LootPoint>();
        if (points == null || points.Length == 0) return false;

        foreach (var p in points)
        {
            if (p != null && !p.isOccupied)
            {
                FillPointGraduation(p, forceHighValue);
                p.isOccupied = true;
                return true;
            }
        }
        return false;
    }

    private void FillPointCapacity(LootPoint point)
    {
        FillPointGraduation(point, point.isHighValue);
    }

    private void FillPointGraduation(LootPoint point, bool isHighVal)
    {
        if (lootDB == null) return;

        // 1. Определяем набор предметов (градация)
        List<LootPointSize> plannedSizes = new List<LootPointSize>();

        if (point.sizeFilter == LootPointSizeFilter.SmallOnly)
        {
            plannedSizes.Add(LootPointSize.Small);
        }
        else if (point.sizeFilter == LootPointSizeFilter.MediumOnly)
        {
            plannedSizes.Add(LootPointSize.Medium);
        }
        else if (point.sizeFilter == LootPointSizeFilter.LargeOnly)
        {
            plannedSizes.Add(LootPointSize.Large);
        }
        else // Any: адаптивная градация на основе capacityUnits
        {
            if (point.capacityUnits <= 1)
            {
                // Малая точка: 1 малый предмет
                plannedSizes.Add(LootPointSize.Small);
            }
            else if (point.capacityUnits == 2)
            {
                // Средняя точка: 50% 1 средний предмет (слиток), 50% 2 малых со смещением (пачки денег)
                float roll = Random.value;
                float medChance = isHighVal ? 0.7f : 0.5f;

                if (roll < medChance)
                {
                    plannedSizes.Add(LootPointSize.Medium);
                }
                else
                {
                    // 2 малых предмета заполняют вместимость средней точки (2 x 1 = 2)
                    plannedSizes.Add(LootPointSize.Small);
                    plannedSizes.Add(LootPointSize.Small);
                }
            }
            else // Крупная точка (Large / capacity >= 4)
            {
                // 40% крупный кейс, 35% 2 средних слика, 25% 2-3 пачки купюр
                float roll = Random.value;
                float largeChance = isHighVal ? 0.6f : 0.4f;

                if (roll < largeChance)
                {
                    plannedSizes.Add(LootPointSize.Large);
                }
                else if (roll < (largeChance + 0.35f))
                {
                    // 2 средних предмета заполняют крупную точку (2 x 2 = 4)
                    plannedSizes.Add(LootPointSize.Medium);
                    plannedSizes.Add(LootPointSize.Medium);
                }
                else
                {
                    // 2-3 малых предмета аккуратно разложенных рядом
                    int count = Random.Range(2, 4);
                    for (int i = 0; i < count; i++) plannedSizes.Add(LootPointSize.Small);
                }
            }
        }

        // 2. Спавним каждый запланированный предмет со Scatter-смещением
        int totalItems = plannedSizes.Count;
        List<LootData> spawnedAtThisPoint = new List<LootData>();

        for (int i = 0; i < totalItems; i++)
        {
            LootPointSize size = plannedSizes[i];

            // Исключаем дубликаты на одной точке (самоцветы и уникальные предметы не дублируются,
            // дублироваться разрешено только пачкам денег Cash)
            List<LootData> excludeList = new List<LootData>();
            foreach (var spawned in spawnedAtThisPoint)
            {
                if (spawned != null && spawned.lootType != LootType.Cash && !spawned.name.ToLower().Contains("cash"))
                {
                    excludeList.Add(spawned);
                }
            }

            LootData lootData = lootDB.GetRandomLoot(size, isHighVal, excludeList);
            if (lootData == null) lootData = lootDB.GetRandomLoot(size, isHighVal); // Fallback если все исключены
            if (lootData == null) continue;

            spawnedAtThisPoint.Add(lootData);

            Vector3 spawnPos;
            Quaternion spawnRot;

            // Если у точки заданы ручные под-точки
            if (point.spawnPositions != null && point.spawnPositions.Count > i && point.spawnPositions[i] != null)
            {
                Transform t = point.spawnPositions[i];
                spawnPos = t.position + Vector3.up * point.verticalOffset;
                spawnRot = t.rotation;
            }
            else
            {
                // Автоматический естественный Scatter (разнос в стороны, чтобы не слипались)
                Vector3 basePos = point.transform.position + Vector3.up * point.verticalOffset;
                Vector3 scatterOffset = Vector3.zero;

                if (totalItems == 2)
                {
                    float side = (i == 0) ? -0.13f : 0.13f;
                    scatterOffset = point.transform.right * side + point.transform.forward * Random.Range(-0.03f, 0.03f);
                }
                else if (totalItems >= 3)
                {
                    if (i == 0) scatterOffset = -point.transform.right * 0.15f + point.transform.forward * Random.Range(-0.03f, 0.03f);
                    else if (i == 1) scatterOffset = point.transform.forward * 0.08f;
                    else scatterOffset = point.transform.right * 0.15f + point.transform.forward * Random.Range(-0.03f, 0.03f);
                }

                spawnPos = basePos + scatterOffset;
                spawnRot = point.transform.rotation;
            }

            // Добавляем случайный поворот по Y для естественности
            float jitter = Random.Range(-randomRotationJitter, randomRotationJitter);
            spawnRot *= Quaternion.Euler(0, jitter, 0);

            SpawnAt(lootData, spawnPos, spawnRot, isHighVal);
        }
    }

    private void SpawnAt(LootData lootData, Vector3 pos, Quaternion rot, bool isHighValue)
    {
        if (lootData.prefab == null) return;

        Quaternion finalRot = rot * lootData.prefab.transform.rotation;
        Transform parentTransform = lootContainer != null ? lootContainer : this.transform;

        // Поиск фактической поверхности (стола, полки или пола) прямо под точкой
        Vector3 spawnPos = pos;
        if (Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 1.5f))
        {
            spawnPos.y = hit.point.y + 0.05f; // 5 см над столом/поверхностью для мягкого падения
        }
        else
        {
            spawnPos.y += 0.05f;
        }

        GameObject lootObj = Instantiate(lootData.prefab, spawnPos, finalRot, parentTransform);

        // Полная стандартизация экземпляра лута
        StandardizeLootInstance(lootObj, lootData, isHighValue);
    }

    private void StandardizeLootInstance(GameObject lootObj, LootData lootData, bool isHighValue)
    {
        if (lootObj == null) return;

        // 1. Проверяем наличие коллайдера.
        // Если автор уже настроил коллайдер на префабе (BoxCollider, MeshCollider, SphereCollider и т.д.),
        // мы ни в коем случае не трогаем его и не меняем масштаб объекта!
        Collider existingCol = lootObj.GetComponentInChildren<Collider>();
        if (existingCol == null)
        {
            // Только если у объекта вообще нет никакого коллайдера - создаем аккуратный BoxCollider
            BoxCollider box = lootObj.AddComponent<BoxCollider>();
            Renderer r = lootObj.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                box.center = box.transform.InverseTransformPoint(r.bounds.center);
                Vector3 localSz = box.transform.InverseTransformVector(r.bounds.size);
                box.size = new Vector3(Mathf.Abs(localSz.x), Mathf.Abs(localSz.y), Mathf.Abs(localSz.z));
            }
            else
            {
                box.size = new Vector3(0.2f, 0.2f, 0.2f);
            }
        }

        // 3. Компонент LootItem
        LootItem item = lootObj.GetComponent<LootItem>();
        if (item == null) item = lootObj.AddComponent<LootItem>();
        item.data = lootData;
        item.Initialize(isHighValue);

        // 4. Мягкая физика естественного приземления
        LootPhysicsSettler settler = lootObj.GetComponent<LootPhysicsSettler>();
        if (settler == null) settler = lootObj.AddComponent<LootPhysicsSettler>();
        settler.BeginSettle();
    }
}
