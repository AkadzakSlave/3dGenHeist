using UnityEngine;
using System.Collections.Generic;

public enum LootPointSize { Small, Medium, Large }

[CreateAssetMenu(fileName = "LootDatabase", menuName = "Heist/Loot Database")]
public class LootDatabase : ScriptableObject
{
    public List<LootData> smallLoot = new List<LootData>();
    public List<LootData> mediumLoot = new List<LootData>();
    public List<LootData> largeLoot = new List<LootData>();

    public LootData GetRandomLoot(LootPointSize size, bool highValueOnly = false)
    {
        List<LootData> targetList = null;
        switch (size)
        {
            case LootPointSize.Small: targetList = smallLoot; break;
            case LootPointSize.Medium: targetList = mediumLoot; break;
            case LootPointSize.Large: targetList = largeLoot; break;
        }

        // Если запрошенный список пуст, ищем в смежных категориях
        if (targetList == null || targetList.Count == 0)
        {
            if (size == LootPointSize.Large && mediumLoot.Count > 0) targetList = mediumLoot;
            else if (size == LootPointSize.Medium && smallLoot.Count > 0) targetList = smallLoot;
            else if (smallLoot.Count > 0) targetList = smallLoot;
            else return null;
        }

        if (highValueOnly)
        {
            // Фильтруем предметы повышенной ценности (золото, кейсы, драгоценности или от $400)
            var highVal = targetList.FindAll(x => x != null && (x.minValue >= 400 || x.lootType == LootType.Gold || x.lootType == LootType.Case || x.lootType == LootType.Brilliant));
            if (highVal.Count > 0)
            {
                return highVal[Random.Range(0, highVal.Count)];
            }
        }

        var valid = targetList.FindAll(x => x != null);
        if (valid.Count == 0) return null;
        return valid[Random.Range(0, valid.Count)];
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
