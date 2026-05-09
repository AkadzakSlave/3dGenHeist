using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LootDatabase", menuName = "Heist/Loot Database")]
public class LootDatabase : ScriptableObject
{
    public List<LootData> smallLoot = new List<LootData>();
    public List<LootData> mediumLoot = new List<LootData>();
    public List<LootData> largeLoot = new List<LootData>();

    public LootData GetRandomLoot(LootPointSize size)
    {
        List<LootData> targetList = null;
        switch (size)
        {
            case LootPointSize.Small: targetList = smallLoot; break;
            case LootPointSize.Medium: targetList = mediumLoot; break;
            case LootPointSize.Large: targetList = largeLoot; break;
        }

        if (targetList == null || targetList.Count == 0) return null;
        return targetList[Random.Range(0, targetList.Count)];
    }
}
