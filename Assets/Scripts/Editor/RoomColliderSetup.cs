using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Инструмент для быстрой настройки коллайдеров на объектах комнаты.
/// Меню: Tools → Room Collider Setup
/// </summary>
public class RoomColliderSetup : EditorWindow
{
    private enum ColliderType { BoxCollider, MeshCollider, SphereCollider, CapsuleCollider }

    private ColliderType colliderType = ColliderType.BoxCollider;
    private bool convexMesh = true;
    private bool skipIfHasCollider = true;
    private bool skipIfNoRenderer = true;
    private bool includeChildren = true;
    private bool autoFitBox = true;

    // Фильтр по имени объекта
    private string filterContains = "";
    private bool useFilter = false;

    private Vector2 scroll;

    [MenuItem("Tools/Room Collider Setup")]
    public static void OpenWindow()
    {
        var win = GetWindow<RoomColliderSetup>("Collider Setup");
        win.minSize = new Vector2(320, 380);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("── Тип коллайдера ──", EditorStyles.boldLabel);
        colliderType = (ColliderType)EditorGUILayout.EnumPopup("Тип", colliderType);

        if (colliderType == ColliderType.MeshCollider)
            convexMesh = EditorGUILayout.Toggle("Convex (для движущихся)", convexMesh);

        if (colliderType == ColliderType.BoxCollider)
            autoFitBox = EditorGUILayout.Toggle("Auto-Fit по Renderer bounds", autoFitBox);

        EditorGUILayout.Space(8);

        GUILayout.Label("── Опции ──", EditorStyles.boldLabel);
        includeChildren    = EditorGUILayout.Toggle("Обработать дочерние объекты", includeChildren);
        skipIfHasCollider  = EditorGUILayout.Toggle("Пропускать объекты с коллайдером", skipIfHasCollider);
        skipIfNoRenderer   = EditorGUILayout.Toggle("Пропускать без Renderer (невидимые)", skipIfNoRenderer);

        EditorGUILayout.Space(8);

        GUILayout.Label("── Фильтр по имени ──", EditorStyles.boldLabel);
        useFilter = EditorGUILayout.Toggle("Включить фильтр", useFilter);
        if (useFilter)
        {
            filterContains = EditorGUILayout.TextField("Имя содержит", filterContains);
            EditorGUILayout.HelpBox("Например: 'Table', 'Cabinet', 'Shelf' — добавит коллайдер только этим объектам.", MessageType.Info);
        }

        EditorGUILayout.Space(12);

        var sel = Selection.gameObjects;
        EditorGUILayout.HelpBox(
            "Выделено объектов: " + sel.Length + "\nВыдели нужные объекты в Hierarchy, затем нажми кнопку.",
            MessageType.None);

        EditorGUILayout.Space(8);

        GUI.enabled = sel.Length > 0;

        if (GUILayout.Button("Добавить коллайдеры", GUILayout.Height(36)))
            ProcessSelected(sel);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Удалить коллайдеры с выделенных", GUILayout.Height(28)))
            RemoveColliders(sel);

        GUI.enabled = true;

        EditorGUILayout.Space(12);

        GUILayout.Label("── Быстрые пресеты ──", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Мебель\n(Box, Static)", GUILayout.Height(40)))
            ApplyPreset(ColliderType.BoxCollider, false, true, true, true, false, "");
        if (GUILayout.Button("Лестницы\n(Mesh, Static)", GUILayout.Height(40)))
            ApplyPreset(ColliderType.MeshCollider, false, true, true, true, false, "");
        if (GUILayout.Button("Бочки/Ящики\n(Mesh, Convex)", GUILayout.Height(40)))
            ApplyPreset(ColliderType.MeshCollider, true, true, false, true, false, "");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void ApplyPreset(ColliderType type, bool convex, bool skipExisting, bool skipNoRenderer, bool children, bool filter, string filterStr)
    {
        colliderType       = type;
        convexMesh         = convex;
        skipIfHasCollider  = skipExisting;
        skipIfNoRenderer   = skipNoRenderer;
        includeChildren    = children;
        useFilter          = filter;
        filterContains     = filterStr;
        ProcessSelected(Selection.gameObjects);
    }

    private void ProcessSelected(GameObject[] selected)
    {
        if (selected.Length == 0)
        {
            Debug.LogWarning("[RoomColliderSetup] Ничего не выделено.");
            return;
        }

        int added = 0, skipped = 0;

        var targets = new List<GameObject>();
        foreach (var go in selected)
        {
            targets.Add(go);
            if (includeChildren)
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    if (child.gameObject != go)
                        targets.Add(child.gameObject);
        }

        Undo.SetCurrentGroupName("Add Colliders (RoomColliderSetup)");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var go in targets)
        {
            // Фильтр по имени
            if (useFilter && !string.IsNullOrEmpty(filterContains))
                if (!go.name.Contains(filterContains, System.StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }

            // Пропускаем объекты без Renderer
            if (skipIfNoRenderer && go.GetComponent<Renderer>() == null) { skipped++; continue; }

            // Пропускаем если уже есть коллайдер
            if (skipIfHasCollider && go.GetComponent<Collider>() != null) { skipped++; continue; }

            Undo.RecordObject(go, "Add Collider");

            switch (colliderType)
            {
                case ColliderType.BoxCollider:
                    var bc = Undo.AddComponent<BoxCollider>(go);
                    if (autoFitBox) FitBoxCollider(bc);
                    break;
                case ColliderType.MeshCollider:
                    var mc = Undo.AddComponent<MeshCollider>(go);
                    mc.convex = convexMesh;
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null) mc.sharedMesh = mf.sharedMesh;
                    break;
                case ColliderType.SphereCollider:
                    Undo.AddComponent<SphereCollider>(go);
                    break;
                case ColliderType.CapsuleCollider:
                    Undo.AddComponent<CapsuleCollider>(go);
                    break;
            }

            added++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("[RoomColliderSetup] Добавлено коллайдеров: " + added + ", пропущено: " + skipped);
    }

    private void FitBoxCollider(BoxCollider bc)
    {
        var renderer = bc.GetComponent<Renderer>();
        if (renderer == null) return;
        var bounds = renderer.localBounds;
        bc.center = bounds.center;
        bc.size   = bounds.size;
    }

    private void RemoveColliders(GameObject[] selected)
    {
        int removed = 0;
        Undo.SetCurrentGroupName("Remove Colliders (RoomColliderSetup)");
        int undoGroup = Undo.GetCurrentGroup();

        var targets = new List<GameObject>();
        foreach (var go in selected)
        {
            targets.Add(go);
            if (includeChildren)
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    if (child.gameObject != go)
                        targets.Add(child.gameObject);
        }

        foreach (var go in targets)
        {
            var colliders = go.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                Undo.DestroyObjectImmediate(col);
                removed++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("[RoomColliderSetup] Удалено коллайдеров: " + removed);
    }
}
