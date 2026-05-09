using UnityEngine;
using System.Collections.Generic;

public enum LootPointSize { Small, Medium, Large }

public class LootPoint : MonoBehaviour
{
    [Header("Settings")]
    public LootPointSize pointSize = LootPointSize.Small;
    [Range(0, 100)] public float spawnChance = 80f;

    [Header("Spawn Locations")]
    [Tooltip("Точки, где появятся предметы. Если пусто - появится в центре объекта.")]
    public List<Transform> spawnPositions = new List<Transform>();

    public bool isOccupied = false;

    private void OnDrawGizmos()
    {
        Gizmos.color = pointSize == LootPointSize.Small ? Color.green : (pointSize == LootPointSize.Medium ? Color.yellow : Color.red);
        
        if (spawnPositions.Count > 0)
        {
            foreach (var p in spawnPositions)
            {
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.1f);
            }
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
        }
    }
}
