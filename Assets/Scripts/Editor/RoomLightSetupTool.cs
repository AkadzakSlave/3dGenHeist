using System.IO;
using UnityEditor;
using UnityEngine;

namespace Heist.EditorTools
{
    public class RoomLightSetupTool : EditorWindow
    {
        [SerializeField] private GameObject lightPrefab;
        [SerializeField] private Material bulbEmissionMaterial;
        [SerializeField] private Vector3 lightOffsetInBulb = new Vector3(0f, -0.05f, 0f);

        [MenuItem("Tools/Heist/Расставить свет по комнатам (Room Lights)")]
        public static void ShowWindow()
        {
            var window = GetWindow<RoomLightSetupTool>("Room Lights Setup");
            window.minSize = new Vector2(440, 360);
        }

        private void OnEnable()
        {
            if (lightPrefab == null)
            {
                lightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Point Light.prefab");
            }
            if (bulbEmissionMaterial == null)
            {
                bulbEmissionMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_LampBulb_Emission.mat");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Привязка Point Light к лампочкам (M_LampBulb_Emission)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Этот инструмент находит 3D-модели плафонов (с материалом M_LampBulb_Emission) " +
                "и встраивает ваш Point Light прямо внутрь каждой лампочки, чтобы она физически светила.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            lightPrefab = (GameObject)EditorGUILayout.ObjectField("Префаб света (Point Light):", lightPrefab, typeof(GameObject), false);
            bulbEmissionMaterial = (Material)EditorGUILayout.ObjectField("Материал плафона (Emission):", bulbEmissionMaterial, typeof(Material), false);
            lightOffsetInBulb = EditorGUILayout.Vector3Field("Смещение внутри плафона:", lightOffsetInBulb);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Действия:", EditorStyles.boldLabel);

            // Кнопка для быстрого добавления в текущий выделенный объект
            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f);
            if (GUILayout.Button("1. Встроить Point Light в ВЫДЕЛЕННЫЙ объект (Cube.153)", GUILayout.Height(34)))
            {
                AttachLightToSelectedObject();
            }

            EditorGUILayout.Space(5);
            // Кнопка автоматического поиска по всем комнатам
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("2. Найти все лампочки во ВСЕХ комнатах (Assets/Rooms/) и встроить свет", GUILayout.Height(38)))
            {
                AttachLightsToAllRoomBulbs();
            }

            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Удалить добавленные Point Light из лампочек", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Подтверждение", "Удалить Point Light из лампочек комнат?", "Да, удалить", "Отмена"))
                {
                    RemoveLightsFromBulbs();
                }
            }

            GUI.backgroundColor = Color.white;
        }

        private void AttachLightToSelectedObject()
        {
            if (lightPrefab == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Назначьте префаб Point Light!", "OK");
                return;
            }

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Объект не выбран", "Пожалуйста, выделите объект лампочки (Cube.153) в окне Hierarchy или Scene!", "OK");
                return;
            }

            // Убеждаемся, что на объекте висит светящийся материал
            if (bulbEmissionMaterial != null)
            {
                var renderer = selected.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Undo.RecordObject(renderer, "Assign Emission Material");
                    renderer.sharedMaterial = bulbEmissionMaterial;
                }
            }

            // Удаляем старый свет внутри лампочки, если он там уже был
            var existingLight = selected.GetComponentInChildren<Light>();
            if (existingLight != null && existingLight.transform.parent == selected.transform)
            {
                Undo.DestroyObjectImmediate(existingLight.gameObject);
            }

            // Создаем экземпляр Point Light префаба
            GameObject newLight;
            if (PrefabUtility.IsPartOfPrefabAsset(selected))
            {
                newLight = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab);
                newLight.transform.SetParent(selected.transform, false);
            }
            else
            {
                newLight = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab, selected.transform);
                Undo.RegisterCreatedObjectUndo(newLight, "Instantiate Light in Bulb");
            }

            newLight.transform.localPosition = lightOffsetInBulb;
            newLight.transform.localRotation = Quaternion.identity;

            EditorUtility.SetDirty(selected);
            EditorUtility.DisplayDialog("Готово!", $"Свет успешно помещен внутрь лампочки '{selected.name}'!", "OK");
        }

        private void AttachLightsToAllRoomBulbs()
        {
            if (lightPrefab == null)
            {
                EditorUtility.DisplayDialog("Ошибка", "Назначьте префаб Point Light!", "OK");
                return;
            }

            string[] roomGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms" });
            if (roomGuids == null || roomGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("Ошибка", "Префабы комнат не найдены!", "OK");
                return;
            }

            int bulbsFoundTotal = 0;
            int roomsModified = 0;

            try
            {
                for (int i = 0; i < roomGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(roomGuids[i]);
                    EditorUtility.DisplayProgressBar("Встраивание света в лампочки", $"Комната: {Path.GetFileName(path)}", (float)i / roomGuids.Length);

                    GameObject roomRoot = PrefabUtility.LoadPrefabContents(path);
                    if (roomRoot == null) continue;

                    var renderers = roomRoot.GetComponentsInChildren<MeshRenderer>(true);
                    bool roomChanged = false;

                    foreach (var mr in renderers)
                    {
                        if (IsBulbRenderer(mr))
                        {
                            // Назначаем свежий светящийся материал, если нужно
                            if (bulbEmissionMaterial != null)
                            {
                                mr.sharedMaterial = bulbEmissionMaterial;
                            }

                            // Проверяем, нет ли уже света внутри этой лампочки
                            var childLight = mr.GetComponentInChildren<Light>();
                            if (childLight != null && childLight.transform.parent == mr.transform)
                            {
                                DestroyImmediate(childLight.gameObject);
                            }

                            // Встраиваем Point Light прямо внутрь плафона лампочки
                            GameObject lightInstance = (GameObject)PrefabUtility.InstantiatePrefab(lightPrefab, mr.transform);
                            if (lightInstance != null)
                            {
                                lightInstance.transform.localPosition = lightOffsetInBulb;
                                lightInstance.transform.localRotation = Quaternion.identity;
                                bulbsFoundTotal++;
                                roomChanged = true;
                            }
                        }
                    }

                    if (roomChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(roomRoot, path);
                        roomsModified++;
                    }

                    PrefabUtility.UnloadPrefabContents(roomRoot);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Успех!", $"Point Light успешно встроен в {bulbsFoundTotal} лампочек по всем комнатам ({roomsModified} комнат обновлено)!", "Отлично");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool IsBulbRenderer(MeshRenderer mr)
        {
            if (mr == null) return false;

            // 1. Проверка по материалу
            if (bulbEmissionMaterial != null)
            {
                foreach (var mat in mr.sharedMaterials)
                {
                    if (mat != null && (mat == bulbEmissionMaterial || mat.name.Contains("M_LampBulb")))
                    {
                        return true;
                    }
                }
            }

            // 2. Проверка по имени объекта или меша
            string objName = mr.gameObject.name.ToLower();
            if (objName.Contains("cube.153") || objName.Contains("bulb") || objName.Contains("lampbulb"))
            {
                return true;
            }

            return false;
        }

        private void RemoveLightsFromBulbs()
        {
            string[] roomGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Rooms" });
            int removed = 0;

            try
            {
                for (int i = 0; i < roomGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(roomGuids[i]);
                    GameObject roomRoot = PrefabUtility.LoadPrefabContents(path);
                    if (roomRoot == null) continue;

                    var lights = roomRoot.GetComponentsInChildren<Light>(true);
                    bool changed = false;
                    for (int j = lights.Length - 1; j >= 0; j--)
                    {
                        if (lights[j] != null)
                        {
                            DestroyImmediate(lights[j].gameObject);
                            removed++;
                            changed = true;
                        }
                    }

                    if (changed) PrefabUtility.SaveAsPrefabAsset(roomRoot, path);
                    PrefabUtility.UnloadPrefabContents(roomRoot);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Готово", $"Удалено {removed} источников света.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
