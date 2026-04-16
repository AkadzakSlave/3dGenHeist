using UnityEngine;

public class LootItem : MonoBehaviour, IInteractable
{
    public string itemName = "Cash Bundle";
    public int value = 100;
    public int weight = 5;
    
    [Header("Audio")]
    public AudioClip collectSound;

    public void Interact()
    {
        Collect();
    }

    public string GetInteractText()
    {
        return $"Collect {itemName} (${value})";
    }

    public void Collect()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoneyToBag(value, weight);
        }

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        Debug.Log($"[Loot] Собрано: {itemName} за ${value}");
        Destroy(gameObject);
    }
}
