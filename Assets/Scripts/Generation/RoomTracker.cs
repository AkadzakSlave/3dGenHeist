using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Отслеживает статус исследования комнаты игроком, её границы и сокеты/точки спавна.
/// Используется для Тумана Войны на мини-карте и спавна патрулей только в исследованных зонах.
/// </summary>
public class RoomTracker : MonoBehaviour
{
    public static readonly List<RoomTracker> All = new List<RoomTracker>();
    public static event Action<RoomTracker> OnRoomExplored;

    [Header("Room Identity")]
    public string roomName = "Room";
    public bool isExplored = false;
    public bool isVault = false;
    public bool isStartOrVan = false;

    [Header("Calculated Bounds")]
    public Bounds localBounds;
    public Bounds worldBounds;

    private Transform cachedPlayer;
    private float checkTimer = 0f;

    private void OnEnable()
    {
        if (!All.Contains(this))
        {
            All.Add(this);
        }
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    public void Initialize(string rName, Bounds lBounds, bool isVaultRoom = false, bool isStartOrVanRoom = false)
    {
        roomName = rName.Replace("(Clone)", "").Trim();
        localBounds = lBounds;
        isVault = isVaultRoom || roomName.ToLower().Contains("safe") || roomName.ToLower().Contains("s2");
        isStartOrVan = isStartOrVanRoom || roomName.ToLower().Contains("van") || roomName.ToLower().Contains("start");

        UpdateWorldBounds();

        // Стартовая комната и фургон исследованы сразу же при старте
        if (isStartOrVan)
        {
            SetExplored(silent: true);
        }
    }

    public void UpdateWorldBounds()
    {
        Vector3 worldCenter = transform.TransformPoint(localBounds.center);
        Vector3 worldExtents = Vector3.Scale(localBounds.extents, transform.lossyScale);
        // Задаем щедрую высоту для проверки игрока по Y
        worldBounds = new Bounds(worldCenter, new Vector3(Mathf.Abs(worldExtents.x) * 2f, 10f, Mathf.Abs(worldExtents.z) * 2f));
    }

    private void Update()
    {
        if (isExplored) return;

        checkTimer += Time.deltaTime;
        if (checkTimer < 0.25f) return;
        checkTimer = 0f;

        if (cachedPlayer == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                cachedPlayer = GameManager.Instance.playerTransform;
            }
            else
            {
                PlayerMovement pm = FindAnyObjectByType<PlayerMovement>();
                if (pm != null) cachedPlayer = pm.transform;
            }
        }

        if (cachedPlayer == null) return;

        Vector3 pPos = cachedPlayer.position;
        // Проверяем нахождение игрока внутри границ комнаты в плоскости XZ
        float halfX = worldBounds.extents.x;
        float halfZ = worldBounds.extents.z;
        Vector3 c = worldBounds.center;

        if (Mathf.Abs(pPos.x - c.x) <= halfX && Mathf.Abs(pPos.z - c.z) <= halfZ)
        {
            SetExplored();
        }
    }

    public void SetExplored(bool silent = false)
    {
        if (isExplored) return;
        isExplored = true;

        if (!silent)
        {
            Debug.Log($"<color=cyan>[Minimap] Комната исследована: {roomName}</color>");
        }

        OnRoomExplored?.Invoke(this);
    }
}

