using UnityEngine;

public enum EnemyType
{
    Guard,
    Assaulter,
    Elite
}

[CreateAssetMenu(fileName = "New Enemy Data", menuName = "Heist/Enemies/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public EnemyType type;
    public string enemyName = "Enemy";
    public GameObject prefab;
    
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float angularSpeed = 120f;
    public float acceleration = 8f;

    [Header("Health")]
    public int maxHealth = 100;
    
    [Header("AI")]
    public float detectionRadius = 15f;

    [Header("Combat & Weapon")]
    public int damagePerShot = 10;
    public float attackRange = 14f;
    public float fireInterval = 0.2f;
    public int magazineSize = 10;
    public float reloadTime = 2.5f;
    public int burstCount = 3;
    public float burstPause = 1.2f;
    [Range(0f, 1f)] public float hitChance = 0.75f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Audio (FMOD)")]
    public FMODUnity.EventReference fireEvent;
}
