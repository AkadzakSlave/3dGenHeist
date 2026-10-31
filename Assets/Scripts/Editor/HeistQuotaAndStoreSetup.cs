using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

[InitializeOnLoad]
public static class HeistQuotaAndStoreSetup
{
    private static readonly string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Inter SDF.asset";

    static HeistQuotaAndStoreSetup()
    {
        EditorApplication.delayCall += EnsureSetup;
    }

    [MenuItem("Tools/Heist/Setup Quota Don UI and Store Loadout in Scene")]
    public static void EnsureSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || string.IsNullOrEmpty(activeScene.path)) return;

        bool changed = false;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 1. Setup DonResultUI on Canvas
        changed |= EnsureDonResultUI(font);

        // 2. Setup Loadout Bar on StoreUI
        changed |= EnsureStoreLoadoutBar(font);

        // 3. Setup StoreTerminal in Lobby
        changed |= EnsureStoreTerminalInLobby();

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=green>[HeistSetup] Успешно настроены DonResultUI, Store Loadout Bar и StoreTerminal на сцене!</color>");
        }
    }

    private static bool EnsureDonResultUI(TMP_FontAsset font)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return false;

        GameManager gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        DonResultUI donUI = Object.FindAnyObjectByType<DonResultUI>(FindObjectsInactive.Include);

        bool modified = false;

        if (donUI == null)
        {
            // Создаем DonResult_Panel
            GameObject donPanel = new GameObject("DonResult_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            donPanel.transform.SetParent(canvas.transform, false);

            RectTransform rtPanel = donPanel.GetComponent<RectTransform>();
            rtPanel.anchorMin = Vector2.zero;
            rtPanel.anchorMax = Vector2.one;
            rtPanel.offsetMin = Vector2.zero;
            rtPanel.offsetMax = Vector2.zero;

            Image imgBg = donPanel.GetComponent<Image>();
            imgBg.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            imgBg.raycastTarget = true;

            donUI = donPanel.AddComponent<DonResultUI>();
            donUI.panel = donPanel;

            // Центровое диалоговое окно
            GameObject dialog = new GameObject("DialogBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dialog.transform.SetParent(donPanel.transform, false);
            RectTransform rtDialog = dialog.GetComponent<RectTransform>();
            rtDialog.anchorMin = new Vector2(0.5f, 0.5f);
            rtDialog.anchorMax = new Vector2(0.5f, 0.5f);
            rtDialog.sizeDelta = new Vector2(880, 680);
            rtDialog.anchoredPosition = Vector2.zero;

            Image imgDialog = dialog.GetComponent<Image>();
            imgDialog.color = new Color(0.10f, 0.12f, 0.16f, 0.98f);

            // Заголовок
            donUI.titleText = CreateTMP(dialog.transform, "TitleText", "ФИНАНСОВЫЙ ОТЧЁТ ПЕРЕД ДОНОМ", 32, true, new Color(1f, 0.8f, 0.2f), font);
            RectTransform rtTitle = donUI.titleText.rectTransform;
            rtTitle.anchorMin = new Vector2(0.5f, 1f);
            rtTitle.anchorMax = new Vector2(0.5f, 1f);
            rtTitle.anchoredPosition = new Vector2(0, -55);
            rtTitle.sizeDelta = new Vector2(800, 50);
            donUI.titleText.alignment = TextAlignmentOptions.Center;

            // Разделитель
            GameObject line = new GameObject("DividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(dialog.transform, false);
            RectTransform rtLine = line.GetComponent<RectTransform>();
            rtLine.anchorMin = new Vector2(0.5f, 1f);
            rtLine.anchorMax = new Vector2(0.5f, 1f);
            rtLine.anchoredPosition = new Vector2(0, -95);
            rtLine.sizeDelta = new Vector2(760, 2);
            line.GetComponent<Image>().color = new Color(1f, 0.8f, 0.2f, 0.4f);

            // Строки отчёта
            donUI.totalEarnedText = CreateTMP(dialog.transform, "TotalEarnedText", "Добыто в банках: $0", 24, false, Color.white, font);
            SetRowPosition(donUI.totalEarnedText.rectTransform, -140);

            donUI.quotaText = CreateTMP(dialog.transform, "QuotaText", "Доля Дона (Квота #1): -$0", 24, false, new Color(1f, 0.45f, 0.45f), font);
            SetRowPosition(donUI.quotaText.rectTransform, -190);

            donUI.profitText = CreateTMP(dialog.transform, "ProfitText", "Чистая прибыль: $0", 28, true, new Color(0.35f, 1f, 0.5f), font);
            SetRowPosition(donUI.profitText.rectTransform, -245);

            donUI.sessionMoneyText = CreateTMP(dialog.transform, "SessionMoneyText", "В общак убежища (90%): +$0", 22, false, new Color(1f, 0.88f, 0.35f), font);
            SetRowPosition(donUI.sessionMoneyText.rectTransform, -305);

            donUI.cleanMoneyText = CreateTMP(dialog.transform, "CleanMoneyText", "На депозит мафии (10%): +$0", 22, false, new Color(0.35f, 0.85f, 1f), font);
            SetRowPosition(donUI.cleanMoneyText.rectTransform, -350);

            donUI.nextQuotaText = CreateTMP(dialog.transform, "NextQuotaText", "СЛЕДУЮЩАЯ КВОТА: $4,500", 24, true, new Color(1f, 0.7f, 0.2f), font);
            SetRowPosition(donUI.nextQuotaText.rectTransform, -410);

            // Штамп Дона
            donUI.stampText = CreateTMP(dialog.transform, "StampText", "✓ КВОТА ЗАКРЫТА: ДОН ДОВОЛЕН", 30, true, new Color(0.2f, 1f, 0.4f), font);
            RectTransform rtStamp = donUI.stampText.rectTransform;
            rtStamp.anchorMin = new Vector2(0.5f, 1f);
            rtStamp.anchorMax = new Vector2(0.5f, 1f);
            rtStamp.anchoredPosition = new Vector2(0, -475);
            rtStamp.sizeDelta = new Vector2(800, 50);
            donUI.stampText.alignment = TextAlignmentOptions.Center;
            rtStamp.localEulerAngles = new Vector3(0, 0, -4f);

            // Кнопка продолжения
            GameObject btnObj = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(dialog.transform, false);
            RectTransform rtBtn = btnObj.GetComponent<RectTransform>();
            rtBtn.anchorMin = new Vector2(0.5f, 0f);
            rtBtn.anchorMax = new Vector2(0.5f, 0f);
            rtBtn.anchoredPosition = new Vector2(0, 60);
            rtBtn.sizeDelta = new Vector2(460, 55);

            Image imgBtn = btnObj.GetComponent<Image>();
            imgBtn.color = new Color(0.55f, 0.12f, 0.12f);

            donUI.continueButton = btnObj.GetComponent<Button>();
            ColorBlock cb = donUI.continueButton.colors;
            cb.normalColor = new Color(0.55f, 0.12f, 0.12f);
            cb.highlightedColor = new Color(0.75f, 0.18f, 0.18f);
            cb.pressedColor = new Color(0.4f, 0.08f, 0.08f);
            donUI.continueButton.colors = cb;

            donUI.continueButtonText = CreateTMP(btnObj.transform, "ButtonText", "ВЕРНУТЬСЯ В УБЕЖИЩЕ [ПРОБЕЛ]", 20, true, Color.white, font);
            RectTransform rtBtnTxt = donUI.continueButtonText.rectTransform;
            rtBtnTxt.anchorMin = Vector2.zero;
            rtBtnTxt.anchorMax = Vector2.one;
            rtBtnTxt.offsetMin = Vector2.zero;
            rtBtnTxt.offsetMax = Vector2.zero;
            donUI.continueButtonText.alignment = TextAlignmentOptions.Center;

            donPanel.SetActive(false);
            EditorUtility.SetDirty(donUI);
            modified = true;
        }

        if (gm != null && gm.donResultUI != donUI)
        {
            gm.donResultUI = donUI;
            EditorUtility.SetDirty(gm);
            modified = true;
        }

        return modified;
    }

    private static bool EnsureStoreLoadoutBar(TMP_FontAsset font)
    {
        StoreUI store = Object.FindAnyObjectByType<StoreUI>(FindObjectsInactive.Include);
        if (store == null || store.mainStoreWindow == null) return false;

        bool modified = false;
        Transform windowTr = store.mainStoreWindow.transform;
        Transform existingBar = windowTr.Find("LoadoutBar_Panel");

        GameObject barObj;
        if (existingBar != null)
        {
            barObj = existingBar.gameObject;
        }
        else
        {
            barObj = new GameObject("LoadoutBar_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObj.transform.SetParent(windowTr, false);

            RectTransform rt = barObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 15);
            rt.sizeDelta = new Vector2(-60, 95);

            Image img = barObj.GetComponent<Image>();
            img.color = new Color(0.08f, 0.10f, 0.15f, 0.95f);

            // Заголовок панели
            TextMeshProUGUI header = CreateTMP(barObj.transform, "Header", "ТЕКУЩАЯ ЭКИПИРОВКА И ВЕС (ЗАГРУЗКА НА ВЫЛАЗКУ)", 16, true, new Color(1f, 0.85f, 0.35f), font);
            RectTransform rtH = header.rectTransform;
            rtH.anchorMin = new Vector2(0f, 1f);
            rtH.anchorMax = new Vector2(1f, 1f);
            rtH.anchoredPosition = new Vector2(25, -18);
            rtH.sizeDelta = new Vector2(-50, 24);

            // Слоты
            store.loadoutSlot1Text = CreateTMP(barObj.transform, "Slot1Text", "[1] Инструмент: Кувалда", 17, false, Color.white, font);
            SetHorizontalPosition(store.loadoutSlot1Text.rectTransform, 25, -48, 300);

            store.loadoutSlot2Text = CreateTMP(barObj.transform, "Slot2Text", "[2] Оружие: Thompson", 17, false, Color.white, font);
            SetHorizontalPosition(store.loadoutSlot2Text.rectTransform, 335, -48, 300);

            store.loadoutSlot3Text = CreateTMP(barObj.transform, "Slot3Text", "[3] Спецсредство: [Пусто]", 17, false, Color.white, font);
            SetHorizontalPosition(store.loadoutSlot3Text.rectTransform, 645, -48, 300);

            // Патроны и вес
            store.loadoutAmmoText = CreateTMP(barObj.transform, "AmmoText", "Боезапас: Лёгкие 0 | Средние 0 | Дробь 0", 16, false, new Color(0.45f, 0.85f, 1f), font);
            SetHorizontalPosition(store.loadoutAmmoText.rectTransform, 25, -74, 500);

            store.loadoutWeightText = CreateTMP(barObj.transform, "WeightText", "Вес: 0 / 40 кг (Свободно: 40 кг)", 16, true, new Color(1f, 0.88f, 0.4f), font);
            SetHorizontalPosition(store.loadoutWeightText.rectTransform, 540, -74, 450);

            EditorUtility.SetDirty(store);
            modified = true;
        }

        return modified;
    }

    private static bool EnsureStoreTerminalInLobby()
    {
        StoreTerminal terminal = Object.FindAnyObjectByType<StoreTerminal>(FindObjectsInactive.Include);
        if (terminal != null) return false;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StoreTerminal.prefab");
        if (prefab == null) return false;

        GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (inst == null) return false;

        inst.name = "StoreTerminal";
        inst.transform.position = new Vector3(-73.5f, -0.5f, 2.5f);
        inst.transform.rotation = Quaternion.Euler(0, 90f, 0);

        StoreTerminal st = inst.GetComponent<StoreTerminal>();
        if (st != null)
        {
            st.terminalTitle = "Оружейная и склад (Магазин / Загрузка)";
            EditorUtility.SetDirty(st);
        }

        Undo.RegisterCreatedObjectUndo(inst, "Create StoreTerminal in Lobby");
        return true;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text, float fontSize, bool bold, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        if (font != null) tmp.font = font;

        return tmp;
    }

    private static void SetRowPosition(RectTransform rt, float posY)
    {
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, posY);
        rt.sizeDelta = new Vector2(720, 35);
    }

    private static void SetHorizontalPosition(RectTransform rt, float posX, float posY, float width)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(posX, posY);
        rt.sizeDelta = new Vector2(width, 24);
    }
}

