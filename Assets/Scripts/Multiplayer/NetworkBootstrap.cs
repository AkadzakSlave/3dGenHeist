using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

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

        // Защита от двойного запуска или повторного нажатия
        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
        {
            Debug.LogWarning("[NET] Network session already running.");
            return;
        }

        Debug.Log("[NET] Starting Host");
        ConfigureTransport();

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
        ConfigureTransport();

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

    private void ConfigureTransport()
    {
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null) return;

        string ipAddress = "127.0.0.1";
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            ipAddress = ipInputField.text.Trim();
        }

        transport.SetConnectionData(ipAddress, defaultPort);

        // Логирование конфигурации подключения
        Debug.Log($"[NET] IP: {ipAddress}");
        Debug.Log($"[NET] Port: {defaultPort}");
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
