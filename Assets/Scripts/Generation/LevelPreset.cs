using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CityConfig
{
    public string cityName;
    public int minDifficulty; // 1-10
    public int maxDifficulty; // 1-10
}

[CreateAssetMenu(fileName = "New Level Preset", menuName = "Heist/Level Preset")]
public class LevelPreset : ScriptableObject
{
    [Header("Preset Information")]
    public string levelName = "Texas";
    public List<CityConfig> cities = new List<CityConfig>();
    public List<string> bankNamesPool = new List<string> { "Zloop Bank", "Mamut Raxal", "Go Ven Iy", "Iron Vault" };

    [Header("Economy & Progression")]
    public int entryFee = 0;
    public List<LevelPreset> unlockPresets;

    [Header("Visual Architecture Prefabs")]
    [Tooltip("The starting room where the heist begins")]
    public RoomTemplate startRoom;
    [Tooltip("The extraction van zone prefab for this layout")]
    public GameObject vanZonePrefab;
    [Tooltip("List of all available rooms to generate for this theme")]
    public List<RoomTemplate> availableRooms;
}
