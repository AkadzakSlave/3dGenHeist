using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Редактор инструментов для настройки и расстановки точек спавна лута в комнатах.
/// Меню: Tools → Loot Setup & Placer
/// </summary>
public class LootSetupTool : EditorWindow
{
    private enum PlacementPreset
    {
        Small_Loot,   // Мелочь, деньги, сигары (1 unit)
        Medium_Loot,  // Золото, фигурки (2 units)
        Large_Loot,   // Чемоданы, сейфы (4 units)
        Vault_Special // Особая точка сейфа (High Value, 4 units)
    }

    private int selectedTab = 0;
    private readonly string[] tabNames = new string[] { "📊 Дашборд комнат", "🎯 Расстановка в сцене (Click-to-Place)", "⚙️ Пакетная миграция" };

    // Placement Mode Settings
    private bool placementMode = false;
    private PlacementPreset currentPreset = PlacementPreset.Small_Loot;
    private float customVerticalOffset = 0.05f;
    private float customSpawnChance = 80f;

    private Vector2 scrollPos;
    private LootDatabase cachedLootDB;

    [MenuItem("Tools/Loot Setup & Placer")]
    public static void OpenWindow()
    {
        var win = GetWindow<LootSetupTool>("Loot Placer & Hub");
        win.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FindLootDatabase();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void FindLootDatabase()
    {
        if (cachedLootDB != null) return;
        string[] guids = AssetDatabase.FindAssets("t:LootDatabase");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedLootDB = AssetDatabase.LoadAssetAtPath<LootDatabase>(path);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        selectedTab = GUILayout.Toolbar(selectedTab, tabNames, GUILayout.Height(28));
        EditorGUILayout.Space(8);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        switch (selectedTab)
        {
            case 0:
                DrawDashboardTab();
                break;
            case 1:
                DrawPlacementTab();
                break;
            case 2:
                DrawMigrationTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region Tab 1: Dashboard

    private void DrawDashboardTab()
    {
        GUILayout.Label("Состояние комнат и распределение лута", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Здесь отображены все префабы комнат из Assets/Rooms/. Вы можете проверить наличие точек лута и открыть префаб в один клик.", MessageType.Info);

        if (GUILayout.Button("🔄 Обновить список комнат", GUILayout.Height(26)))
        {
            Repaint();
        }

        EditorGUILayout.Space(8);

        string[] prefabPaths = Directory.GetFiles("Assets/Rooms", "*.prefab", SearchOption.TopDirectoryOnly);
        if (prefabPaths == null || prefabPaths.Length == 0)
        {
            EditorGUILayout.LabelField("Префабы не найдены в Assets/Rooms.");
            return;
        }

        foreach (string path in prefabPaths)
        {
            string roomName = Path.GetFileNameWithoutExtension(path);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            RoomLootManager lootManager = prefab.GetComponent<RoomLootManager>();
            LootPoint[] points = prefab.GetComponentsInChildren<LootPoint>(true);

            int smallCount = 0;
            int medCount = 0;
            int largeCount = 0;
            int highValCount = 0;

            if (points != null)
            {
                foreach (var p in points)
                {
                    if (p.isHighValue) highValCount++;
                    if (p.sizeFilter == LootPointSizeFilter.LargeOnly || p.capacityUnits >= 4) largeCount++;
                    else if (p.sizeFilter == LootPointSizeFilter.MediumOnly || p.capacityUnits >= 2) medCount++;
                    else smallCount++;
                }
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            bool isVault = roomName.ToLower().Contains("safe");
            string titlePrefix = isVault ? "🏦 [VAULT] " : "🚪 ";
            GUILayout.Label(titlePrefix + roomName, EditorStyles.boldLabel, GUILayout.Width(140));

            if (lootManager == null)
            {
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label("Нет RoomLootManager!", GUILayout.Width(140));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = lootManager.lootDB != null ? Color.green : Color.yellow;
                GUILayout.Label(lootManager.lootDB != null ? "✓ LootDB OK" : "⚠ DB пуста", GUILayout.Width(90));
                GUI.color = Color.white;
            }

            if (GUILayout.Button("Открыть", GUILayout.Width(75)))
            {
                PrefabStageUtility.OpenPrefab(path);
            }

            EditorGUILayout.EndHorizontal();

            // Статистика точек
            int total = points != null ? points.Length : 0;
            string stats = total == 0 
                ? "<color=#ff6666>Точек лута нет (0)</color>" 
                : $"Точек: <b>{total}</b> (🟢 Малых: {smallCount} | 🔵 Средних: {medCount} | 🟣 Крупных: {largeCount} | ⭐ Сейф: {highValCount})";

            GUIStyle richLabel = new GUIStyle(EditorStyles.miniLabel) { richText = true };
            EditorGUILayout.LabelField(stats, richLabel);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    #endregion

    #region Tab 2: Click-to-Place Mode

    private void DrawPlacementTab()
    {
        GUILayout.Label("Интерактивная расстановка точек (Scene View)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("1. Откройте нужный префаб комнаты (или сцену).\n2. Включите режим клика ниже.\n3. Кликайте по столам, полкам или полу в окне Scene View — точка лута создастся автоматически!", MessageType.Info);

        EditorGUILayout.Space(6);

        GUI.backgroundColor = placementMode ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button(placementMode ? "🟢 РЕЖИМ РАССТАНОВКИ ВКЛЮЧЕН (Кликните в Scene View)" : "⚪ ВКЛЮЧИТЬ РЕЖИМ РАССТАНОВКИ", GUILayout.Height(38)))
        {
            placementMode = !placementMode;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        GUILayout.Label("── Пресет создаваемой точки ──", EditorStyles.boldLabel);
        currentPreset = (PlacementPreset)EditorGUILayout.EnumPopup("Тип лута", currentPreset);

        customSpawnChance = EditorGUILayout.Slider("Шанс спавна (%)", customSpawnChance, 10f, 100f);
        customVerticalOffset = EditorGUILayout.FloatField("Смещение вверх (Y Offset)", customVerticalOffset);

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(GetPresetDescription(currentPreset), MessageType.None);
    }

    private string GetPresetDescription(PlacementPreset preset)
    {
        switch (preset)
        {
            case PlacementPreset.Small_Loot:
                return "🟢 Small: Пачки денег, сигары, мелкие драгоценности (Capacity = 1). Идеально для столов и полок.";
            case PlacementPreset.Medium_Loot:
                return "🔵 Medium: Золотые слитки, статуэтки, дорогие часы (Capacity = 2). Для кабинетов и тумб.";
            case PlacementPreset.Large_Loot:
                return "🟣 Large: Кейсы с деньгами, чемоданы (Capacity = 4). Для пола, сейфов и багажных полок.";
            case PlacementPreset.Vault_Special:
                return "⭐ Vault: Высокая стоимость (High Value)! Спавнит самые дорогие предметы с бонусом к цене.";
            default:
                return "";
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!placementMode) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            // Рисуем маркер под курсором
            Color markerColor = GetPresetColor(currentPreset);
            Handles.color = markerColor;
            Handles.DrawWireDisc(hit.point + hit.normal * 0.02f, hit.normal, 0.25f);
            Handles.DrawDottedLine(hit.point, hit.point + Vector3.up * customVerticalOffset, 3f);

            // Обработка клика мыши
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                CreateLootPointAt(hit.point, hit.normal);
                e.Use();
            }
        }

        sceneView.Repaint();
    }

    private Color GetPresetColor(PlacementPreset preset)
    {
        switch (preset)
        {
            case PlacementPreset.Small_Loot: return Color.green;
            case PlacementPreset.Medium_Loot: return Color.cyan;
            case PlacementPreset.Large_Loot: return new Color(0.85f, 0.2f, 1f);
            case PlacementPreset.Vault_Special: return new Color(1f, 0.85f, 0.1f);
            default: return Color.white;
        }
    }

    private void CreateLootPointAt(Vector3 surfacePos, Vector3 surfaceNormal)
    {
        // Ищем контейнер комнаты
        Transform rootRoom = null;
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
        {
            rootRoom = prefabStage.prefabContentsRoot.transform;
        }
        else if (Selection.activeTransform != null)
        {
            rootRoom = Selection.activeTransform.root;
        }

        Transform parentFolder = null;
        if (rootRoom != null)
        {
            parentFolder = rootRoom.Find("_LootPoints");
            if (parentFolder == null)
            {
                GameObject folderGo = new GameObject("_LootPoints");
                folderGo.transform.SetParent(rootRoom, false);
                parentFolder = folderGo.transform;
                Undo.RegisterCreatedObjectUndo(folderGo, "Create _LootPoints Folder");
            }
        }

        string pointName = $"LootPoint_{currentPreset}";
        GameObject pointGo = new GameObject(pointName);
        pointGo.transform.position = surfacePos;
        pointGo.transform.rotation = Quaternion.identity;

        if (parentFolder != null)
        {
            pointGo.transform.SetParent(parentFolder, true);
        }

        LootPoint lp = pointGo.AddComponent<LootPoint>();
        lp.verticalOffset = customVerticalOffset;
        lp.spawnChance = customSpawnChance;

        switch (currentPreset)
        {
            case PlacementPreset.Small_Loot:
                lp.capacityUnits = 1;
                lp.sizeFilter = LootPointSizeFilter.SmallOnly;
                lp.isHighValue = false;
                break;
            case PlacementPreset.Medium_Loot:
                lp.capacityUnits = 2;
                lp.sizeFilter = LootPointSizeFilter.MediumOnly;
                lp.isHighValue = false;
                break;
            case PlacementPreset.Large_Loot:
                lp.capacityUnits = 4;
                lp.sizeFilter = LootPointSizeFilter.LargeOnly;
                lp.isHighValue = false;
                break;
            case PlacementPreset.Vault_Special:
                lp.capacityUnits = 4;
                lp.sizeFilter = LootPointSizeFilter.LargeOnly;
                lp.isHighValue = true;
                break;
        }

        Undo.RegisterCreatedObjectUndo(pointGo, "Create LootPoint");
        Selection.activeGameObject = pointGo;

        Debug.Log($"<color=green>[LootPlacer] Создана точка {pointName} в {surfacePos}</color>");

        if (prefabStage != null)
        {
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }
    }

    #endregion

    #region Tab 3: Migration & Auto-Fix

    private void DrawMigrationTab()
    {
        GUILayout.Label("Пакетная миграция и очистка комнат", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Эта операция выполнит:\n1. Конвертацию старых объектов 'Loot/LootSpawn' в полноценные LootPoint.\n2. Назначение RoomLootManager и базы TexasLoot на все комнаты.\n3. Удаление устаревшего LootSpawner со спавном бургеров.\n4. Настройку точек сейфа (S2_Safe) как HighValue.", MessageType.Warning);

        EditorGUILayout.Space(8);

        FindLootDatabase();
        cachedLootDB = (LootDatabase)EditorGUILayout.ObjectField("База лута (LootDatabase)", cachedLootDB, typeof(LootDatabase), false);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("🚀 Выполнить миграцию всех комнат (Fix All)", GUILayout.Height(36)))
        {
            if (cachedLootDB == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Сначала выберите или создайте LootDatabase (например TexasLoot)!", "OK");
                return;
            }

            MigrateAllRooms();
        }
    }

    public void MigrateAllRooms()
    {
        string[] prefabPaths = Directory.GetFiles("Assets/Rooms", "*.prefab", SearchOption.TopDirectoryOnly);
        int modifiedCount = 0;

        foreach (string path in prefabPaths)
        {
            string roomName = Path.GetFileNameWithoutExtension(path);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;

            bool isVault = roomName.ToLower().Contains("safe");
            bool dirty = false;

            // 1. Убеждаемся в наличии RoomLootManager
            RoomLootManager lootManager = prefabRoot.GetComponent<RoomLootManager>();
            if (lootManager == null)
            {
                lootManager = prefabRoot.AddComponent<RoomLootManager>();
                dirty = true;
            }

            if (lootManager.lootDB == null && cachedLootDB != null)
            {
                lootManager.lootDB = cachedLootDB;
                dirty = true;
            }
            lootManager.spawnOnStart = false;

            // 2. Ищем и конвертируем старый LootSpawner
            var oldSpawner = prefabRoot.GetComponent<LootSpawner>();
            if (oldSpawner != null)
            {
                if (oldSpawner.spawnPoints != null)
                {
                    foreach (var pt in oldSpawner.spawnPoints)
                    {
                        if (pt != null)
                        {
                            var existingLp = pt.GetComponent<LootPoint>();
                            if (existingLp == null)
                            {
                                existingLp = pt.gameObject.AddComponent<LootPoint>();
                                existingLp.capacityUnits = isVault ? 4 : 2;
                                existingLp.isHighValue = isVault;
                                existingLp.sizeFilter = isVault ? LootPointSizeFilter.LargeOnly : LootPointSizeFilter.Any;
                                dirty = true;
                            }
                        }
                    }
                }

                DestroyImmediate(oldSpawner, true);
                dirty = true;
                Debug.Log($"[Migration] Удален старый LootSpawner из {roomName}");
            }

            // 3. Ищем любые дочерние объекты со старыми именами Loot / LootSpawn
            Transform[] allChildren = prefabRoot.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.StartsWith("loot") && child.GetComponent<LootPoint>() == null && child.GetComponent<RoomLootManager>() == null)
                {
                    var lp = child.gameObject.AddComponent<LootPoint>();
                    lp.capacityUnits = isVault ? 4 : 1;
                    lp.isHighValue = isVault;
                    dirty = true;
                }
            }

            // 4. Если комната сейфа, убеждаемся что все точки в ней помечены как HighValue
            if (isVault)
            {
                LootPoint[] vaultPoints = prefabRoot.GetComponentsInChildren<LootPoint>(true);
                foreach (var vp in vaultPoints)
                {
                    vp.isHighValue = true;
                    if (vp.capacityUnits < 2) vp.capacityUnits = 2;
                    dirty = true;
                }
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                modifiedCount++;
                Debug.Log($"<color=green>[Migration] Префаб {roomName} успешно обновлен и сохранен.</color>");
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        EditorUtility.DisplayDialog("Миграция завершена", $"Успешно обработано комнат: {modifiedCount}", "OK");
        Repaint();
    }

    #endregion
}
