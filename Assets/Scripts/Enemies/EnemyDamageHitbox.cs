using UnityEngine;

/// <summary>
/// Тип части тела хитбокса (тело или голова для множителя урона).
/// </summary>
public enum EnemyBodyPart
{
    Body,
    Head
}

/// <summary>
/// Дочерний хитбокс увеличенного размера для комфортного попадания игрока по врагу.
/// Поддерживает разделение на тело и голову с множителем критического урона (хэдшот 1.5x).
/// </summary>
public class EnemyDamageHitbox : MonoBehaviour
{
    [Tooltip("Часть тела: Head получает x1.5 урона")]
    public EnemyBodyPart bodyPart = EnemyBodyPart.Body;
    [Tooltip("Множитель урона при попадании в эту зону")]
    public float damageMultiplier = 1f;

    private EnemyHealth parentHealth;
    private EnemyController parentController;

    private void Awake()
    {
        parentHealth = GetComponentInParent<EnemyHealth>();
        parentController = GetComponentInParent<EnemyController>();
    }

    public bool IsHeadshot => bodyPart == EnemyBodyPart.Head;

    /// <summary>
    /// Принимает урон с расчетом зоны попадания (множитель x1.5 для головы).
    /// Возвращает итоговый нанесенный урон.
    /// </summary>
    public int TakeDamageWithHitbox(int baseDamage)
    {
        int finalDamage = Mathf.RoundToInt(baseDamage * damageMultiplier);
        if (parentHealth != null)
        {
            parentHealth.TakeDamage(finalDamage);
        }
        return finalDamage;
    }

    public void TakeDamage(int damage)
    {
        TakeDamageWithHitbox(damage);
    }

    public EnemyHealth ParentHealth => parentHealth != null ? parentHealth : (parentHealth = GetComponentInParent<EnemyHealth>());
    public EnemyController ParentController => parentController != null ? parentController : (parentController = GetComponentInParent<EnemyController>());
}

