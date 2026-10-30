using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Редактор инструментов для настройки и расстановки точек спавна врагов в комнатах банка.
/// Меню: Tools → Enemy Setup & Placer
/// </summary>
public class EnemySetupTool : EditorWindow
{
    private int selectedTab = 0;
    private readonly string[] tabNames = new string[] { "📊 Дашборд комнат", "🎯 Ручная расстановка", "⚡ Авто-заселение" };

    private EnemySpawnRole currentPreset = EnemySpawnRole.Guard_Post;
    private EnemyType currentEnemyType = EnemyType.Guard;
    private float currentSpawnChance = 65f;
    private int currentPriority = 5;

    private Vector2 scrollPos;

    [MenuItem("Tools/Enemy Setup & Placer")]
    public static void OpenWindow()
    {
        var win = GetWindow<EnemySetupTool>("Enemy Placer & Hub");
        win.minSize = new Vector2(420, 520);
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
                DrawAutoPopulateTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region Tab 1: Dashboard

    private void DrawDashboardTab()
    {
        GUILayout.Label("Состояние комнат и точки охраны", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Здесь отображены все префабы комнат из Assets/Rooms/. Вы можете проверить наличие постов охраны, дверей патрулей и открыть любой префаб в 1 клик.", MessageType.Info);

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

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Комната", EditorStyles.boldLabel, GUILayout.Width(130));
        GUILayout.Label("Посты", EditorStyles.boldLabel, GUILayout.Width(60));
        GUILayout.Label("Сейф", EditorStyles.boldLabel, GUILayout.Width(50));
        GUILayout.Label("Двери", EditorStyles.boldLabel, GUILayout.Width(50));
        GUILayout.Label("Действие", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        int totalPosts = 0;
        int totalVault = 0;
        int totalDoors = 0;

        foreach (var path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            EnemySpawnPoint[] points = prefab.GetComponentsInChildren<EnemySpawnPoint>(true);
            int postCount = 0;
            int vaultCount = 0;
            int doorCount = 0;

            foreach (var p in points)
            {
                if (p.spawnRole == EnemySpawnRole.Vault_Guard) vaultCount++;
                else if (p.pointType == SpawnPointType.PatrolDoor || p.spawnRole == EnemySpawnRole.Patrol_Door) doorCount++;
                else postCount++;
            }

            totalPosts += postCount;
            totalVault += vaultCount;
            totalDoors += doorCount;

            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label(prefab.name, EditorStyles.label, GUILayout.Width(130));

            // Посты охраны
            GUI.color = postCount > 0 ? Color.white : Color.gray;
            GUILayout.Label(postCount.ToString(), EditorStyles.label, GUILayout.Width(60));

            // Охрана сейфа
            GUI.color = vaultCount > 0 ? Color.yellow : Color.gray;
            GUILayout.Label(vaultCount.ToString(), EditorStyles.label, GUILayout.Width(50));

            // Двери патрулей
            GUI.color = doorCount > 0 ? Color.cyan : Color.gray;
            GUILayout.Label(doorCount.ToString(), EditorStyles.label, GUILayout.Width(50));
            GUI.color = Color.white;

            if (GUILayout.Button("Открыть", GUILayout.Width(75)))
            {
                AssetDatabase.OpenAsset(prefab);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox($"Всего на комнатах: {totalPosts} постов охраны, {totalVault} охраны сейфа, {totalDoors} дверей патрулей.", MessageType.None);
    }

    #endregion

    #region Tab 2: Manual Placement

    private void DrawPlacementTab()
    {
        GUILayout.Label("Быстрая расстановка точек в открытой комнате", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Откройте префаб комнаты в режиме Prefab Stage (или выберите комнату в сцене) и добавьте точку спавна в 1 клик.", MessageType.Info);

        EditorGUILayout.Space(6);
        currentPreset = (EnemySpawnRole)EditorGUILayout.EnumPopup("Пресет роли:", currentPreset);

        switch (currentPreset)
        {
            case EnemySpawnRole.Guard_Post:
                currentSpawnChance = 65f;
                currentPriority = 5;
                currentEnemyType = EnemyType.Guard;
                EditorGUILayout.HelpBox("Пост охраны: патрулирует домашнюю комнату, 65% шанс появления.", MessageType.None);
                break;
            case EnemySpawnRole.Vault_Guard:
                currentSpawnChance = 100f;
                currentPriority = 10;
                currentEnemyType = EnemyType.Guard;
                EditorGUILayout.HelpBox("Охрана сейфа: гарантированный спавн (100%), охраняет хранилище.", MessageType.None);
                break;
            case EnemySpawnRole.Corridor_Patrol:
                currentSpawnChance = 35f;
                currentPriority = 2;
                currentEnemyType = EnemyType.Guard;
                EditorGUILayout.HelpBox("Патруль коридора: случайный патрульный (35% шанс).", MessageType.None);
                break;
            case EnemySpawnRole.Patrol_Door:
                currentSpawnChance = 100f;
                currentPriority = 0;
                EditorGUILayout.HelpBox("Дверь подкреплений: скрытая точка для таймерных патрулей и штурма спецназа.", MessageType.None);
                break;
        }

        EditorGUILayout.Space(6);
        currentSpawnChance = EditorGUILayout.Slider("Шанс спавна (%):", currentSpawnChance, 0f, 100f);
        currentPriority = EditorGUILayout.IntSlider("Приоритет квоты:", currentPriority, 1, 10);
        currentEnemyType = (EnemyType)EditorGUILayout.EnumPopup("Базовый тип врага:", currentEnemyType);

        EditorGUILayout.Space(14);

        if (GUILayout.Button("➕ Создать точку в центре выбранного объекта", GUILayout.Height(34)))
        {
            CreatePointAtSelection();
        }
    }

    private void CreatePointAtSelection()
    {
        GameObject targetParent = Selection.activeGameObject;
        if (targetParent == null)
        {
            EditorUtility.DisplayDialog("Внимание", "Пожалуйста, выделите префаб комнаты или объект в Scene/Hierarchy, куда нужно добавить точку.", "OK");
            return;
        }

        Vector3 spawnPos = targetParent.transform.position + Vector3.up * 0.1f;
        GameObject pointObj = new GameObject($"EnemySpawn_{currentPreset}");
        pointObj.transform.SetParent(targetParent.transform);
        pointObj.transform.position = spawnPos;
        pointObj.transform.rotation = targetParent.transform.rotation;

        var point = pointObj.AddComponent<EnemySpawnPoint>();
        point.spawnRole = currentPreset;
        point.spawnChance = currentSpawnChance;
        point.priority = currentPriority;
        point.initialEnemyType = currentEnemyType;
        point.pointType = (currentPreset == EnemySpawnRole.Patrol_Door) ? SpawnPointType.PatrolDoor : SpawnPointType.Initial;

        Selection.activeGameObject = pointObj;
        Undo.RegisterCreatedObjectUndo(pointObj, "Create Enemy Spawn Point");
        EditorUtility.SetDirty(targetParent);

        Debug.Log($"<color=green>[EnemySetupTool] Создана точка {pointObj.name} в {targetParent.name}</color>");
    }

    #endregion

    #region Tab 3: Auto-Populate

    private void DrawAutoPopulateTab()
    {
        GUILayout.Label("Автоматическое заселение всех комнат банка", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Инструмент автоматически просканирует все префабы комнат в Assets/Rooms/ и создаст правильные точки спавна охраны и дверей подкрепления на основе геометрии комнат:\n\n" +
            "• S2_Safe (Сейф): охрана сейфа Vault_Guard (100%) + пост охраны\n" +
            "• B1, B2, B3, B5, B7 (Большие залы): 1-2 поста охраны Guard_Post + 1 дверь подкреплений Patrol_Door\n" +
            "• S1, S3, S4, S5, L1 (Малые комнаты): 1 пост охраны Guard_Post (65%)\n" +
            "• B4, B6 (Коридоры): 1 патруль Corridor_Patrol (35%)\n" +
            "• Start (Старт): без врагов (зона безопасности)", MessageType.Info);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.3f, 1f, 0.4f);
        if (GUILayout.Button("⚡ ЗАСЕЛИТЬ ВСЕ КОМНАТЫ БАНКА В 1 КЛИК", GUILayout.Height(44)))
        {
            if (EditorUtility.DisplayDialog("Авто-заселение комнат", "Расставить точки охраны и дверей во все префабы Assets/Rooms/?\nСуществующие точки будут сохранены или обновлены.", "Да, заселить", "Отмена"))
            {
                AutoPopulateAllRooms();
            }
        }
        GUI.backgroundColor = Color.white;
    }

    public static void AutoPopulateAllRooms()
    {
        string[] prefabPaths = Directory.GetFiles("Assets/Rooms", "*.prefab", SearchOption.TopDirectoryOnly);
        int modifiedCount = 0;

        foreach (var path in prefabPaths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.Equals("Start", System.StringComparison.OrdinalIgnoreCase))
            {
                continue; // Стартовая комната без врагов
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            if (prefabRoot == null) continue;

            Transform spawnContainer = prefabRoot.transform.Find("_EnemySpawns");
            if (spawnContainer == null)
            {
                var go = new GameObject("_EnemySpawns");
                go.transform.SetParent(prefabRoot.transform, false);
                spawnContainer = go.transform;
            }

            // Находим центр пола или коллайдеров
            Vector3 centerPos = Vector3.zero;
            Collider[] colliders = prefabRoot.GetComponentsInChildren<Collider>();
            int floorCount = 0;

            foreach (var col in colliders)
            {
                if (col.gameObject.name.ToLower().Contains("floor"))
                {
                    centerPos += col.bounds.center;
                    floorCount++;
                }
            }

            if (floorCount > 0)
            {
                centerPos /= floorCount;
                centerPos.y = 0.05f; // На высоте пола
            }

            bool isSafeRoom = fileName.Contains("Safe") || fileName.Contains("S2");
            bool isBigRoom = fileName.StartsWith("B1") || fileName.StartsWith("B2") || fileName.StartsWith("B3") || fileName.StartsWith("B5") || fileName.StartsWith("B7");
            bool isCorridor = fileName.Contains("Corridor") || fileName.StartsWith("B4") || fileName.StartsWith("B6");

            // Проверяем, есть ли уже точки
            EnemySpawnPoint[] existingPoints = spawnContainer.GetComponentsInChildren<EnemySpawnPoint>();
            if (existingPoints.Length == 0)
            {
                if (isSafeRoom)
                {
                    // 1. Охрана сейфа
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_VaultGuard", centerPos + Vector3.forward * 1.5f, Quaternion.identity, EnemySpawnRole.Vault_Guard, 100f, 10, EnemyType.Guard);
                    // 2. Дополнительный пост у входа
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_GuardPost", centerPos - Vector3.forward * 2.5f, Quaternion.Euler(0, 180, 0), EnemySpawnRole.Guard_Post, 70f, 5, EnemyType.Guard);
                }
                else if (isBigRoom)
                {
                    // 1-2 поста охраны
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_GuardPost_1", centerPos + new Vector3(-2f, 0, 1.5f), Quaternion.Euler(0, 45, 0), EnemySpawnRole.Guard_Post, 75f, 6, EnemyType.Guard);
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_GuardPost_2", centerPos + new Vector3(2f, 0, -1.5f), Quaternion.Euler(0, 225, 0), EnemySpawnRole.Guard_Post, 60f, 5, EnemyType.Guard);
                    // 1 дверь патрулей/подкреплений у стены
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_PatrolDoor", centerPos + new Vector3(0, 0, 5.5f), Quaternion.Euler(0, 180, 0), EnemySpawnRole.Patrol_Door, 100f, 0, EnemyType.Guard);
                }
                else if (isCorridor)
                {
                    // Патруль коридора
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_CorridorPatrol", centerPos, Quaternion.identity, EnemySpawnRole.Corridor_Patrol, 35f, 2, EnemyType.Guard);
                }
                else
                {
                    // Обычная малая комната (S1, S3, S4, S5, L1)
                    CreateSpawnPoint(spawnContainer, "EnemySpawn_GuardPost", centerPos, Quaternion.identity, EnemySpawnRole.Guard_Post, 65f, 5, EnemyType.Guard);
                }

                modifiedCount++;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }

            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[EnemySetupTool] Успешно заселено комнат: {modifiedCount}. Точки спавна сохранены в префабы.</color>");
    }

    private static void CreateSpawnPoint(Transform parent, string name, Vector3 localPos, Quaternion localRot, EnemySpawnRole role, float chance, int priority, EnemyType type)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        var point = go.AddComponent<EnemySpawnPoint>();
        point.spawnRole = role;
        point.spawnChance = chance;
        point.priority = priority;
        point.initialEnemyType = type;
        point.pointType = (role == EnemySpawnRole.Patrol_Door) ? SpawnPointType.PatrolDoor : SpawnPointType.Initial;
    }

    #endregion
}

