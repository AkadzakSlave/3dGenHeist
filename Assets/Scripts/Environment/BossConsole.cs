using UnityEngine;

public class BossConsole : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (BossRoomManager.Instance != null)
        {
            BossRoomManager.Instance.CheckConsoleInteraction();
        }
        else
        {
            Debug.LogError("[BossConsole] BossRoomManager не найден на сцене!");
        }
    }

    public string GetInteractText()
    {
        if (GameManager.Instance != null)
        {
            return $"Pay Boss Quota (${GameManager.Instance.operationTargetQuota})";
        }
        return "Pay Boss";
    }
}
