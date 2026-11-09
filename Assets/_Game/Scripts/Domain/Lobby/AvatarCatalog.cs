using UnityEngine;

namespace Game.Domain.Lobby
{
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

        public int RandomId() => Count == 0 ? 0 : Random.Range(0, Count);
    }
}