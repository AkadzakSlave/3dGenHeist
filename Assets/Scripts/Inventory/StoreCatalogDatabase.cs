using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShopCategory
{
    All = 0,
    Tools = 1,
    Weapons = 2,
    Utility = 3,
    Consumables = 4
}

[Serializable]
public class StoreCatalogEntry
{
    public ItemData itemData;
    public int price = 500;
    public int maxCapacity = 4;
    public ShopCategory category = ShopCategory.Tools;
    public bool requiresLicense = false;
    public bool isDonExclusive = false;
    [TextArea(2, 5)] public string description = "";
}

[CreateAssetMenu(fileName = "StoreCatalogDatabase", menuName = "Heist/Store Catalog Database")]
public class StoreCatalogDatabase : ScriptableObject
{
    public List<StoreCatalogEntry> items = new List<StoreCatalogEntry>();

    public StoreCatalogEntry GetEntry(ItemData data)
    {
        if (data == null || items == null) return null;
        return items.Find(e => e != null && e.itemData == data);
    }

    public List<StoreCatalogEntry> GetCategoryItems(ShopCategory cat)
    {
        if (items == null) return new List<StoreCatalogEntry>();
        if (cat == ShopCategory.All) return new List<StoreCatalogEntry>(items);
        return items.FindAll(e => e != null && e.category == cat);
    }
}
