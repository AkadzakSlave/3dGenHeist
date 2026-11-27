using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.FizzySteam;
using WildBunch.Networking.Config;
using WildBunch.Networking.Players;

namespace WildBunch.Networking.Core
{
    /// <summary>
    /// Custom NetworkManager managing co-op lifecycle, FizzySteamworks transport,
    /// player spawning and session state.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkSessionManager : NetworkManager
    {
        public static new NetworkSessionManager Instance => singleton as NetworkSessionManager;

        public static int ConnectedPlayerCount { get; private set; }

        private readonly List<NetworkPlayerIdentity> m_RegisteredPlayers = new List<NetworkPlayerIdentity>();
        public IReadOnlyList<NetworkPlayerIdentity> RegisteredPlayers => m_RegisteredPlayers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (singleton != null) return;

            GameObject mgrObj = new GameObject("[NetworkSessionManager]");
            DontDestroyOnLoad(mgrObj);

            // Attach FizzySteamworks transport
            FizzySteamworks transport = mgrObj.AddComponent<FizzySteamworks>();
            Transport.active = transport;

            NetworkSessionManager netMgr = mgrObj.AddComponent<NetworkSessionManager>();
            netMgr.transport = transport;

            // Load default network player prefab from Resources
            GameObject playerPrefab = Resources.Load<GameObject>("NetworkPlayer");
            if (playerPrefab == null)
            {
                playerPrefab = Resources.Load<GameObject>("Player");
            }
            if (playerPrefab != null)
            {
                netMgr.playerPrefab = playerPrefab;
            }

            netMgr.autoCreatePlayer = true;
            netMgr.dontDestroyOnLoad = true;

            // Also attach SteamLobbyManager
            if (Lobby.SteamLobbyManager.Instance == null)
            {
                mgrObj.AddComponent<Lobby.SteamLobbyManager>();
            }

            // Also attach NetworkWorldState
            if (World.NetworkWorldState.Instance == null)
            {
                mgrObj.AddComponent<World.NetworkWorldState>();
            }

            // Also attach NetworkDebugOverlay
            if (Debug_Overlay.NetworkDebugOverlay.Instance == null)
            {
                mgrObj.AddComponent<Debug_Overlay.NetworkDebugOverlay>();
            }
        }

        public override void Awake()
        {
            base.Awake();

            NetworkSessionConfig config = NetworkSessionConfig.Instance;
            maxConnections = config != null ? config.MaxPlayers : 4;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[NetworkSessionManager] Network Server started on host.");
            m_RegisteredPlayers.Clear();
            ConnectedPlayerCount = 0;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            Debug.Log("[NetworkSessionManager] Network Server stopped.");
            m_RegisteredPlayers.Clear();
            ConnectedPlayerCount = 0;
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            ConnectedPlayerCount++;
            Debug.Log($"[NetworkSessionManager] Client connected. ID={conn.connectionId}, Total={ConnectedPlayerCount}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            ConnectedPlayerCount = Mathf.Max(0, ConnectedPlayerCount - 1);
            Debug.Log($"[NetworkSessionManager] Client disconnected. ID={conn.connectionId}, Remaining={ConnectedPlayerCount}");

            if (conn.identity != null)
            {
                NetworkPlayerIdentity playerIdentity = conn.identity.GetComponent<NetworkPlayerIdentity>();
                if (playerIdentity != null)
                {
                    m_RegisteredPlayers.Remove(playerIdentity);
                }
            }

            base.OnServerDisconnect(conn);
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Transform startPos = GetStartPosition();
            Vector3 spawnPosition = startPos != null ? startPos.position : Vector3.zero;
            Quaternion spawnRotation = startPos != null ? startPos.rotation : Quaternion.identity;

            // Offset each player slightly around spawn point so they do not intersect
            int playerIndex = numPlayers;
            Vector3 offset = new Vector3((playerIndex % 2 == 0 ? 1f : -1f) * (playerIndex * 0.75f), 0, (playerIndex > 1 ? -1f : 0f));
            spawnPosition += offset;

            GameObject playerObj = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            NetworkServer.AddPlayerForConnection(conn, playerObj);

            NetworkPlayerIdentity netPlayer = playerObj.GetComponent<NetworkPlayerIdentity>();
            if (netPlayer != null)
            {
                m_RegisteredPlayers.Add(netPlayer);
            }

            Debug.Log($"[NetworkSessionManager] Player spawned for connId={conn.connectionId} at {spawnPosition}");
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[NetworkSessionManager] Local client connected to server successfully.");
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            Debug.Log("[NetworkSessionManager] Local client disconnected from server.");
        }
    }
}
