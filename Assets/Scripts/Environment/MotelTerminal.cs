using UnityEngine;

public class MotelTerminal : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ProceedToHeist();
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
