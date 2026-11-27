using UnityEngine;
using Mirror;
using WildBunch.Networking.Steam;

namespace WildBunch.Networking.Players
{
    /// <summary>
    /// Handles network ownership, player separation (camera, input, audio listener),
    /// and Steam persona synchronization across the network.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkPlayerIdentity : NetworkBehaviour
    {
        public static NetworkPlayerIdentity LocalPlayerInstance { get; private set; }

        [SyncVar(hook = nameof(OnSteamIdChanged))]
        private ulong m_SteamId;

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        private string m_PlayerName = "Player";

        public ulong SteamId => m_SteamId;
        public string PlayerName => m_PlayerName;

        [Header("Components to isolate")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private MonoBehaviour playerMovement;
        [SerializeField] private MonoBehaviour playerCameraController;
        [SerializeField] private MonoBehaviour playerInteraction;
        [SerializeField] private GameObject remoteBodyMesh;

        private TextMesh m_Nameplate;

        private void Awake()
        {
            AutoDiscoverComponents();
        }

        private void AutoDiscoverComponents()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }

            if (audioListener == null && playerCamera != null)
            {
                audioListener = playerCamera.GetComponent<AudioListener>();
            }

            if (playerMovement == null)
            {
                playerMovement = GetComponent<PlayerMovement>();
            }

            if (playerCameraController == null)
            {
                playerCameraController = GetComponentInChildren<PlayerCamera>(true);
            }

            if (playerInteraction == null)
            {
                playerInteraction = GetComponent<PlayerInteraction>();
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            LocalPlayerInstance = this;

            // Enable local control
            if (playerCamera != null) playerCamera.enabled = true;
            if (audioListener != null) audioListener.enabled = true;
            if (playerMovement != null) playerMovement.enabled = true;
            if (playerCameraController != null) playerCameraController.enabled = true;
            if (playerInteraction != null) playerInteraction.enabled = true;

            // Ensure proper body shadow/mesh visibility
            PlayerBodyVisibility bodyVis = GetComponent<PlayerBodyVisibility>();
            if (bodyVis != null)
            {
                bodyVis.SetLocalPlayer(true);
            }

            // Send local identity to server
            ulong localId = SteamBootstrap.CurrentSteamId != Steamworks.CSteamID.Nil ? SteamBootstrap.CurrentSteamId.m_SteamID : 0;
            string localName = SteamBootstrap.CurrentPersonaName;
            CmdSetIdentity(localId, localName);

            Debug.Log($"[NetworkPlayerIdentity] Local player initialized: {localName} (SteamID: {localId})");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            CreateNameplate();

            if (!isLocalPlayer)
            {
                // Disable remote player local sensory and input components
                if (playerCamera != null) playerCamera.enabled = false;
                if (audioListener != null) audioListener.enabled = false;
                if (playerMovement != null) playerMovement.enabled = false;
                if (playerCameraController != null) playerCameraController.enabled = false;
                if (playerInteraction != null) playerInteraction.enabled = false;

                // Ensure remote body mesh is visible
                PlayerBodyVisibility remoteBodyVis = GetComponent<PlayerBodyVisibility>();
                if (remoteBodyVis != null)
                {
                    remoteBodyVis.SetLocalPlayer(false);
                }
                else if (remoteBodyMesh != null)
                {
                    remoteBodyMesh.SetActive(true);
                }
            }
        }

        private void CreateNameplate()
        {
            if (isLocalPlayer) return; // Local player doesn't need to see their own nameplate

            GameObject nameplateObj = new GameObject("Nameplate");
            nameplateObj.transform.SetParent(transform);
            nameplateObj.transform.localPosition = new Vector3(0, 2.1f, 0);

            m_Nameplate = nameplateObj.AddComponent<TextMesh>();
            m_Nameplate.text = m_PlayerName;
            m_Nameplate.fontSize = 28;
            m_Nameplate.characterSize = 0.05f;
            m_Nameplate.alignment = TextAlignment.Center;
            m_Nameplate.anchor = TextAnchor.MiddleCenter;
            m_Nameplate.color = Color.white;
        }

        private void LateUpdate()
        {
            // Face nameplate towards the local player camera
            if (m_Nameplate != null && Camera.main != null)
            {
                m_Nameplate.transform.rotation = Quaternion.LookRotation(m_Nameplate.transform.position - Camera.main.transform.position);
            }
        }

        [Command]
        private void CmdSetIdentity(ulong id, string name)
        {
            m_SteamId = id;
            m_PlayerName = string.IsNullOrEmpty(name) ? "Player" : name;
        }

        private void OnSteamIdChanged(ulong oldId, ulong newId)
        {
            m_SteamId = newId;
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            m_PlayerName = newName;
            if (m_Nameplate != null)
            {
                m_Nameplate.text = newName;
            }
        }

        private void OnDestroy()
        {
            if (LocalPlayerInstance == this)
            {
                LocalPlayerInstance = null;
            }
        }
    }
}
