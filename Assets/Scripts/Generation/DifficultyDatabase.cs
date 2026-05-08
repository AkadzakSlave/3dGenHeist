using UnityEngine;

[CreateAssetMenu(fileName = "DifficultyDatabase", menuName = "Heist/Difficulty Database")]
public class DifficultyDatabase : ScriptableObject
{
    public DifficultyPreset[] difficultyPresets; // Массив из 10 пресетов

    public DifficultyPreset GetDifficulty(int level)
    {
        // Уровни 1-10 превращаем в индекс 0-9
        int index = Mathf.Clamp(level - 1, 0, difficultyPresets.Length - 1);
        return difficultyPresets[index];
    }
}
