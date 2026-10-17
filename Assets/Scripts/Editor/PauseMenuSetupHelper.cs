using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PauseMenuSetupHelper
{
    static PauseMenuSetupHelper()
    {
        EditorApplication.delayCall += EnsurePauseMenuDarkBackground;
    }

    [MenuItem("Tools/Heist/Setup Pause Menu Dark Background in Scene")]
    public static void EnsurePauseMenuDarkBackground()
    {
        PauseMenuUI pauseMenu = Object.FindAnyObjectByType<PauseMenuUI>();
        if (pauseMenu == null) return;

        if (pauseMenu.darkBackgroundPanel != null) return;

        Transform controllerTransform = pauseMenu.transform;
        Transform existing = controllerTransform.Find("DarkBackground");

        GameObject darkObj;
        if (existing != null)
        {
            darkObj = existing.gameObject;
        }
        else
        {
            darkObj = new GameObject("DarkBackground", typeof(RectTransform), typeof(Image));
            darkObj.transform.SetParent(controllerTransform, false);
            darkObj.transform.SetAsFirstSibling();

            RectTransform rt = darkObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            Image img = darkObj.GetComponent<Image>();
            img.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);
            img.raycastTarget = false; // Не блокирует клики по кнопкам

            Undo.RegisterCreatedObjectUndo(darkObj, "Create DarkBackground for PauseMenu");
        }

        // Помещаем на самый нижний слой (ниже всех меню)
        darkObj.transform.SetAsFirstSibling();

        Undo.RecordObject(pauseMenu, "Assign DarkBackground to PauseMenuUI");
        pauseMenu.darkBackgroundPanel = darkObj;

        darkObj.SetActive(false);

        EditorUtility.SetDirty(pauseMenu);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("<color=green>[PauseMenuSetupHelper] Статический DarkBackground успешно настроен в иерархии сцены под всеми элементами паузы!</color>");
    }
}
