using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode.Components;

public class NetworkBootstrap : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Поле ввода IP-адреса для подключения")]
    public TMP_InputField ipInputField;
    
    [Tooltip("Кнопка запуска в режиме Хоста (Server + Client)")]
    public Button hostButton;
    
    [Tooltip("Кнопка запуска в режиме Клиента")]
    public Button clientButton;

    [Tooltip("Кнопка отключения от сессии")]
    public Button disconnectButton;

    [Header("Network Configuration")]
    [Tooltip("Порт по умолчанию для передачи данных")]
    public ushort defaultPort = 7777;

    [Tooltip("Должен ли данный скрипт сохраняться при переходе между сценами")]
    public bool surviveSceneChange = true;

    [Header("Debug Settings")]
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Клавиша для открытия/закрытия отладочного меню в новой системе ввода")]
    public UnityEngine.InputSystem.Key toggleKeyNew = UnityEngine.InputSystem.Key.F1;
#else
    [Tooltip("Клавиша для открытия/закрытия отладочного меню")]
    public KeyCode toggleKey = KeyCode.F1;
#endif

    [Tooltip("Объект визуального контейнера панели UI")]
    public GameObject uiContainer;

    [Tooltip("Компонент для отображения отладочной текстовой информации")]
    public TMP_Text debugTextInfo;

    private bool isUiActive = false;
    private bool isValidated = false;

    private void Awake()
    {
        if (surviveSceneChange)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        // 1. Проверяем наличие NetworkManager и UnityTransport
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NET ERROR] NetworkManager not found.");
            return;
        }

        Debug.Log("[NET] NetworkManager Ready");

        // Убеждаемся, что NetworkManager сохранится при загрузке новых сцен
        if (NetworkManager.Singleton.gameObject != null)
        {
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[NET ERROR] UnityTransport not found.");
            return;
        }

        Debug.Log("[NET] Transport Ready");
        Debug.Log("[NET] Bootstrap Initialized");
        isValidated = true;

        // 2. Настраиваем слушатели кнопок
        if (hostButton != null)
            hostButton.onClick.AddListener(StartHost);

        if (clientButton != null)
            clientButton.onClick.AddListener(StartClient);

        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(ShutdownNetwork);

        // Инициализируем состояние отладочной панели (скрыта на старте)
        if (uiContainer != null)
        {
            uiContainer.SetActive(false);
        }
        isUiActive = false;

        // 3. Подписываемся на события сетевого менеджера
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;

        UpdateUI();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current[toggleKeyNew].wasPressedThisFrame)
        {
            ToggleDebugMenu();
        }
#else
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleDebugMenu();
        }
