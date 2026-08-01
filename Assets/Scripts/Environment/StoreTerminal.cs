using UnityEngine;

public class StoreTerminal : MonoBehaviour, IInteractable
{
    [Header("Terminal Configuration")]
    public string terminalTitle = "Team Store & Equipment Storage";

    public void Interact()
    {
        if (StoreUI.Instance != null)
        {
            StoreUI.Instance.OpenStore();
        }
        else
        {
            Debug.LogError("[StoreTerminal] StoreUI.Instance не найден на сцене!");
        }
    }

    public string GetInteractText()
    {
        return $"[E] {terminalTitle}";
    }
}
