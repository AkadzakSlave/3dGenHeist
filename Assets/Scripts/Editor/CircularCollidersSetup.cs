using UnityEngine;
using UnityEditor;

/// <summary>
/// Создаёт N BoxCollider'ов расставленных по окружности — для круглых стен, балконов, ротонд.
/// Меню: Tools → Create Circular Colliders
/// </summary>
public class CircularCollidersSetup : EditorWindow
{
    private int   segments       = 10;
    private float radius         = 9f;
    private float wallHeight     = 3f;
    private float wallThickness  = 0.3f;
    private float yOffset        = 0f;
    private string parentName    = "CircularColliders";

    [MenuItem("Tools/Create Circular Colliders")]
    public static void OpenWindow()
    {
        var win = GetWindow<CircularCollidersSetup>("Circular Colliders");
        win.minSize = new Vector2(280, 300);
    }

    private void OnGUI()
    {
        GUILayout.Label("── Параметры ──", EditorStyles.boldLabel);

        segments      = EditorGUILayout.IntSlider("Кол-во сегментов", segments, 4, 32);
        radius        = EditorGUILayout.FloatField("Радиус (м)", radius);
        wallHeight    = EditorGUILayout.FloatField("Высота стены (м)", wallHeight);
        wallThickness = EditorGUILayout.FloatField("Толщина стены (м)", wallThickness);
        yOffset       = EditorGUILayout.FloatField("Смещение по Y", yOffset);
        parentName    = EditorGUILayout.TextField("Имя родителя", parentName);

        EditorGUILayout.Space(8);

        float segAngle    = 360f / segments;
        float segArcLen   = 2f * Mathf.PI * radius / segments;

        EditorGUILayout.HelpBox(
            "Угол сегмента: " + segAngle.ToString("F1") + "°\n" +
            "Длина сегмента: ~" + segArcLen.ToString("F2") + " м\n" +
            "Выдели объект в Hierarchy — коллайдеры создадутся как его дочерние.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        var sel = Selection.activeGameObject;

        if (GUILayout.Button("Создать коллайдеры по кругу", GUILayout.Height(36)))
        {
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Нет выделения",
                    "Выдели в Hierarchy объект-контейнер (например, корень комнаты).", "OK");
                return;
            }
            CreateCircularColliders(sel.transform);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Совет: после создания включи Gizmos в Scene View,\n" +
            "чтобы видеть зелёные BoxCollider'ы и подогнать радиус.",
            MessageType.None);
    }

    private void CreateCircularColliders(Transform parent)
    {
        Undo.SetCurrentGroupName("Create Circular Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        // Создаём контейнер
        GameObject container = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(container, "Create container");
        container.transform.SetParent(parent);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;

        float segAngle  = 360f / segments;
        float segLength = 2f * Mathf.PI * radius / segments + 0.05f; // небольшой overlap

        for (int i = 0; i < segments; i++)
        {
            float angle = i * segAngle;
            float rad   = angle * Mathf.Deg2Rad;

            // Позиция центра сегмента на окружности
            Vector3 pos = new Vector3(
                Mathf.Sin(rad) * radius,
                yOffset + wallHeight * 0.5f,
                Mathf.Cos(rad) * radius
            );

            GameObject seg = new GameObject("Wall_Seg_" + i.ToString("D2"));
            Undo.RegisterCreatedObjectUndo(seg, "Create segment");
            seg.transform.SetParent(container.transform);
            seg.transform.localPosition = pos;
            seg.transform.localRotation = Quaternion.Euler(0, angle, 0);

            BoxCollider bc   = Undo.AddComponent<BoxCollider>(seg);
            bc.size   = new Vector3(segLength, wallHeight, wallThickness);
            bc.center = Vector3.zero;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = container;

        Debug.Log("[CircularColliders] Создано " + segments + " сегментов. Радиус=" + radius +
                  "м, Высота=" + wallHeight + "м, Толщина=" + wallThickness + "м");
    }
}
