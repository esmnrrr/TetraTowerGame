using System;
using System.Collections.Generic;

namespace Game.Domain.Lobby
{
    /// <summary>Lobi kuralları (tek yerde dursun, sihirli sayı olmasın).</summary>
    public static class LobbyRules
    {
        public const int MaxPlayers = 2;
        public const int MaxNameLength = 16;
    }

    /// <summary>Oyuncunun kendi seçtiği kimlik: isim + avatar.</summary>
    public readonly struct PlayerProfile
    {
        public readonly string Name;
        public readonly int AvatarId;
        public PlayerProfile(string name, int avatarId) { Name = name; AvatarId = avatarId; }
    }

    /// <summary>Lobideki bir oyuncunun UI'ın okuyacağı salt-okunur görüntüsü (Netcode tipi içermez).</summary>
    public readonly struct LobbyPlayerInfo
    {
        public readonly ulong ClientId;
        public readonly string Name;
        public readonly int AvatarId;
        public readonly bool IsReady;
        public readonly bool IsHost;

        public LobbyPlayerInfo(ulong clientId, string name, int avatarId, bool isReady, bool isHost)
        {
            ClientId = clientId; Name = name; AvatarId = avatarId; IsReady = isReady; IsHost = isHost;
        }
    }

    public enum LobbyCloseReason { LeftByUser, HostLeft, ConnectionLost }

    /// <summary>
    /// UI'ın bildiği TEK ağ arayüzü. Altında Netcode (IP) da olabilir, yarın Relay da.
    /// UI katmanı Unity.Netcode'u hiç görmez.
    /// </summary>
    public interface ILobbyService
    {
        event Action LobbyOpened;                              // host kurdu / client girdi
        event Action<LobbyCloseReason, string> LobbyClosed;    // lobi kapandı -> ana menü
        event Action<string> JoinFailed;                       // bağlanılamadı
        event Action RosterChanged;                            // oyuncu listesi / ready değişti
        event Action MapChanged;

        bool IsHost { get; }
        bool IsInLobby { get; }
        ulong LocalClientId { get; }
        string LobbyCode { get; }
        void Join(string code, PlayerProfile profile);
        int MapIndex { get; }
        IReadOnlyList<LobbyPlayerInfo> Roster { get; }

        void Host(PlayerProfile profile, int mapIndex);
        //void Join(string address, PlayerProfile profile);
        void SetReady(bool ready);
        void StartGame();
        void Leave();
    }
}
