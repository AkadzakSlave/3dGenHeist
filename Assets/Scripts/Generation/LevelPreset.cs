using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Level Preset", menuName = "Heist/Level Preset")]
public class LevelPreset : ScriptableObject
{
    [Header("Preset Information")]
    public string levelName = "Commercial Bank";

    [Header("Visual Architecture Prefabs")]
    [Tooltip("The starting room where the heist begins")]
    public RoomTemplate startRoom;
    [Tooltip("The extraction van zone prefab for this layout")]
    public GameObject vanZonePrefab;
    [Tooltip("List of all available rooms to generate for this theme")]
    public List<RoomTemplate> availableRooms;
}
