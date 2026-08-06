using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapSelectionUI : MonoBehaviour
{
    public static MapSelectionUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject mapWindowPanel;
    public Transform stateCardsContainer;
    public GameObject stateCardPrefab;

    [Header("Side Details Panel")]
    public TextMeshProUGUI stateTitleText;
    public TextMeshProUGUI stateDescriptionText;
    public TextMeshProUGUI stateCitiesText;
    public TextMeshProUGUI entryFeeText;
    public TextMeshProUGUI playerBalanceText;
    public TextMeshProUGUI unlocksNextStatesText;
    public TextMeshProUGUI warningText;
    public Button confirmTravelButton;
    public Button closeButton;

    [Header("Interactive Map Nodes (On Map Image)")]
    public Transform mapNodesParent;
    public List<MapStateNodeUI> mapNodes = new List<MapStateNodeUI>();

    [Header("Presets Data")]
    public List<LevelPreset> availablePresets = new List<LevelPreset>();

    [Header("Input Reader")]
    public InputReader inputReader;

    private LevelPreset currentlySelectedPreset;
    private bool isMapOpen = false;
    public bool IsMapOpen => isMapOpen;

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
        if (confirmTravelButton != null) confirmTravelButton.onClick.AddListener(OnConfirmTravelClicked);
        if (closeButton != null) closeButton.onClick.AddListener(CloseMap);

        if (mapWindowPanel != null) mapWindowPanel.SetActive(false);
    }

    private void Update()
    {
        if (isMapOpen && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMap();
        }
    }

    public void OpenMap()
    {
        isMapOpen = true;
        if (mapWindowPanel != null) mapWindowPanel.SetActive(true);

        if (inputReader != null) inputReader.DisableAllActions();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PopulateStateCards();

        if (availablePresets != null && availablePresets.Count > 0)
        {
            SelectState(availablePresets[0]);
        }
        else if (GameManager.Instance != null && GameManager.Instance.activeOperationPreset != null)
        {
            SelectState(GameManager.Instance.activeOperationPreset);
        }
    }

    public void CloseMap()
    {
        isMapOpen = false;
        if (mapWindowPanel != null) mapWindowPanel.SetActive(false);

        if (inputReader != null) inputReader.EnableAllActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void PopulateStateCards()
    {
        if (stateCardsContainer == null || stateCardPrefab == null) return;

        foreach (Transform child in stateCardsContainer)
        {
            Destroy(child.gameObject);
        }

        List<LevelPreset> presetsToDisplay = new List<LevelPreset>();
        if (GameManager.Instance != null && GameManager.Instance.unlockedPresets != null && GameManager.Instance.unlockedPresets.Count > 0)
        {
            presetsToDisplay = GameManager.Instance.unlockedPresets;
        }
        else
        {
            presetsToDisplay = availablePresets;
        }

        foreach (var preset in presetsToDisplay)
        {
            if (preset == null) continue;
            GameObject cardObj = Instantiate(stateCardPrefab, stateCardsContainer);
            MapStateCardUI card = cardObj.GetComponent<MapStateCardUI>();
            if (card != null)
            {
                bool isSelected = preset == currentlySelectedPreset;
                card.Setup(preset, this, isSelected);
            }
        }
    }

    public void SelectState(LevelPreset preset)
    {
        currentlySelectedPreset = preset;
        UpdateDetailsPanel();
        PopulateStateCards();
        RefreshMapNodes();
    }

    public void RefreshMapNodes()
    {
        if (mapNodesParent != null && (mapNodes == null || mapNodes.Count == 0))
        {
            mapNodes.AddRange(mapNodesParent.GetComponentsInChildren<MapStateNodeUI>(true));
        }

        foreach (var node in mapNodes)
        {
            if (node == null) continue;
            node.InitNode(this);
            bool isSelected = node.statePreset != null && node.statePreset == currentlySelectedPreset;
            node.SetSelected(isSelected);
        }
    }

    private void UpdateDetailsPanel()
    {
        if (currentlySelectedPreset == null) return;

        int playerBalance = GameManager.Instance != null ? GameManager.Instance.globalBankBalance : 0;

        if (stateTitleText != null) stateTitleText.text = currentlySelectedPreset.levelName;
        if (stateDescriptionText != null) stateDescriptionText.text = currentlySelectedPreset.stateDescription;

        // Cities & Difficulty text
        if (stateCitiesText != null)
        {
            string citiesInfo = "Города штата:\n";
            if (currentlySelectedPreset.cities != null && currentlySelectedPreset.cities.Count > 0)
            {
                foreach (var city in currentlySelectedPreset.cities)
                {
                    citiesInfo += $"• {city.cityName} (Сложность: {city.minDifficulty}-{city.maxDifficulty})\n";
                }
            }
            else
            {
                citiesInfo += "• Стандартные города (Сложность: 1-3)";
            }
            stateCitiesText.text = citiesInfo;
        }

        // Fee & Balance
        if (entryFeeText != null) entryFeeText.text = $"Стоимость перелета: ${currentlySelectedPreset.entryFee}";
        if (playerBalanceText != null) playerBalanceText.text = $"Ваш баланс: ${playerBalance}";

        // Unlocks info
        if (unlocksNextStatesText != null)
        {
            if (currentlySelectedPreset.unlockPresets != null && currentlySelectedPreset.unlockPresets.Count > 0)
            {
                string unlockInfo = "Открывает при прохождении:\n";
                foreach (var nextPreset in currentlySelectedPreset.unlockPresets)
                {
                    if (nextPreset != null)
                    {
                        unlockInfo += $"• {nextPreset.levelName}\n";
                    }
                }
                unlocksNextStatesText.text = unlockInfo;
                unlocksNextStatesText.gameObject.SetActive(true);
            }
            else
            {
                unlocksNextStatesText.gameObject.SetActive(false);
            }
        }

        // Check if state is unlocked in GameManager
        bool isUnlocked = true;
        if (GameManager.Instance != null && GameManager.Instance.unlockedPresets != null && GameManager.Instance.unlockedPresets.Count > 0)
        {
            isUnlocked = GameManager.Instance.unlockedPresets.Contains(currentlySelectedPreset);
        }

        // Can afford & unlock check
        bool canAfford = playerBalance >= currentlySelectedPreset.entryFee;
        bool canTravel = isUnlocked && canAfford;

        if (confirmTravelButton != null)
        {
            confirmTravelButton.interactable = canTravel;
        }

        if (warningText != null)
        {
            if (!isUnlocked)
            {
                warningText.text = "<color=red>🔒 Штат заблокирован! Пройдите предыдущий штат.</color>";
            }
            else if (canAfford)
            {
                warningText.text = "<color=green>Средств достаточно для въезда!</color>";
            }
            else
            {
                warningText.text = $"<color=red>Недостаточно средств! Нужно ещё ${currentlySelectedPreset.entryFee - playerBalance}</color>";
            }
        }
    }

    private void OnConfirmTravelClicked()
    {
        if (currentlySelectedPreset == null) return;

        if (GameManager.Instance != null)
        {
            Debug.Log($"[MapUI] Подтвержден перелет в штат: {currentlySelectedPreset.levelName}");
            CloseMap();
            GameManager.Instance.StartOperation(currentlySelectedPreset);
        }
    }
}
