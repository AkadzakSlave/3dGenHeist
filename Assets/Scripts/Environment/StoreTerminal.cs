using UnityEngine;

public class StoreTerminal : MonoBehaviour, IInteractable
{
    [Header("Terminal Configuration")]
    public string terminalTitle = "Оружейная и склад (Магазин / Загрузка)";

    public void Interact()
    {
        if (StoreUI.Instance != null)
        {
            StoreUI.Instance.OpenStore();
        }
        else
        {
            StoreUI store = FindAnyObjectByType<StoreUI>(FindObjectsInactive.Include);
            if (store != null)
            {
                store.OpenStore();
            }
            else
            {
                Debug.LogError("[StoreTerminal] StoreUI не найден на сцене!");
            }
        }
    }

    public string GetInteractText()
    {
        return $"[E] {terminalTitle}";
    }
}
