using UnityEngine;

public class HeistExtractionZone : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public string zoneName = "Truck Cargo Area";
    public Color debugColor = Color.green;

    [Header("Hold Unload Settings")]
    [Tooltip("Длительность удержания кнопки E для разгрузки сумки в фургон (в секундах)")]
    public float holdDuration = 2.5f;
    private float currentHoldTimer = 0f;
    private bool isPlayerInside = false;

    [Header("3D World Display")]
    [Tooltip("3D надпись на багажнике фургона с суммой загруженного лута")]
    public TextMesh cargo3DText;
    public Vector3 textOffset = new Vector3(0f, 1.2f, -0.6f);

    private void Start()
    {
        Ensure3DText();
        UpdateCargoDisplay();

        if (GameManager.Instance != null && GameManager.Instance.onMoneyChanged != null)
        {
            GameManager.Instance.onMoneyChanged.AddListener(UpdateCargoDisplay);
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null && GameManager.Instance.onMoneyChanged != null)
        {
            GameManager.Instance.onMoneyChanged.RemoveListener(UpdateCargoDisplay);
        }
    }

    private void Update()
    {
        // Проверяем удержание кнопки E, когда игрок рядом с багажником
        HandleHoldInteraction();
    }

    private void HandleHoldInteraction()
    {
        if (GameManager.Instance == null) return;

        BagTool heldBag = GameManager.Instance.GetHeldBag();
        bool hasMoneyInBag = heldBag != null && heldBag.storedMoney > 0;

        // Если игрок держит клавишу взаимодействия
        bool isEKeyPressed = false;
        
        // 1. Проверяем через InputReader, если он есть
        var inputReader = FindAnyObjectByType<InputReader>();
        if (inputReader != null && inputReader.IsInteractPressed)
        {
            isEKeyPressed = true;
        }
        else if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.isPressed)
        {
            isEKeyPressed = true;
        }

        // Удерживать можно, если игрок внутри зоны триггера фургона
        if (isPlayerInside && hasMoneyInBag && isEKeyPressed)
        {
            currentHoldTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(currentHoldTimer / holdDuration);

            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.SetInteractionProgress(progress, $"Выгрузка сумки (${heldBag.storedMoney})");
            }

            if (currentHoldTimer >= holdDuration)
            {
                currentHoldTimer = 0f;
                UnloadHeldBag(heldBag);
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.HideInteractionProgress();
                }
            }
        }
        else
        {
            if (currentHoldTimer > 0f)
            {
                currentHoldTimer = 0f;
                if (HeistUI.Instance != null)
                {
                    HeistUI.Instance.HideInteractionProgress();
                }
            }
        }
    }

    public void Interact()
    {
        // При обычном клике E информируем игрока о необходимости удержания
        if (GameManager.Instance == null) return;
        BagTool heldBag = GameManager.Instance.GetHeldBag();

        if (heldBag != null && heldBag.storedMoney > 0)
        {
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.ShowNotification($"Удерживайте [E] 2 сек для выгрузки ${heldBag.storedMoney}!", new Color(1f, 0.85f, 0.3f, 1f));
            }
        }
    }

    public string GetInteractText()
    {
        if (GameManager.Instance == null) return "Багажник фургона";

        BagTool heldBag = GameManager.Instance.GetHeldBag();
        if (heldBag != null && heldBag.storedMoney > 0)
        {
            return $"[Удерживайте E] Выгрузить сумку (${heldBag.storedMoney})";
        }

        int loaded = GameManager.Instance.depositedMoney;
        int quota = GameManager.Instance.operationTargetQuota;
        return $"Багажник фургона (Загружено: ${loaded} / ${quota})";
    }

    private void UnloadHeldBag(BagTool bag)
    {
        if (bag == null || bag.storedMoney <= 0) return;

        int moneyExtracted = bag.storedMoney;
        GameManager.Instance.depositedMoney += moneyExtracted;

        bag.storedMoney = 0;
        bag.storedWeight = 0;

        GameManager.Instance.onMoneyChanged?.Invoke();

        if (GameManager.Instance.audioSource != null && GameManager.Instance.depositSound != null)
        {
            GameManager.Instance.audioSource.PlayOneShot(GameManager.Instance.depositSound);
        }

        if (HeistUI.Instance != null)
        {
            HeistUI.Instance.ShowNotification($"+${moneyExtracted} Выгружено в фургон!", new Color(0.2f, 1f, 0.4f, 1f));
        }

        UpdateCargoDisplay();
        Debug.Log($"<color=green>[Van] Сумка успешно разгружена! Загружено: +${moneyExtracted}</color>");
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Физическая сумка, брошенная в багажник
        WorldEquipment droppedItem = other.GetComponent<WorldEquipment>();
        if (droppedItem != null && droppedItem.itemData != null)
        {
            if (droppedItem.itemData.itemType == ItemType.Tool && droppedItem.storedMoney > 0)
            {
                ExtractBag(droppedItem);
                return;
            }
        }

        // 2. Игрок зашел в зону кузова
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponentInParent<PlayerMovement>() != null)
        {
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponentInParent<PlayerMovement>() != null)
        {
            isPlayerInside = false;
            currentHoldTimer = 0f;
            if (HeistUI.Instance != null)
            {
                HeistUI.Instance.HideInteractionProgress();
            }
        }
    }

    private void ExtractBag(WorldEquipment bagOnFloor)
    {
        if (GameManager.Instance == null) return;

        int moneyExtracted = bagOnFloor.storedMoney;
        GameManager.Instance.depositedMoney += moneyExtracted;

        bagOnFloor.storedMoney = 0;
        bagOnFloor.storedWeight = 0;

        GameManager.Instance.onMoneyChanged?.Invoke();

        if (GameManager.Instance.audioSource != null && GameManager.Instance.depositSound != null)
        {
            GameManager.Instance.audioSource.PlayOneShot(GameManager.Instance.depositSound);
        }

        if (HeistUI.Instance != null && moneyExtracted > 0)
        {
            HeistUI.Instance.ShowNotification($"+${moneyExtracted} Брошено в фургон!", new Color(0.35f, 0.95f, 0.45f, 1f));
        }

        UpdateCargoDisplay();
    }

    public void UpdateCargoDisplay()
    {
        if (cargo3DText == null) return;

        int deposited = GameManager.Instance != null ? GameManager.Instance.depositedMoney : 0;
        int quota = GameManager.Instance != null ? GameManager.Instance.operationTargetQuota : 3000;

        bool quotaMet = deposited >= quota;
        string colorTag = quotaMet ? "#55FF55" : "#FFD700";

        cargo3DText.text = $"<color={colorTag}>ГРУЗ: ${deposited}</color>\n<size=20>Квота: ${quota}</size>";
    }

    private void Ensure3DText()
    {
        if (cargo3DText == null)
        {
            cargo3DText = GetComponentInChildren<TextMesh>();
        }

        if (cargo3DText == null)
        {
            GameObject textObj = new GameObject("Cargo3DDisplay");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = textOffset;
            textObj.transform.localRotation = Quaternion.Euler(0, 180, 0); // Лицом назад к подходящему игроку

            cargo3DText = textObj.AddComponent<TextMesh>();
            cargo3DText.characterSize = 0.08f;
            cargo3DText.fontSize = 32;
            cargo3DText.alignment = TextAlignment.Center;
            cargo3DText.anchor = TextAnchor.MiddleCenter;
            cargo3DText.fontStyle = FontStyle.Bold;
            cargo3DText.color = new Color(1f, 0.85f, 0.1f, 1f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}
