using UnityEngine;

public class DossierTableTerminal : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    public string interactPrompt = "Выбрать досье банка";

    public void Interact()
    {
        if (DossierSelectionUI.Instance != null)
        {
            DossierSelectionUI.Instance.OpenUI();
        }
        else
        {
            Debug.LogWarning("[DossierTableTerminal] DossierSelectionUI Instance not found!");
        }
    }

    public string GetInteractText()
    {
        return interactPrompt;
    }
}