#endif
    }

    private void ToggleDebugMenu()
    {
        isUiActive = !isUiActive;
        
        if (uiContainer != null)
        {
            uiContainer.SetActive(isUiActive);
        }

        if (isUiActive)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }
    }

    public void StartHost()
    {
        if (!isValidated)
        {
            Debug.LogError("[NET ERROR] Cannot start host: Validation failed on initialization.");
            return;
        }

        // Запуск валидации настроек перед стартом хоста
        if (!ValidateHostSettings())
        {
            return;
        }

        // Защита от двойного запуска или повторного нажатия
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            Debug.LogWarning("[NET] Network session already running.");
            return;
        }

        Debug.Log("[NET] Starting Host");
        ConfigureTransport(true);

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("[NET] Host Started");
            UpdateUI();
        }
        else
        {
            Debug.LogError("[NET ERROR] Failed to start Host!");
        }
    }

    public void StartClient()
    {
        if (!isValidated)
        {
            Debug.LogError("[NET ERROR] Cannot start client: Validation failed on initialization.");
            return;
        }

        // Защита от двойного подключения
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[NET] Already connected.");
            return;
        }

        Debug.Log("[NET] Starting Client");
        ConfigureTransport(false);

        if (NetworkManager.Singleton.StartClient())
        {
            UpdateUI();
        }
        else
        {
            Debug.LogError("[NET ERROR] Failed to start Client!");
        }
    }

    public void StopHost()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            ShutdownNetwork();
        }
    }

    public void StopClient()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            ShutdownNetwork();
        }
    }

    public void ShutdownNetwork()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[NET] Shutdown complete.");
            UpdateUI();
        }
    }

    private bool ValidateHostSettings()
    {
        bool success = true;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NET ERROR] Validation Failed: NetworkManager does not exist in the scene.");
            return false;
        }

        Debug.Log("[NET] Validation Check: NetworkManager exists. [OK]");

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[NET ERROR] Validation Failed: UnityTransport component not found on NetworkManager.");
            success = false;
        }
        else
        {
            Debug.Log("[NET] Validation Check: UnityTransport exists. [OK]");
        }

        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab == null)
        {
            Debug.LogError("[NET ERROR] Validation Failed: Player Prefab is not assigned in NetworkManager NetworkConfig.");
            success = false;
        }
        else
        {
            Debug.Log("[NET] Validation Check: Player Prefab assigned in NetworkManager. [OK]");
            GameObject playerPrefab = NetworkManager.Singleton.NetworkConfig.PlayerPrefab;

            if (playerPrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[NET ERROR] Validation Failed: NetworkObject component is missing on the Player Prefab.");
                success = false;
            }
            else
            {
                Debug.Log("[NET] Validation Check: NetworkObject present on Player Prefab. [OK]");
            }

            if (playerPrefab.GetComponent<NetworkTransform>() == null)
            {
                Debug.LogError("[NET ERROR] Validation Failed: NetworkTransform component is missing on the Player Prefab.");
                success = false;
            }
            else
            {
                Debug.Log("[NET] Validation Check: NetworkTransform present on Player Prefab. [OK]");
            }

            if (playerPrefab.GetComponent<NetworkPlayerSetup>() == null)
            {
                Debug.LogError("[NET ERROR] Validation Failed: NetworkPlayerSetup component is missing on the Player Prefab.");
                success = false;
            }
            else
            {
                Debug.Log("[NET] Validation Check: NetworkPlayerSetup present on Player Prefab. [OK]");
            }
        }

        return success;
    }

    private void ConfigureTransport(bool isHost)
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        string ipAddress = "127.0.0.1";
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            ipAddress = ipInputField.text.Trim();
        }

        if (isHost)
        {
            // Host listens on all interfaces (0.0.0.0)
            transport.SetConnectionData("127.0.0.1", defaultPort, "0.0.0.0");
        }
        else
        {
            // Client connects to the specific IP address entered
            transport.SetConnectionData(ipAddress, defaultPort);
        }

        // Логирование конфигурации подключения
        Debug.Log($"[NET] Connection IP: {transport.ConnectionData.Address}");
        Debug.Log($"[NET] Listen Address: {transport.ConnectionData.ServerListenAddress}");
        Debug.Log($"[NET] Port: {transport.ConnectionData.Port}");
        Debug.Log($"[NET] Connection State: {(NetworkManager.Singleton.IsListening ? "Active" : "Connecting/Inactive")}");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[NET] Client Connected: ID {clientId}");
        UpdateUI();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NET] Client Disconnected: ID {clientId}");
        UpdateUI();
    }

    private void OnTransportFailure()
    {
        Debug.LogError("[NET] Transport Failure");
    }

    private void UpdateUI()
    {
        bool isNetworkActive = NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        // Блокировка/скрытие полей во время активной сессии
        if (hostButton != null) hostButton.gameObject.SetActive(!isNetworkActive);
        if (clientButton != null) clientButton.gameObject.SetActive(!isNetworkActive);
        if (ipInputField != null) ipInputField.gameObject.SetActive(!isNetworkActive);
        
        if (disconnectButton != null) disconnectButton.gameObject.SetActive(isNetworkActive);
    }

    private void OnGUI()
    {
        if (!isUiActive) return;

        // Построение отладочной строки
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== MULTIPLAYER DEBUG PANEL ===");
        
        if (NetworkManager.Singleton == null)
        {
            sb.AppendLine("NetworkManager: NOT FOUND");
        }
        else
        {
            bool isHost = NetworkManager.Singleton.IsHost;
            bool isServer = NetworkManager.Singleton.IsServer;
            bool isClient = NetworkManager.Singleton.IsClient;
            
            string mode = "Idle";
            if (isHost) mode = "Host";
            else if (isServer) mode = "Server";
            else if (isClient) mode = "Client";
            
            sb.AppendLine($"Host/Client Mode: {mode}");
            sb.AppendLine($"Network Status: {(NetworkManager.Singleton.IsListening ? "Connected/Active" : "Disconnected/Inactive")}");
            
            if (isClient || isServer)
            {
                sb.AppendLine($"Local Client ID: {NetworkManager.Singleton.LocalClientId}");
                
                System.Collections.Generic.List<string> clientIds = new System.Collections.Generic.List<string>();
                if (NetworkManager.Singleton.ConnectedClientsList != null)
                {
                    foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    {
                        clientIds.Add(client.ClientId.ToString());
                    }
                }
                sb.AppendLine($"Connected Client IDs: {string.Join(", ", clientIds)}");
                
                NetworkObject localPlayer = null;
                if (NetworkManager.Singleton.SpawnManager != null)
                {
                    localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
                }
                
                if (localPlayer != null)
                {
                    sb.AppendLine($"Player Object ID: {localPlayer.NetworkObjectId}");
                    sb.AppendLine($"Owner Client ID: {localPlayer.OwnerClientId}");
                    sb.AppendLine($"Current Position: {localPlayer.transform.position.ToString("F3")}");
                }
                else
                {
                    sb.AppendLine("Player Object: NOT SPAWNED");
                }
            }
        }

        string debugText = sb.ToString();

        // Обновляем текстовый компонент, если он назначен
        if (debugTextInfo != null)
        {
            debugTextInfo.text = debugText;
        }

        // Рисуем экранный оверлей на случай, если TMP_Text не настроен в инспекторе сцены
        GUI.Box(new Rect(15, 15, 320, 240), "");
        GUI.Label(new Rect(25, 20, 300, 230), debugText);
    }

    // Вспомогательный метод для подготовки к переходу между сценами
    // Позволяет серверу загружать нужные сцены для всех клиентов через SceneManager Netcode
    public void LoadNetworkScene(string sceneName)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
