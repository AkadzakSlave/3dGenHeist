using UnityEngine;

public class BossConsole : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (GameManager.Instance != null)
        {
            Debug.Log("[Boss] Оплата квоты...");
            GameManager.Instance.PayBoss();
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
