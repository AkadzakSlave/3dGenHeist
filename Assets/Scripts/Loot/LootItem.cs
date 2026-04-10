using UnityEngine;

public class LootItem : MonoBehaviour
{
    public string itemName = "Cash Bundle";
    public int value = 100;
    public int weight = 5;
    
    [Header("Audio")]
    public AudioClip collectSound;

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
