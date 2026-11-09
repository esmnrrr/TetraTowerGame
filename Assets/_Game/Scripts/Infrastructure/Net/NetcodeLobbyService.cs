using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Domain.Lobby;

namespace Game.Infrastructure.Net
{
    /// <summary>
    /// ILobbyService'in Unity Multiplayer Services (Session + Relay) uygulaması.
    /// "LobbyNetworkManager" objesinin üstünde durur.
    ///  - Host: Session oluşturur -> SDK Relay'i ayarlar ve NGO host'u otomatik başlatır -> 6 haneli kod (session.Code).
    ///  - Join: Kodla session'a katılır -> SDK NGO client'ı otomatik başlatır.
    /// Oyuncu listesi LobbyNetworkState'te (değişmedi), ekranlar Presentation'da.
    /// </summary>
    public class NetcodeLobbyService : MonoBehaviour, ILobbyService
    {
        [Header("Referanslar")]
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private LobbyNetworkState statePrefab;
        [SerializeField] private MapCatalog maps;

        [Header("Ayarlar")]
        [SerializeField] private float networkStartTimeout = 10f;
        [SerializeField] private float joinTimeout = 12f;

        public event Action LobbyOpened;
        public event Action<LobbyCloseReason, string> LobbyClosed;
        public event Action<string> JoinFailed;
        public event Action RosterChanged;
        public event Action MapChanged;

        private ISession session;
        private LobbyNetworkState state;
        private PlayerProfile pendingProfile;
        private int pendingMap;
        private string pendingCode = "";
        private bool busy;
        private bool leaving;
        private bool matchStarted;
        private Coroutine joinTimer;

        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsInLobby => state != null;
        public ulong LocalClientId => networkManager != null ? networkManager.LocalClientId : 0;
        public string LobbyCode => state != null ? state.Code : pendingCode;
        public int MapIndex => state != null ? state.MapIndex : pendingMap;
        public IReadOnlyList<LobbyPlayerInfo> Roster =>
            state != null ? state.Snapshot() : (IReadOnlyList<LobbyPlayerInfo>)Array.Empty<LobbyPlayerInfo>();

        // ------------------------------------------------------------------ Kurulum
        private void Awake()
        {
            if (networkManager == null) networkManager = GetComponent<NetworkManager>();

            // Host ve client AYNI ağ ayarıyla başlamalı
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = Approve;
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

        private void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            bool full = networkManager.ConnectedClientsIds.Count >= LobbyRules.MaxPlayers;
            res.Approved = !full && !matchStarted;
            res.CreatePlayerObject = false;
            res.Reason = full ? "Lobi dolu." : (matchStarted ? "Oyun başladı." : "");
        }

        // ------------------------------------------------------------------ Unity Services girişi
        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
#if UNITY_EDITOR
                // Multiplayer Play Mode: her pencere AYRI oyuncu kimliği alsın (yoksa ikisi aynı kişi sayılır)
                options.SetProfile(Guid.NewGuid().ToString("N").Substring(0, 20));
#endif
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // ------------------------------------------------------------------ HOST
        public void Host(PlayerProfile profile, int mapIndex) => _ = HostAsync(profile, mapIndex);

        private async Task HostAsync(PlayerProfile profile, int mapIndex)
        {
            if (busy || IsInLobby || networkManager.IsListening || networkManager.ShutdownInProgress) return;
            busy = true;
            pendingProfile = profile;
            pendingMap = mapIndex;
            matchStarted = false;

            try
            {
                await EnsureSignedInAsync();

                var options = new SessionOptions { MaxPlayers = LobbyRules.MaxPlayers }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);   // NGO host'u da başlatır
                pendingCode = session.Code;

                if (!await WaitUntilAsync(() => networkManager.IsHost, networkStartTimeout))
                    throw new TimeoutException("Host ağı başlamadı.");

                var instance = Instantiate(statePrefab);
                instance.NetworkObject.Spawn(false);        // sahne değişince silinmesin
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await ReleaseSessionAsync(wasHost: true);
                JoinFailed?.Invoke("Lobi kurulamadı. İnternet / Unity Services ayarlarını kontrol et.");
            }
            finally { busy = false; }
        }

        // ------------------------------------------------------------------ JOIN
        public void Join(string code, PlayerProfile profile) => _ = JoinAsync(code, profile);

        private async Task JoinAsync(string code, PlayerProfile profile)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { JoinFailed?.Invoke("Lobi kodunu gir."); return; }
            if (busy || IsInLobby || networkManager.IsListening || networkManager.ShutdownInProgress) return;

