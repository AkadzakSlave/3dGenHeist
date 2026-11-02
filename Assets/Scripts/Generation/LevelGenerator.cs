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
    public static LevelGenerator Instance { get; private set; }
    public List<GameObject> SpawnedRooms => spawnedRooms;

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

    [Header("Branching & Vaults Settings (REPO Style)")]
    [Range(0f, 1f)]
    [Tooltip("Шанс переключения на параллельную ветку при генерации (ветвление как в REPO). 0 = одна линия, 0.4 = естественные ветки/корни в разные стороны")]
    public float branchSwitchChance = 0.40f;

    [Tooltip("Целевое количество хранилищ (сейфов) на уровне. Если комнат достаточно (>= 10), спавнится до 2 сейфов в разных ветках!")]
    public int targetVaults = 2;
    public int spawnedVaultsCount = 0;

    [Header("Room Lighting & Bulbs (Method B)")]
    [Tooltip("Включить обработку освещения генератором. Для Способа Б оставляем включенным, но с override = false, чтобы префабы управляли светом сами")]
    public bool enableRoomLighting = true;

    [Tooltip("Если включено, перезаписывает лампы в префабах значениями ниже. Для Способа Б должно быть FALSE, чтобы сохранялись ваши настройки из префабов!")]
    public bool overrideExistingRoomLights = false;

    [Tooltip("Создавать потолочный светильник в комнатах, где вообще нет ламп. Если FALSE — генератор не создает ничего лишнего (чистый Способ Б).")]
    public bool spawnFallbackLampIfMissing = false;

    [Tooltip("Цвет света в комнатах (для оверрайда или автоламп)")]
    public Color roomLightColor = new Color(1.0f, 0.95f, 0.86f);

    [Tooltip("Яркость света в комнатах (по умолчанию 2.6)")]
    public float roomLightIntensity = 2.6f;

    [Tooltip("Радиус освещения ламп (в метрах)")]
    public float roomLightRange = 22f;

    [Tooltip("Высота автоматического светильника от пола комнаты (Y)")]
    public float defaultLampHeight = 5.0f;

    [Tooltip("Создавать визуальный светящийся шарик (плафон) на лампочке")]
    public bool spawnGlowingBulbMesh = false;

    [Tooltip("Размер светящегося шарика-плафона")]
    public float bulbMeshScale = 0.45f;

    [Tooltip("Материал светящегося плафона лампочки (по умолчанию M_LampBulb_Emission)")]
    public Material bulbEmissionMaterial;

    private int generatedRoomsCount = 0;
    private List<SocketData> openSockets = new List<SocketData>();
    private List<GameObject> spawnedRooms = new List<GameObject>();
    private List<GameObject> spawnedWalls = new List<GameObject>();
    private int connectedLoopsCount = 0;
    private int removedDuplicateWallsCount = 0;

    [HideInInspector]
    public Transform vanSpawnPoint;

    // Лимиты уникальных комнат
    private bool vaultSpawned = false;
    private bool securitySpawned = false;
    private bool vipSpawned = false;
    private Dictionary<GameObject, RoomTemplate> roomTemplatesMap = new Dictionary<GameObject, RoomTemplate>();

    private Bounds buildingBounds;

    private void Awake()
    {
        Instance = this;
    }

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
        prefabBoundsCache.Clear();
        
        generatedRoomsCount = 0;
        connectedLoopsCount = 0;
        removedDuplicateWallsCount = 0;
        vaultSpawned = false;
        spawnedVaultsCount = 0;
        securitySpawned = false;
        vipSpawned = false;

        if (HeistMinimap.Instance != null)
        {
            HeistMinimap.Instance.ClearMinimap();
        }
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

    public IEnumerator GenerateLevelWithRetries()
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
                PlaceVaultMaximin();
                ConnectLoops();
                SealDeadEnds();
                ResolveSmartWalls();
                SetupSpecialInteractables();
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
            ResolveSmartWalls();
            SetupSpecialInteractables();
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
        AttachRoomTracker(startObj, activePreset.startRoom.prefab.name, isStartOrVan: true);
        Debug.Log($"<color=cyan>[Gen] Заспавнена стартовая комната: {startObj.name} (Локация: {activePreset.levelName})</color>");

        // 2. Фургон
        RoomSocket streetSocket = FindSocketByName(startObj, streetSocketName);
        if (streetSocket != null && activePreset.vanZonePrefab != null)
        {
            // Защита от дублирования улицы: если в префабе Start был вложенный Van_Room, удаляем его
            for (int i = startObj.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = startObj.transform.GetChild(i);
                if (child.name.ToLower().Contains("van"))
                {
                    Destroy(child.gameObject);
                }
            }

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
            SocketData currentData = PickNextSocket();
            if (currentData == null) break;
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

                // Спавним временно под полом (y = -500), сохраняя объект активным для корректного расчета физики и матриц
                GameObject newRoom = Instantiate(selectedTemplate.prefab, Vector3.down * 500f, Quaternion.identity);

                // Проверяем все сокеты комнаты в случайном порядке (любой сокет может быть входом!)
                RoomSocket[] freeSockets = newRoom.GetComponentsInChildren<RoomSocket>();
                OrderSocketsWithPrimaryBias(freeSockets, 0.60f);
                bool placedSuccessfully = false;

                foreach (RoomSocket matchingSocket in freeSockets)
                {
                    if (matchingSocket == null || matchingSocket.isConnected) continue;

                    AlignRooms(targetSocket, matchingSocket, newRoom);

                    bool inBounds = IsWithinBounds(newRoom);
                    bool noOverlap = CanPlaceRoom(newRoom);

                    if (inBounds && noOverlap)
                    {
                        newRoom.transform.SetParent(transform);
                        
                        targetSocket.isConnected = true;
                        matchingSocket.isConnected = true;
                        
                        generatedRoomsCount++;
                        spawnedRooms.Add(newRoom);
                        roomTemplatesMap[newRoom] = selectedTemplate;
                        RegisterUniqueSpawn(selectedTemplate.category);
                        AttachRoomTracker(newRoom, selectedTemplate.prefab.name);
                        Debug.Log($"[Gen] УСПЕШНО установлена комната #{generatedRoomsCount} ({selectedTemplate.prefab.name}) на глубине {currentDepth}");

                        if (destructibleWallPrefab != null)
                        {
                            GameObject wall = Instantiate(destructibleWallPrefab, targetSocket.transform.position, targetSocket.transform.rotation, transform);
                            spawnedWalls.Add(wall);
                        }

                        // Проверяем и соединяем соседние соприкасающиеся сокеты (создание петель и сплетение веток!)
                        ConnectAdjacentSocketsForRoom(newRoom);

                        AddSocketsToStack(newRoom, currentDepth + 1);
                        placedSuccessfully = true;
                        yield return new WaitForEndOfFrame();
                        break;
                    }
                }

                if (placedSuccessfully)
                {
                    break;
                }

                Destroy(newRoom);
            }
        }
    }


    /// <summary>
    /// [Maximin Placement для Хранилища/Сейфа (Vault)]
    /// Находит среди всех открытых сокетов тот, который находится на максимальном топологическом и 
    /// физическом удалении от стартовой комнаты (входа с фургоном).
    /// Гарантирует, что Сейф всегда находится в глубине банка, а не в двух шагах от старта!
    /// </summary>
    private void PlaceVaultMaximin()
    {
        if (activePreset == null) return;

        RoomTemplate vaultTemplate = activePreset.availableRooms.FirstOrDefault(t => t.category == RoomCategory.Vault);
        if (vaultTemplate == null || vaultTemplate.prefab == null) return;

        GameObject startRoomObj = spawnedRooms.FirstOrDefault();
        Vector3 startPos = startRoomObj != null ? startRoomObj.transform.position : Vector3.zero;

        // Если комнат достаточно (>= 10) и включено ветвление, пробуем поставить до targetVaults (2) сейфов
        int vaultsToPlace = (generatedRoomsCount >= 10 && targetVaults >= 2) ? 2 : 1;
        List<Vector3> placedVaultPositions = new List<Vector3>();

        for (int vIndex = 1; vIndex <= vaultsToPlace; vIndex++)
        {
            List<RoomSocket> candidateSockets = new List<RoomSocket>();
            foreach (GameObject room in spawnedRooms)
            {
                if (room == null || room == startRoomObj) continue;
                RoomSocket[] sockets = room.GetComponentsInChildren<RoomSocket>();
                foreach (var s in sockets)
                {
                    if (s != null && !s.isConnected)
                    {
                        candidateSockets.Add(s);
                    }
                }
            }

            if (candidateSockets.Count == 0) break;

            // Сортировка:
            // Для 1-го сейфа — максимальная дистанция от входа.
            // Для 2-го сейфа — максимальная дистанция от входа и от 1-го сейфа (разные ветки!).
            candidateSockets.Sort((a, b) =>
            {
                float scoreA = Vector3.Distance(a.transform.position, startPos);
                float scoreB = Vector3.Distance(b.transform.position, startPos);

                if (placedVaultPositions.Count > 0)
                {
                    float dist1A = Vector3.Distance(a.transform.position, placedVaultPositions[0]);
                    float dist1B = Vector3.Distance(b.transform.position, placedVaultPositions[0]);
                    scoreA = (scoreA * 0.4f) + (dist1A * 0.6f);
                    scoreB = (scoreB * 0.4f) + (dist1B * 0.6f);
                }

                return scoreB.CompareTo(scoreA);
            });

            bool vaultPlaced = false;
            foreach (RoomSocket targetSocket in candidateSockets)
            {
                if (targetSocket == null || targetSocket.isConnected) continue;

                // Для 2-го сейфа проверяем минимальную дистанцию от 1-го (не ближе 16 метров)
                if (placedVaultPositions.Count > 0)
                {
                    float distToFirst = Vector3.Distance(targetSocket.transform.position, placedVaultPositions[0]);
                    if (distToFirst < 16f) continue;
                }

                GameObject newVault = Instantiate(vaultTemplate.prefab);
                newVault.SetActive(true);

                RoomSocket[] freeSockets = newVault.GetComponentsInChildren<RoomSocket>();
                OrderSocketsWithPrimaryBias(freeSockets, 0.60f);

                foreach (RoomSocket matchingSocket in freeSockets)
                {
                    if (matchingSocket == null || matchingSocket.isConnected) continue;

                    AlignRooms(targetSocket, matchingSocket, newVault);

                    bool inBounds = IsWithinBounds(newVault);
                    bool noOverlap = CanPlaceRoom(newVault);

                    if (inBounds && noOverlap)
                    {
                        newVault.transform.SetParent(transform);
                        targetSocket.isConnected = true;
                        matchingSocket.isConnected = true;

                        generatedRoomsCount++;
                        spawnedRooms.Add(newVault);
                        roomTemplatesMap[newVault] = vaultTemplate;
                        vaultSpawned = true;
                        spawnedVaultsCount++;
                        placedVaultPositions.Add(newVault.transform.position);

                        AttachRoomTracker(newVault, $"{vaultTemplate.prefab.name}_{spawnedVaultsCount}", isVault: true);

                        if (destructibleWallPrefab != null)
                        {
                            GameObject wall = Instantiate(destructibleWallPrefab, targetSocket.transform.position, targetSocket.transform.rotation, transform);
                            spawnedWalls.Add(wall);
                        }

                        // Соединяем соприкасающиеся сокеты сейфа с соседними комнатами, если есть
                        ConnectAdjacentSocketsForRoom(newVault);

                        float finalDistance = Vector3.Distance(newVault.transform.position, startPos);
                        Debug.Log($"<color=cyan>[Gen/Maximin] Хранилище #{spawnedVaultsCount} (Vault) успешно установлено в ветке ({finalDistance:F1}м от старта)!</color>");
                        vaultPlaced = true;
                        break;
                    }
                }

                if (vaultPlaced) break;
                else Destroy(newVault);
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

                // Проверяем расстояние между сокетами (до 2.4м по горизонтали, 0.6м по вертикали)
                Vector3 delta = socketA.transform.position - socketB.transform.position;
                if (Mathf.Abs(delta.y) > 0.6f) continue;
                delta.y = 0;
                if (delta.magnitude > 2.4f) continue;

                // Проверяем, что сокеты направлены навстречу друг другу
                float dot = Vector3.Dot(socketA.transform.forward, -socketB.transform.forward);
                if (dot < 0.70f) continue;

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

    private void AnalyzeLevelPacingAndLayout()
    {
        // 1. Проверка темпа: большие залы (B1, B2, B3)
        List<string> bigRoomsSequence = new List<string>();
        int bigRoomsCount = 0;
        foreach (var r in spawnedRooms)
        {
            if (r == null) continue;
            string n = r.name.ToUpper();
            if (n.StartsWith("B1") || n.StartsWith("B2") || n.StartsWith("B3"))
            {
                bigRoomsCount++;
                bigRoomsSequence.Add(r.name.Replace("(Clone)", "").Trim());
            }
        }

        // 2. Расстояние до Хранилища
        GameObject vaultObj = spawnedRooms.FirstOrDefault(r => r != null && (r.name.ToLower().Contains("safe") || r.name.ToLower().Contains("s2")));
        GameObject startObj = spawnedRooms.FirstOrDefault();
        float vaultDist = (vaultObj != null && startObj != null) ? Vector3.Distance(vaultObj.transform.position, startObj.transform.position) : 0f;

        // 3. Вывод анализа вердикта
        Debug.Log($"<color=#38bdf8>[Layout Analytics] Больших залов: {bigRoomsCount} шт. | Дистанция до Сейфа: {vaultDist:F1}м | Темп: {(bigRoomsCount >= 2 ? "Сбалансированный банк" : "Компактная планировка")}</color>");
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
        report += $"   - Хранилища (Vault): {(vaultSpawned ? $"<color=green>{spawnedVaultsCount} шт.</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - Охранная (Security): {(securitySpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"   - VIP: {(vipSpawned ? "<color=green>Да</color>" : "<color=red>Нет</color>")}\n";
        report += $"▪ Создано закольцованных путей (петель): <b>{connectedLoopsCount}</b>\n";
        report += $"▪ Запечатано тупиков стенками: <b>{spawnedWalls.Count}</b>\n";
        report += $"▪ Умных стен оптимизировано (удален Z-fighting): <b>{removedDuplicateWallsCount}</b>\n";
        report += "<color=#00FF00><b>================================</b></color>";
        
        Debug.Log(report);
        AnalyzeLevelPacingAndLayout();
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
            AttachRoomTracker(vanObj, "Van_Room", isStartOrVan: true);
            Debug.Log("<color=green>[Gen] Фургон успешно припаркован к Socket_Street!</color>");
        }
        else
        {
            Debug.LogError("<color=red>[Gen] В префабе Фургона не найден RoomSocket!</color>");
            Destroy(vanObj);
        }
    }

    private void AttachRoomTracker(GameObject roomObj, string roomName, bool isVault = false, bool isStartOrVan = false)
    {
        if (roomObj == null) return;
        RoomTracker tracker = roomObj.GetComponent<RoomTracker>();
        if (tracker == null) tracker = roomObj.AddComponent<RoomTracker>();

        Bounds localBounds;
        string key = roomName.Replace("(Clone)", "").Trim();
        if (!prefabBoundsCache.TryGetValue(key, out localBounds))
        {
            if (!CalculateRoomLocalBounds(roomObj, out localBounds))
            {
                localBounds = new Bounds(Vector3.zero, new Vector3(16f, 4f, 16f));
            }
        }

        tracker.Initialize(roomName, localBounds, isVault, isStartOrVan);

        // Гарантируем яркое основное освещение комнат с горящими плафонами
        EnsureRoomPrimaryLighting(roomObj, isStartOrVan);
    }

    private void EnsureRoomPrimaryLighting(GameObject roomObj, bool isStartOrVan)
    {
        if (!enableRoomLighting || roomObj == null || isStartOrVan) return;

        // 1. Ищем существующие лампы в комнате
        Light[] existingLights = roomObj.GetComponentsInChildren<Light>();
        if (existingLights != null && existingLights.Length > 0)
        {
            foreach (var l in existingLights)
            {
                if (overrideExistingRoomLights)
                {
                    l.type = LightType.Point;
                    l.intensity = roomLightIntensity;
                    l.range = roomLightRange;
                    l.color = roomLightColor;
                    l.shadows = LightShadows.None;

                    if (l.transform.localPosition.y > 7f)
                    {
                        Vector3 lp = l.transform.localPosition;
                        lp.y = defaultLampHeight;
                        l.transform.localPosition = lp;
                    }
                }
                else
                {
                    // Сохраняем индивидуальные настройки ламп из префаба комнаты!
                    // Отключаем динамические тени для гладкой производительности
                    l.shadows = LightShadows.None;
                }

                if (spawnGlowingBulbMesh)
                {
                    EnsureBulbMesh(l.gameObject);
                }
            }
            return;
        }

        // 2. Если в комнате нет ламп и выключен спавн фоллбэк-ламп — ничего не делаем (чистый Способ Б)
        if (!spawnFallbackLampIfMissing) return;

        // Создаем потолочный светильник с настройками из Инспектора только если включен спавн запасных ламп
        GameObject lampObj = new GameObject("CeilingLamp");
        lampObj.transform.SetParent(roomObj.transform, false);
        lampObj.transform.localPosition = new Vector3(0f, defaultLampHeight, 0f);

        Light roomLight = lampObj.AddComponent<Light>();
        roomLight.type = LightType.Point;
        roomLight.range = roomLightRange;
        roomLight.intensity = roomLightIntensity;
        roomLight.color = roomLightColor;
        roomLight.shadows = LightShadows.None;

        if (spawnGlowingBulbMesh)
        {
            EnsureBulbMesh(lampObj);
        }
    }

    private void EnsureBulbMesh(GameObject lampObj)
    {
        if (lampObj == null || lampObj.transform.Find("GlowingBulb") != null) return;

        // Создаем аккуратный светящийся шарик-плафон
        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "GlowingBulb";
        bulb.transform.SetParent(lampObj.transform, false);
        bulb.transform.localPosition = Vector3.zero;
        bulb.transform.localScale = Vector3.one * bulbMeshScale;

        Collider c = bulb.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Material emissiveMat = bulbEmissionMaterial;
        if (emissiveMat == null)
        {
            emissiveMat = Resources.Load<Material>("Materials/M_LampBulb_Emission");
        }
        if (emissiveMat == null)
        {
            Shader std = Shader.Find("Standard");
            if (std != null)
            {
                emissiveMat = new Material(std);
                emissiveMat.color = new Color(1f, 0.98f, 0.92f);
                emissiveMat.EnableKeyword("_EMISSION");
                emissiveMat.SetColor("_EmissionColor", new Color(2.5f, 2.3f, 1.8f));
            }
        }

        if (emissiveMat != null)
        {
            MeshRenderer mr = bulb.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = emissiveMat;
        }
    }


    /// <summary>
    /// [Smart Walls Resolver 3.0]
    /// Точное разрешение Z-fighting:
    /// Отключает стену ТОЛЬКО когда две параллельные стены комнат действительно соприкасаются
    /// и перекрывают друг друга в пространстве. Внешние глухие стены коридоров никогда не трогаются!
    /// </summary>
    private void ResolveSmartWalls()
    {
        removedDuplicateWallsCount = 0;

        // 1. Собираем все стены префабов комнат и процедурно установленные стены (spawnedWalls)
        List<GameObject> roomWalls = new List<GameObject>();

        foreach (GameObject room in spawnedRooms)
        {
            if (room == null) continue;
            Transform[] children = room.GetComponentsInChildren<Transform>(true);
            foreach (var t in children)
            {
                if (t == null || t.gameObject == room) continue;

                string lowerName = t.name.ToLower();
                // Игнорируем родительский контейнер 'Walls' и перемычки над дверями 'WallDoor'
                if (lowerName == "walls" || lowerName.Contains("walldoor")) continue;

                if (lowerName.StartsWith("wall"))
                {
                    Renderer rend = t.GetComponent<Renderer>();
                    Collider col = t.GetComponent<Collider>();
                    if (rend != null || col != null)
                    {
                        roomWalls.Add(t.gameObject);
                    }
                }
            }
        }

        // Добавляем процедурные стены (DestructibleWall и SolidWall из spawnedWalls)
        if (spawnedWalls != null)
        {
            foreach (var sw in spawnedWalls)
            {
                if (sw != null && sw.activeSelf && !roomWalls.Contains(sw))
                {
                    roomWalls.Add(sw);
                }
            }
        }

        // 2. Ликвидация Z-fighting и дублирующих друг друга стен
        HashSet<GameObject> wallsToDisable = new HashSet<GameObject>();

        for (int i = 0; i < roomWalls.Count; i++)
        {
            GameObject wallA = roomWalls[i];
            if (wallA == null || !wallA.activeSelf || wallsToDisable.Contains(wallA)) continue;

            Renderer rendA = wallA.GetComponent<Renderer>();
            Collider colA = wallA.GetComponent<Collider>();
            Bounds bA = rendA != null ? rendA.bounds : (colA != null ? colA.bounds : new Bounds(wallA.transform.position, Vector3.one));

            for (int j = i + 1; j < roomWalls.Count; j++)
            {
                GameObject wallB = roomWalls[j];
                if (wallB == null || !wallB.activeSelf || wallsToDisable.Contains(wallB)) continue;

                Renderer rendB = wallB.GetComponent<Renderer>();
                Collider colB = wallB.GetComponent<Collider>();
                Bounds bB = rendB != null ? rendB.bounds : (colB != null ? colB.bounds : new Bounds(wallB.transform.position, Vector3.one));

                // 1. Проверка по вертикали (стены на разных этажах никогда не конфликтуют)
                if (Mathf.Abs(bA.center.y - bB.center.y) > 0.85f) continue;

                // 2. Стены одной и той же комнаты пропускаем (за исключением абсолютных дубликатов в одной точке < 0.25м)
                bool sameRoom = GetRoomRoot(wallA) == GetRoomRoot(wallB);
                float dist3D = Vector3.Distance(bA.center, bB.center);
                if (sameRoom && dist3D > 0.25f) continue;

                // 3. Определение ориентации стен (вдоль оси X или вдоль оси Z)
                bool isA_alongX = (bA.size.x > bA.size.z);
                bool isB_alongX = (bB.size.x > bB.size.z);

                // ПЕРПЕНДИКУЛЯРНЫЕ СТЕНЫ (одна вдоль X, другая вдоль Z) образуют угол комнат и НИКОГДА не удаляются!
                if (isA_alongX != isB_alongX) continue;

                // 4. Проверка копланарности (расстояние между плоскостями параллельных стен по нормали)
                float normalDist = isA_alongX ? Mathf.Abs(bA.center.z - bB.center.z) : Mathf.Abs(bA.center.x - bB.center.x);
                if (normalDist > 0.35f) continue;

                // 5. Проверка длины и взаимного перекрытия
                float lenA = isA_alongX ? bA.size.x : bA.size.z;
                float lenB = isB_alongX ? bB.size.x : bB.size.z;
                float minLen = Mathf.Min(lenA, lenB);

                float overlap = isA_alongX
                    ? (Mathf.Min(bA.max.x, bB.max.x) - Mathf.Max(bA.min.x, bB.min.x))
                    : (Mathf.Min(bA.max.z, bB.max.z) - Mathf.Max(bA.min.z, bB.min.z));

                // Стены, стоящие в стык друг за другом вдоль коридора (overlap ~ 0), НЕ являются дубликатами!
                // Для признания дубликатом они должны перекрываться минимум на 70% длины меньшей стены:
                if (overlap < minLen * 0.70f) continue;

                // 6. ЗАЩИТА БОЛЬШИХ ВНЕШНИХ СТЕН:
                // Если одна стена — длинная стена комнаты (например, 16м в S3), а вторая — короткая перегородка (например, 4м),
                // длинная стена НИ ПРИ КАКИХ ОБСТОЯТЕЛЬСТВАХ не удаляется!
                if (lenA >= lenB * 1.5f && lenA > 6f)
                {
                    // Сохраняем стену A, отключаем дублирующую короткую стену B
                    wallsToDisable.Add(wallB);
                    continue;
                }
                if (lenB >= lenA * 1.5f && lenB > 6f)
                {
                    // Сохраняем стену B, отключаем дублирующую короткую стену A
                    wallsToDisable.Add(wallA);
                    break;
                }

                // 7. Разрешение между сравнимыми по размеру параллельными стенками:
                // Приоритет: разрушаемая стена (DestructibleWall) ВСЕГДА сохраняется перед глухой
                DestructibleWall destA = wallA.GetComponentInParent<DestructibleWall>();
                DestructibleWall destB = wallB.GetComponentInParent<DestructibleWall>();

                if (destA != null && destB == null)
                {
                    wallsToDisable.Add(wallB);
                }
                else if (destB != null && destA == null)
                {
                    wallsToDisable.Add(wallA);
                    break;
                }
                else
                {
                    // Одинаковые стены — отключаем wallB, wallA остаётся
                    wallsToDisable.Add(wallB);
                }
            }
        }

        foreach (var w in wallsToDisable)
        {
            if (w != null)
            {
                w.SetActive(false);
                removedDuplicateWallsCount++;
            }
        }

        Debug.Log($"<color=green>[SmartWalls 4.0] Устранено {removedDuplicateWallsCount} конфликтов/наложений стен! DestructibleWall сохранены.</color>");
    }

    private void SetupSpecialInteractables()
    {
        // 1. Автоматическая привязка VaultDoor ко всем дверям сейфов на карте
        int safeDoorsCount = 0;
        foreach (GameObject room in spawnedRooms)
        {
            if (room == null) continue;
            foreach (Transform t in room.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("safedoor"))
                {
                    VaultDoor vd = t.GetComponent<VaultDoor>();
                    if (vd == null) vd = t.GetComponentInParent<VaultDoor>();
                    if (vd == null) vd = t.GetComponentInChildren<VaultDoor>();
                    if (vd == null)
                    {
                        vd = t.gameObject.AddComponent<VaultDoor>();
                    }
                    vd.AutoConfigureReferences();

                    // Удаляем узкий невыпуклый MeshCollider, который мешает рейкастам
                    MeshCollider mc = t.GetComponent<MeshCollider>();
                    if (mc != null && !mc.convex) DestroyImmediate(mc);

                    BoxCollider box = t.GetComponent<BoxCollider>();
                    if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        box.center = mf.sharedMesh.bounds.center;
                        box.size = Vector3.Max(mf.sharedMesh.bounds.size, new Vector3(1.4f, 2.2f, 0.6f));
                    }
                    else
                    {
                        box.center = Vector3.zero;
                        box.size = new Vector3(1.4f, 2.2f, 0.6f);
                    }
                    safeDoorsCount++;
                }
            }
        }
        if (safeDoorsCount > 0)
        {
            Debug.Log($"<color=yellow>[Gen] Настроено дверей сейфов с мини-игрой взлома: {safeDoorsCount}</color>");
        }

        // 2. Настройка багажника фургона для выгрузки лута
        if (vanSpawnPoint != null)
        {
            EnsureTrunkExtractionZone(vanSpawnPoint.gameObject);
            EnsureVanLighting(vanSpawnPoint.gameObject);
        }

        // 3. Автоматическая настройка разрушаемых лампочек и атмосферного мерцания
        int breakableLightsCount = 0;
        foreach (GameObject room in spawnedRooms)
        {
            if (room == null) continue;
            foreach (Light l in room.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional) continue;

                // Назначаем BreakableLight напрямую на сам объект источника света (а не на стену-родитель!)
                BreakableLight bl = l.GetComponent<BreakableLight>();
                if (bl == null)
                {
                    bl = l.gameObject.AddComponent<BreakableLight>();
                }
                bl.targetLight = l;
                bl.FindBulbRenderer();
                bl.InitializeComponents();
                breakableLightsCount++;
            }
        }
        if (breakableLightsCount > 0)
        {
            Debug.Log($"<color=yellow>[Gen] Настроено разрушаемых лампочек с мерцанием: {breakableLightsCount}</color>");
        }
    }

    private void EnsureVanLighting(GameObject vanObj)
    {
        if (vanObj == null) return;
        Transform existingLight = vanObj.transform.Find("VanStreetLamp");
        if (existingLight != null) return;

        GameObject lampObj = new GameObject("VanStreetLamp");
        lampObj.transform.SetParent(vanObj.transform, false);
        lampObj.transform.localPosition = new Vector3(0f, 4.5f, 2.5f);

        Light lamp = lampObj.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 16f;
        lamp.color = new Color(1.0f, 0.72f, 0.38f); // Теплый янтарный свет уличного фонаря базы
        lamp.intensity = 2.0f;
        lamp.shadows = LightShadows.None; // 0 нагрузки на видеокарту
    }

    private void EnsureTrunkExtractionZone(GameObject vanObj)
    {
        // Настройка зоны выгрузки сумок (все размещенные в Van_Room предметы остаются активными)

        // 2. Настройка зоны выгрузки сумок
        HeistExtractionZone zone = vanObj.GetComponentInChildren<HeistExtractionZone>();
        if (zone == null)
        {
            Transform term = vanObj.transform.Find("VanTerminal");
            Vector3 pos = term != null ? term.position + new Vector3(2.5f, 0f, 0f) : vanObj.transform.position + new Vector3(0f, 0f, 4f);

            GameObject trunkObj = new GameObject("VanCargoTrunk");
            trunkObj.transform.SetParent(vanObj.transform, true);
            trunkObj.transform.position = pos;

            BoxCollider col = trunkObj.AddComponent<BoxCollider>();
            col.size = new Vector3(3.5f, 2.5f, 3.5f);
            col.isTrigger = true;

            zone = trunkObj.AddComponent<HeistExtractionZone>();
            zone.zoneName = "Багажник фургона";
            zone.textOffset = new Vector3(0f, 1.3f, 0f);
        }
        else
        {
            zone.zoneName = "Багажник фургона";
            Collider col = zone.GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                box.size = Vector3.Max(box.size, new Vector3(3.0f, 2.2f, 3.0f));
                box.isTrigger = true;
            }
        }
        zone.UpdateCargoDisplay();
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

        // Примечание: Хранилище (Vault) больше не спавнится случайно вблизи входа.
        // Оно размещается на этапе PlaceVaultMaximin() в самый глубокий и дальний конец банка!

        foreach (var t in activePreset.availableRooms)
        {
            if (t.category == RoomCategory.Vault)
            {
                // Vault размещается выделенно через Maximin алгоритм
                continue;
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

        // 1. Запрещаем стыковку одинаковых комнат подряд
        if (excludedTemplate != null && valid.Count > 1)
        {
            valid.RemoveAll(t => t == excludedTemplate || (t.prefab != null && excludedTemplate.prefab != null && t.prefab == excludedTemplate.prefab));
        }

        // 2. [Темповый баланс архитектуры]: Запрещаем спавн двух огромных залов (32x32) подряд!
        // Если родительская комната - большой зал (B1, B2, B3), следующая комната должна быть коридором или офисом.
        if (excludedTemplate != null && excludedTemplate.prefab != null)
        {
            string pName = excludedTemplate.prefab.name.ToUpper();
            bool isParentBigHall = pName.StartsWith("B1") || pName.StartsWith("B2") || pName.StartsWith("B3");

            if (isParentBigHall && valid.Count > 1)
            {
                var nonBigRooms = valid.Where(t => t.prefab != null && 
                    !t.prefab.name.ToUpper().StartsWith("B1") && 
                    !t.prefab.name.ToUpper().StartsWith("B2") && 
                    !t.prefab.name.ToUpper().StartsWith("B3")).ToList();

                if (nonBigRooms.Count > 0)
                {
                    valid = nonBigRooms;
                }
            }
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

    private float GetDynamicTemplateWeight(RoomTemplate t, int difficulty)
    {
        if (t == null) return 1f;
        float baseWeight = Mathf.Max(1f, t.weight);

        if (t.prefab == null) return baseWeight;

        string pName = t.prefab.name.ToUpper();
        bool isBigHall = pName.StartsWith("B1") || pName.StartsWith("B2") || pName.StartsWith("B3");

        if (isBigHall)
        {
            if (difficulty <= 4)
            {
                // Сложность 1–4 (8–12 комнат): стандартная базовая система
                return baseWeight;
            }
            else if (difficulty <= 8)
            {
                // Сложность 5–8 (13–16 комнат): повышаем шанс больших залов для разнообразия
                return baseWeight * 2.2f;
            }
            else
            {
                // Сложность 9–10 (17–25 комнат): банк огромный, много центральных залов и атриумов
                return baseWeight * 3.5f;
            }
        }

        return baseWeight;
    }

    private RoomTemplate GetRandomWeightedTemplate(List<RoomTemplate> templates)
    {
        int difficulty = 1;
        if (GameManager.Instance != null && GameManager.Instance.selectedDossier != null)
        {
            difficulty = Mathf.Clamp(GameManager.Instance.selectedDossier.difficultyLevel, 1, 10);
        }

        float totalWeight = 0;
        foreach (var t in templates) totalWeight += GetDynamicTemplateWeight(t, difficulty);

        float randomVal = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var t in templates)
        {
            currentWeight += GetDynamicTemplateWeight(t, difficulty);
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

    private readonly Dictionary<string, Bounds> prefabBoundsCache = new Dictionary<string, Bounds>();

        private bool CalculateRoomLocalBounds(GameObject room, out Bounds localBounds)
    {
        localBounds = new Bounds(Vector3.zero, Vector3.zero);

        // 1. Сначала ищем пол ('floor') или монолитный BoxCollider комнаты
        BoxCollider[] boxes = room.GetComponentsInChildren<BoxCollider>(true);
        BoxCollider bestBox = null;
        float maxArea = 0f;

        foreach (var b in boxes)
        {
            if (b == null) continue;
            // Учитываем реальный масштаб объекта в мировом/локальном пространстве!
            Vector3 realSize = Vector3.Scale(b.size, b.transform.lossyScale);
            float area = Mathf.Abs(realSize.x * realSize.z);

            // Если это явно пол или габаритный коллайдер комнаты
            string lowerName = b.name.ToLower();
            bool isFloorOrRoot = lowerName.Contains("floor") || lowerName.Contains("b1") || lowerName.Contains("b2") || lowerName.Contains("b3") || lowerName.Contains("start");

            if (isFloorOrRoot && area > maxArea)
            {
                maxArea = area;
                bestBox = b;
            }
            else if (bestBox == null && area >= 30f && area > maxArea)
            {
                maxArea = area;
                bestBox = b;
            }
        }

        if (bestBox != null)
        {
            Vector3 worldCenter = bestBox.transform.TransformPoint(bestBox.center);
            Vector3 localCenter = room.transform.InverseTransformPoint(worldCenter);
            Vector3 localSize = Vector3.Scale(bestBox.size, bestBox.transform.lossyScale);
            localBounds = new Bounds(localCenter, localSize);
            return true;
        }

        // 2. Если отдельного пола нет, вычисляем границы объединением всех коллайдеров
        bool hasBounds = false;
        Collider[] allCols = room.GetComponentsInChildren<Collider>(true);
        foreach (var col in allCols)
        {
            if (col == null || col.isTrigger) continue;

            Bounds b;
            if (col is BoxCollider bc)
            {
                Vector3 wc = bc.transform.TransformPoint(bc.center);
                Vector3 lc = room.transform.InverseTransformPoint(wc);
                Vector3 ls = Vector3.Scale(bc.size, bc.transform.lossyScale);
                b = new Bounds(lc, ls);
            }
            else
            {
                Vector3 wc = col.bounds.center;
                Vector3 lc = room.transform.InverseTransformPoint(wc);
                b = new Bounds(lc, col.bounds.size);
            }

            if (!hasBounds)
            {
                localBounds = b;
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(b);
            }
        }

        if (hasBounds) return true;

        // Фолбек
        localBounds = new Bounds(Vector3.zero, new Vector3(32f, 4f, 32f));
        return true;
    }

    private bool GetRoomFootprint(GameObject room, out Vector3 worldCenter, out Vector3 worldSize, out Quaternion worldRotation)
    {
        worldRotation = room.transform.rotation;

        string key = room.name.Replace("(Clone)", "").Trim();
        if (!prefabBoundsCache.TryGetValue(key, out Bounds localBounds))
        {
            if (CalculateRoomLocalBounds(room, out localBounds))
            {
                prefabBoundsCache[key] = localBounds;
            }
            else
            {
                localBounds = new Bounds(Vector3.zero, new Vector3(8f, 3.5f, 8f));
            }
        }

        worldCenter = room.transform.TransformPoint(localBounds.center);
        worldSize = Vector3.Scale(localBounds.size, room.transform.lossyScale);
        return true;
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
            return false;
        }

        // Отступ 0.20м: соприкасающиеся внешние стены корректно стыкуются, но любое проникновение комнат друг в друга блокируется
        OBB2D candidateOBB = CreateOBB2D(center, size, rotation, margin: 0.20f);

        // 1. Точная 2D OBB проверка со всеми уже размещенными комнатами
        foreach (var existingRoom in spawnedRooms)
        {
            if (existingRoom == null || existingRoom == room) continue;

            if (GetRoomFootprint(existingRoom, out Vector3 exCenter, out Vector3 exSize, out Quaternion exRot))
            {
                OBB2D existingOBB = CreateOBB2D(exCenter, exSize, exRot, margin: 0.20f);
                if (Overlaps2D(candidateOBB, existingOBB))
                {
                    return false; // Настоящее наложение комнат заблокировано
                }
            }
        }

        return true;
    }

    // --- УТИЛИТЫ ---


    /// <summary>
    /// Упорядочивает сокеты кандидата: 
    /// С шансом 60% первым проверяется основной дефолтный вход (сокет 0 в иерархии префаба),
    /// а с шансом 40% выбирается случайный альтернативный сокет для неожиданных разворотов комнаты.
    /// </summary>
    private static void OrderSocketsWithPrimaryBias(RoomSocket[] sockets, float primarySocketBias = 0.60f)
    {
        if (sockets == null || sockets.Length <= 1) return;

        // Если выпадает 40% на альтернативный вход — меняем нулевой сокет со случайным другим
        if (Random.value > primarySocketBias)
        {
            int altIndex = Random.Range(1, sockets.Length);
            RoomSocket temp = sockets[0];
            sockets[0] = sockets[altIndex];
            sockets[altIndex] = temp;
        }

        // Остальные сокеты после первого слегка перемешиваем
        for (int i = 1; i < sockets.Length; i++)
        {
            int rnd = Random.Range(i, sockets.Length);
            RoomSocket temp = sockets[i];
            sockets[i] = sockets[rnd];
            sockets[rnd] = temp;
        }
    }

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

    private SocketData PickNextSocket()
    {
        if (openSockets == null || openSockets.Count == 0) return null;

        // Ветвление в разные стороны (как в REPO):
        // С шансом branchSwitchChance переключаемся на параллельную ветку или более раннюю развилку
        if (openSockets.Count > 1 && Random.value < branchSwitchChance)
        {
            int randIdx = Random.Range(0, openSockets.Count);
            SocketData data = openSockets[randIdx];
            openSockets.RemoveAt(randIdx);
            return data;
        }
        else
        {
            int lastIdx = openSockets.Count - 1;
            SocketData data = openSockets[lastIdx];
            openSockets.RemoveAt(lastIdx);
            return data;
        }
    }

    private void AddSocketsToStack(GameObject room, int depth)
    {
        RoomSocket[] sockets = room.GetComponentsInChildren<RoomSocket>();
        foreach (var socket in sockets)
        {
            if (!socket.isConnected)
            {
                openSockets.Add(new SocketData(socket, depth));
            }
        }
    }

    private void ConnectAdjacentSocketsForRoom(GameObject room)
    {
        if (room == null) return;
        RoomSocket[] mySockets = room.GetComponentsInChildren<RoomSocket>();
        foreach (var mySock in mySockets)
        {
            if (mySock == null || mySock.isConnected) continue;

            foreach (var otherRoom in spawnedRooms)
            {
                if (otherRoom == null || otherRoom == room) continue;

                RoomSocket[] otherSockets = otherRoom.GetComponentsInChildren<RoomSocket>();
                foreach (var otherSock in otherSockets)
                {
                    if (otherSock == null || otherSock.isConnected) continue;

                    Vector3 delta = mySock.transform.position - otherSock.transform.position;
                    if (Mathf.Abs(delta.y) > 0.6f) continue;
                    delta.y = 0f;
                    if (delta.magnitude > 2.4f) continue;

                    float dot = Vector3.Dot(mySock.transform.forward, -otherSock.transform.forward);
                    if (dot < 0.70f) continue;

                    // Соединяем сокеты между комнатами/ветками!
                    mySock.isConnected = true;
                    otherSock.isConnected = true;

                    if (destructibleWallPrefab != null)
                    {
                        Vector3 midPos = (mySock.transform.position + otherSock.transform.position) * 0.5f;
                        GameObject wall = Instantiate(destructibleWallPrefab, midPos, mySock.transform.rotation, transform);
                        spawnedWalls.Add(wall);
                    }

                    connectedLoopsCount++;
                    Debug.Log($"<color=cyan>[Gen/Loop] Стыковка веток на лету: петля создана между {room.name} и {otherRoom.name}!</color>");
                    break;
                }

                if (mySock.isConnected) break;
            }
        }
    }
}
