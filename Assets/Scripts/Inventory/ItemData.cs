using UnityEngine;

public enum ItemType 
{ 
    Tool = 0,      // Слот 1 (Молот, Сумка)
    Weapon = 1,    // Слот 2 (Оружие)
    Utility = 2    // Слот 3 (Утилиты: Динамит, Гранаты и т.д.)
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

    [Header("Drop Customize Options")]
    [Tooltip("Дополнительное локальное смещение позиции при выбросе")]
    public Vector3 dropPositionOffset = Vector3.zero;

    [Tooltip("Дополнительный поворот при выбросе")]
    public Vector3 dropRotationOffset = Vector3.zero;
}
