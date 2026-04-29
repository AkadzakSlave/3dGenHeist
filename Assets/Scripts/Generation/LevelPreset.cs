using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Level Preset", menuName = "Heist/Level Preset")]
public class LevelPreset : ScriptableObject
{
    [Header("Preset Information")]
    public string levelName = "Texas";
    public List<string> cities = new List<string> { "Austin", "Dallas", "Houston" };
    
    [Header("Generation Settings")]
    public int minRooms = 10;
    public int maxRooms = 20;

    [Header("Economy & Progression")]
    public int entryFee = 0;
    public List<LevelPreset> unlockPresets;

    [Header("Difficulty Range")]
    public int minDifficulty = 1;
    public int maxDifficulty = 3;

    [Header("Visual Architecture Prefabs")]
    [Tooltip("The starting room where the heist begins")]
    public RoomTemplate startRoom;
    [Tooltip("The extraction van zone prefab for this layout")]
    public GameObject vanZonePrefab;
    [Tooltip("List of all available rooms to generate for this theme")]
    public List<RoomTemplate> availableRooms;
}
