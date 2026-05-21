using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PatrolTier
{
    [Tooltip("Начало диапазона Уровня патруля (в % от 0 до 100)")]
    public float minPatrolLevel;
    [Tooltip("Конец диапазона Уровня патруля (в % от 0 до 100)")]
    public float maxPatrolLevel;
    
    [Header("Количество врагов")]
    public int minGuards;
    public int maxGuards;
    
    public int minAssaulters;
    public int maxAssaulters;
    
    public int minElites;
    public int maxElites;
}

[CreateAssetMenu(fileName = "New Patrol Database", menuName = "Heist/Enemies/Patrol Database")]
public class PatrolDatabase : ScriptableObject
{
    [Tooltip("Шанс появления патруля каждую минуту (в долях от 0 до 1)")]
    public float baseSpawnChance = 0.05f;
    
    [Tooltip("Увеличение Уровня патруля каждую минуту (в % от 0 до 100)")]
    public float patrolLevelIncreasePerMinute = 3f;

    [Tooltip("Таблица спавна патрулей (Легенда)")]
    public List<PatrolTier> patrolTiers = new List<PatrolTier>();
    
    // Получение тира на основе текущего процента
    public PatrolTier GetTierForLevel(float patrolLevel)
    {
        foreach (var tier in patrolTiers)
        {
            if (patrolLevel >= tier.minPatrolLevel && patrolLevel < tier.maxPatrolLevel)
            {
                return tier;
            }
        }
        
        // По умолчанию возвращаем последний тир, если уровень выше максимального
        if (patrolTiers.Count > 0)
        {
            return patrolTiers[patrolTiers.Count - 1];
        }
        
        return new PatrolTier();
    }
}
