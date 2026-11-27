using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using WildBunch.Networking.Config;
using WildBunch.Networking.Core;

namespace WildBunch.Networking.Lobby
{
    /// <summary>
    /// Manages Steam Matchmaking lobbies, metadata filtering (Spacewar 480 isolation),
    /// invites, and handoff to NetworkSessionManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        public const string KeyGameId = "game_id";
        public const string KeyGameVersion = "game_version";
        public const string KeyProtocolVersion = "protocol_version";
        public const string KeySessionState = "session_state";
        public const string KeyHostSteamId = "host_steam_id";
        public const string KeyMaxPlayers = "max_players";

        public static CSteamID CurrentLobbyId { get; private set; } = CSteamID.Nil;
        public static bool IsInLobby => CurrentLobbyId != CSteamID.Nil;
        public static bool IsHost { get; private set; }

        public static event Action<bool, ulong> OnLobbyCreatedEvent;
        public static event Action<ulong> OnLobbyEnteredEvent;
        public static event Action OnLobbyLeftEvent;
        public static event Action<List<CSteamID>> OnMemberListUpdatedEvent;
        public static event Action<string> OnLobbyErrorEvent;

        private Callback<LobbyCreated_t> m_LobbyCreated;
        private Callback<LobbyEnter_t> m_LobbyEntered;
        private Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            m_LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            m_LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

