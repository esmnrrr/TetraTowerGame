using System;
using UnityEngine;

namespace Game.Domain.Lobby
{
    [Serializable]
    public class MapEntry
    {
        public string displayName = "Kitchen";
        public string sceneName = "Map_Kitchen";
        public Sprite icon;
        public Sprite preview;
        public bool unlocked = true;
    }

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