            busy = true;
            pendingProfile = profile;
            matchStarted = false;

            try
            {
                await EnsureSignedInAsync();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);   // NGO client'ı da başlatır
                joinTimer = StartCoroutine(JoinTimeoutRoutine());   // LobbyNetworkState gelene kadar bekle
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await ReleaseSessionAsync(wasHost: false);
                JoinFailed?.Invoke("Lobi bulunamadı, dolu ya da bağlantı kurulamadı.");
            }
            finally { busy = false; }
        }

        private IEnumerator JoinTimeoutRoutine()
        {
            yield return new WaitForSeconds(joinTimeout);
            joinTimer = null;
            if (state == null)
            {
                _ = ReleaseSessionAsync(wasHost: false);
                JoinFailed?.Invoke("Host'a bağlanılamadı.");
            }
        }

        // ------------------------------------------------------------------ State doğunca
        private void OnStateSpawned(LobbyNetworkState s)
        {
            state = s;
            if (joinTimer != null) { StopCoroutine(joinTimer); joinTimer = null; }

            s.RosterChanged += ForwardRoster;
            s.MapChanged += ForwardMap;

            PlayerPrefs.SetString("SelectedGameMode", "Multiplayer");

            if (networkManager.IsServer)
                s.ServerInit(pendingProfile, pendingMap, pendingCode);
            else
                s.SubmitProfileRpc(pendingProfile.Name, pendingProfile.AvatarId);

            LobbyOpened?.Invoke();
        }

        private void ForwardRoster() => RosterChanged?.Invoke();
        private void ForwardMap() => MapChanged?.Invoke();

        // ------------------------------------------------------------------ Ready / Start
        public void SetReady(bool ready)
        {
            if (state != null) state.SetReadyRpc(ready);
        }

        public void StartGame()
        {
            if (!IsHost || state == null || !state.AllReady()) return;

            matchStarted = true;
            string scene = maps.Get(state.MapIndex).sceneName;
            networkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        // ------------------------------------------------------------------ Ayrılma / düşme
        public void Leave() => _ = LeaveAsync(true);

        /// <summary>Oyun içi Exit butonu da bunu bekleyip ana menüye dönebilir.</summary>
        public async Task LeaveAsync(bool notify)
        {
            if (leaving) return;
            leaving = true;

            bool wasHost = IsHost;
            DetachState();
            await ReleaseSessionAsync(wasHost);

            if (networkManager.IsListening) networkManager.Shutdown();
            await WaitUntilAsync(() => !networkManager.IsListening && !networkManager.ShutdownInProgress, 5f);

            leaving = false;
            if (notify) LobbyClosed?.Invoke(LobbyCloseReason.LeftByUser, "");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (leaving) return;
            if (networkManager.IsServer) return;                 // host tarafını LobbyNetworkState hallediyor
            if (clientId != networkManager.LocalClientId) return;

            string reason = networkManager.DisconnectReason;
            bool wasInLobby = state != null;

            DetachState();
            _ = ReleaseSessionAsync(wasHost: false);
            if (networkManager.IsListening) networkManager.Shutdown();

            if (wasInLobby) LobbyClosed?.Invoke(LobbyCloseReason.HostLeft, reason);
            else JoinFailed?.Invoke(string.IsNullOrEmpty(reason) ? "Bağlanılamadı." : reason);
        }

        // ------------------------------------------------------------------ Yardımcılar
        private void DetachState()
        {
            if (joinTimer != null) { StopCoroutine(joinTimer); joinTimer = null; }
            if (state != null)
            {
                state.RosterChanged -= ForwardRoster;
                state.MapChanged -= ForwardMap;
                state = null;
            }
            pendingCode = "";
        }

        /// <summary>Host: session'ı SİL (yoksa host başkasına devredilir). Client: session'dan ayrıl.</summary>
        private async Task ReleaseSessionAsync(bool wasHost)
        {
            var s = session;
            session = null;
            if (s == null) return;

            try
            {
                if (wasHost && s.IsHost) await s.AsHost().DeleteAsync();
                else await s.LeaveAsync();
            }
            catch (Exception e) { Debug.LogWarning($"[Lobby] Session kapatılırken: {e.Message}"); }
        }

        private static async Task<bool> WaitUntilAsync(Func<bool> condition, float timeoutSeconds)
        {
            float t = 0f;
            while (!condition())
            {
                if (t >= timeoutSeconds) return false;
                await Task.Yield();
                t += Time.unscaledDeltaTime;
            }
            return true;
        }
    }
}