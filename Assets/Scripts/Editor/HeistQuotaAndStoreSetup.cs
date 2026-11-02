using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

// [InitializeOnLoad] - Отключено, чтобы настройки пользователя в префабах и сцене не сбрасывались при запуске
public static class HeistQuotaAndStoreSetup
{
    private static readonly string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Inter SDF.asset";

    static HeistQuotaAndStoreSetup()
    {
        // Авто-запуск отключен. Запуск только вручную через меню Tools при необходимости.
    }

    [MenuItem("Tools/Heist/Setup Quota Don UI and Store Loadout in Scene")]
    public static void EnsureSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded || string.IsNullOrEmpty(activeScene.path)) return;

        bool changed = false;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 1. Setup DonResultUI on Main Canvas
        changed |= EnsureDonResultUI(activeScene, font);

        // 2. Setup Loadout Bar on StoreUI
        changed |= EnsureStoreLoadoutBar(font);

        // 3. Setup StoreTerminal in Lobby
        changed |= EnsureStoreTerminalInLobby(activeScene);

        // 4. Apply Noir Shop UI Art Sprites & Fix Styling
        changed |= ApplyShopSpritesAndVisuals(activeScene, font);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("<color=green>[HeistSetup] Успешно настроены DonResultUI, Store Loadout Bar и StoreTerminal на сцене!</color>");
        }
    }

    private static Canvas FindMainCanvas()
    {
        HeistUI heistUI = Object.FindAnyObjectByType<HeistUI>(FindObjectsInactive.Include);
        if (heistUI != null)
        {
            Canvas c = heistUI.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }

        Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in allCanvases)
        {
            if (c.name == "Canvas" && c.transform.parent == null)
            {
                return c;
            }
        }
        return Object.FindAnyObjectByType<Canvas>();
    }

    private static bool EnsureDonResultUI(Scene activeScene, TMP_FontAsset font)
    {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) return false;

        GameManager gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        DonResultUI donUI = Object.FindAnyObjectByType<DonResultUI>(FindObjectsInactive.Include);

        bool modified = false;

        if (donUI == null)
        {
            // Создаем DonResult_Panel непосредственно под главным Canvas
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

            SetLayerRecursively(donPanel, 5);
            donPanel.SetActive(false);
            EditorUtility.SetDirty(donUI);
            modified = true;
        }
        else
        {
            // Убеждаемся, что DonResult_Panel привязан именно к главному Canvas, а не к отключенному мотелю!
            if (donUI.transform.parent != canvas.transform)
            {
                donUI.transform.SetParent(canvas.transform, false);
                SetLayerRecursively(donUI.gameObject, 5);
                EditorUtility.SetDirty(donUI);
                modified = true;
            }
            if (donUI.panel == null)
            {
                donUI.panel = donUI.gameObject;
                EditorUtility.SetDirty(donUI);
                modified = true;
            }
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
        if (store == null || store.storageTabPanel == null) return false;

        bool modified = false;
        Transform storageTr = store.storageTabPanel.transform;

        // If an old bar exists directly under mainStoreWindow, move it to storageTabPanel
        if (store.mainStoreWindow != null)
        {
            Transform oldBar = store.mainStoreWindow.transform.Find("LoadoutBar_Panel");
            if (oldBar != null && oldBar.parent != storageTr)
            {
                oldBar.SetParent(storageTr, false);
                modified = true;
            }
        }

        Transform existingBar = storageTr.Find("LoadoutBar_Panel");
        GameObject barObj;
        if (existingBar != null)
        {
            barObj = existingBar.gameObject;
        }
        else
        {
            barObj = new GameObject("LoadoutBar_Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barObj.transform.SetParent(storageTr, false);

            RectTransform rt = barObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 48);
            rt.sizeDelta = new Vector2(-120, 80);

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
            store.loadoutAmmoText = CreateTMP(barObj.transform, "AmmoText", "Боезапас: Лёгкие 0 | Средние 0 | Дробь 0", 15, false, new Color(0.45f, 0.85f, 1f), font);
            SetHorizontalPosition(store.loadoutAmmoText.rectTransform, 25, -74, 420);

            store.loadoutWeightText = CreateTMP(barObj.transform, "WeightText", "Вес: 0 / 40 кг (Свободно: 40 кг)", 15, true, new Color(1f, 0.88f, 0.4f), font);
            SetHorizontalPosition(store.loadoutWeightText.rectTransform, 440, -74, 300);

            // Кнопки быстрой покупки патронов
            store.buyLightAmmoButton = CreateAmmoButton(barObj.transform, "BuyLightAmmoBtn", "+18 Лёгкие ($30)", 730, -72, 115, font);
            store.buyMediumAmmoButton = CreateAmmoButton(barObj.transform, "BuyMediumAmmoBtn", "+50 Средние ($60)", 855, -72, 125, font);
            store.buyShotgunAmmoButton = CreateAmmoButton(barObj.transform, "BuyShotgunAmmoBtn", "+12 Дробь ($40)", 990, -72, 115, font);

            SetLayerRecursively(barObj, 5);
            EditorUtility.SetDirty(store);
            modified = true;
        }

        // Если бар уже был создан, обновляем его позицию и привязываем кнопки патронов
        if (barObj != null)
        {
            RectTransform bRt = barObj.GetComponent<RectTransform>();
            if (bRt != null)
            {
                bRt.anchorMin = new Vector2(0f, 0f);
                bRt.anchorMax = new Vector2(1f, 0f);
                bRt.pivot = new Vector2(0.5f, 0f);
                bRt.anchoredPosition = new Vector2(0, 48);
                bRt.sizeDelta = new Vector2(-120, 80);
            }

            if (store.buyLightAmmoButton == null)
                store.buyLightAmmoButton = CreateAmmoButton(barObj.transform, "BuyLightAmmoBtn", "+18 Лёгкие ($30)", 730, -72, 115, font);
            if (store.buyMediumAmmoButton == null)
                store.buyMediumAmmoButton = CreateAmmoButton(barObj.transform, "BuyMediumAmmoBtn", "+50 Средние ($60)", 855, -72, 125, font);
            if (store.buyShotgunAmmoButton == null)
                store.buyShotgunAmmoButton = CreateAmmoButton(barObj.transform, "BuyShotgunAmmoBtn", "+12 Дробь ($40)", 990, -72, 115, font);
            EditorUtility.SetDirty(store);
            modified = true;
        }

        return modified;
    }

    private static Button CreateAmmoButton(Transform parent, string name, string label, float posX, float posY, float width, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        GameObject btnObj;
        if (existing != null) btnObj = existing.gameObject;
        else
        {
            btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(posX, posY);
        rt.sizeDelta = new Vector2(width, 24);

        Image img = btnObj.GetComponent<Image>();
        Sprite redBtn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/16_button_red.png");
        if (redBtn != null) img.sprite = redBtn;
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();

        Transform lblTr = btnObj.transform.Find("Label");
        TextMeshProUGUI tmp;
        if (lblTr != null) tmp = lblTr.GetComponent<TextMeshProUGUI>();
        else
        {
            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(btnObj.transform, false);
            tmp = lblObj.GetComponent<TextMeshProUGUI>();
            RectTransform lRt = lblObj.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.sizeDelta = Vector2.zero;
            lRt.anchoredPosition = Vector2.zero;
        }

        tmp.text = label;
        tmp.fontSize = 11;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return btn;
    }

    private static bool EnsureStoreTerminalInLobby(Scene activeScene)
    {
        StoreTerminal[] sceneTerminals = Object.FindObjectsByType<StoreTerminal>(FindObjectsInactive.Include);
        foreach (var t in sceneTerminals)
        {
            if (t.gameObject.scene == activeScene)
            {
                return false; // Уже есть на активной сцене
            }
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/StoreTerminal.prefab");
        if (prefab == null) return false;

        GameObject inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (inst == null) return false;

        inst.name = "StoreTerminal";
        
        GameObject lobbyPivot = GameObject.Find("LobbyPivot");
        if (lobbyPivot != null)
        {
            inst.transform.SetParent(lobbyPivot.transform, true);
        }

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

    private static bool ApplyShopSpritesAndVisuals(Scene activeScene, TMP_FontAsset font)
    {
        StoreUI store = Object.FindAnyObjectByType<StoreUI>(FindObjectsInactive.Include);
        if (store == null || store.mainStoreWindow == null) return false;

        bool modified = false;

        Sprite headerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/01_store_header.png");
        Sprite closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/02_close.png");
        Sprite mainPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/03_main_panel.png");
        Sprite detailsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/04_product_details.png");
        Sprite tabAmmoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/05_tab_ammo_yellow.png");
        Sprite tabSettingsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/06_tab_settings_dark.png");
        Sprite balanceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/07_balance.png");
        Sprite remainingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/08_remaining.png");
        Sprite clearSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/09_clear.png");
        Sprite buySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/10_buy.png");
        Sprite redBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/16_button_red.png");
        Sprite buttonBlankSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/17_button_blank_cream.png");
        Sprite greenProgressSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/22_progress_green.png");
        Sprite productBlankSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/26_product_blank_1.png");
        Sprite cartBlankSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/28_cart_placeholder.png");

        // 1. StoreWindow RectTransform & Background
        GameObject win = store.mainStoreWindow;
        RectTransform winRt = win.GetComponent<RectTransform>();
        if (winRt != null)
        {
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.pivot = new Vector2(0.5f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = new Vector2(1160f, 720f);
            winRt.localScale = Vector3.one;
            modified = true;
        }

        Image winImg = win.GetComponent<Image>();
        if (winImg != null && mainPanelSprite != null)
        {
            winImg.sprite = mainPanelSprite;
            winImg.color = Color.white;
            winImg.type = Image.Type.Simple;
            modified = true;
        }

        // 2. MainTab (Fix 220px shift and transparent background)
        Transform mainTabTr = win.transform.Find("MainTab");
        if (mainTabTr != null)
        {
            RectTransform mRt = mainTabTr.GetComponent<RectTransform>();
            mRt.anchorMin = Vector2.zero;
            mRt.anchorMax = Vector2.one;
            mRt.pivot = new Vector2(0.5f, 0.5f);
            mRt.anchoredPosition = Vector2.zero;
            mRt.sizeDelta = Vector2.zero;
            mRt.localScale = Vector3.one;

            Image mImg = mainTabTr.GetComponent<Image>();
            if (mImg != null)
            {
                mImg.color = new Color(0, 0, 0, 0); // No milky wash!
                mImg.raycastTarget = false;
            }
        }

        // 3. Header Banner
        Transform headerTr = win.transform.Find("StoreHeader_Banner");
        GameObject headerObj;
        if (headerTr != null) headerObj = headerTr.gameObject;
        else
        {
            headerObj = new GameObject("StoreHeader_Banner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerObj.transform.SetParent(win.transform, false);
            modified = true;
        }
        headerObj.transform.SetAsFirstSibling();

        RectTransform hRt = headerObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0.5f, 1f);
        hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.anchoredPosition = new Vector2(10f, -22f);
        hRt.sizeDelta = new Vector2(380f, 70f);

        Image hImg = headerObj.GetComponent<Image>();
        if (hImg != null && headerSprite != null)
        {
            hImg.sprite = headerSprite;
            hImg.color = Color.white;
            hImg.raycastTarget = false;
        }

        // 4. Close Button
        if (store.closeButton != null)
        {
            Image cImg = store.closeButton.GetComponent<Image>();
            if (cImg != null && closeSprite != null)
            {
                cImg.sprite = closeSprite;
                cImg.color = Color.white;
            }
            RectTransform cRt = store.closeButton.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-28f, -24f);
            cRt.sizeDelta = new Vector2(48f, 48f);

            foreach (var t in store.closeButton.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
            foreach (var t in store.closeButton.GetComponentsInChildren<Text>(true)) t.text = "";
            modified = true;
        }

        // 5. Tab Buttons on Left Edge
        if (store.storeTabButton != null)
        {
            Image tab1Img = store.storeTabButton.GetComponent<Image>();
            if (tab1Img != null && tabAmmoSprite != null)
            {
                tab1Img.sprite = tabAmmoSprite;
                tab1Img.color = Color.white;
            }
            RectTransform t1Rt = store.storeTabButton.GetComponent<RectTransform>();
            t1Rt.anchorMin = new Vector2(0f, 0.5f);
            t1Rt.anchorMax = new Vector2(0f, 0.5f);
            t1Rt.pivot = new Vector2(0f, 0.5f);
            t1Rt.anchoredPosition = new Vector2(8f, 105f);
            t1Rt.sizeDelta = new Vector2(80f, 78f);

            foreach (var t in store.storeTabButton.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
            foreach (var t in store.storeTabButton.GetComponentsInChildren<Text>(true)) t.text = "";
            modified = true;
        }

        if (store.storageTabButton != null)
        {
            Image tab2Img = store.storageTabButton.GetComponent<Image>();
            if (tab2Img != null && tabSettingsSprite != null)
            {
                tab2Img.sprite = tabSettingsSprite;
                tab2Img.color = Color.white;
            }
            RectTransform t2Rt = store.storageTabButton.GetComponent<RectTransform>();
            t2Rt.anchorMin = new Vector2(0f, 0.5f);
            t2Rt.anchorMax = new Vector2(0f, 0.5f);
            t2Rt.pivot = new Vector2(0f, 0.5f);
            t2Rt.anchoredPosition = new Vector2(8f, 20f);
            t2Rt.sizeDelta = new Vector2(80f, 78f);

            foreach (var t in store.storageTabButton.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
            foreach (var t in store.storageTabButton.GetComponentsInChildren<Text>(true)) t.text = "";
            modified = true;
        }

        // 6. StoreTab panel (Full transparent, no yellow tint)
        if (store.storeTabPanel != null)
        {
            RectTransform stRt = store.storeTabPanel.GetComponent<RectTransform>();
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.pivot = new Vector2(0.5f, 0.5f);
            stRt.anchoredPosition = Vector2.zero;
            stRt.sizeDelta = Vector2.zero;
            stRt.localScale = Vector3.one;

            Image stImg = store.storeTabPanel.GetComponent<Image>();
            if (stImg != null)
            {
                stImg.color = new Color(0, 0, 0, 0); // No yellow wash!
                stImg.raycastTarget = false;
            }
        }

        // 7. CatalogContainer (Grid 3 columns x 4 rows, NO pink box, generous spacing)
        if (store.catalogContainer != null)
        {
            RectTransform catRt = store.catalogContainer.GetComponent<RectTransform>();
            catRt.anchorMin = new Vector2(0f, 0.5f);
            catRt.anchorMax = new Vector2(0f, 0.5f);
            catRt.pivot = new Vector2(0f, 0.5f);
            catRt.anchoredPosition = new Vector2(112f, 16f);
            catRt.sizeDelta = new Vector2(680f, 480f);
            catRt.localScale = Vector3.one;

            Image catImg = store.catalogContainer.GetComponent<Image>();
            if (catImg != null)
            {
                catImg.color = new Color(0, 0, 0, 0); // NO PINK!
                catImg.raycastTarget = false;
            }

            GridLayoutGroup grid = store.catalogContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = store.catalogContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210f, 110f);
            grid.spacing = new Vector2(16f, 12f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperLeft;
        }

        // 8. CartContainer (Right panel, NO cyan box)
        if (store.cartContainer != null)
        {
            RectTransform cartRt = store.cartContainer.GetComponent<RectTransform>();
            cartRt.anchorMin = new Vector2(1f, 0.5f);
            cartRt.anchorMax = new Vector2(1f, 0.5f);
            cartRt.pivot = new Vector2(1f, 0.5f);
            cartRt.anchoredPosition = new Vector2(-54f, 16f);
            cartRt.sizeDelta = new Vector2(265f, 480f);
            cartRt.localScale = Vector3.one;

            Image cartImg = store.cartContainer.GetComponent<Image>();
            if (cartImg != null)
            {
                if (cartBlankSprite != null) cartImg.sprite = cartBlankSprite;
                cartImg.color = Color.white;
                cartImg.type = Image.Type.Simple;
                cartImg.preserveAspect = true;
                cartImg.raycastTarget = false;
            }

            VerticalLayoutGroup vlg = store.cartContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = store.cartContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(4, 4, 8, 8);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
        }

        // 9. Bottom Bar Controls
        if (store.availableMoneyText != null)
        {
            RectTransform rt = store.availableMoneyText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(125f, 52f);
            rt.sizeDelta = new Vector2(210f, 40f);
            store.availableMoneyText.enableAutoSizing = false;
            store.availableMoneyText.fontSize = 20;
            store.availableMoneyText.fontStyle = FontStyles.Bold;
            store.availableMoneyText.alignment = TextAlignmentOptions.Left;
            store.availableMoneyText.color = Color.white;
            if (font != null) store.availableMoneyText.font = font;
        }

        if (store.remainingMoneyText != null)
        {
            RectTransform rt = store.remainingMoneyText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(355f, 52f);
            rt.sizeDelta = new Vector2(210f, 40f);
            store.remainingMoneyText.enableAutoSizing = false;
            store.remainingMoneyText.fontSize = 20;
            store.remainingMoneyText.fontStyle = FontStyles.Bold;
            store.remainingMoneyText.alignment = TextAlignmentOptions.Left;
            store.remainingMoneyText.color = Color.white;
            if (font != null) store.remainingMoneyText.font = font;
        }

        if (store.totalCostText != null)
        {
            RectTransform rt = store.totalCostText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -600f);
            rt.sizeDelta = new Vector2(100f, 20f);
        }

        if (store.clearCartButton != null)
        {
            Image clImg = store.clearCartButton.GetComponent<Image>();
            if (clImg != null && clearSprite != null)
            {
                clImg.sprite = clearSprite;
                clImg.color = Color.white;
            }
            RectTransform clRt = store.clearCartButton.GetComponent<RectTransform>();
            clRt.anchorMin = new Vector2(1f, 0f);
            clRt.anchorMax = new Vector2(1f, 0f);
            clRt.pivot = new Vector2(1f, 0.5f);
            clRt.anchoredPosition = new Vector2(-255f, 52f);
            clRt.sizeDelta = new Vector2(165f, 50f);

            foreach (var t in store.clearCartButton.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
            foreach (var t in store.clearCartButton.GetComponentsInChildren<Text>(true)) t.text = "";
            modified = true;
        }

        if (store.purchaseButton != null)
        {
            Image bImg = store.purchaseButton.GetComponent<Image>();
            if (bImg != null && buySprite != null)
            {
                bImg.sprite = buySprite;
                bImg.color = Color.white;
            }
            RectTransform bRt = store.purchaseButton.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-75f, 52f);
            bRt.sizeDelta = new Vector2(170f, 50f);

            foreach (var t in store.purchaseButton.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
            foreach (var t in store.purchaseButton.GetComponentsInChildren<Text>(true)) t.text = "";
            modified = true;
        }

        // 10. StorageTab panel (transparent, clean)
        if (store.storageTabPanel != null)
        {
            RectTransform sRt = store.storageTabPanel.GetComponent<RectTransform>();
            sRt.anchorMin = Vector2.zero;
            sRt.anchorMax = Vector2.one;
            sRt.pivot = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = Vector2.zero;
            sRt.sizeDelta = Vector2.zero;

            Image sImg = store.storageTabPanel.GetComponent<Image>();
            if (sImg != null)
            {
                sImg.color = new Color(0, 0, 0, 0); // No green wash!
                sImg.raycastTarget = false;
            }

            if (store.storageContainer != null)
            {
                RectTransform scRt = store.storageContainer.GetComponent<RectTransform>();
                scRt.anchorMin = new Vector2(0f, 0.5f);
                scRt.anchorMax = new Vector2(1f, 0.5f);
                scRt.pivot = new Vector2(0.5f, 0.5f);
                scRt.anchoredPosition = new Vector2(40f, 35f);
                scRt.sizeDelta = new Vector2(-180f, 440f);
                scRt.localScale = Vector3.one;

                Image scImg = store.storageContainer.GetComponent<Image>();
                if (scImg != null)
                {
                    scImg.color = new Color(0, 0, 0, 0);
                    scImg.raycastTarget = false;
                }

                GridLayoutGroup grid = store.storageContainer.GetComponent<GridLayoutGroup>();
                if (grid == null) grid = store.storageContainer.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(225f, 90f);
                grid.spacing = new Vector2(16f, 14f);
                grid.padding = new RectOffset(10, 10, 10, 10);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.childAlignment = TextAnchor.UpperLeft;
            }

            if (store.storageStatusText != null)
            {
                RectTransform stRt = store.storageStatusText.rectTransform;
                stRt.anchorMin = new Vector2(0.5f, 0.5f);
                stRt.anchorMax = new Vector2(0.5f, 0.5f);
                stRt.pivot = new Vector2(0.5f, 0.5f);
                stRt.anchoredPosition = new Vector2(40f, 40f);
                stRt.sizeDelta = new Vector2(750f, 60f);
                store.storageStatusText.fontSize = 18;
                store.storageStatusText.fontStyle = FontStyles.Bold;
                store.storageStatusText.color = new Color(0.25f, 0.2f, 0.15f, 1f);
                store.storageStatusText.alignment = TextAlignmentOptions.Center;
                store.storageStatusText.raycastTarget = false;
                if (font != null) store.storageStatusText.font = font;
            }
        }

        // 11. Restyle Item Prefabs (Отключено, чтобы не сбрасывать пользовательские настройки префабов)
        // RestyleItemPrefabs(productBlankSprite, redBtnSprite, greenProgressSprite, cartBlankSprite, font);

        SetLayerRecursively(win, 5);
        EditorUtility.SetDirty(win);
        return modified;
    }

    private static void RestyleItemPrefabs(Sprite productBlank, Sprite redBtn, Sprite greenProgress, Sprite cartBlank, TMP_FontAsset font)
    {
        // 1. StoreItemCardPrefab
        string p1Path = "Assets/Prefabs/StoreItemCardPrefab.prefab";
        GameObject p1 = PrefabUtility.LoadPrefabContents(p1Path);
        if (p1 != null)
        {
            RectTransform rootRt = p1.GetComponent<RectTransform>();
            if (rootRt != null) rootRt.sizeDelta = new Vector2(210f, 110f);

            Transform cardBack = p1.transform.Find("CardBack");
            if (cardBack != null)
            {
                RectTransform cbRt = cardBack.GetComponent<RectTransform>();
                cbRt.anchorMin = Vector2.zero;
                cbRt.anchorMax = Vector2.one;
                cbRt.sizeDelta = Vector2.zero;
                cbRt.anchoredPosition = Vector2.zero;

                Image img = cardBack.GetComponent<Image>();
                if (img != null)
                {
                    if (productBlank != null) img.sprite = productBlank;
                    img.color = Color.white;
                }
            }

            Transform icon = p1.transform.Find("Icon");
            if (icon != null)
            {
                RectTransform iRt = icon.GetComponent<RectTransform>();
                iRt.anchorMin = new Vector2(0f, 0.5f);
                iRt.anchorMax = new Vector2(0f, 0.5f);
                iRt.pivot = new Vector2(0.5f, 0.5f);
                iRt.anchoredPosition = new Vector2(36f, 10f);
                iRt.sizeDelta = new Vector2(52f, 52f);

                Image iImg = icon.GetComponent<Image>();
                if (iImg != null)
                {
                    iImg.preserveAspect = true;
                    iImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                    iImg.raycastTarget = false;
                }
            }

            Transform nameTr = p1.transform.Find("Name");
            if (nameTr != null)
            {
                RectTransform nRt = nameTr.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(1f, 1f);
                nRt.pivot = new Vector2(0f, 1f);
                nRt.anchoredPosition = new Vector2(68f, -10f);
                nRt.sizeDelta = new Vector2(-76f, 22f);

                TextMeshProUGUI tmp = nameTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 16;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(0.12f, 0.08f, 0.05f, 1f); // Dark black/brown
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.raycastTarget = false;
                }
            }

            Transform descTr = p1.transform.Find("Description");
            if (descTr != null)
            {
                RectTransform dRt = descTr.GetComponent<RectTransform>();
                dRt.anchorMin = new Vector2(0f, 1f);
                dRt.anchorMax = new Vector2(1f, 1f);
                dRt.pivot = new Vector2(0f, 1f);
                dRt.anchoredPosition = new Vector2(68f, -32f);
                dRt.sizeDelta = new Vector2(-76f, 30f);

                TextMeshProUGUI tmp = descTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 10.5f;
                    tmp.fontStyle = FontStyles.Normal;
                    tmp.color = new Color(0.35f, 0.28f, 0.22f, 1f); // Muted dark warm brown
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.raycastTarget = false;
                }
            }

            // Price badge background (Image) + Price text (TMP)
            Transform priceTr = p1.transform.Find("Price");
            Transform badgeTr = p1.transform.Find("PriceBadge");
            GameObject badgeObj;
            if (badgeTr == null)
            {
                badgeObj = new GameObject("PriceBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeObj.transform.SetParent(p1.transform, false);
            }
            else
            {
                badgeObj = badgeTr.gameObject;
            }
            if (priceTr != null)
            {
                badgeObj.transform.SetSiblingIndex(priceTr.GetSiblingIndex());
            }
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0f);
            bRt.anchorMax = new Vector2(0f, 0f);
            bRt.pivot = new Vector2(0f, 0f);
            bRt.anchoredPosition = new Vector2(8f, 8f);
            bRt.sizeDelta = new Vector2(68f, 28f);

            Image bImg = badgeObj.GetComponent<Image>();
            if (bImg != null)
            {
                if (redBtn != null) bImg.sprite = redBtn;
                bImg.color = Color.white;
                bImg.type = Image.Type.Sliced;
                bImg.raycastTarget = true; // Кликабельно для мгновенной покупки!
            }

            Button bBtn = badgeObj.GetComponent<Button>();
            if (bBtn == null) bBtn = badgeObj.AddComponent<Button>();

            if (priceTr != null)
            {
                RectTransform pRt = priceTr.GetComponent<RectTransform>();
                pRt.anchorMin = new Vector2(0f, 0f);
                pRt.anchorMax = new Vector2(0f, 0f);
                pRt.pivot = new Vector2(0f, 0f);
                pRt.anchoredPosition = new Vector2(8f, 8f);
                pRt.sizeDelta = new Vector2(68f, 28f);

                TextMeshProUGUI tmp = priceTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 15;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = Color.white;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                }
            }

            Transform capTr = p1.transform.Find("Capacity");
            if (capTr != null)
            {
                RectTransform cRt = capTr.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0f, 0f);
                cRt.anchorMax = new Vector2(0f, 0f);
                cRt.pivot = new Vector2(0f, 0f);
                cRt.anchoredPosition = new Vector2(84f, 20f);
                cRt.sizeDelta = new Vector2(114f, 16f);

                TextMeshProUGUI tmp = capTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 11;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(0.2f, 0.15f, 0.1f, 1f); // Dark brown
                    tmp.alignment = TextAlignmentOptions.Left;
                    tmp.raycastTarget = false;
                }
            }

            // Progress Bar under Capacity
            Transform progBar = p1.transform.Find("ProgressBar");
            GameObject progObj;
            if (progBar != null) progObj = progBar.gameObject;
            else
            {
                progObj = new GameObject("ProgressBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                progObj.transform.SetParent(p1.transform, false);
            }
            RectTransform progRt = progObj.GetComponent<RectTransform>();
            progRt.anchorMin = new Vector2(0f, 0f);
            progRt.anchorMax = new Vector2(0f, 0f);
            progRt.pivot = new Vector2(0f, 0f);
            progRt.anchoredPosition = new Vector2(84f, 8f);
            progRt.sizeDelta = new Vector2(112f, 8f);
            Image progBg = progObj.GetComponent<Image>();
            progBg.color = new Color(0.2f, 0.18f, 0.16f, 0.9f);
            progBg.raycastTarget = false;

            Transform progFill = progObj.transform.Find("Fill");
            GameObject fillObj;
            if (progFill != null) fillObj = progFill.gameObject;
            else
            {
                fillObj = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObj.transform.SetParent(progObj.transform, false);
            }
            RectTransform fRt = fillObj.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.sizeDelta = Vector2.zero;
            fRt.anchoredPosition = Vector2.zero;
            Image fillImg = fillObj.GetComponent<Image>();
            if (greenProgress != null) fillImg.sprite = greenProgress;
            fillImg.color = new Color(0.2f, 0.82f, 0.2f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;

            // Удаляем ненужный 2D коллайдер, блокировавший рейкаст
            var col2d = p1.GetComponent<BoxCollider2D>();
            if (col2d != null) Object.DestroyImmediate(col2d, true);

            StoreItemCardUI cardUI = p1.GetComponent<StoreItemCardUI>();
            if (cardUI != null)
            {
                cardUI.capacityBarFill = fillImg;
                cardUI.instantBuyButton = bBtn;
            }

            // Add Button: transparent over the entire card
            Transform addTr = p1.transform.Find("Add");
            if (addTr != null)
            {
                addTr.SetAsLastSibling(); // Ensure button receives raycasts
                RectTransform aRt = addTr.GetComponent<RectTransform>();
                aRt.anchorMin = Vector2.zero;
                aRt.anchorMax = Vector2.one;
                aRt.sizeDelta = Vector2.zero;
                aRt.anchoredPosition = Vector2.zero;

                Image aImg = addTr.GetComponent<Image>();
                if (aImg != null)
                {
                    aImg.color = new Color(0, 0, 0, 0); // transparent hit area
                    aImg.raycastTarget = true;
                }
                foreach (var t in addTr.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = "";
                foreach (var t in addTr.GetComponentsInChildren<Text>(true)) t.text = "";
            }

            PrefabUtility.SaveAsPrefabAsset(p1, p1Path);
            PrefabUtility.UnloadPrefabContents(p1);
        }

        // 2. StoreCartCardPrefab
        string p2Path = "Assets/Prefabs/StoreCartCardPrefab.prefab";
        GameObject p2 = PrefabUtility.LoadPrefabContents(p2Path);
        if (p2 != null)
        {
            RectTransform rootRt = p2.GetComponent<RectTransform>();
            if (rootRt != null) rootRt.sizeDelta = new Vector2(250f, 48f);

            Sprite creamCard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/27_product_blank_2.png");
            Sprite squareDark = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/18_square_dark.png");
            Sprite squareRed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Images/Shop/19_square_red.png");

            Transform cardBack = p2.transform.Find("CardBack");
            if (cardBack != null)
            {
                Image img = cardBack.GetComponent<Image>();
                if (img != null)
                {
                    if (creamCard != null) img.sprite = creamCard;
                    img.color = Color.white;
                    img.type = Image.Type.Sliced;
                    img.raycastTarget = false;
                }
            }

            Transform addBtnTr = p2.transform.Find("Add");
            if (addBtnTr != null)
            {
                Image aImg = addBtnTr.GetComponent<Image>();
                if (aImg != null)
                {
                    if (squareDark != null) aImg.sprite = squareDark;
                    aImg.color = Color.white;
                    aImg.raycastTarget = true;
                }
            }

            Transform remBtnTr = p2.transform.Find("Remove");
            if (remBtnTr != null)
            {
                Image rImg = remBtnTr.GetComponent<Image>();
                if (rImg != null)
                {
                    if (squareRed != null) rImg.sprite = squareRed;
                    rImg.color = Color.white;
                    rImg.raycastTarget = true;
                }
            }

            foreach (var tmp in p2.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (font != null) tmp.font = font;
                tmp.raycastTarget = false;
                if (tmp.name == "Name") tmp.color = new Color(0.12f, 0.08f, 0.05f, 1f);
                else if (tmp.name == "Quantity") tmp.color = new Color(0.35f, 0.28f, 0.22f, 1f);
                else if (tmp.name == "Subtotal") tmp.color = new Color(0.55f, 0.12f, 0.1f, 1f);
            }

            PrefabUtility.SaveAsPrefabAsset(p2, p2Path);
            PrefabUtility.UnloadPrefabContents(p2);
        }

        // 3. StorageCardPrefab
        string p3Path = "Assets/Prefabs/StorageCardPrefab.prefab";
        GameObject p3 = PrefabUtility.LoadPrefabContents(p3Path);
        if (p3 != null)
        {
            RectTransform rootRt = p3.GetComponent<RectTransform>();
            if (rootRt != null) rootRt.sizeDelta = new Vector2(225f, 90f);

            Transform cardBack = p3.transform.Find("CardBack");
            if (cardBack != null)
            {
                Image img = cardBack.GetComponent<Image>();
                if (img != null)
                {
                    if (productBlank != null) img.sprite = productBlank;
                    img.color = Color.white;
                    img.raycastTarget = false;
                }
            }

            Transform icon = p3.transform.Find("Icon");
            if (icon != null)
            {
                RectTransform iRt = icon.GetComponent<RectTransform>();
                iRt.anchorMin = new Vector2(0f, 0.5f);
                iRt.anchorMax = new Vector2(0f, 0.5f);
                iRt.pivot = new Vector2(0.5f, 0.5f);
                iRt.anchoredPosition = new Vector2(34f, 6f);
                iRt.sizeDelta = new Vector2(46f, 46f);

                Image iImg = icon.GetComponent<Image>();
                if (iImg != null)
                {
                    iImg.preserveAspect = true;
                    iImg.color = new Color(0.12f, 0.12f, 0.12f, 1f);
                    iImg.raycastTarget = false;
                }
            }

            Transform nameTr = p3.transform.Find("Name");
            if (nameTr != null)
            {
                RectTransform nRt = nameTr.GetComponent<RectTransform>();
                nRt.anchorMin = new Vector2(0f, 1f);
                nRt.anchorMax = new Vector2(1f, 1f);
                nRt.pivot = new Vector2(0f, 1f);
                nRt.anchoredPosition = new Vector2(65f, -12f);
                nRt.sizeDelta = new Vector2(-75f, 22f);

                TextMeshProUGUI tmp = nameTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 15;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.color = new Color(0.12f, 0.08f, 0.05f, 1f);
                    tmp.raycastTarget = false;
                }
            }

            Transform qTr = p3.transform.Find("Quantity");
            if (qTr != null)
            {
                RectTransform qRt = qTr.GetComponent<RectTransform>();
                qRt.anchorMin = new Vector2(0f, 0f);
                qRt.anchorMax = new Vector2(0f, 0f);
                qRt.pivot = new Vector2(0f, 0f);
                qRt.anchoredPosition = new Vector2(65f, 8f);
                qRt.sizeDelta = new Vector2(125f, 36f);

                TextMeshProUGUI tmp = qTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 11;
                    tmp.color = new Color(0.18f, 0.14f, 0.10f, 1f);
                    tmp.raycastTarget = false;
                }
            }

            Transform cImg1 = p3.transform.Find("capacityImg");
            if (cImg1 != null) cImg1.gameObject.SetActive(false);
            Transform cImg2 = p3.transform.Find("capacityImg (1)");
            if (cImg2 != null) cImg2.gameObject.SetActive(false);

            // Equip Button
            Transform eqTr = p3.transform.Find("EquipButton");
            GameObject eqObj;
            if (eqTr != null) eqObj = eqTr.gameObject;
            else
            {
                eqObj = new GameObject("EquipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                eqObj.transform.SetParent(p3.transform, false);
            }
            RectTransform eqRt = eqObj.GetComponent<RectTransform>();
            eqRt.anchorMin = new Vector2(1f, 0f);
            eqRt.anchorMax = new Vector2(1f, 0f);
            eqRt.pivot = new Vector2(1f, 0f);
            eqRt.anchoredPosition = new Vector2(-8f, 8f);
            eqRt.sizeDelta = new Vector2(95f, 32f);

            Image eqImg = eqObj.GetComponent<Image>();
            if (redBtn != null) eqImg.sprite = redBtn;
            eqImg.type = Image.Type.Sliced;
            eqImg.color = Color.white;
            eqImg.raycastTarget = true;

            Button eqBtn = eqObj.GetComponent<Button>();

            Transform lblTr = eqObj.transform.Find("Label");
            TextMeshProUGUI eqLbl;
            if (lblTr != null) eqLbl = lblTr.GetComponent<TextMeshProUGUI>();
            else
            {
                GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(eqObj.transform, false);
                eqLbl = lblObj.GetComponent<TextMeshProUGUI>();
                RectTransform lRt = lblObj.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero;
                lRt.anchorMax = Vector2.one;
                lRt.sizeDelta = Vector2.zero;
                lRt.anchoredPosition = Vector2.zero;
            }
            eqLbl.text = "ВЗЯТЬ";
            eqLbl.fontSize = 13;
            eqLbl.fontStyle = FontStyles.Bold;
            eqLbl.color = Color.white;
            eqLbl.alignment = TextAlignmentOptions.Center;
            eqLbl.raycastTarget = false;
            if (font != null) eqLbl.font = font;

            StorageItemCardUI scUI = p3.GetComponent<StorageItemCardUI>();
            if (scUI != null)
            {
                scUI.equipButton = eqBtn;
                scUI.equipButtonText = eqLbl;
            }

            PrefabUtility.SaveAsPrefabAsset(p3, p3Path);
            PrefabUtility.UnloadPrefabContents(p3);
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            if (child != null) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
