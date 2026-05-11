using UnityEngine;
using System.Collections.Generic;

// LootPointSize перенесен в LootDatabase.cs для глобальной видимости

public class LootPoint : MonoBehaviour
{
    [Header("Capacity Settings")]
    [Tooltip("Сколько 'единиц объема' вмещает точка. Малый предмет = 1, Средний = 2, Крупный = 4")]
    public int capacityUnits = 1;
    
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
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position + Vector3.up * verticalOffset;
        Gizmos.DrawWireCube(center, new Vector3(0.3f, 0.05f, 0.3f));
        
        if (spawnPositions.Count > 0)
        {
            foreach (var p in spawnPositions)
            {
                if (p != null) Gizmos.DrawRay(p.position, Vector3.up * 0.1f);
            }
        }
    }
}
