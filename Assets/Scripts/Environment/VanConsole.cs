using UnityEngine;

public class VanConsole : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (GameManager.Instance != null)
        {
            Debug.Log("[Van] Запущена кнопка эвакуации!");
            GameManager.Instance.ExtractFromHeist();
        }
    }

    public string GetInteractText()
    {
        return "Drive Away (Return to Lobby)";
    }
}
