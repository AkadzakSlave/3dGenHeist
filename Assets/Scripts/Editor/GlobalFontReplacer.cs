using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class GlobalFontReplacer : EditorWindow
{
    private TMP_FontAsset targetFont;

    [MenuItem("Tools/Heist/Apply Inter Font To All UI (Scene & Prefabs)")]
    public static void ShowWindow()
    {
        GetWindow<GlobalFontReplacer>("Global Font Replacer");
    }

    private void OnEnable()
    {
        if (targetFont == null)
        {
            targetFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Inter SDF.asset");
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Замена шрифта во всем проекте", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Целевой шрифт (Font Asset):", targetFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Применить ко всей открытой Сцене", GUILayout.Height(35)))
        {
            ApplyFontToCurrentScene();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Применить ко ВСЕМ Префабам в проекте", GUILayout.Height(35)))
        {
            ApplyFontToAllPrefabs();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Применить ВЕЗДЕ (Сцена + Префабы)", GUILayout.Height(40)))
        {
            ApplyFontToCurrentScene();
            ApplyFontToAllPrefabs();
        }
    }

    private void ApplyFontToCurrentScene()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Пожалуйста, укажите целевой шрифт!", "OK");
            return;
        }

        TMP_Text[] sceneTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
        int count = 0;

        Undo.RecordObjects(sceneTexts, "Apply Inter Font to Scene");

        foreach (var textComp in sceneTexts)
        {
            if (textComp != null)
            {
                textComp.font = targetFont;
                EditorUtility.SetDirty(textComp);
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"<color=green>[FontReplacer] Успешно заменен шрифт на '{targetFont.name}' в {count} компонентах текущей сцены!</color>");
        EditorUtility.DisplayDialog("Готово", $"Шрифт успешно заменен в {count} текстовых объектах текущей сцены!", "OK");
    }

    private void ApplyFontToAllPrefabs()
    {
        if (targetFont == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Пожалуйста, укажите целевой шрифт!", "OK");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int prefabCount = 0;
        int textCount = 0;

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                EditorUtility.DisplayProgressBar("Замена шрифта в префабах...", path, (float)i / prefabGuids.Length);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);
                if (texts != null && texts.Length > 0)
                {
                    bool changed = false;
                    foreach (var txt in texts)
                    {
                        if (txt != null && txt.font != targetFont)
                        {
                            txt.font = targetFont;
                            EditorUtility.SetDirty(txt);
                            textCount++;
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SavePrefabAsset(prefab);
                        prefabCount++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green>[FontReplacer] Успешно обновлено {textCount} текстов в {prefabCount} префабах!</color>");
        EditorUtility.DisplayDialog("Готово", $"Шрифт успешно заменен в {textCount} текстах в {prefabCount} префабах!", "OK");
    }
}
