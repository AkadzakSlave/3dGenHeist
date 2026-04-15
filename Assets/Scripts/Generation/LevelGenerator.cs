using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class SocketData
{
    public RoomSocket socket;
    public int depth;

    public SocketData(RoomSocket socket, int depth)
    {
        this.socket = socket;
        this.depth = depth;
    }
}

public class LevelGenerator : MonoBehaviour
{
    [Header("Generation Settings (Dynamic)")]
    // Текущий выбранный пресет карты (позже будет передаваться из Лобби)
    public LevelPreset activePreset; 
    
    public int minRooms = 10;
    public int maxRooms = 20;
    [Tooltip("Retry count if generated rooms are less than minRooms")]
    public int maxRetries = 3;

    [Header("Building Bounds")]
    public Vector3 boundsCenter = Vector3.zero;
    public Vector3 boundsSize = new Vector3(50, 20, 50);

    [Header("Sockets and Rules")]
    public string streetSocketName = "Socket_Street";
    public LayerMask roomLayerMask;

    [Header("Prefabs")]
    public GameObject destructibleWallPrefab;
    public GameObject solidWallPrefab;

    private int generatedRoomsCount = 0;
    private Stack<SocketData> openSockets = new Stack<SocketData>();
    private List<GameObject> spawnedRooms = new List<GameObject>();
    private List<GameObject> spawnedWalls = new List<GameObject>();

    // Лимиты уникальных комнат
    private bool vaultSpawned = false;
    private bool securitySpawned = false;
    private bool vipSpawned = false;

    private Bounds buildingBounds;

    void Start()
    {
        buildingBounds = new Bounds(boundsCenter, boundsSize);
        StartCoroutine(GenerateLevelWithRetries());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }

    private void ClearLevel()
    {
        foreach (var room in spawnedRooms) 
        {
            if (room != null) 
            {
                room.SetActive(false); // Обязательно отключаем, чтобы коллайдеры исчезли СРАЗУ (до конца кадра)
                Destroy(room);
            }
        }
        foreach (var wall in spawnedWalls) 
        {
            if (wall != null) 
            {
                wall.SetActive(false);
                Destroy(wall);
            }
        }
        
        spawnedRooms.Clear();
        spawnedWalls.Clear();
        openSockets.Clear();
        
        generatedRoomsCount = 0;
        vaultSpawned = false;
        securitySpawned = false;
        vipSpawned = false;
    }

    IEnumerator GenerateLevelWithRetries()
    {
        int currentAttempt = 0;
        bool generationSuccess = false;

        Debug.Log("<color=cyan>[Gen] Запуск генератора уровня...</color>");

        while (currentAttempt < maxRetries && !generationSuccess)
        {
            if (currentAttempt > 0)
            {
                Debug.Log($"<color=orange>[Gen] Попытка генерации #{currentAttempt + 1}. Очистка старого уровня...</color>");
                ClearLevel();
                yield return new WaitForEndOfFrame();
            }

            yield return StartCoroutine(BuildSimulation());

            if (generatedRoomsCount >= minRooms)
            {
                generationSuccess = true;
                SealDeadEnds();
                PrintGenerationReport(currentAttempt + 1);
            }
            else
            {
                Debug.LogWarning($"<color=red>[Gen] Сбой попытки #{currentAttempt + 1}: сгенерировано {generatedRoomsCount} комнат, что меньше минимума ({minRooms}).</color>");
                currentAttempt++;
            }
        }

        if (!generationSuccess)
        {
            Debug.LogError($"<color=red>[Gen] ФАТАЛЬНАЯ ОШИБКА: Не удалось достичь minRooms ({minRooms}) за {maxRetries} попыток! Оставляем последний результат.</color>");
            SealDeadEnds();
        }
    }

