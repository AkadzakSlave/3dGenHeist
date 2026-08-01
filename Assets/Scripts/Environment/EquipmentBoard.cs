using System;
using System.Collections.Generic;
using UnityEngine;

public class EquipmentBoard : MonoBehaviour
{
    [Serializable]
    public class BoardRackSlot
    {
        public string slotName = "Equipment Rack";
        public ItemData itemData;
        public GameObject customPickupPrefab;
        public Transform[] spawnPoints;
        [HideInInspector] public List<GameObject> spawnedInstances = new List<GameObject>();
    }

    [Header("Rack Configurations")]
    public List<BoardRackSlot> rackSlots = new List<BoardRackSlot>();

    private void Start()
    {
        if (TeamStorageManager.Instance != null)
        {
            TeamStorageManager.Instance.onStorageChanged.AddListener(RefreshBoard);
        }
        RefreshBoard();
    }

    private void OnDestroy()
    {
        if (TeamStorageManager.Instance != null)
        {
            TeamStorageManager.Instance.onStorageChanged.RemoveListener(RefreshBoard);
        }
    }

    public void RefreshBoard()
    {
        if (TeamStorageManager.Instance == null) return;

        foreach (var slot in rackSlots)
        {
            if (slot == null || slot.itemData == null || slot.spawnPoints == null) continue;

            // Clear old instances
            foreach (var instance in slot.spawnedInstances)
            {
                if (instance != null) Destroy(instance);
            }
            slot.spawnedInstances.Clear();

            int ownedCount = TeamStorageManager.Instance.GetItemCount(slot.itemData);
            int spawnLimit = Mathf.Min(ownedCount, slot.spawnPoints.Length);

            GameObject prefabToSpawn = slot.customPickupPrefab != null ? slot.customPickupPrefab : slot.itemData.dropPrefab;
            if (prefabToSpawn == null) continue;

            for (int i = 0; i < spawnLimit; i++)
            {
                Transform anchor = slot.spawnPoints[i];
                if (anchor == null) continue;

                GameObject obj = Instantiate(prefabToSpawn, anchor.position, anchor.rotation, anchor);
                
                WorldEquipment worldEq = obj.GetComponent<WorldEquipment>();
                if (worldEq == null) worldEq = obj.AddComponent<WorldEquipment>();

                worldEq.itemData = slot.itemData;
                worldEq.isFromTeamStorage = true;

                // Disable gravity if placed on a wall rack so it doesn't fall off
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                }

                slot.spawnedInstances.Add(obj);
            }
        }
    }
}
