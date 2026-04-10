using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Heist Settings")]
    public int targetQuota = 1000;
    
    [Header("Economy Data (Read Only)")]
    public int bagMoney = 0;
    public int depositedMoney = 0;
    public int currentWeight = 0;
    public int maxWeight = 40;

    [Header("Timer Settings")]
    public float heistTimer = 300f; // 5 минут
    public bool isHeistActive = false;
    private int reinforcementCount = 0;
    private bool timerFinished = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip depositSound;

    [Header("Events")]
    public UnityEvent onMoneyChanged;
    public UnityEvent onQuotaMet;

    private bool quotaMetTriggered = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float zeroTimerDelay = 3f;
    private float currentZeroDelay = 0f;

    private void Update()
    {
        if (isHeistActive)
        {
            if (heistTimer > 0)
            {
                heistTimer -= Time.deltaTime;
            }
            else
            {
                heistTimer = 0;
                currentZeroDelay += Time.deltaTime;
                
                if (currentZeroDelay >= zeroTimerDelay)
                {
                    currentZeroDelay = 0;
                    OnTimerReachZero();
                }
            }
        }
    }

    public void StartHeist()
    {
        if (!isHeistActive)
        {
            isHeistActive = true;
            Debug.Log("<color=red>[Alarm] Тишина закончилась! Таймер запущен.</color>");
        }
    }

    private void OnTimerReachZero()
    {
        reinforcementCount++;
        Debug.Log($"<color=red>[Backup] Прибыло подкрепление! (#{reinforcementCount})</color>");
        
        // Перезапускаем таймер на 1 минуту
        heistTimer = 60f;
    }

    public void AddMoneyToBag(int amount, int weight)
    {
        bagMoney += amount;
        currentWeight += weight;
        Debug.Log($"[Economy] Собрано: ${amount} (Вес: {weight}). Всего в сумке: ${bagMoney} (Вес: {currentWeight}/{maxWeight})");
        onMoneyChanged?.Invoke();
    }

    public void DepositBag()
    {
        if (bagMoney <= 0) return;

        depositedMoney += bagMoney;
        Debug.Log($"[Economy] Деньги сданы! Квота: {depositedMoney} / {targetQuota}");
        bagMoney = 0;
        currentWeight = 0;
        
        if (audioSource != null && depositSound != null)
        {
            audioSource.PlayOneShot(depositSound);
        }

        onMoneyChanged?.Invoke();

        if (depositedMoney >= targetQuota && !quotaMetTriggered)
        {
            quotaMetTriggered = true;
            Debug.Log("<color=green>[Победа] Квота выполнена! Живем!</color>");
            onQuotaMet?.Invoke();
        }
    }

    public bool IsQuotaMet()
    {
        return depositedMoney >= targetQuota;
    }
}