    IEnumerator BuildSimulation()
    {
        if (activePreset == null || activePreset.startRoom == null) 
        {
            Debug.LogError("<color=red>[Gen] ОШИБКА: Active Preset не назначен в LevelGenerator!</color>");
            yield break;
        }

        // 1. Стартовая комната
        GameObject startObj = Instantiate(activePreset.startRoom.prefab, Vector3.zero, Quaternion.identity, transform);
        generatedRoomsCount++;
        spawnedRooms.Add(startObj);
        Debug.Log($"<color=cyan>[Gen] Заспавнена стартовая комната: {startObj.name} (Локация: {activePreset.levelName})</color>");

        // 2. Фургон
        RoomSocket streetSocket = FindSocketByName(startObj, streetSocketName);
        if (streetSocket != null && activePreset.vanZonePrefab != null)
        {
            SpawnVan(streetSocket);
        }

        AddSocketsToStack(startObj, 1);

        // 3. Основной цикл
        while (openSockets.Count > 0 && generatedRoomsCount < maxRooms)
        {
            SocketData currentData = openSockets.Pop();
            RoomSocket targetSocket = currentData.socket;
            int currentDepth = currentData.depth;

            if (targetSocket.isConnected) continue;
            
            List<RoomTemplate> validTemplates = GetValidTemplates(currentDepth);

            while (validTemplates.Count > 0)
            {
                RoomTemplate selectedTemplate = GetRandomWeightedTemplate(validTemplates);
                validTemplates.Remove(selectedTemplate);

                GameObject newRoom = Instantiate(selectedTemplate.prefab);
                newRoom.SetActive(false); 

                RoomSocket matchingSocket = FindFirstFreeSocket(newRoom);
                if (matchingSocket != null)
                {
                    AlignRooms(targetSocket, matchingSocket, newRoom);

                    bool inBounds = IsWithinBounds(newRoom);
                    bool noOverlap = CanPlaceRoom(newRoom);

                    if (inBounds && noOverlap)
                    {
                        newRoom.SetActive(true);
                        newRoom.transform.SetParent(transform);
                        
                        targetSocket.isConnected = true;
                        matchingSocket.isConnected = true;
                        
                        generatedRoomsCount++;
                        spawnedRooms.Add(newRoom);
                        RegisterUniqueSpawn(selectedTemplate.category);
                        Debug.Log($"[Gen] УСПЕШНО установлена комната #{generatedRoomsCount} (Тип: {selectedTemplate.category}) на глубине {currentDepth}");

                        if (destructibleWallPrefab != null)
                        {
                            GameObject wall = Instantiate(destructibleWallPrefab, targetSocket.transform.position, targetSocket.transform.rotation, transform);
                            spawnedWalls.Add(wall);
                        }

                        AddSocketsToStack(newRoom, currentDepth + 1);
                        yield return new WaitForEndOfFrame();
                        break;
                    }
                    else
                    {
                        if (!inBounds) Debug.Log($"<color=yellow>[Gen/Reject]</color> {selectedTemplate.prefab.name} выходит за Building Bounds.");
                        if (!noOverlap) Debug.Log($"<color=yellow>[Gen/Reject]</color> {selectedTemplate.prefab.name} столкнулась с другой комнатой.");
                    }
                }
                
                Destroy(newRoom);
            }
        }
    }

    private void SealDeadEnds()
    {
        int sealedCount = 0;
        foreach (GameObject room in spawnedRooms)
        {
            if (room == null) continue;
            RoomSocket[] sockets = room.GetComponentsInChildren<RoomSocket>();
            foreach (var s in sockets)
            {
                if (!s.isConnected && solidWallPrefab != null)
                {
                    GameObject wall = Instantiate(solidWallPrefab, s.transform.position, s.transform.rotation, transform);
                    spawnedWalls.Add(wall);
                    s.isConnected = true;
                    sealedCount++;
                }
            }
        }
    }

    private void PrintGenerationReport(int attempts)
    {
        string report = "<color=#00FF00><b>=== ОТЧЕТ ГЕНЕРАЦИИ УРОВНЯ ===</b></color>\n";
        report += $"▪ Успешно на попытке: <b>{attempts} из {maxRetries}</b>\n";
        report += $"▪ Всего комнат: <b>{generatedRoomsCount} / {maxRooms}</b> (Мин: {minRooms})\n";
        report += $"▪ Уникальные комнаты:\n";
        report += $"   - Хранилище (Vault): {(vaultSpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - Охранная (Security): {(securitySpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - VIP: {(vipSpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"▪ Запечатано тупиков стенками: <b>{spawnedWalls.Count}</b>\n";
        report += "<color=#00FF00><b>================================</b></color>";
        
        Debug.Log(report);
    }

