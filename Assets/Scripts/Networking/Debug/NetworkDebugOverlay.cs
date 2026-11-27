using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Steamworks;
using WildBunch.Networking.Steam;
using WildBunch.Networking.Lobby;
using WildBunch.Networking.Config;

namespace WildBunch.Networking.Debug_Overlay
{
    /// <summary>
    /// In-game diagnostic overlay toggled with F8.
    /// Provides live network telemetry, Steam lobby controls, and player list.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkDebugOverlay : MonoBehaviour
    {
        public static NetworkDebugOverlay Instance { get; private set; }

        [SerializeField] private bool showOverlay = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;

        private Rect m_WindowRect = new Rect(20, 20, 360, 480);
        private GUIStyle m_HeaderStyle;
        private GUIStyle m_BoxStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                showOverlay = !showOverlay;
            }
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            InitStyles();
            m_WindowRect = GUI.Window(9999, m_WindowRect, DrawWindow, "Wild Bunch — Network Co-op Debug [F8]");
        }

        private void InitStyles()
        {
            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };
            }

            if (m_BoxStyle == null)
            {
                m_BoxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft
                };
            }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // Status block
            string netMode = "Offline";
            if (NetworkServer.active && NetworkClient.isConnected) netMode = "Host (Listen Server)";
            else if (NetworkClient.isConnected) netMode = "Client";
            else if (NetworkServer.active) netMode = "Server Only";

            GUILayout.Label($"Mode: {netMode}", m_HeaderStyle);
            GUILayout.Label($"Steam Available: {SteamBootstrap.IsSteamAvailable}");
            GUILayout.Label($"Steam User: {SteamBootstrap.CurrentPersonaName} ({SteamBootstrap.CurrentSteamId})");
            GUILayout.Label($"App ID: {SteamBootstrap.ActiveAppId}");

            if (SteamLobbyManager.IsInLobby)
            {
                GUILayout.Label($"Lobby ID: {SteamLobbyManager.CurrentLobbyId.m_SteamID}");
                if (GUILayout.Button("Copy Lobby ID to Clipboard"))
                {
                    GUIUtility.systemCopyBuffer = SteamLobbyManager.CurrentLobbyId.m_SteamID.ToString();
                }
            }
            else
            {
                GUILayout.Label("Lobby: None");
            }

            if (NetworkClient.isConnected)
            {
                GUILayout.Label($"Ping (RTT): {(NetworkTime.rtt * 1000):F0} ms");
            }

            GUILayout.Space(10);
            GUILayout.Label("--- Actions ---", m_HeaderStyle);

            if (!SteamLobbyManager.IsInLobby && !NetworkClient.isConnected && !NetworkServer.active)
            {
                if (GUILayout.Button("Create Steam Lobby (Friends Only)"))
                {
                    SteamLobbyManager.Instance.CreateLobby(true);
                }

                if (GUILayout.Button("Create Steam Lobby (Public)"))
                {
                    SteamLobbyManager.Instance.CreateLobby(false);
                }
            }
            else
            {
                if (SteamLobbyManager.IsInLobby)
                {
                    if (GUILayout.Button("Invite Friends (Overlay)"))
                    {
                        SteamLobbyManager.Instance.OpenInviteOverlay();
                    }
                }

                if (GUILayout.Button("Leave Lobby & Disconnect"))
                {
                    SteamLobbyManager.Instance.LeaveLobby();
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("--- Lobby Members ---", m_HeaderStyle);

            if (SteamLobbyManager.IsInLobby && SteamLobbyManager.Instance != null)
            {
                List<CSteamID> members = SteamLobbyManager.Instance.GetCurrentMembers();
                GUILayout.Label($"Total Members: {members.Count} / {NetworkSessionConfig.Instance.MaxPlayers}");

                foreach (CSteamID member in members)
                {
                    string memberName = SteamFriends.GetFriendPersonaName(member);
                    bool isHostMember = member == SteamBootstrap.CurrentSteamId && SteamLobbyManager.IsHost;
                    GUILayout.Label($"• {memberName} {(isHostMember ? "[HOST]" : "")}");
                }
            }
            else
            {
                GUILayout.Label("No active lobby.");
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
}
