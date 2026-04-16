using UnityEngine;

public class LobbyTerminal : MonoBehaviour, IInteractable
{
    [Header("Map Configuration")]
    [Tooltip("The map preset to load when interacting with this console.")]
    public LevelPreset targetPreset;

    public void Interact()
    {
        if (GameManager.Instance != null && targetPreset != null)
        {
            Debug.Log($"[Lobby] Запуск ограбления: {targetPreset.levelName}");
            // Call GameManager to handle the transition
            GameManager.Instance.StartLoadingHeist(targetPreset);
        }
        else
        {
            Debug.LogError("[Lobby] GameManager или TargetPreset не назначены!");
        }
    }

    public string GetInteractText()
    {
        return targetPreset != null ? $"Launch: {targetPreset.levelName}" : "Launch Heist";
    }
}
