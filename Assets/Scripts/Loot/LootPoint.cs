using UnityEngine;
using System.Collections.Generic;

// LootPointSize перенесен в LootDatabase.cs для глобальной видимости

public enum LootPointSizeFilter { Any, SmallOnly, MediumOnly, LargeOnly }

public class LootPoint : MonoBehaviour
{
    [Header("Capacity Settings")]
    [Tooltip("Сколько 'единиц объема' вмещает точка. Малый предмет = 1, Средний = 2, Крупный = 4")]
    public int capacityUnits = 1;

    [Tooltip("Ограничение по размеру предмета (Any = авто по вместимости)")]
    public LootPointSizeFilter sizeFilter = LootPointSizeFilter.Any;

    [Tooltip("Особая точка (для сейфа/хранилища): спавнит дорогой лут (золото, кейсы, драгоценности, пачки денег с бонусом к цене)")]
    public bool isHighValue = false;
    
    [Range(0, 100)] public float spawnChance = 80f;

    [Header("Visual Settings")]
    [Tooltip("Смещение по высоте, чтобы лут не тонул в столе")]
    public float verticalOffset = 0.05f;

    [Header("Spawn Locations")]
    [Tooltip("Конкретные точки. Если их меньше, чем предметов, предметы будут спавниться в одной позиции (кучей)")]
    public List<Transform> spawnPositions = new List<Transform>();

    public bool isOccupied = false;

    private void OnDrawGizmos()
    {
        Color gizmoColor;
        if (isHighValue)
        {
            gizmoColor = new Color(1f, 0.85f, 0.1f, 0.9f); // Gold for safe/high value
        }
        else if (sizeFilter == LootPointSizeFilter.LargeOnly || capacityUnits >= 4)
        {
            gizmoColor = new Color(0.85f, 0.2f, 1f, 0.9f); // Magenta for Large
        }
        else if (sizeFilter == LootPointSizeFilter.MediumOnly || capacityUnits >= 2)
        {
            gizmoColor = new Color(0.2f, 0.7f, 1f, 0.9f); // Cyan for Medium
        }
        else
        {
            gizmoColor = new Color(0.2f, 1f, 0.4f, 0.9f); // Green for Small
        }

        Gizmos.color = gizmoColor;
        Vector3 boxSize = new Vector3(0.25f, 0.08f, 0.25f);
        if (sizeFilter == LootPointSizeFilter.LargeOnly || capacityUnits >= 4) boxSize = new Vector3(0.5f, 0.25f, 0.4f);
        else if (sizeFilter == LootPointSizeFilter.MediumOnly || capacityUnits >= 2) boxSize = new Vector3(0.35f, 0.15f, 0.35f);

        // Центр куба на половине высоты, чтобы его дно касалось поверхности
        Vector3 center = transform.position + Vector3.up * (verticalOffset + boxSize.y * 0.5f);

        Gizmos.DrawWireCube(center, boxSize);
        Gizmos.DrawRay(transform.position, Vector3.up * (verticalOffset + boxSize.y));
        
        if (spawnPositions != null && spawnPositions.Count > 0)
        {
            foreach (var p in spawnPositions)
            {
                if (p != null)
                {
                    Gizmos.color = gizmoColor * 0.8f;
                    Gizmos.DrawRay(p.position, Vector3.up * 0.15f);
                }
            }
        }
    }
}
