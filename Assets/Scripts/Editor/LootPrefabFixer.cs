using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Утилита для стандартизации и автоматической калибровки всех префабов лута.
/// Меню: Tools → Standardize All Loot Prefabs
/// </summary>
public static class LootPrefabFixer
{
    private const string SoundPath = "event:/Environment/Items/Loot/Loot_PickUp";

    [MenuItem("Tools/Standardize All Loot Prefabs")]
    public static void StandardizeAll()
    {
        int fixedCount = 0;

        // 1. Поиск базы лута TexasLoot
        string[] dbGuids = AssetDatabase.FindAssets("TexasLoot t:LootDatabase");
        LootDatabase db = null;
        if (dbGuids.Length > 0)
        {
            db = AssetDatabase.LoadAssetAtPath<LootDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
        }

        // 2. Проверяем и создаем Ruby.prefab, если нужно
        EnsureRubyPrefab();

        // 3. Проверяем и создаем GangStatue.prefab, если нужно
        EnsureGangStatuePrefab();

        // 4. Калибруем все префабы в Assets/Prefabs/Loot
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Loot" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Table_Rich")) continue; // Стол с пивотами пропускаем

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            bool dirty = false;

            // Находим все рендереры для вычисления истинных визуальных границ модели
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                // Если модель миниатюрная (единицы измерения FBX < 5см), масштабируем
                if (b.size.magnitude < 0.05f)
                {
                    root.transform.localScale *= 50f;
                    b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                    dirty = true;
                }

                // Настраиваем BoxCollider ровно по модели
                BoxCollider box = root.GetComponent<BoxCollider>();
                if (box == null)
                {
                    box = root.AddComponent<BoxCollider>();
                    dirty = true;
                }

                Vector3 localCenter = box.transform.InverseTransformPoint(b.center);
                Vector3 localSize = box.transform.InverseTransformVector(b.size);
                Vector3 targetSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                targetSize = Vector3.Max(targetSize, new Vector3(0.08f, 0.04f, 0.08f));

                if (Vector3.Distance(box.center, localCenter) > 0.005f || Vector3.Distance(box.size, targetSize) > 0.005f)
                {
                    box.center = localCenter;
                    box.size = targetSize;
                    dirty = true;
                }
            }

            // Настраиваем компонент LootItem
            LootItem item = root.GetComponent<LootItem>();
            if (item == null)
            {
                item = root.AddComponent<LootItem>();
                dirty = true;
            }

            // Привязываем LootData, если еще не привязана
            if (item.data == null)
            {
                string baseName = Path.GetFileNameWithoutExtension(path);
                string[] dataGuids = AssetDatabase.FindAssets($"{baseName} t:LootData");
                if (dataGuids.Length > 0)
                {
                    item.data = AssetDatabase.LoadAssetAtPath<LootData>(AssetDatabase.GUIDToAssetPath(dataGuids[0]));
                    dirty = true;
                }
            }

            // Устанавливаем звук подбора
            if (string.IsNullOrEmpty(item.pickUpSound.Path))
            {
                item.pickUpSound = FMODUnity.RuntimeManager.PathToEventReference(SoundPath);
                dirty = true;
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                fixedCount++;
                Debug.Log($"<color=green>[LootFixer] Префаб успешно откалиброван: {Path.GetFileName(path)}</color>");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>[LootFixer] Стандартизация завершена. Обработано префабов: {fixedCount}</color>");
        EditorUtility.DisplayDialog("Loot Standardizer", $"Стандартизация завершена!\nОткалибровано префабов: {fixedCount}", "OK");
    }

    private static void EnsureRubyPrefab()
    {
        string prefabPath = "Assets/Prefabs/Loot/Ruby.prefab";
        string fbxPath = "Assets/fbx/Loot/Ruby.fbx";
        string dataPath = "Assets/Prefabs/Loot/LootAsset/Ruby.asset";

        if (!File.Exists(prefabPath) && File.Exists(fbxPath))
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx != null)
            {
                GameObject instance = Object.Instantiate(fbx);
                instance.name = "Ruby";
                instance.transform.localScale = Vector3.one * 15f; // Реалистичный размер драгоценного камня (~10-15см)

                var box = instance.AddComponent<BoxCollider>();
                Renderer r = instance.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    box.center = instance.transform.InverseTransformPoint(r.bounds.center);
                    box.size = instance.transform.InverseTransformVector(r.bounds.size);
                    box.size = Vector3.Max(box.size, new Vector3(0.08f, 0.05f, 0.08f));
                }

                var loot = instance.AddComponent<LootItem>();
                LootData data = AssetDatabase.LoadAssetAtPath<LootData>(dataPath);
                loot.data = data;
                loot.pickUpSound = FMODUnity.RuntimeManager.PathToEventReference(SoundPath);

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                if (data != null)
                {
                    data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    EditorUtility.SetDirty(data);
                }
                Debug.Log("<color=green>[LootFixer] Создан новый префаб: Ruby.prefab</color>");
            }
        }
    }

    private static void EnsureGangStatuePrefab()
    {
        string prefabPath = "Assets/Prefabs/Loot/GangStatue.prefab";
        string fbxPath = "Assets/fbx/Loot/maya.fbx";
        string dataPath = "Assets/Prefabs/Loot/LootAsset/GangStatue.asset";

        if (!File.Exists(prefabPath) && File.Exists(fbxPath))
        {
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx != null)
            {
                GameObject instance = Object.Instantiate(fbx);
                instance.name = "GangStatue";
                instance.transform.localScale = Vector3.one * 0.25f; // Реалистичный размер золотой статуэтки (~25-30см)

                var box = instance.AddComponent<BoxCollider>();
                Renderer r = instance.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    box.center = instance.transform.InverseTransformPoint(r.bounds.center);
                    box.size = instance.transform.InverseTransformVector(r.bounds.size);
                    box.size = Vector3.Max(box.size, new Vector3(0.12f, 0.25f, 0.12f));
                }

                var loot = instance.AddComponent<LootItem>();
                LootData data = AssetDatabase.LoadAssetAtPath<LootData>(dataPath);
                loot.data = data;
                loot.pickUpSound = FMODUnity.RuntimeManager.PathToEventReference(SoundPath);

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);

                if (data != null)
                {
                    data.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    EditorUtility.SetDirty(data);
                }
                Debug.Log("<color=green>[LootFixer] Создан новый префаб: GangStatue.prefab</color>");
            }
        }
    }
}
