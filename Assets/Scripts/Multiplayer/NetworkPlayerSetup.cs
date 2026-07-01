using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class NetworkPlayerSetup : NetworkBehaviour
{
    private PlayerMovement localPlayerMovement;
    private PlayerInteraction localPlayerInteraction;
    private PlayerCamera localPlayerCamera;
    private Camera localCamera;
    private AudioListener localAudioListener;
    private CharacterController characterController;

    private void Awake()
    {
        // Динамический поиск компонентов, чтобы исключить ошибки ручной настройки в инспекторе
        localPlayerMovement = GetComponent<PlayerMovement>();
        localPlayerInteraction = GetComponent<PlayerInteraction>();
        localPlayerCamera = GetComponentInChildren<PlayerCamera>();
        localCamera = GetComponentInChildren<Camera>();
        localAudioListener = GetComponentInChildren<AudioListener>();
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // Логирование спавна игрока с координатами (Часть 4)
        Debug.Log($"[NET] Player spawned at:\nPosition={transform.position}\nRotation={transform.rotation.eulerAngles}");

        // Логирование информации о владении (Часть 1)
        LogOwnershipInfo("OnNetworkSpawn");

        // Очищаем синглплеерного игрока со сцены, если он остался
        CleanSingleplayerPlayer();

        if (IsOwner)
        {
            Debug.Log($"[NET] Spawned Player Owner={OwnerClientId}");
            Debug.Log("[NET] Local Player Initialized");

            // Регистрируем локального игрока в GameManager
            RegisterInGameManager();

            // Включаем компоненты управления локального игрока
            EnableLocalPlayer();
        }
        else
        {
            Debug.Log($"[NET] Spawned Player Owner={OwnerClientId}");
            Debug.Log("[NET] Remote Player Initialized");

            // Отключаем компоненты управления удаленного игрока
            DisableRemotePlayer();
        }
    }

    public override void OnNetworkDespawn()
    {
        Debug.Log($"[NET] Player despawned: OwnerClientId={OwnerClientId}");
        
        if (IsOwner)
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.playerTransform == transform)
                {
                    GameManager.Instance.playerTransform = null;
                }
                if (GameManager.Instance.playerController == characterController)
                {
                    GameManager.Instance.playerController = null;
                }
                Debug.Log("[NET] Unregistered local player from GameManager on despawn.");
            }
        }
    }

    public override void OnGainedOwnership()
    {
        Debug.Log($"[NET] Ownership changed: Gained ownership. New OwnerClientId={OwnerClientId}");
        LogOwnershipInfo("OnGainedOwnership");
        EnableLocalPlayer();
    }

    public override void OnLostOwnership()
    {
        Debug.Log($"[NET] Ownership changed: Lost ownership. New OwnerClientId={OwnerClientId}");
        LogOwnershipInfo("OnLostOwnership");
        DisableRemotePlayer();
    }

    private void LogOwnershipInfo(string phase)
    {
        ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 999;
        Debug.Log($"[NET]\nClientId={localClientId}\nOwnerClientId={OwnerClientId}\nIsOwner={IsOwner}\nIsLocalPlayer={IsLocalPlayer}\nIsServer={IsServer}\nIsClient={IsClient}");
    }

    private IEnumerator Start()
    {
        // Ждем 1 секунду для стабилизации соединения и логируем состояние владения
        yield return new WaitForSeconds(1.0f);

        LogOwnershipInfo("Start-Delayed");

        if (IsOwner)
        {
            // На всякий случай повторно активируем компоненты локального игрока
            RegisterInGameManager();
            EnableLocalPlayer();
            Debug.Log("[NET DEBUG START] Local player components verified and enabled.");
        }
        else
        {
            DisableRemotePlayer();
        }
    }

    private void CleanSingleplayerPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p != gameObject)
            {
                NetworkObject nObj = p.GetComponent<NetworkObject>();
                // Если объект не сетевой или не является зарегистрированным PlayerObject
                if (nObj == null || !nObj.IsPlayerObject)
                {
                    Destroy(p);
                    Debug.Log($"[NET] Destroyed non-network/duplicate Player object: {p.name}");
                }
            }
        }
    }

    private void RegisterInGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.playerTransform = transform;
            GameManager.Instance.playerController = characterController;
            Debug.Log("[NET] Registered local player in GameManager.");
        }
    }

    private void EnableLocalPlayer()
    {
        SetCharacterControllerState(true);
        EnableComponent(localPlayerMovement, "PlayerMovement");
        EnableComponent(localPlayerInteraction, "PlayerInteraction");
        EnableComponent(localPlayerCamera, "PlayerCamera");
        EnableComponent(localCamera, "Camera");
        EnableComponent(localAudioListener, "AudioListener");
    }

    private void DisableRemotePlayer()
    {
        SetCharacterControllerState(false);
        DisableComponent(localPlayerMovement, "PlayerMovement");
        DisableComponent(localPlayerInteraction, "PlayerInteraction");
        DisableComponent(localPlayerCamera, "PlayerCamera");
        DisableComponent(localCamera, "Camera");
        DisableComponent(localAudioListener, "AudioListener");
    }

    private void EnableComponent(Behaviour comp, string name)
    {
        if (comp != null)
        {
            comp.enabled = true;
            Debug.Log($"[NET] Enabled {name}");
        }
    }

    private void DisableComponent(Behaviour comp, string name)
    {
        if (comp != null)
        {
            comp.enabled = false;
            Debug.Log($"[NET] Disabled {name}");
        }
    }

    private void SetCharacterControllerState(bool enabled)
    {
        if (characterController != null)
        {
            characterController.enabled = enabled;
            Debug.Log($"[NET] {(enabled ? "Enabled" : "Disabled")} CharacterController");
        }
    }
}
