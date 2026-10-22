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
    private readonly string[] tabNames = new string[] { "📊 Дашборд комнат", "🎯 Быстрая расстановка лута", "⚙️ Пакетная миграция" };

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
        FindLootDatabase();
    }

    private void OnDisable()
    {
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

    #region Tab 2: Placement Mode

    private void DrawPlacementTab()
    {
        GUILayout.Label("Расстановка точек лута на мебель", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("1. Откройте нужный префаб комнаты.\n2. Выделите в SceneView или в Hierarchy мебель (стол, полку, тумбу, комод).\n3. Нажмите кнопку нужного размера лута — точка встанет ровно на верхнюю поверхность!\n(Можно выделять сразу несколько объектов через Ctrl/Shift).", MessageType.Info);

        EditorGUILayout.Space(10);
        customSpawnChance = EditorGUILayout.Slider("Шанс спавна (%)", customSpawnChance, 10f, 100f);
        customVerticalOffset = EditorGUILayout.FloatField("Смещение вверх (Y Offset)", customVerticalOffset);

        EditorGUILayout.Space(14);
        GUILayout.Label("── 🎯 Поставить точку на ВЫДЕЛЕННЫЕ объекты ──", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("🟢 Поставить Small\n(Полки, узкие тумбы)", GUILayout.Height(44)))
        {
            PlaceOnSelectedObjects(PlacementPreset.Small_Loot);
        }

        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("🔵 Поставить Medium\n(Столы, комоды)", GUILayout.Height(44)))
        {
            PlaceOnSelectedObjects(PlacementPreset.Medium_Loot);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.9f, 0.4f, 1f);
        if (GUILayout.Button("🟣 Поставить Large\n(Широкие столы, пол, комод)", GUILayout.Height(40)))
        {
            PlaceOnSelectedObjects(PlacementPreset.Large_Loot);
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
        if (GUILayout.Button("⭐ Поставить Vault\n(Сейф / Золото)", GUILayout.Height(40)))
        {
            PlaceOnSelectedObjects(PlacementPreset.Vault_Special);
        }
        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox("💡 Адаптивный спавн: на Large точке могут случайно появиться 1 большой кейс, 1-2 слитка золота или 2-3 пачки денег рядом! На Medium — 1 слиток или 1-2 пачки денег.", MessageType.None);

        EditorGUILayout.Space(14);
        GUILayout.Label("── 🔍 Авто-детект по именам моделей (Blender / Unity) ──", EditorStyles.boldLabel);
        if (GUILayout.Button("🔍 Найти мебель (Стеллажи, столы, шкафы) и расставить лут", GUILayout.Height(32)))
        {
            AutoDetectFurnitureInCurrentRoom();
        }
    }

    private void PlaceOnSelectedObjects(PlacementPreset preset)
    {
        currentPreset = preset;
        PlaceOnSelectedObjects();
    }

    private void PlaceOnSelectedObjects()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Внимание", "Выделите в Hierarchy или в SceneView хотя бы один объект (стол, полку, тумбу)!", "OK");
            return;
        }

        int count = 0;
        foreach (var go in Selection.gameObjects)
        {
            if (go == null) continue;
            Renderer rend = go.GetComponent<Renderer>();
            Vector3 pos;
            if (rend != null)
            {
                pos = new Vector3(rend.bounds.center.x, rend.bounds.max.y, rend.bounds.center.z);
            }
            else
            {
                pos = go.transform.position;
            }

            CreateLootPointAt(pos, Vector3.up);
            count++;
        }

        Debug.Log($"<color=green>[LootPlacer] Создано точек на выделенных объектах: {count}</color>");
    }

    private void AutoDetectFurnitureInCurrentRoom()
    {
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

        if (rootRoom == null)
        {
            EditorUtility.DisplayDialog("Внимание", "Откройте префаб комнаты в Prefab Mode или выделите комнату на сцене!", "OK");
            return;
        }

        // Поддержка русских названий из Blender (стелаж, стол, полка, шкаф и т.д.)
        string[] keywords = new string[] {
            "стелаж", "стеллаж", "стол", "тумба", "шкаф", "полка", "комод", "сейф", "стойка",
            "table", "desk", "shelf", "counter", "cabinet", "stand", "cupboard", "commode", "safe"
        };
        Renderer[] allRenderers = rootRoom.GetComponentsInChildren<Renderer>(true);

        int created = 0;
        foreach (var rend in allRenderers)
        {
            if (rend == null) continue;
            string nameLower = rend.gameObject.name.ToLower();

            bool matched = false;
            foreach (var kw in keywords)
            {
                if (nameLower.Contains(kw))
                {
                    matched = true;
                    break;
                }
            }

            if (!matched) continue;

            // Проверяем, нет ли уже рядом точки лута
            Vector3 topCenter = new Vector3(rend.bounds.center.x, rend.bounds.max.y, rend.bounds.center.z);
            LootPoint[] existing = rootRoom.GetComponentsInChildren<LootPoint>(true);
            bool alreadyHasPoint = false;
            foreach (var ep in existing)
            {
                if (ep != null && Vector3.Distance(ep.transform.position, topCenter) < 0.4f)
                {
                    alreadyHasPoint = true;
                    break;
                }
            }

            if (alreadyHasPoint) continue;

            // Определяем тип точки по имени/размеру
            if (nameLower.Contains("safe") || nameLower.Contains("сейф"))
            {
                currentPreset = PlacementPreset.Vault_Special;
            }
            else if (nameLower.Contains("shelf") || nameLower.Contains("стелаж") || nameLower.Contains("полка") || rend.bounds.size.x < 0.6f || rend.bounds.size.z < 0.6f)
            {
                currentPreset = PlacementPreset.Small_Loot;
            }
            else if (rend.bounds.size.x >= 1.2f || rend.bounds.size.z >= 1.2f)
            {
                currentPreset = PlacementPreset.Large_Loot;
            }
            else
            {
                currentPreset = PlacementPreset.Medium_Loot;
            }

            CreateLootPointAt(topCenter, Vector3.up);
            created++;
        }

        EditorUtility.DisplayDialog("Авто-детект завершен", $"Найдено мебели и создано точек: {created}", "OK");
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
                lp.sizeFilter = LootPointSizeFilter.Any; // 1 средний или 1-2 малых со смещением
                lp.isHighValue = false;
                break;
            case PlacementPreset.Large_Loot:
                lp.capacityUnits = 4;
                lp.sizeFilter = LootPointSizeFilter.Any; // 1 крупный, 1-2 средних или 2-3 малых
                lp.isHighValue = false;
                break;
            case PlacementPreset.Vault_Special:
                lp.capacityUnits = 4;
                lp.sizeFilter = LootPointSizeFilter.Any; // Богатый лут сейфа
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
