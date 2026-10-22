using UnityEngine;

[CreateAssetMenu(fileName = "New Loot Data", menuName = "Heist/Loot Data")]
public class LootData : ScriptableObject
{
    public string itemName = "Loot";
    public LootType lootType;
    public int minValue = 100;
    public int maxValue = 200;
    public int weight = 5;
    
    [Header("Rarity Settings")]
    [Tooltip("Вес выпадения в процентах/долях (больше = чаще выпадает)")]
    [Range(1, 100)] public int dropWeight = 50;

    [Header("Visuals")]
    public GameObject prefab; // Визуал предмета
}
