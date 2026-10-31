using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Презентабельный экран отчета перед Доном (расчет квоты, сверхприбыли и начисление денег).
/// Отображает детальный финансовый отчет банды и задерживает игрока до подтверждения.
/// </summary>
public class DonResultUI : MonoBehaviour
{
    public static DonResultUI Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject panel;

    [Header("Text Fields")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI totalEarnedText;
    public TextMeshProUGUI quotaText;
    public TextMeshProUGUI profitText;
    public TextMeshProUGUI sessionMoneyText;
    public TextMeshProUGUI cleanMoneyText;
    public TextMeshProUGUI nextQuotaText;
    public TextMeshProUGUI stampText;

    [Header("Controls")]
    public Button continueButton;
    public TextMeshProUGUI continueButtonText;

    [Header("Animation Settings")]
    public float countStepDelay = 0.35f;
    public float numberLerpDuration = 0.6f;

    [HideInInspector]
    public bool isOpen = false;

    private Coroutine activeAnimRoutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (panel != null) panel.SetActive(false);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(CloseAndGoLobby);
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // Быстрое закрытие по Space или Enter
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
            {
                CloseAndGoLobby();
            }
        }
    }

    /// <summary>
    /// Запуск презентабельного отчета с кинематографичной анимацией
    /// </summary>
    public void ShowResults(int total, int quota, int clean, int session, int nextQuota = 0, int quotaNumber = 1, bool isSuccess = true)
    {
        if (panel != null) panel.SetActive(true);
        isOpen = true;

        // Разблокируем курсор для клика
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (continueButton != null)
        {
            continueButton.interactable = false;
        }

        if (activeAnimRoutine != null) StopCoroutine(activeAnimRoutine);
        activeAnimRoutine = StartCoroutine(AnimateResultsRoutine(total, quota, clean, session, nextQuota, quotaNumber, isSuccess));
    }

    private IEnumerator AnimateResultsRoutine(int total, int quota, int clean, int session, int nextQuota, int quotaNumber, bool isSuccess)
    {
        int profit = total - quota;

        if (titleText != null)
        {
            titleText.text = "ФИНАНСОВЫЙ ОТЧЁТ ПЕРЕД ДОНОМ";
        }

        // Очищаем строки перед анимацией
        if (totalEarnedText != null) totalEarnedText.text = "Добыто в банках: $0";
        if (quotaText != null) quotaText.text = $"Доля Дона (Квота #{quotaNumber}): -$0";
        if (profitText != null) profitText.text = "Чистая прибыль: $0";
        if (sessionMoneyText != null) sessionMoneyText.text = "В общак убежища (90%): +$0";
        if (cleanMoneyText != null) cleanMoneyText.text = "На депозит мафии (10%): +$0";
        if (nextQuotaText != null) nextQuotaText.text = "";
        if (stampText != null) stampText.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.2f);

        // 1. Анимация сбора в банках
        yield return StartCoroutine(LerpNumberText(totalEarnedText, "Добыто в банках: ", total, Color.white));
        yield return new WaitForSeconds(countStepDelay);

        // 2. Анимация квоты Дона
        yield return StartCoroutine(LerpNumberText(quotaText, $"Доля Дона (Квота #{quotaNumber}): -", quota, new Color(1f, 0.45f, 0.45f, 1f)));
        yield return new WaitForSeconds(countStepDelay);

        // 3. Чистая прибыль
        if (profit >= 0)
        {
            yield return StartCoroutine(LerpNumberText(profitText, "Чистая прибыль: +", profit, new Color(0.35f, 1f, 0.5f, 1f)));
            yield return new WaitForSeconds(countStepDelay);

            // 4. Распределение денег
            yield return StartCoroutine(LerpNumberText(sessionMoneyText, "В общак убежища (90%): +", session, new Color(1f, 0.88f, 0.35f, 1f)));
            yield return new WaitForSeconds(countStepDelay * 0.7f);

            yield return StartCoroutine(LerpNumberText(cleanMoneyText, "На депозит мафии (10%): +", clean, new Color(0.35f, 0.85f, 1f, 1f)));
            yield return new WaitForSeconds(countStepDelay);

            if (nextQuotaText != null && nextQuota > 0)
            {
                nextQuotaText.text = $"СЛЕДУЮЩАЯ КВОТА #{quotaNumber + 1}: ${nextQuota:N0}";
            }

            // Штамп одобрения Дона
            if (stampText != null)
            {
                stampText.gameObject.SetActive(true);
                stampText.text = "✓ КВОТА ЗАКРЫТА: ДОН ДОВОЛЕН";
                stampText.color = new Color(0.2f, 1f, 0.4f, 0.95f);
            }
        }
        else
        {
            if (profitText != null)
            {
                profitText.text = $"<color=#FF3333>Нехватка средств: -${Mathf.Abs(profit):N0}</color>";
            }
            if (sessionMoneyText != null) sessionMoneyText.text = "<color=#888888>В общак убежища: $0</color>";
            if (cleanMoneyText != null) cleanMoneyText.text = "<color=#888888>На депозит мафии: $0</color>";

            if (stampText != null)
            {
                stampText.gameObject.SetActive(true);
                stampText.text = "✗ НЕУПЛАТА: ПРИГОВОР ВЫНЕСЕН";
                stampText.color = new Color(1f, 0.2f, 0.2f, 0.95f);
            }
        }

        yield return new WaitForSeconds(0.3f);

        // Активируем кнопку продолжения
        if (continueButton != null)
        {
            continueButton.interactable = true;
            if (continueButtonText != null)
            {
                continueButtonText.text = "ВЕРНУТЬСЯ В УБЕЖИЩЕ [ПРОБЕЛ]";
            }
        }
    }

    private IEnumerator LerpNumberText(TextMeshProUGUI txt, string prefix, int targetValue, Color color)
    {
        if (txt == null) yield break;

        txt.color = color;
        float elapsed = 0f;
        while (elapsed < numberLerpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / numberLerpDuration);
            int cur = Mathf.RoundToInt(Mathf.Lerp(0, targetValue, t));
            txt.text = $"{prefix}${cur:N0}";
            yield return null;
        }
        txt.text = $"{prefix}${targetValue:N0}";
    }

    public void CloseAndGoLobby()
    {
        if (!isOpen) return;

        isOpen = false;
        if (panel != null) panel.SetActive(false);

        // Курсор блокируется обратно только если мы в игре/лобби
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
