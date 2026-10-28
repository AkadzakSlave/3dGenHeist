using UnityEngine;

public class VanConsole : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (GameManager.Instance != null)
        {
            // Автоматически выгружаем лут из сумки при эвакуации, если игрок еще не выгрузил
            BagTool bag = GameManager.Instance.GetHeldBag();
            if (bag != null && bag.storedMoney > 0)
            {
                GameManager.Instance.DepositBag();
            }

            Debug.Log("[Van] Запущена эвакуация на фургоне!");
            GameManager.Instance.ExtractFromHeist();
        }
    }

    public string GetInteractText()
    {
        if (GameManager.Instance == null) return "Сесть в фургон и уехать";

        int deposited = GameManager.Instance.depositedMoney;
        int quota = GameManager.Instance.operationTargetQuota;

        if (deposited >= quota)
        {
            return $"Сесть в фургон и уехать <color=#55FF55>(Квота выполнена: ${deposited})</color>";
        }
        else
        {
            return $"Сесть в фургон и уехать <color=#FFCC00>(Квота: ${deposited}/${quota})</color>";
        }
    }
}
