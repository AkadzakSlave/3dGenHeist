using UnityEngine;

public abstract class EquipableItem : MonoBehaviour
{
    [Tooltip("Данные предмета (название, вес, иконка)")]
    public ItemData itemData;

    // Вызывается Менеджером Инвентаря, когда игрок берет предмет в руки
    public abstract void Equip();

    // Вызывается Менеджером Инвентаря, когда игрок прячет предмет
    public abstract void Unequip();

    // Воспроизводит основное действие (ЛКМ) — Удар молотом или стрельба
    public abstract void PrimaryAction();

    // Получить вес предмета (для Сумки он будет зависеть от лута внутри)
    public virtual int GetTotalWeight()
    {
        if (itemData != null)
        {
            return itemData.baseWeight;
        }
        return 0;
    }
}
