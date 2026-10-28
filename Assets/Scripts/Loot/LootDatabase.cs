using UnityEngine;
using System.Collections.Generic;

public enum LootPointSize { Small, Medium, Large }

[CreateAssetMenu(fileName = "LootDatabase", menuName = "Heist/Loot Database")]
public class LootDatabase : ScriptableObject
{
    public List<LootData> smallLoot = new List<LootData>();
    public List<LootData> mediumLoot = new List<LootData>();
    public List<LootData> largeLoot = new List<LootData>();

    public LootData GetRandomLoot(LootPointSize size, bool highValueOnly = false, ICollection<LootData> excludeItems = null)
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

        // Исключаем нежелательные дубликаты (если передан список исключений)
        List<LootData> filteredList = targetList;
        if (excludeItems != null && excludeItems.Count > 0)
        {
            var withoutExcluded = targetList.FindAll(x => x != null && !excludeItems.Contains(x));
            if (withoutExcluded.Count > 0)
            {
                filteredList = withoutExcluded;
            }
        }

        if (highValueOnly)
        {
            // Фильтруем предметы повышенной ценности (золото, кейсы, драгоценности или от $400)
            var highVal = filteredList.FindAll(x => x != null && (x.minValue >= 400 || x.lootType == LootType.Gold || x.lootType == LootType.Case || x.lootType == LootType.Brilliant));
            if (highVal.Count > 0)
            {
                return SelectWeighted(highVal);
            }
        }

        var valid = filteredList.FindAll(x => x != null);
        if (valid.Count == 0) return null;
        return SelectWeighted(valid);
    }

    private LootData SelectWeighted(List<LootData> items)
    {
        if (items == null || items.Count == 0) return null;
        if (items.Count == 1) return items[0];

        int totalWeight = 0;
        foreach (var it in items)
        {
            totalWeight += Mathf.Max(1, it.dropWeight);
        }

        int roll = Random.Range(0, totalWeight);
        int current = 0;
        foreach (var it in items)
        {
            current += Mathf.Max(1, it.dropWeight);
            if (roll < current) return it;
        }

        return items[items.Count - 1];
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
