using System;
using UnityEngine;

namespace Game.Domain.Lobby
{
    /// <summary>Sağ tık > Create > Game > Avatar Catalog. 8 avatar sprite'ını sırayla koy (sıra = AvatarId).</summary>
    [CreateAssetMenu(menuName = "Game/Avatar Catalog", fileName = "AvatarCatalog")]
    public class AvatarCatalog : ScriptableObject
    {
        [SerializeField] private Sprite[] avatars;

        public int Count => avatars != null ? avatars.Length : 0;

        public Sprite Get(int id)
        {
            if (Count == 0) return null;
            return avatars[Mathf.Clamp(id, 0, Count - 1)];
        }

        public int RandomId() => Count == 0 ? 0 : UnityEngine.Random.Range(0, Count);
    }

    [Serializable]
    public class MapEntry
    {
        public string displayName = "Kitchen";
        [Tooltip("Build Settings'teki sahne adı")] public string sceneName = "Map_Kitchen";
        public Sprite icon;
        public Sprite preview;
        public bool unlocked = true;
    }

    /// <summary>Sağ tık > Create > Game > Map Catalog. Şimdilik 1 açık (Kitchen), 3 kilitli girdi koy.</summary>
    [CreateAssetMenu(menuName = "Game/Map Catalog", fileName = "MapCatalog")]
    public class MapCatalog : ScriptableObject
    {
        [SerializeField] private MapEntry[] maps;

        public int Count => maps != null ? maps.Length : 0;

        public MapEntry Get(int index)
        {
            if (Count == 0) return null;
            return maps[Mathf.Clamp(index, 0, Count - 1)];
        }
    }
}
