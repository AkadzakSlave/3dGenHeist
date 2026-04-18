using UnityEngine;
using UnityEngine.Events;

public class ExtractionZone : MonoBehaviour
{
    [Header("Settings")]
    public string vanName = "Van";
    public UnityEvent onMissionSuccess;

    private bool missionComplete = false;

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что в триггер попал игрок (можно использовать тег "Player")
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            if (GameManager.Instance != null && GameManager.Instance.bagMoney > 0)
            {
                GameManager.Instance.DepositBag();
                // Деньги просто скидываются в фургон (Победа теперь выдается у Босса)
            }
        }
    }
}
