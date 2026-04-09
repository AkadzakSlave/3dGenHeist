using UnityEngine;

public class LootItem : MonoBehaviour
{
    public string itemName = "Money Bag";
    public int value = 100;
    
    [Header("Audio")]
    public AudioClip collectSound;

    public void Collect()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddMoneyToBag(value);
        }

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        Debug.Log($"[Loot] Собрано: {itemName} за ${value}");
        Destroy(gameObject);
    }
}
