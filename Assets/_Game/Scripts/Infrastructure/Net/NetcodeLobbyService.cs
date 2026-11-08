using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Domain.Lobby;

namespace Game.Infrastructure.Net
{
    /// <summary>
    /// ILobbyService'in Netcode (doğrudan IP) uygulaması. "LobbyNetworkManager" objesinin üstünde durur.
    /// Görevi: host/join/leave/start akışı + bağlantı olaylarını UI'ın anlayacağı olaylara çevirmek (Facade/Adapter).
    /// Oyuncu listesi verisi LobbyNetworkState'te, ekran mantığı Presentation'da.
    /// </summary>
    public class NetcodeLobbyService : MonoBehaviour, ILobbyService
    {
        [Header("Referanslar")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private LobbyNetworkState statePrefab;
        [SerializeField] private MapCatalog maps;

        [Header("Ayarlar")]
        [SerializeField] private ushort port = 7777;
        [SerializeField] private float joinTimeout = 8f;
        [Tooltip("Aynı PC'de (Multiplayer Play Mode) test ederken aç: adres 127.0.0.1 gösterilir")]
        [SerializeField] private bool useLoopbackAddress = true;

        public event Action LobbyOpened;
        public event Action<LobbyCloseReason, string> LobbyClosed;
        public event Action<string> JoinFailed;
        public event Action RosterChanged;
        public event Action MapChanged;

        private LobbyNetworkState state;
        private PlayerProfile pendingProfile;
        private int pendingMap;
        private bool leaving;
        private bool matchStarted;
        private Coroutine joinTimer;

        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsInLobby => state != null;
        public ulong LocalClientId => networkManager != null ? networkManager.LocalClientId : 0;
        public string LobbyCode => state != null ? state.Code : "";
        public string Address { get; private set; } = "127.0.0.1";
        public int MapIndex => state != null ? state.MapIndex : pendingMap;
        public IReadOnlyList<LobbyPlayerInfo> Roster =>
            state != null ? state.Snapshot() : (IReadOnlyList<LobbyPlayerInfo>)Array.Empty<LobbyPlayerInfo>();

        // ------------------------------------------------------------------ Kurulum
        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();
            if (transport == null) transport = GetComponent<UnityTransport>();
        }

        private void OnEnable()
        {
            LobbyNetworkState.Spawned += OnStateSpawned;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDisable()
        {
            LobbyNetworkState.Spawned -= OnStateSpawned;
            if (networkManager != null) networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // ------------------------------------------------------------------ HOST
        public void Host(PlayerProfile profile, int mapIndex)
        {
            if (IsInLobby || networkManager.IsListening || networkManager.ShutdownInProgress) return;

            pendingProfile = profile;
            pendingMap = mapIndex;
            matchStarted = false;

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = Approve;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0"); // 0.0.0.0 = dışarıdan gelenleri de dinle

            if (!networkManager.StartHost())
            {
                JoinFailed?.Invoke("Lobi kurulamadı (port dolu olabilir).");
                return;
            }

            Address = useLoopbackAddress ? "127.0.0.1" : LocalAddress.GetLocalIPv4();
            var instance = Instantiate(statePrefab);
            instance.NetworkObject.Spawn(false); // false: sahne değişince silinmesin
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            bool full = networkManager.ConnectedClientsIds.Count >= LobbyRules.MaxPlayers;
            res.Approved = !full && !matchStarted;
            res.CreatePlayerObject = false;      // oyuncu objesi yok; masa oyunu, parçaları sonra spawn edeceğiz
            res.Reason = full ? "Lobi dolu." : (matchStarted ? "Oyun başladı." : "");
        }

        // ------------------------------------------------------------------ JOIN
        public void Join(string address, PlayerProfile profile)
        {
            if (IsInLobby || networkManager.IsListening || networkManager.ShutdownInProgress) return;

            if (!LocalAddress.TryParse(address, port, out string ip, out ushort p))
            {
                JoinFailed?.Invoke("Geçersiz IP adresi.");
                return;
            }

            pendingProfile = profile;
            matchStarted = false;
            Address = ip;
            transport.SetConnectionData(ip, p);

            if (!networkManager.StartClient())
            {
                JoinFailed?.Invoke("Bağlantı başlatılamadı.");
                return;
            }
            joinTimer = StartCoroutine(JoinTimeoutRoutine());
        }

        private IEnumerator JoinTimeoutRoutine()
        {
            yield return new WaitForSeconds(joinTimeout);
            joinTimer = null;
            if (state == null)
            {
                networkManager.Shutdown();
                JoinFailed?.Invoke("Host bulunamadı (IP / güvenlik duvarı?).");
            }
        }

        // ------------------------------------------------------------------ State doğunca
        private void OnStateSpawned(LobbyNetworkState s)
        {
            state = s;
            if (joinTimer != null) { StopCoroutine(joinTimer); joinTimer = null; }

            s.RosterChanged += ForwardRoster;
            s.MapChanged += ForwardMap;

            // Her iki taraf da gameplay sahnesinde Multiplayer modunu görsün (TurnCoordinator bunu okuyor)
            PlayerPrefs.SetString("SelectedGameMode", "Multiplayer");

            if (networkManager.IsServer)
                s.ServerInit(pendingProfile, pendingMap, MakeCode());
            else
                s.SubmitProfileRpc(pendingProfile.Name, pendingProfile.AvatarId);

            LobbyOpened?.Invoke();
        }

        private void ForwardRoster() => RosterChanged?.Invoke();
        private void ForwardMap() => MapChanged?.Invoke();

        private static string MakeCode() => "#" + Guid.NewGuid().ToString("N").Substring(0, 5);

        // ------------------------------------------------------------------ Ready / Start
        public void SetReady(bool ready)
        {
            if (state != null) state.SetReadyRpc(ready);
        }

        public void StartGame()
        {
            if (!IsHost || state == null || !state.AllReady()) return;

            matchStarted = true; // artık yeni biri giremez
            string scene = maps.Get(state.MapIndex).sceneName;
            networkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single); // herkes aynı sahneye geçer
        }

        // ------------------------------------------------------------------ Ayrılma / düşme
        public void Leave()
        {
            if (leaving || (!IsInLobby && !networkManager.IsListening)) return;
            StartCoroutine(LeaveRoutine());
        }

        private IEnumerator LeaveRoutine()
        {
            leaving = true;
            Teardown();
            while (networkManager.ShutdownInProgress || networkManager.IsListening) yield return null;
            leaving = false;
            LobbyClosed?.Invoke(LobbyCloseReason.LeftByUser, "");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (leaving) return;
            if (networkManager.IsServer) return;           // host tarafını LobbyNetworkState hallediyor (slot boşalır)
            if (clientId != networkManager.LocalClientId) return;

            string reason = networkManager.DisconnectReason;
            bool wasInLobby = state != null;
            Teardown();

            if (wasInLobby) LobbyClosed?.Invoke(LobbyCloseReason.HostLeft, reason);
            else JoinFailed?.Invoke(string.IsNullOrEmpty(reason) ? "Bağlanılamadı." : reason);
        }

        private void Teardown()
        {
            if (joinTimer != null) { StopCoroutine(joinTimer); joinTimer = null; }
            if (state != null)
            {
                state.RosterChanged -= ForwardRoster;
                state.MapChanged -= ForwardMap;
                state = null;
            }
            if (networkManager.IsListening) networkManager.Shutdown();
        }
    }
}
