using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DossierSelectionUI : MonoBehaviour
{
    public static DossierSelectionUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject dossierWindowPanel;
    public Transform dossierCardsContainer;
    public GameObject dossierCardPrefab;
    public DossierCardUI[] fixedDossierCards; // Опционально: 2 фиксированных карточки на UI

    [Header("Buttons & Status")]
    public Button confirmSelectionButton;
    public Button closeButton;
    public TextMeshProUGUI statusText;

    [Header("Input Reader")]
    public InputReader inputReader;

    private BankDossier currentlySelectedDossier;
    private bool isDossierOpen = false;
    public bool IsDossierOpen => isDossierOpen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (confirmSelectionButton != null) confirmSelectionButton.onClick.AddListener(OnConfirmButtonClicked);
        if (closeButton != null) closeButton.onClick.AddListener(CloseUI);

        if (dossierWindowPanel != null) dossierWindowPanel.SetActive(false);
    }

    private void Update()
    {
        if (isDossierOpen && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseUI();
        }
    }

    public void OpenUI()
    {
        isDossierOpen = true;
        if (dossierWindowPanel != null) dossierWindowPanel.SetActive(true);

        if (inputReader != null) inputReader.DisableAllActions();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentlySelectedDossier = GameManager.Instance != null ? GameManager.Instance.selectedDossier : null;

        RefreshDossiers();
    }

    public void CloseUI()
    {
        isDossierOpen = false;
        if (dossierWindowPanel != null) dossierWindowPanel.SetActive(false);

        if (inputReader != null) inputReader.EnableAllActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RefreshDossiers()
    {
        if (GameManager.Instance == null || GameManager.Instance.bankDossiers == null) return;

        BankDossier[] dossiers = GameManager.Instance.bankDossiers;

        // Вариант 1: Заполнение 2 фиксированных карточек на экране
        if (fixedDossierCards != null && fixedDossierCards.Length > 0)
        {
            for (int i = 0; i < fixedDossierCards.Length; i++)
            {
                if (fixedDossierCards[i] == null) continue;

                if (i < dossiers.Length && dossiers[i] != null)
                {
                    fixedDossierCards[i].gameObject.SetActive(true);
                    bool isSelected = dossiers[i] == currentlySelectedDossier;
                    fixedDossierCards[i].Setup(dossiers[i], i, this, isSelected);
                }
                else
                {
                    fixedDossierCards[i].gameObject.SetActive(false);
                }
            }
        }
        // Вариант 2: Динамический спавн в контейнере
        else if (dossierCardsContainer != null && dossierCardPrefab != null)
        {
            foreach (Transform child in dossierCardsContainer)
            {
                Destroy(child.gameObject);
            }

            for (int i = 0; i < dossiers.Length; i++)
            {
                if (dossiers[i] == null) continue;
                GameObject cardObj = Instantiate(dossierCardPrefab, dossierCardsContainer);
                DossierCardUI card = cardObj.GetComponent<DossierCardUI>();
                if (card != null)
                {
                    bool isSelected = dossiers[i] == currentlySelectedDossier;
                    card.Setup(dossiers[i], i, this, isSelected);
                }
            }
        }

        UpdateStatusText();
    }

    public void SelectDossier(BankDossier dossier)
    {
        currentlySelectedDossier = dossier;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SelectDossier(dossier);
        }

        RefreshDossiers();
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            if (currentlySelectedDossier != null)
            {
                statusText.text = $"<color=green>Выбран банк: {currentlySelectedDossier.bankName}</color>";
            }
            else
            {
                statusText.text = "<color=yellow>Выберите досье для ограбления</color>";
            }
        }

        if (confirmSelectionButton != null)
        {
            confirmSelectionButton.interactable = currentlySelectedDossier != null;
        }
    }

    private void OnConfirmButtonClicked()
    {
        if (currentlySelectedDossier == null) return;

        Debug.Log($"[DossierUI] Досье '{currentlySelectedDossier.bankName}' подтверждено!");
        CloseUI();
    }
}