    private void SpawnVan(RoomSocket streetSocket)
    {
        GameObject vanObj = Instantiate(activePreset.vanZonePrefab);
        vanObj.SetActive(true); // Форсируем включение, если префаб был случайно сохранен выключенным
        
        RoomSocket vanSocket = FindFirstFreeSocket(vanObj);
        
        if (vanSocket != null)
        {
            AlignRooms(streetSocket, vanSocket, vanObj);
            streetSocket.isConnected = true;
            vanSocket.isConnected = true;
            vanObj.transform.SetParent(transform);
            spawnedRooms.Add(vanObj);
            Debug.Log("<color=green>[Gen] Фургон успешно припаркован к Socket_Street!</color>");
        }
        else
        {
            Debug.LogError("<color=red>[Gen] В префабе Фургона не найден RoomSocket!</color>");
            Destroy(vanObj);
        }
    }

    // --- ЛОГИКА ОГРАНИЧЕНИЙ И ВЕСОВ ---

    private List<RoomTemplate> GetValidTemplates(int depth)
    {
        List<RoomTemplate> valid = new List<RoomTemplate>();
        foreach (var t in activePreset.availableRooms)
        {
            if (t.category == RoomCategory.Vault)
            {
                if (vaultSpawned || depth < 5) continue;
            }
            else if (t.category == RoomCategory.Security)
            {
                if (securitySpawned) continue;
            }
            else if (t.category == RoomCategory.VIP)
            {
                if (vipSpawned) continue;
            }
            
            valid.Add(t);
        }
        return valid;
    }

    private RoomTemplate GetRandomWeightedTemplate(List<RoomTemplate> templates)
    {
        float totalWeight = 0;
        foreach (var t in templates) totalWeight += t.weight;

        float randomVal = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var t in templates)
        {
            currentWeight += t.weight;
            if (currentWeight >= randomVal)
            {
                return t;
            }
        }
        return templates[0];
    }

    private void RegisterUniqueSpawn(RoomCategory category)
    {
        if (category == RoomCategory.Vault) vaultSpawned = true;
        if (category == RoomCategory.Security) securitySpawned = true;
        if (category == RoomCategory.VIP) vipSpawned = true;
    }

    // --- ПРОВЕРКИ КОЛЛИЗИЙ И ГРАНИЦ ---

    private bool IsWithinBounds(GameObject room)
    {
        BoxCollider box = room.GetComponent<BoxCollider>();
        if (box == null) return true;

        Vector3 center = room.transform.TransformPoint(box.center);
        Vector3 extents = Vector3.Scale(box.size, room.transform.lossyScale) / 2f;

        Bounds roomBounds = new Bounds(center, extents * 2f);
        return buildingBounds.Contains(roomBounds.min) && buildingBounds.Contains(roomBounds.max);
    }

    private bool CanPlaceRoom(GameObject room)
    {
        BoxCollider box = room.GetComponent<BoxCollider>();
        if (box == null) return true;

        Vector3 center = room.transform.TransformPoint(box.center);
        Vector3 halfExtents = Vector3.Scale(box.size, room.transform.lossyScale) * 0.48f;
        Quaternion orientation = room.transform.rotation;

        return !Physics.CheckBox(center, halfExtents, orientation, roomLayerMask);
    }

    // --- УТИЛИТЫ ---

    private void AlignRooms(RoomSocket target, RoomSocket matching, GameObject roomToMove)
    {
        Quaternion targetRotation = Quaternion.LookRotation(-target.transform.forward, Vector3.up);
        Quaternion rotationOffset = targetRotation * Quaternion.Inverse(matching.transform.rotation);
        roomToMove.transform.rotation = rotationOffset * roomToMove.transform.rotation;

        Vector3 positionOffset = target.transform.position - matching.transform.position;
        roomToMove.transform.position += positionOffset;
    }

    private RoomSocket FindSocketByName(GameObject obj, string name)
    {
        RoomSocket[] sockets = obj.GetComponentsInChildren<RoomSocket>();
        foreach (var s in sockets)
        {
            if (s.gameObject.name.Contains(name)) return s;
        }
        return null;
    }

    private RoomSocket FindFirstFreeSocket(GameObject obj)
    {
        RoomSocket[] sockets = obj.GetComponentsInChildren<RoomSocket>();
        foreach (var s in sockets)
        {
            if (!s.isConnected) return s;
        }
        return null;
    }

    private void AddSocketsToStack(GameObject room, int depth)
    {
        RoomSocket[] sockets = room.GetComponentsInChildren<RoomSocket>();
        foreach (var socket in sockets)
        {
            if (!socket.isConnected)
            {
                openSockets.Push(new SocketData(socket, depth));
            }
        }
    }
}
