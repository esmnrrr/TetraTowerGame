using System;
using Unity.Collections;
using Unity.Netcode;

namespace Game.Infrastructure.Net
{
    /// <summary>Ağda taşınan oyuncu kaydı (NetworkList içinde durur).</summary>
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public int AvatarId;
        public bool IsReady;
        public bool IsHost;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref AvatarId);
            serializer.SerializeValue(ref IsReady);
            serializer.SerializeValue(ref IsHost);
        }

        public bool Equals(LobbyPlayerState o) =>
            ClientId == o.ClientId && Name.Equals(o.Name) && AvatarId == o.AvatarId &&
            IsReady == o.IsReady && IsHost == o.IsHost;
    }
}
