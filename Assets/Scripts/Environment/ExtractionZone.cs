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
        if (missionComplete) return;

        // Проверяем, что в триггер попал игрок (можно использовать тег "Player")
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.DepositBag();

                if (GameManager.Instance.IsQuotaMet())
                {
                    missionComplete = true;
                    // Сообщение о победе уже выводится внутри GameManager, но можно вызвать и тут
                    onMissionSuccess?.Invoke();
                }
                else
                {
                    Debug.Log("[Extraction] План не выполнен! Собери еще денег.");
                }
            }
        }
    }
}