            Steam.SteamBootstrap.OnLobbyInviteAccepted += HandleInviteAccepted;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Steam.SteamBootstrap.OnLobbyInviteAccepted -= HandleInviteAccepted;
        }

        public void CreateLobby(bool friendsOnly = true)
        {
            if (!Steam.SteamBootstrap.IsSteamAvailable)
            {
                OnLobbyErrorEvent?.Invoke("Steam is not available. Running in offline mode.");
                return;
            }

            if (IsInLobby)
            {
                LeaveLobby();
            }

            NetworkSessionConfig config = NetworkSessionConfig.Instance;
            int maxMembers = config != null ? config.MaxPlayers : 4;
            ELobbyType lobbyType = friendsOnly ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePublic;

            Debug.Log($"[SteamLobbyManager] Requesting Steam lobby creation (Type: {lobbyType}, Max: {maxMembers})...");
            SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
        }

        public void JoinLobby(CSteamID lobbyId)
        {
            if (!Steam.SteamBootstrap.IsSteamAvailable)
            {
                OnLobbyErrorEvent?.Invoke("Steam is not available.");
                return;
            }

            if (IsInLobby)
            {
                LeaveLobby();
            }

            Debug.Log($"[SteamLobbyManager] Joining Steam lobby: {lobbyId}...");
            SteamMatchmaking.JoinLobby(lobbyId);
        }

        public void LeaveLobby()
        {
            if (!IsInLobby) return;

            Debug.Log($"[SteamLobbyManager] Leaving Steam lobby: {CurrentLobbyId}");
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
            IsHost = false;

            if (NetworkSessionManager.Instance != null)
            {
                if (Mirror.NetworkServer.active && Mirror.NetworkClient.isConnected)
                {
                    NetworkSessionManager.Instance.StopHost();
                }
                else if (Mirror.NetworkClient.isConnected)
                {
                    NetworkSessionManager.Instance.StopClient();
                }
            }

            OnLobbyLeftEvent?.Invoke();
        }

        public void OpenInviteOverlay()
        {
            if (!Steam.SteamBootstrap.IsSteamAvailable) return;

            if (IsInLobby)
            {
                SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
            }
            else
            {
                SteamFriends.ActivateGameOverlay("Friends");
            }
        }

        private void OnLobbyCreated(LobbyCreated_t pCallback)
        {
            if (pCallback.m_eResult != EResult.k_EResultOK)
            {
                string errorMsg = $"Lobby creation failed with result: {pCallback.m_eResult}";
                Debug.LogError($"[SteamLobbyManager] {errorMsg}");
                OnLobbyCreatedEvent?.Invoke(false, 0);
                OnLobbyErrorEvent?.Invoke(errorMsg);
                return;
            }

            CurrentLobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);
            IsHost = true;

            NetworkSessionConfig config = NetworkSessionConfig.Instance;
            string gameId = config != null ? config.GameId : "volgiant_wild_bunch";
            int protocolVersion = config != null ? config.ProtocolVersion : 1;
            int maxPlayers = config != null ? config.MaxPlayers : 4;

            // Set required isolation metadata to prevent collision with other Spacewar games
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeyGameId, gameId);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeyGameVersion, Application.version);
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeyProtocolVersion, protocolVersion.ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeySessionState, "Lobby");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeyHostSteamId, SteamUser.GetSteamID().ToString());
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, KeyMaxPlayers, maxPlayers.ToString());

            Debug.Log($"[SteamLobbyManager] Lobby created successfully! ID: {CurrentLobbyId.m_SteamID}, GameId: {gameId}");

            // Start Mirror Host
            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.StartHost();
            }

            OnLobbyCreatedEvent?.Invoke(true, CurrentLobbyId.m_SteamID);
            RefreshMemberList();
        }

        private void OnLobbyEntered(LobbyEnter_t pCallback)
        {
            CurrentLobbyId = new CSteamID(pCallback.m_ulSteamIDLobby);

            if (IsHost)
            {
                // Host already started via OnLobbyCreated
                return;
            }

            // Verify lobby metadata
            string lobbyGameId = SteamMatchmaking.GetLobbyData(CurrentLobbyId, KeyGameId);
            NetworkSessionConfig config = NetworkSessionConfig.Instance;
            string expectedGameId = config != null ? config.GameId : "volgiant_wild_bunch";

            if (lobbyGameId != expectedGameId)
            {
                string errorMsg = $"Incompatible lobby: game_id mismatch ('{lobbyGameId}' vs expected '{expectedGameId}')";
                Debug.LogWarning($"[SteamLobbyManager] {errorMsg}");
                OnLobbyErrorEvent?.Invoke(errorMsg);
                LeaveLobby();
                return;
            }

            string hostSteamIdStr = SteamMatchmaking.GetLobbyData(CurrentLobbyId, KeyHostSteamId);
            if (string.IsNullOrEmpty(hostSteamIdStr))
            {
                string errorMsg = "Host Steam ID missing in lobby metadata!";
                Debug.LogError($"[SteamLobbyManager] {errorMsg}");
                OnLobbyErrorEvent?.Invoke(errorMsg);
                LeaveLobby();
                return;
            }

            Debug.Log($"[SteamLobbyManager] Successfully entered lobby {CurrentLobbyId.m_SteamID}. Host is {hostSteamIdStr}. Connecting Mirror Client...");

            if (NetworkSessionManager.Instance != null)
            {
                NetworkSessionManager.Instance.networkAddress = hostSteamIdStr;
                NetworkSessionManager.Instance.StartClient();
            }

            OnLobbyEnteredEvent?.Invoke(CurrentLobbyId.m_SteamID);
            RefreshMemberList();
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
        {
            Debug.Log($"[SteamLobbyManager] Lobby member update: user {pCallback.m_ulSteamIDUserChanged}, state flags: {pCallback.m_rgfChatMemberStateChange}");
            RefreshMemberList();
        }

        private void HandleInviteAccepted(ulong lobbyId)
        {
            Debug.Log($"[SteamLobbyManager] Handling invite accepted for lobby: {lobbyId}");
            JoinLobby(new CSteamID(lobbyId));
        }

        public List<CSteamID> GetCurrentMembers()
        {
            List<CSteamID> members = new List<CSteamID>();
            if (!IsInLobby) return members;

            int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId);
            for (int i = 0; i < count; i++)
            {
                members.Add(SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, i));
            }
            return members;
        }

        private void RefreshMemberList()
        {
            List<CSteamID> members = GetCurrentMembers();
            OnMemberListUpdatedEvent?.Invoke(members);
        }
    }
}
