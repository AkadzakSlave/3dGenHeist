using UnityEngine;
using Mirror;

namespace WildBunch.Networking.Players
{
    /// <summary>
    /// Synchronizes active equipped tool/weapon, crouching, and head pitch for remote players.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkItemSync : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerCamera playerCam;
        [SerializeField] private Transform thirdPersonHammer;
        [SerializeField] private Transform spineOrHeadBone;

        [SyncVar(hook = nameof(OnSlotChanged))]
        private int m_ActiveSlotIndex = 0;

        [SyncVar]
        private bool m_IsCrouching = false;

        [SyncVar]
        private float m_LookPitch = 0f;

        private float m_LastPitchSent = 0f;
        private float m_PitchSendTimer = 0f;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            if (playerCam == null) playerCam = GetComponentInChildren<PlayerCamera>(true);

            if (thirdPersonHammer == null)
            {
                // Find third person hammer model
                Transform t = transform.Find("Hammer (1)");
                if (t != null) thirdPersonHammer = t;
            }
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();

            // Local player syncs their slot upon start
            if (inventory != null)
            {
                CmdSetSlot(inventory.activeSlotIndex);
            }
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            // Check slot change
            if (inventory != null && inventory.activeSlotIndex != m_ActiveSlotIndex)
            {
                CmdSetSlot(inventory.activeSlotIndex);
            }

            // Check crouch change
            if (movement != null && movement.IsCrouching != m_IsCrouching)
            {
                CmdSetCrouching(movement.IsCrouching);
            }

            // Check pitch change
            if (playerCam != null)
            {
                m_PitchSendTimer += Time.deltaTime;
                if (m_PitchSendTimer >= 0.1f) // 10 Hz rate limit
                {
                    m_PitchSendTimer = 0f;
                    float currentPitch = playerCam.transform.localEulerAngles.x;
                    if (Mathf.Abs(Mathf.DeltaAngle(currentPitch, m_LastPitchSent)) > 1.5f)
                    {
                        m_LastPitchSent = currentPitch;
                        CmdSetLookPitch(currentPitch);
                    }
                }
            }
        }

        [Command]
        private void CmdSetSlot(int slotIndex)
        {
            m_ActiveSlotIndex = slotIndex;
        }

        [Command]
        private void CmdSetCrouching(bool crouching)
        {
            m_IsCrouching = crouching;
        }

        [Command]
        private void CmdSetLookPitch(float pitch)
        {
            m_LookPitch = pitch;
        }

        private void OnSlotChanged(int oldSlot, int newSlot)
        {
            m_ActiveSlotIndex = newSlot;

            if (isLocalPlayer) return; // Local player handles their own visual hands

            // If remote player is holding slot 0 (Tool/Hammer)
            if (thirdPersonHammer != null)
            {
                thirdPersonHammer.gameObject.SetActive(newSlot == 0);
            }
        }

        private void LateUpdate()
        {
            if (isLocalPlayer) return;

            // Apply synced pitch to remote head/spine bone if assigned
            if (spineOrHeadBone != null)
            {
                spineOrHeadBone.localEulerAngles = new Vector3(m_LookPitch, spineOrHeadBone.localEulerAngles.y, spineOrHeadBone.localEulerAngles.z);
            }
        }
    }
}
