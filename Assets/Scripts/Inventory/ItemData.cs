using UnityEngine;

public enum ItemType 
{ 
    Tool, // Слот 1 (Молот, Сумка)
    Weapon // Слот 2 (Оружие)
}

public enum MainEquipmentType
{
    Weapon = 0,
    Hammer = 1,
    Bag = 2
}

[CreateAssetMenu(fileName = "New Item Data", menuName = "Heist/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public MainEquipmentType fmodMainType; // Параметр для звуков подбора/выброса
    public Sprite icon;
    public int baseWeight = 5;
    
    [Tooltip("Префаб, который появится на полу, если игрок нажмет выбросить (G)")]
    public GameObject dropPrefab;
}
