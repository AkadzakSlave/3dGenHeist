using UnityEngine;
using FMODUnity;

public class FootstepOnMove : MonoBehaviour
{
    [SerializeField] private EventReference footstepEvent;  // Ваше событие с шагами
    [SerializeField] private float stepInterval = 0.5f;     // Интервал между шагами (0.5 сек = быстрая ходьба)
    
    private Vector3 lastPosition;   // Где мы были в прошлый раз
    private float stepTimer;         // Таймер до следующего шага
    
    void Start()
    {
        // Запоминаем начальную позицию
        lastPosition = transform.position;
    }
    
    void Update()
    {
        // Проверяем, сдвинулся ли персонаж с места
        if (transform.position != lastPosition)
        {
            // Двигаемся! Уменьшаем таймер
            stepTimer -= Time.deltaTime;
            
            // Если таймер закончился — время делать шаг
            if (stepTimer <= 0)
            {
                PlayFootstep();
                stepTimer = stepInterval;  // Сбрасываем таймер
            }
        }
        
        // Запоминаем текущую позицию для следующего кадра
        lastPosition = transform.position;
    }
    
    void PlayFootstep()
    {
        if (!footstepEvent.IsNull)
        {
            // Проигрываем звук в позиции персонажа
            RuntimeManager.PlayOneShot(footstepEvent, transform.position);
            // Эта строчка поможет понять, что звук срабатывает
            Debug.Log("Шаг: " + Time.time); 
        }
        else
        {
            Debug.LogWarning("Footstep event не назначен!");
        }
    }
}