using UnityEngine;

public class MapTerminal : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    public string interactPrompt = "Открыть карту штатов";

    public void Interact()
    {
        if (MapSelectionUI.Instance != null)
        {
            MapSelectionUI.Instance.OpenMap();
        }
        else
        {
            Debug.LogWarning("[MapTerminal] MapSelectionUI Instance not found!");
        }
    }

    public string GetInteractText()
    {
        return interactPrompt;
    }
}
