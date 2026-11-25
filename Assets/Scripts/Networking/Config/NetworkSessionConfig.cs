using UnityEngine;

namespace WildBunch.Networking.Config
{
    [CreateAssetMenu(fileName = "NetworkSessionConfig", menuName = "Wild Bunch/Networking/Session Config")]
    public class NetworkSessionConfig : ScriptableObject
    {
        private static NetworkSessionConfig s_Instance;
        public static NetworkSessionConfig Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = Resources.Load<NetworkSessionConfig>("NetworkSessionConfig");
                    if (s_Instance == null)
                    {
                        s_Instance = CreateInstance<NetworkSessionConfig>();
                    }
                }
                return s_Instance;
            }
        }

        [Header("Steam Settings")]
        [Tooltip("Steam App ID (480 for Spacewar development/testing)")]
        [SerializeField] private uint appId = 480;

        [Header("Matchmaking & Filtering")]
        [Tooltip("Unique game identifier to filter out non-Wild Bunch Spacewar lobbies")]
        [SerializeField] private string gameId = "volgiant_wild_bunch";

        [Tooltip("Protocol version - prevents connecting clients with mismatched network packets")]
        [SerializeField] private int protocolVersion = 1;

        [Tooltip("Maximum allowed players in a co-op session")]
        [SerializeField] private int maxPlayers = 4;

        [Header("Diagnostics")]
        [SerializeField] private bool verboseLogging = true;

        public uint AppId => appId;
        public string GameId => gameId;
        public int ProtocolVersion => protocolVersion;
        public int MaxPlayers => maxPlayers;
        public bool VerboseLogging => verboseLogging;
    }
}
