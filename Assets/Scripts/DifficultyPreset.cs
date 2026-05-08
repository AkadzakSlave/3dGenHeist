using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyPreset", menuName = "Heist/Difficulty Preset")]
public class DifficultyPreset : ScriptableObject
{
    [Header("Difficulty")]
    public int difficult = 1;

    [Header("Rooms")]
    public int minRooms;
    public int maxRooms;

    [Header("Timer")]
    public int minTimerSeconds;
    public int maxTimerSeconds;

    [Header("Patrol")]
    public int patrolLevelPercent;

    public int GetRandomTimer()
    {
        int min = Mathf.RoundToInt(minTimerSeconds / 10f);
        int max = Mathf.RoundToInt(maxTimerSeconds / 10f);

        return Random.Range(min, max + 1) * 10;
    }

    public string GetFormattedTimer()
    {
        int seconds = GetRandomTimer();
        int minutes = seconds / 60;
        int remainingSeconds = seconds % 60;

        return $"{minutes:00}:{remainingSeconds:00}";
    }
}