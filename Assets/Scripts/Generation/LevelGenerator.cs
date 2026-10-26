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
    public LevelPreset defaultPreset; // Пресет по умолчанию для тестов
    public LevelPreset activePreset; 
    
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
    private int connectedLoopsCount = 0;

    [HideInInspector]
    public Transform vanSpawnPoint;

    // Лимиты уникальных комнат
    private bool vaultSpawned = false;
    private bool securitySpawned = false;
    private bool vipSpawned = false;
    private Dictionary<GameObject, RoomTemplate> roomTemplatesMap = new Dictionary<GameObject, RoomTemplate>();

    private Bounds buildingBounds;

    void Start()
    {
        buildingBounds = new Bounds(boundsCenter, boundsSize);
        // Generation is now triggered manually by the GameManager
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }

    public void ClearLevel()
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
        roomTemplatesMap.Clear();
        
        generatedRoomsCount = 0;
        connectedLoopsCount = 0;
        vaultSpawned = false;
        securitySpawned = false;
        vipSpawned = false;
    }

    public void GenerateAsync(LevelPreset preset, System.Action onComplete)
    {
        activePreset = preset;
        StartCoroutine(GenerateWrapper(onComplete));
    }

    private IEnumerator GenerateWrapper(System.Action onComplete)
    {
        yield return StartCoroutine(GenerateLevelWithRetries());

        // 1. Temporarily deactivate all destructible walls so the NavMesh can bake uninterrupted through doorways
        List<GameObject> breakableWalls = new List<GameObject>();
        if (spawnedWalls != null)
        {
            foreach (var wall in spawnedWalls)
            {
                if (wall != null && wall.GetComponent<DestructibleWall>() != null)
                {
                    breakableWalls.Add(wall);
                    wall.SetActive(false);
                }
            }
        }

        // 2. Bake NavMesh dynamically at runtime for modular procedural rooms
        var surface = GetComponent<Unity.AI.Navigation.NavMeshSurface>();
        if (surface != null)
        {
            Debug.Log("<color=cyan>[Gen] Перезапекание NavMesh для всего процедурного уровня с мебелью...</color>");
            
            // Настраиваем мебель: убираем синие островки навмеша со столов/стульев, сохраняя их как препятствия
            ConfigureNavMeshModifiersForRooms();

            // Force the use of Physics Colliders for modular rooms with furniture
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f; // High precision for furniture navigation
            surface.minRegionArea = 3.0f; // Discard tiny isolated islands on tables/props
            
            surface.BuildNavMesh();
        }
        else
        {
            Debug.LogWarning("<color=orange>[Gen] NavMeshSurface не найден на LevelGenerator. NavMesh не будет пересобран.</color>");
        }

        // 3. Reactivate all destructible walls
        foreach (var wall in breakableWalls)
        {
            if (wall != null)
            {
                wall.SetActive(true);
            }
        }

        // 4. Centralized loot spawn for all generated rooms (runs after NavMesh is baked)
        SpawnAllRoomsLoot();

        onComplete?.Invoke();
    }

    private void SpawnAllRoomsLoot()
    {
        if (spawnedRooms == null || spawnedRooms.Count == 0) return;

        List<RoomLootManager> managers = new List<RoomLootManager>();
        foreach (var room in spawnedRooms)
        {
            if (room != null)
            {
                RoomLootManager lootManager = room.GetComponent<RoomLootManager>();
                if (lootManager != null)
                {
                    lootManager.SpawnLoot();
                    managers.Add(lootManager);
                }
            }
        }

        // Гарантированный бюджет карты (Anti-Softlock)
        EnforceAntiSoftlockBudget(managers);
    }

    private void EnforceAntiSoftlockBudget(List<RoomLootManager> managers)
    {
        if (managers == null || managers.Count == 0) return;

        // 1. Рассчитываем необходимый порог выручки с учетом квоты дня и запаса (1.6x)
        int minBudget = 2200; // Базовый минимум для тестов

        if (GameManager.Instance != null)
        {
            int targetQuota = GameManager.Instance.operationTargetQuota;
            int alreadySaved = GameManager.Instance.accumulatedOperationMoney + GameManager.Instance.depositedMoney;
            int neededToPass = Mathf.Max(0, targetQuota - alreadySaved);
            int daysLeft = Mathf.Clamp(4 - GameManager.Instance.currentDay, 1, 3);
            int dailyNeed = Mathf.CeilToInt((float)neededToPass / daysLeft);

            // Запас 1.6x: игрок может не найти все углы или не унести из-за перегруза
            minBudget = Mathf.Max(1800, Mathf.RoundToInt(dailyNeed * 1.6f));
        }

        // 2. Считаем текущую стоимость заспавненного лута
        int currentTotal = 0;
        foreach (var m in managers)
        {
            if (m != null) currentTotal += m.GetTotalSpawnedValue();
        }

        // 3. Если лута меньше безопасного порога — добираем в свободных точках
        if (currentTotal < minBudget)
        {
            Debug.Log($"<color=orange>[AntiSoftlock] Начальный лут: ${currentTotal}. Требуется гарантированный минимум: ${minBudget}. Докидываем лут...</color>");

            int safetyIterations = 40;
            while (currentTotal < minBudget && safetyIterations > 0)
            {
                safetyIterations--;
                bool pointFound = false;

                // Сначала ищем точки в сейфе / богатых комнатах
                foreach (var m in managers)
                {
                    if (m != null && m.gameObject.name.ToLower().Contains("safe"))
                    {
                        if (m.TrySpawnInUnoccupiedPoint(forceHighValue: true))
                        {
                            pointFound = true;
                            break;
                        }
                    }
                }

                // Затем в любых других комнатах
                if (!pointFound)
                {
                    foreach (var m in managers)
                    {
                        if (m != null && m.TrySpawnInUnoccupiedPoint(forceHighValue: false))
                        {
                            pointFound = true;
                            break;
                        }
                    }
                }

                // Пересчитываем сумму
                currentTotal = 0;
                foreach (var m in managers)
                {
                    if (m != null) currentTotal += m.GetTotalSpawnedValue();
                }

                // Если все точки на всей карте уже заняты — выходим
                if (!pointFound) break;
            }
        }

        Debug.Log($"<color=yellow>[AntiSoftlock] Итоговый бюджет банка: ${currentTotal} (Порог квоты: ${minBudget}). Софтлок исключен!</color>");
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

            int minR = 10;
            if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
                minR = GameManager.Instance.selectedDossier.minRooms;
            else if (activePreset != null)
                minR = 8; // Базовый фолбек

            if (generatedRoomsCount >= minR)
            {
                generationSuccess = true;
                ConnectLoops();
                SealDeadEnds();
                PrintGenerationReport(currentAttempt + 1);
            }
            else
            {
                Debug.LogWarning($"<color=red>[Gen] Сбой попытки #{currentAttempt + 1}: сгенерировано {generatedRoomsCount} комнат, что меньше минимума ({minR}).</color>");
                currentAttempt++;
            }
        }

        if (!generationSuccess)
        {
            int minR = 10;
            if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
                minR = GameManager.Instance.selectedDossier.minRooms;
            
            Debug.LogError($"<color=red>[Gen] ФАТАЛЬНАЯ ОШИБКА: Не удалось достичь minRooms ({minR}) за {maxRetries} попыток! Оставляем последний результат.</color>");
            ConnectLoops();
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
        roomTemplatesMap[startObj] = activePreset.startRoom;
        Debug.Log($"<color=cyan>[Gen] Заспавнена стартовая комната: {startObj.name} (Локация: {activePreset.levelName})</color>");

        // 2. Фургон
        RoomSocket streetSocket = FindSocketByName(startObj, streetSocketName);
        if (streetSocket != null && activePreset.vanZonePrefab != null)
        {
            SpawnVan(streetSocket);
        }

        AddSocketsToStack(startObj, 1);

        int maxR = 20;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
            maxR = GameManager.Instance.selectedDossier.maxRooms;
        else if (activePreset != null)
            maxR = 12; // Базовый фолбек


        // 3. Основной цикл
        while (openSockets.Count > 0 && generatedRoomsCount < maxR)
        {
            SocketData currentData = openSockets.Pop();
            RoomSocket targetSocket = currentData.socket;
            int currentDepth = currentData.depth;

            if (targetSocket.isConnected) continue;
            
            // Находим комнату, которой принадлежит целевой сокет, чтобы не спавнить 2 одинаковые комнаты подряд
            GameObject parentRoom = GetRoomRoot(targetSocket.gameObject);
            RoomTemplate parentTemplate = null;
            if (parentRoom != null && roomTemplatesMap.TryGetValue(parentRoom, out var pTemp))
            {
                parentTemplate = pTemp;
            }

            List<RoomTemplate> validTemplates = GetValidTemplates(currentDepth, parentTemplate);

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
                        roomTemplatesMap[newRoom] = selectedTemplate;
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

    private void ConnectLoops()
    {
        connectedLoopsCount = 0;
        List<RoomSocket> openSocketsList = new List<RoomSocket>();

        foreach (GameObject room in spawnedRooms)
        {
            if (room == null) continue;
            RoomSocket[] sockets = room.GetComponentsInChildren<RoomSocket>();
            foreach (var s in sockets)
            {
                if (!s.isConnected)
                {
                    openSocketsList.Add(s);
                }
            }
        }

        for (int i = 0; i < openSocketsList.Count; i++)
        {
            RoomSocket socketA = openSocketsList[i];
            if (socketA == null || socketA.isConnected) continue;

            for (int j = i + 1; j < openSocketsList.Count; j++)
            {
                RoomSocket socketB = openSocketsList[j];
                if (socketB == null || socketB.isConnected) continue;

                // Не соединяем сокеты одной и той же комнаты
                if (GetRoomRoot(socketA.gameObject) == GetRoomRoot(socketB.gameObject)) continue;

                // Проверяем расстояние между сокетами (в пределах 1.8м по горизонтали, 0.6м по вертикали)
                Vector3 delta = socketA.transform.position - socketB.transform.position;
                if (Mathf.Abs(delta.y) > 0.6f) continue;
                delta.y = 0;
                if (delta.magnitude > 1.8f) continue;

                // Проверяем, что сокеты направлены навстречу друг другу
                float dot = Vector3.Dot(socketA.transform.forward, -socketB.transform.forward);
                if (dot < 0.8f) continue;

                // Соединяем сокеты
                socketA.isConnected = true;
                socketB.isConnected = true;

                // Ставим разрушаемую стену посередине
                if (destructibleWallPrefab != null)
                {
                    Vector3 midPos = (socketA.transform.position + socketB.transform.position) * 0.5f;
                    GameObject wall = Instantiate(destructibleWallPrefab, midPos, socketA.transform.rotation, transform);
                    spawnedWalls.Add(wall);
                }

                connectedLoopsCount++;
                Debug.Log($"<color=cyan>[Gen/Loop] Создан обходной путь (петля) между комнатами {GetRoomRoot(socketA.gameObject).name} и {GetRoomRoot(socketB.gameObject).name}!</color>");
                break;
            }
        }
    }

    private GameObject GetRoomRoot(GameObject obj)
    {
        Transform cur = obj.transform;
        while (cur.parent != null && cur.parent != transform)
        {
            cur = cur.parent;
        }
        return cur.gameObject;
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
        int minR = 10;
        int maxR = 20;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            minR = GameManager.Instance.selectedDossier.minRooms;
            maxR = GameManager.Instance.selectedDossier.maxRooms;
        }

        report += $"▪ Всего комнат: <b>{generatedRoomsCount} / {maxR}</b> (Мин: {minR})\n";
        report += $"▪ Уникальные комнаты:\n";
        report += $"   - Хранилище (Vault): {(vaultSpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - Охранная (Security): {(securitySpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - VIP: {(vipSpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"▪ Создано закольцованных путей (петель): <b>{connectedLoopsCount}</b>\n";
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
            vanSpawnPoint = vanObj.transform;
            Debug.Log("<color=green>[Gen] Фургон успешно припаркован к Socket_Street!</color>");
        }
        else
        {
            Debug.LogError("<color=red>[Gen] В префабе Фургона не найден RoomSocket!</color>");
            Destroy(vanObj);
        }
    }

    // --- ЛОГИКА ОГРАНИЧЕНИЙ И ВЕСОВ ---

    private List<RoomTemplate> GetValidTemplates(int depth, RoomTemplate excludedTemplate = null)
    {
        List<RoomTemplate> valid = new List<RoomTemplate>();
        
        int minR = 10;
        int maxR = 20;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            minR = GameManager.Instance.selectedDossier.minRooms;
            maxR = GameManager.Instance.selectedDossier.maxRooms;
        }

        // Vault depth requirement scales with bank size (depth >= 2 for small banks, >= 3 for normal/large)
        int minVaultDepth = Mathf.Clamp(minR / 3, 2, 4);
        bool forceVault = !vaultSpawned && (generatedRoomsCount >= maxR - 3 || depth >= minVaultDepth);

        // Если близки к лимиту комнат, а хранилище еще не заспавнилось — принудительно отдаем Vault
        if (!vaultSpawned && (generatedRoomsCount >= maxR - 2))
        {
            var vaultTemplate = activePreset.availableRooms.FirstOrDefault(t => t.category == RoomCategory.Vault);
            if (vaultTemplate != null)
            {
                valid.Clear();
                valid.Add(vaultTemplate);
                return valid;
            }
        }

        foreach (var t in activePreset.availableRooms)
        {
            if (t.category == RoomCategory.Vault)
            {
                if (vaultSpawned) continue;
                if (!forceVault && depth < minVaultDepth) continue;
            }
            else if (t.category == RoomCategory.Security)
            {
                if (securitySpawned) continue;
                if (depth < 2) continue;
            }
            else if (t.category == RoomCategory.VIP)
            {
                if (vipSpawned) continue;
                if (depth < 2) continue;
            }
            
            valid.Add(t);
        }

        // Запрещаем стыковку одинаковых комнат подряд (комната не должна спавнить саму себя на своем сокете)
        if (excludedTemplate != null && valid.Count > 1)
        {
            valid.RemoveAll(t => t == excludedTemplate || (t.prefab != null && excludedTemplate.prefab != null && t.prefab == excludedTemplate.prefab));
        }

        // Fallback to Normal rooms if list is empty
        if (valid.Count == 0)
        {
            foreach (var t in activePreset.availableRooms)
            {
                if (t.category == RoomCategory.Normal)
                {
                    valid.Add(t);
                }
            }
            if (excludedTemplate != null && valid.Count > 1)
            {
                valid.RemoveAll(t => t == excludedTemplate || (t.prefab != null && excludedTemplate.prefab != null && t.prefab == excludedTemplate.prefab));
            }
        }

        return valid;
    }

    private void ConfigureNavMeshModifiersForRooms()
    {
        if (spawnedRooms == null) return;

        foreach (var room in spawnedRooms)
        {
            if (room == null) continue;

            Collider[] colliders = room.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;

                string colName = col.gameObject.name.ToLower();

                // Пропускаем несущие элементы ходьбы: пол, лестницы, ступени, пандусы, балконы
                if (colName.Contains("floor") || colName.Contains("stair") || colName.Contains("ramp") || 
                    colName.Contains("step") || colName.Contains("balcony") || colName.Contains("ground"))
                {
                    continue;
                }

                // Столы, стулья, стойки, полки: помечаем Not Walkable, чтобы на них не рисовалась сетка NavMesh
                if (col is BoxCollider box)
                {
                    Vector3 size = box.size;
                    // Мебель обычно компактная (< 5м по сторонам и высота от 0.25м до 2.2м)
                    if (size.x < 5f && size.z < 5f && size.y >= 0.25f && size.y <= 2.2f)
                    {
                        var mod = col.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                        if (mod == null) mod = col.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                        mod.overrideArea = true;
                        mod.area = 1; // 1 = Not Walkable
                    }
                }
            }
        }
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

    private bool GetRoomFootprint(GameObject room, out Vector3 worldCenter, out Vector3 worldSize, out Quaternion worldRotation)
    {
        // 1. Ищем лучший активный BoxCollider комнаты (размером от 3м до 26м)
        BoxCollider[] boxes = room.GetComponentsInChildren<BoxCollider>(true);
        BoxCollider bestBox = null;
        float maxArea = 0f;

        foreach (var b in boxes)
        {
            if (b == null || !b.enabled) continue;
            // Пропускаем отключенные в префабе дочерние объекты (например, декоративные плейны пола/крыши)
            if (!b.gameObject.activeSelf && b.gameObject != room) continue;

            Vector3 scale = b.transform.lossyScale;
            float sx = Mathf.Abs(b.size.x * scale.x);
            float sz = Mathf.Abs(b.size.z * scale.z);

            // Реальные размеры комнат в банке от 3м до 26м. Все, что больше 26м — технические плейны
            if (sx > 26f || sz > 26f || sx < 3f || sz < 3f) continue;

            float area = sx * sz;
            if (area > maxArea)
            {
                maxArea = area;
                bestBox = b;
            }
        }

        if (bestBox != null)
        {
            worldCenter = bestBox.transform.TransformPoint(bestBox.center);
            worldSize = Vector3.Scale(bestBox.size, bestBox.transform.lossyScale);
            worldRotation = bestBox.transform.rotation;
            return true;
        }

        // 2. Фолбек: по границам активных Renderer в префабе комнаты
        Renderer[] rends = room.GetComponentsInChildren<Renderer>(false);
        if (rends == null || rends.Length == 0) rends = room.GetComponentsInChildren<Renderer>(true);

        if (rends != null && rends.Length > 0)
        {
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null && (rends[i].gameObject.activeSelf || rends[i].gameObject == room))
                {
                    b.Encapsulate(rends[i].bounds);
                }
            }
            worldCenter = b.center;
            worldSize = b.size;
            worldRotation = room.transform.rotation;
            return true;
        }

        worldCenter = room.transform.position;
        worldSize = new Vector3(12f, 3f, 12f);
        worldRotation = room.transform.rotation;
        return false;
    }

    private struct OBB2D
    {
        public Vector2[] corners;
        public Vector2[] axes;
    }

    private OBB2D CreateOBB2D(Vector3 center, Vector3 size, Quaternion rotation, float margin)
    {
        float hx = Mathf.Max(0.2f, Mathf.Abs(size.x) * 0.5f - margin);
        float hz = Mathf.Max(0.2f, Mathf.Abs(size.z) * 0.5f - margin);

        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;

        Vector2 c = new Vector2(center.x, center.z);
        Vector2 r = new Vector2(right.x, right.z).normalized * hx;
        Vector2 f = new Vector2(forward.x, forward.z).normalized * hz;

        return new OBB2D
        {
            corners = new Vector2[4]
            {
                c + r + f,
                c - r + f,
                c - r - f,
                c + r - f
            },
            axes = new Vector2[2]
            {
                new Vector2(right.x, right.z).normalized,
                new Vector2(forward.x, forward.z).normalized
            }
        };
    }

    private bool Overlaps2D(OBB2D a, OBB2D b)
    {
        Vector2[] testAxes = new Vector2[4] { a.axes[0], a.axes[1], b.axes[0], b.axes[1] };

        foreach (var axis in testAxes)
        {
            if (axis.sqrMagnitude < 0.001f) continue;

            float minA = float.MaxValue, maxA = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float proj = Vector2.Dot(a.corners[i], axis);
                if (proj < minA) minA = proj;
                if (proj > maxA) maxA = proj;
            }

            float minB = float.MaxValue, maxB = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float proj = Vector2.Dot(b.corners[i], axis);
                if (proj < minB) minB = proj;
                if (proj > maxB) maxB = proj;
            }

            if (maxA <= minB || maxB <= minA)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsWithinBounds(GameObject room)
    {
        if (buildingBounds.size == Vector3.zero)
        {
            buildingBounds = new Bounds(boundsCenter, boundsSize);
        }

        if (GetRoomFootprint(room, out Vector3 center, out Vector3 size, out Quaternion _))
        {
            float halfX = Mathf.Abs(size.x) * 0.45f;
            float halfZ = Mathf.Abs(size.z) * 0.45f;

            Vector3 min = center - new Vector3(halfX, 0f, halfZ);
            Vector3 max = center + new Vector3(halfX, 0f, halfZ);

            // Проверка по горизонтали с запасом 8м
            bool xIn = min.x >= (buildingBounds.min.x - 8f) && max.x <= (buildingBounds.max.x + 8f);
            bool zIn = min.z >= (buildingBounds.min.z - 8f) && max.z <= (buildingBounds.max.z + 8f);
            return xIn && zIn;
        }
        return true;
    }

    private bool CanPlaceRoom(GameObject room)
    {
        if (!GetRoomFootprint(room, out Vector3 center, out Vector3 size, out Quaternion rotation))
        {
            return true;
        }

        // Отступ 0.65м дает запас для соприкасающихся стен и дверных сокетов (до 1.3м на стык)
        OBB2D candidateOBB = CreateOBB2D(center, size, rotation, margin: 0.65f);

        // 1. Точная 2D OBB проверка со всеми уже размещенными комнатами
        foreach (var existingRoom in spawnedRooms)
        {
            if (existingRoom == null || existingRoom == room) continue;

            if (GetRoomFootprint(existingRoom, out Vector3 exCenter, out Vector3 exSize, out Quaternion exRot))
            {
                OBB2D existingOBB = CreateOBB2D(exCenter, exSize, exRot, margin: 0.65f);
                if (Overlaps2D(candidateOBB, existingOBB))
                {
                    return false; // Настоящее наложение комнат заблокировано
                }
            }
        }

        return true;
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
