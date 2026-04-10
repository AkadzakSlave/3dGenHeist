using UnityEngine;

public class DestructibleWall : MonoBehaviour
{
    public int health = 3;

    [Header("Audio Setup")]
    public AudioClip destroySound;

    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log($"Стена получила {damage} урона. Осталось HP: {health}");
        
        if (health <= 0)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartHeist();
            }
            BreakWall();
        }
    }

    private void BreakWall()
    {
        Debug.Log("Стена разрушена!");
        
        if (destroySound != null)
        {
            // PlayClipAtPoint позволяет звуку доиграть даже после удаления стены
            AudioSource.PlayClipAtPoint(destroySound, transform.position);
        }

        Destroy(gameObject);
    }
}
