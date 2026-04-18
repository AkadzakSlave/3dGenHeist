using UnityEngine;

public class MotelTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentDay < 3)
        {
            Debug.Log($"[Motel] Отправление в город #{GameManager.Instance.currentDay + 1}");
            GameManager.Instance.StartNextDay();
        }
    }

    public string GetInteractText()
    {
        if (GameManager.Instance != null)
        {
            return $"Travel to City {GameManager.Instance.currentDay + 1}/3";
        }
        return "Travel to Next City";
    }
}
