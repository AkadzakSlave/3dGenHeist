using System;
using UnityEngine;
using Steamworks;
using WildBunch.Networking.Config;

namespace WildBunch.Networking.Steam
{
    /// <summary>
    /// Core Steam bootstrap and lifecycle manager.
    /// Safely initializes Steamworks without crashing in offline mode,
    /// runs Steam callbacks, and manages Steam user identity.
    /// </summary>
    [DisallowMultipleComponent]
    public class SteamBootstrap : MonoBehaviour
    {
        public static SteamBootstrap Instance { get; private set; }

        public static bool IsSteamAvailable { get; private set; }
        public static CSteamID CurrentSteamId { get; private set; }
        public static string CurrentPersonaName { get; private set; } = "Player";
        public static uint ActiveAppId { get; private set; }

        public static event Action<bool> OnSteamInitialized;
        public static event Action<ulong> OnLobbyInviteAccepted;

        private Callback<GameLobbyJoinRequested_t> m_LobbyJoinRequested;
        private Callback<GameOverlayActivated_t> m_OverlayActivated;

        private bool m_IsInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            GameObject steamObj = new GameObject("[SteamBootstrap]");
            DontDestroyOnLoad(steamObj);
            steamObj.AddComponent<SteamBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeSteam();
        }

        private void InitializeSteam()
        {
            NetworkSessionConfig config = NetworkSessionConfig.Instance;
            ActiveAppId = config != null ? config.AppId : 480;

            try
            {
                if (!Packsize.Test())
                {
                    Debug.LogWarning("[SteamBootstrap] Packsize Test failed! Incorrect Steamworks.NET platform build.");
                    FallbackOffline();
                    return;
                }

                if (!DllCheck.Test())
                {
                    Debug.LogWarning("[SteamBootstrap] DllCheck Test failed! Steamworks binaries mismatch.");
                    FallbackOffline();
                    return;
                }

                // Initialize Steam API
                if (!SteamAPI.Init())
                {
                    Debug.LogWarning("[SteamBootstrap] SteamAPI.Init() failed! Steam client is not running or steam_appid.txt is missing. Starting in Offline Mode.");
                    FallbackOffline();
                    return;
                }

                m_IsInitialized = true;
                IsSteamAvailable = true;

                CurrentSteamId = SteamUser.GetSteamID();
                CurrentPersonaName = SteamFriends.GetPersonaName();

                Debug.Log($"[SteamBootstrap] Steam successfully initialized for AppId {ActiveAppId}! Logged in as: {CurrentPersonaName} ({CurrentSteamId.m_SteamID})");

                // Setup Steam Callbacks
                m_LobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
                m_OverlayActivated = Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated);

                OnSteamInitialized?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SteamBootstrap] Steam initialization encountered exception: {ex.Message}. Starting in Offline Mode.");
                FallbackOffline();
            }
        }

        private void FallbackOffline()
        {
            m_IsInitialized = false;
            IsSteamAvailable = false;
            CurrentSteamId = CSteamID.Nil;
            CurrentPersonaName = "Offline Player";

            Debug.Log("[SteamBootstrap] Running in Offline / Local Mode. Multiplayer features requiring Steam will be disabled.");
            OnSteamInitialized?.Invoke(false);
        }

        private void Update()
        {
            if (m_IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t pCallback)
        {
            Debug.Log($"[SteamBootstrap] Lobby Join requested from friend: LobbyId={pCallback.m_steamIDLobby}");
            OnLobbyInviteAccepted?.Invoke(pCallback.m_steamIDLobby.m_SteamID);
        }

        private void OnGameOverlayActivated(GameOverlayActivated_t pCallback)
        {
            if (pCallback.m_bActive != 0)
            {
                Debug.Log("[SteamBootstrap] Steam Overlay activated.");
            }
            else
            {
                Debug.Log("[SteamBootstrap] Steam Overlay deactivated.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (m_IsInitialized)
            {
                SteamAPI.Shutdown();
                m_IsInitialized = false;
                IsSteamAvailable = false;
                Debug.Log("[SteamBootstrap] SteamAPI cleanly shut down.");
            }
        }
    }
}
