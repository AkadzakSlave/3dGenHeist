using UnityEngine;
using System.Collections.Generic;

public enum LootPointSize { Small, Medium, Large }

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

    public int GetAverageItemValue()
    {
        int total = 0;
        int count = 0;
        List<LootData>[] categories = new List<LootData>[] { smallLoot, mediumLoot, largeLoot };
        foreach (var category in categories)
        {
            if (category == null) continue;
            foreach (var item in category)
            {
                if (item != null)
                {
                    total += (item.minValue + item.maxValue) / 2;
                    count++;
                }
            }
        }
        return count > 0 ? total / count : 250;
    }
}
