using UnityEngine;
using Mirror;

namespace WildBunch.Networking.World
{
    /// <summary>
    /// Synchronizes global world state, including the procedural level seed
    /// and heist phase across all connected clients.
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkWorldState : NetworkBehaviour
    {
        public static NetworkWorldState Instance { get; private set; }

        [SyncVar(hook = nameof(OnSeedChanged))]
        private int m_LevelSeed = 0;

        [SyncVar(hook = nameof(OnHeistActiveChanged))]
        private bool m_IsHeistActive = false;

        [SyncVar(hook = nameof(OnDayChanged))]
        private int m_CurrentDay = 1;

        [SyncVar(hook = nameof(OnQuotaChanged))]
        private int m_OperationTargetQuota = 3000;

        [SyncVar(hook = nameof(OnAccumulatedMoneyChanged))]
        private int m_AccumulatedMoney = 0;

        [SyncVar(hook = nameof(OnDepositedMoneyChanged))]
        private int m_DepositedMoney = 0;

        public int LevelSeed => m_LevelSeed;
        public bool IsHeistActive => m_IsHeistActive;
        public int CurrentDay => m_CurrentDay;
        public int OperationTargetQuota => m_OperationTargetQuota;
        public int AccumulatedMoney => m_AccumulatedMoney;
        public int DepositedMoney => m_DepositedMoney;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Generate authoritative seed for bank procedural generation
            m_LevelSeed = Random.Range(100000, 9999999);
            Debug.Log($"[NetworkWorldState] Server generated authoritative level seed: {m_LevelSeed}");

            if (LevelGenerator.Instance != null)
            {
                LevelGenerator.Instance.SetSeed(m_LevelSeed);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Client receives existing seed on connect
            if (m_LevelSeed != 0 && LevelGenerator.Instance != null)
            {
                LevelGenerator.Instance.SetSeed(m_LevelSeed);
            }
        }

        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            m_LevelSeed = newSeed;
            Debug.Log($"[NetworkWorldState] Received synced bank seed: {newSeed}");

            if (LevelGenerator.Instance != null)
            {
                LevelGenerator.Instance.SetSeed(newSeed);
            }
        }

        private void OnHeistActiveChanged(bool oldVal, bool newVal)
        {
            m_IsHeistActive = newVal;
            Debug.Log($"[NetworkWorldState] Heist Active state changed: {newVal}");

            if (newVal && GameManager.Instance != null && !GameManager.Instance.isHeistActive)
            {
                GameManager.Instance.StartHeist();
            }
        }

        [Server]
        public void ServerNotifyHeistStarted()
        {
            m_IsHeistActive = true;
        }

        [Server]
        public void ServerRegenerateNewSeed()
        {
            m_LevelSeed = Random.Range(100000, 9999999);
            m_IsHeistActive = false;
            Debug.Log($"[NetworkWorldState] Server refreshed level seed for new day: {m_LevelSeed}");

            if (LevelGenerator.Instance != null)
            {
                LevelGenerator.Instance.SetSeed(m_LevelSeed);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdDamageWallAtPosition(Vector3 wallPos, int damage)
        {
            DestructibleWall closestWall = FindWallNear(wallPos);
            if (closestWall != null)
            {
                closestWall.TakeDamage(damage);
            }
        }

        [Command(requiresAuthority = false)]
        public void CmdDamageEnemy(uint enemyNetId, int damage, Vector3 attackerPos)
        {
            if (NetworkServer.spawned.TryGetValue(enemyNetId, out NetworkIdentity identity) && identity != null)
            {
                EnemyHealth eh = identity.GetComponentInChildren<EnemyHealth>();
                if (eh != null)
                {
                    eh.TakeDamage(damage, attackerPos);
                }
            }
        }

        [ClientRpc]
        public void RpcBreakWallAtPosition(Vector3 wallPos)
        {
            DestructibleWall closestWall = FindWallNear(wallPos);
            if (closestWall != null)
            {
                closestWall.BreakWall();
            }
        }

        private void OnDayChanged(int oldDay, int newDay)
        {
            m_CurrentDay = newDay;
            if (GameManager.Instance != null) GameManager.Instance.currentDay = newDay;
        }

        private void OnQuotaChanged(int oldQuota, int newQuota)
        {
            m_OperationTargetQuota = newQuota;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.operationTargetQuota = newQuota;
                GameManager.Instance.onMoneyChanged?.Invoke();
            }
        }

        private void OnAccumulatedMoneyChanged(int oldVal, int newVal)
        {
            m_AccumulatedMoney = newVal;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.accumulatedOperationMoney = newVal;
                GameManager.Instance.onMoneyChanged?.Invoke();
            }
        }

        private void OnDepositedMoneyChanged(int oldVal, int newVal)
        {
            m_DepositedMoney = newVal;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.depositedMoney = newVal;
                GameManager.Instance.onMoneyChanged?.Invoke();
            }
        }

        [Server]
        public void ServerSyncEconomy(int day, int quota, int accumulated, int deposited)
        {
            m_CurrentDay = day;
            m_OperationTargetQuota = quota;
            m_AccumulatedMoney = accumulated;
            m_DepositedMoney = deposited;
        }

        [Command(requiresAuthority = false)]
        public void CmdRequestExtraction()
        {
            if (GameManager.Instance != null && !GameManager.Instance.isInLobby)
            {
                Debug.Log("[NetworkWorldState] Server received extraction request from client. Triggering ExtractFromHeist...");
                GameManager.Instance.ExtractFromHeist();
            }
        }

        [ClientRpc]
        public void RpcTeleportTeam(Vector3 pos, Quaternion rot)
        {
            // На клиенте находим локального персонажа и телепортируем его CharacterController
            var localNetPlayer = Players.NetworkPlayerIdentity.LocalPlayerInstance;
            if (localNetPlayer != null)
            {
                var cc = localNetPlayer.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                localNetPlayer.transform.position = pos;
                localNetPlayer.transform.rotation = rot;
                var pm = localNetPlayer.GetComponent<PlayerMovement>();
                if (pm != null) pm.ResetVelocity();
                if (cc != null) cc.enabled = true;
            }
        }

        [ClientRpc]
        public void RpcShowDonResults(int total, int quota, int clean, int session, int nextQuota, int quotaNumber, bool isSuccess)
        {
            if (DonResultUI.Instance != null)
            {
                DonResultUI.Instance.ShowResults(total, quota, clean, session, nextQuota, quotaNumber, isSuccess);
            }
        }

        [ClientRpc]
        public void RpcShowLoadingScreen(bool show)
        {
            if (HeistUI.Instance != null)
            {
                if (show) HeistUI.Instance.ShowLoadingScreen();
                else HeistUI.Instance.HideLoadingScreen();
            }
        }

        private DestructibleWall FindWallNear(Vector3 pos)
        {
            DestructibleWall[] walls = FindObjectsByType<DestructibleWall>(FindObjectsSortMode.None);
            DestructibleWall best = null;
            float minD = 2.5f;

            foreach (var w in walls)
            {
                if (w == null) continue;
                float d = Vector3.Distance(w.transform.position, pos);
                if (d < minD)
                {
                    minD = d;
                    best = w;
                }
            }

            return best;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
