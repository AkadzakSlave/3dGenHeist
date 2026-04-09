using UnityEngine;

public enum RoomCategory
{
    Normal,
    Vault,
    Security,
    VIP,
    Street
}

[CreateAssetMenu(fileName = "New Room Template", menuName = "Heist/Room Template")]
public class RoomTemplate : ScriptableObject
{
    [Tooltip("Room prefab with RoomSocket components")]
    public GameObject prefab;
    
    [Tooltip("Room category for spawn limits")]
    public RoomCategory category = RoomCategory.Normal;

    [Tooltip("Spawn weight (10 = common, 1 = rare)")]
    public float weight = 10f;
}
