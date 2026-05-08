using UnityEngine;
using TMPro;
using System.Collections.Generic; // Добавлено для List

public class BankDossier : MonoBehaviour, IInteractable
{
    [Header("Data")]
    public string bankName;
    public int difficultyLevel;
    public int timeLimit;
    public int minRooms;
    public int maxRooms;

    [Header("UI Display")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI infoText; // "Time: 5:00 | Difficulty: 3"
    public GameObject selectionVisual; // Подсветка стола

    private bool isSelected = false;

    public void Setup(CityConfig city, DifficultyDatabase db, List<string> namesPool)
    {
        // 1. Рандомим сложность из диапазона города
        difficultyLevel = Random.Range(city.minDifficulty, city.maxDifficulty + 1);
        
        // 2. Получаем данные из базы сложностей
        DifficultyPreset preset = db.GetDifficulty(difficultyLevel);
        
        // 3. Рандомим имя и статы
        bankName = namesPool[Random.Range(0, namesPool.Count)];
        timeLimit = preset.GetRandomTimer();
        minRooms = preset.minRooms;
        maxRooms = preset.maxRooms;

        // 4. Обновляем визуальную часть (Экран)
        if (nameText != null) nameText.text = bankName;
        if (infoText != null) 
        {
            string formattedTime = $"{(timeLimit / 60):00}:{(timeLimit % 60):00}";
            infoText.text = $"Difficulty: {difficultyLevel}\nTime: {formattedTime}\nRooms: {minRooms}-{maxRooms}";
        }

        Deselect();
    }

    public void Interact()
    {
        GameManager.Instance.SelectDossier(this);
    }

    // Реализация недостающего метода интерфейса IInteractable
    public string GetInteractText()
    {
        return isSelected ? "Selected" : $"Select {bankName}";
    }

    public void Select()
    {
        isSelected = true;
        if (selectionVisual != null) selectionVisual.SetActive(true);
    }

    public void Deselect()
    {
        isSelected = false;
        if (selectionVisual != null) selectionVisual.SetActive(false);
    }
}
