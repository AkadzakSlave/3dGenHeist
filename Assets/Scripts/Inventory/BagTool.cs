using UnityEngine;

public class BagTool : EquipableItem
{
    [Header("Stored Loot")]
    public int storedMoney = 0;
    public int storedWeight = 0;
    public int maxCapacity = 50; // Максимальный вес лута, который влезет в сумку

    [Header("Visuals")]
    [Tooltip("3D Модель сумки в руках игрока")]
    public GameObject bagMesh;
    public Animator animator;
    public string animatorLayerName = "Bag Layer";

    public override void Equip()
    {
        if (bagMesh != null) bagMesh.SetActive(true);
        if (animator != null)
        {
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 1f);
        }
        Debug.Log($"[Bag] Взята в руки. Внутри: ${storedMoney}, вес: {storedWeight}кг");
    }

    public override void Unequip()
    {
        if (bagMesh != null) bagMesh.SetActive(false);
        if (animator != null)
        {
            int idx = animator.GetLayerIndex(animatorLayerName);
            if (idx != -1) animator.SetLayerWeight(idx, 0f);
        }
        Debug.Log("[Bag] Спрятана.");
    }

    public override void PrimaryAction()
    {
        // Пока что сумкой нельзя ударить (в отличие от молота), 
        // так что нажатие ЛКМ ничего не делает.
        Debug.Log("[Bag] Вы просто держите сумку в руках.");
    }

    // Вес сумки = Базовый вес ткани + Вес лута внутри
    public override int GetTotalWeight()
    {
        return base.GetTotalWeight() + storedWeight;
    }

    public bool AddLoot(int moneyAmt, int weightAmt)
    {
        if (storedWeight + weightAmt > maxCapacity)
        {
            Debug.Log($"[Bag] Сумка полна! Не влезает еще {weightAmt}кг.");
            return false;
        }

        storedMoney += moneyAmt;
        storedWeight += weightAmt;
        Debug.Log($"[Bag] Добавлено лута. Теперь внутри: ${storedMoney}, вес: {storedWeight}кг / {maxCapacity}кг");
        return true;
    }
}
