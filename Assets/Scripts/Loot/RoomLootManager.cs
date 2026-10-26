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
        for (int i = 0; i < totalItems; i++)
        {
            LootPointSize size = plannedSizes[i];
            LootData lootData = lootDB.GetRandomLoot(size, isHighVal);
            if (lootData == null) continue;

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

        // 1. Проверяем масштаб: если FBX модель миниатюрная (< 5см), увеличиваем до реального размера
        Renderer[] renderers = lootObj.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            if (b.size.magnitude < 0.05f)
            {
                lootObj.transform.localScale *= 50f;
                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            }

            // 2. Настраиваем BoxCollider ровно по визуальным границам модели
            BoxCollider box = lootObj.GetComponent<BoxCollider>();
            if (box == null) box = lootObj.AddComponent<BoxCollider>();

            box.center = box.transform.InverseTransformPoint(b.center);
            Vector3 localSz = box.transform.InverseTransformVector(b.size);
            box.size = new Vector3(Mathf.Abs(localSz.x), Mathf.Abs(localSz.y), Mathf.Abs(localSz.z));

            // Минимальные габариты для удобного наведения взгляда и устойчивости
            box.size = Vector3.Max(box.size, new Vector3(0.08f, 0.04f, 0.08f));
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
