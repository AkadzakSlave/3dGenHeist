using UnityEngine;

public enum ItemType 
{ 
    Tool, // Слот 1 (Молот, Сумка)
    Weapon // Слот 2 (Оружие)
}

[CreateAssetMenu(fileName = "New Item Data", menuName = "Heist/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public ItemType itemType;
    public Sprite icon;
    public int baseWeight = 5;
    
    [Tooltip("Префаб, который появится на полу, если игрок нажмет выбросить (G)")]
    public GameObject dropPrefab;
}
