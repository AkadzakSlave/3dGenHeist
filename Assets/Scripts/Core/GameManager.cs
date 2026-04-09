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

    public void AddMoneyToBag(int amount)
    {
        bagMoney += amount;
        Debug.Log($"[Economy] Собрано: ${amount}. В сумке теперь: ${bagMoney}");
        onMoneyChanged?.Invoke();
    }

    public void DepositBag()
    {
        if (bagMoney <= 0) return;

        depositedMoney += bagMoney;
        Debug.Log($"[Economy] Деньги сданы! Квота: {depositedMoney} / {targetQuota}");
        bagMoney = 0;
        
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
