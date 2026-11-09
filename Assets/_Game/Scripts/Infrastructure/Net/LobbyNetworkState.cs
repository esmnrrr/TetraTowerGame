using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Game.Domain.Lobby;

namespace Game.Infrastructure.Net
{
    /// <summary>
    /// Lobinin "ortak tahtası". Host tarafından spawn edilir; client'lar otomatik kopyasını alır.
    /// Sadece VERİ + doğrulama içerir. Bağlanma/ayrılma/sahne yükleme burada YOK (NetcodeLobbyService'te).
    /// Prefab olarak hazırlanır: boş obje + NetworkObject + bu script, NetworkPrefabsList'e eklenir.
    /// </summary>
    public class LobbyNetworkState : NetworkBehaviour
    {
        public static LobbyNetworkState Instance { get; private set; }
        public static event Action<LobbyNetworkState> Spawned;

        public event Action RosterChanged;
        public event Action MapChanged;

        private NetworkList<LobbyPlayerState> players;
        private readonly NetworkVariable<int> mapIndex = new NetworkVariable<int>(0);
        private readonly NetworkVariable<FixedString32Bytes> code = new NetworkVariable<FixedString32Bytes>();

        public int MapIndex => mapIndex.Value;
        public string Code => code.Value.ToString();

        private void Awake()
        {
            // NetworkList mutlaka Awake'te oluşturulmalı
            players = new NetworkList<LobbyPlayerState>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            players.OnListChanged += HandleListChanged;
            mapIndex.OnValueChanged += HandleMapChanged;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += ServerOnClientDisconnected;
            Spawned?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            players.OnListChanged -= HandleListChanged;
            mapIndex.OnValueChanged -= HandleMapChanged;
            if (IsServer && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= ServerOnClientDisconnected;
            if (Instance == this) Instance = null;
        }

        public override void OnDestroy()
        {
            players?.Dispose();
            base.OnDestroy();
        }

        private void HandleListChanged(NetworkListEvent<LobbyPlayerState> e) => RosterChanged?.Invoke();
        private void HandleMapChanged(int oldV, int newV) => MapChanged?.Invoke();

        // ------------------------------------------------------------ Okuma (herkes)
        public List<LobbyPlayerInfo> Snapshot()
        {
            var list = new List<LobbyPlayerInfo>(players.Count);
            foreach (var p in players)
                list.Add(new LobbyPlayerInfo(p.ClientId, p.Name.ToString(), p.AvatarId, p.IsReady, p.IsHost));
            return list;
        }

        public bool AllReady()
        {
            if (players.Count < LobbyRules.MaxPlayers) return false;
            foreach (var p in players) if (!p.IsReady) return false;
            return true;
        }

        // ------------------------------------------------------------ Server tarafı
        public void ServerInit(PlayerProfile hostProfile, int map, string lobbyCode)
        {
            if (!IsServer) return;
            code.Value = lobbyCode;
            mapIndex.Value = map;
            ServerAdd(NetworkManager.LocalClientId, hostProfile.Name, hostProfile.AvatarId, true);
        }

        private void ServerAdd(ulong clientId, string name, int avatarId, bool isHost)
        {
            if (players.Count >= LobbyRules.MaxPlayers) return;
            if (IndexOf(clientId) >= 0) return;

            name = (name ?? "").Trim();
            if (name.Length == 0) name = "Player";
            if (name.Length > LobbyRules.MaxNameLength) name = name.Substring(0, LobbyRules.MaxNameLength);

            players.Add(new LobbyPlayerState
            {
                ClientId = clientId,
                Name = name,
                AvatarId = Math.Max(0, avatarId),
                IsReady = false,
                IsHost = isHost
            });
        }

        private void ServerOnClientDisconnected(ulong clientId)
        {
            int i = IndexOf(clientId);
            if (i >= 0) players.RemoveAt(i);
        }

        private int IndexOf(ulong clientId)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].ClientId == clientId) return i;
            return -1;
        }

        // ------------------------------------------------------------ RPC'ler (client -> server)
        // Not: NGO 2.x yeni stil. Sürümün [Rpc] kabul etmezse [ServerRpc(RequireOwnership = false)] kullan.
        [Rpc(SendTo.Server)]
        public void SubmitProfileRpc(FixedString64Bytes name, int avatarId, RpcParams rpcParams = default)
        {
            ServerAdd(rpcParams.Receive.SenderClientId, name.ToString(), avatarId, false);
        }

        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            int i = IndexOf(rpcParams.Receive.SenderClientId);   // kimin yolladığını SERVER bilir, client'a güvenme
            if (i < 0) return;
            var p = players[i];
            p.IsReady = ready;
            players[i] = p;
        }
    }
}
