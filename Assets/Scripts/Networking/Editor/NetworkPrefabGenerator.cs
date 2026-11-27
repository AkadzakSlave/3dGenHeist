#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Mirror;
using WildBunch.Networking.Players;

namespace WildBunch.Networking.Editor
{
    [InitializeOnLoad]
    public static class NetworkPrefabGenerator
    {
        private const string SourcePrefabPath = "Assets/Prefabs/Player.prefab";
        private const string TargetPrefabPath = "Assets/Resources/NetworkPlayer.prefab";

        static NetworkPrefabGenerator()
        {
            EditorApplication.delayCall += EnsureNetworkPlayerPrefab;
        }

        [MenuItem("Tools/Wild Bunch/Generate Network Player Prefab")]
        public static void EnsureNetworkPlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) != null)
            {
                // Already generated
                return;
            }

            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourcePrefab == null)
            {
                Debug.LogWarning($"[NetworkPrefabGenerator] Source prefab not found at {SourcePrefabPath}");
                return;
            }

            // Instantiate temporary instance in editor
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            instance.name = "NetworkPlayer";

            // Add Network components if missing
            if (instance.GetComponent<NetworkIdentity>() == null)
            {
                instance.AddComponent<NetworkIdentity>();
            }

            if (instance.GetComponent<NetworkTransformReliable>() == null)
            {
                NetworkTransformReliable netTransform = instance.AddComponent<NetworkTransformReliable>();
                netTransform.syncDirection = SyncDirection.ClientToServer; // Local player controls their own movement
            }

            if (instance.GetComponent<NetworkPlayerIdentity>() == null)
            {
                instance.AddComponent<NetworkPlayerIdentity>();
            }

            if (instance.GetComponent<NetworkItemSync>() == null)
            {
                instance.AddComponent<NetworkItemSync>();
            }

            // Save as new prefab in Resources
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath);
            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[NetworkPrefabGenerator] Successfully generated {TargetPrefabPath} for co-op!");
        }
    }
}
#endif
