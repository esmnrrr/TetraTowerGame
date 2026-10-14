using UnityEngine;

namespace Game.Domain.Pieces
{
    [CreateAssetMenu(fileName = "New Piece Definition", menuName = "Game/Piece Definition")]
    public class PieceDefinition : ScriptableObject
    {
        [Header("Identity")] 
        [Tooltip("Bloklarin kodun icindeki ismi")]
        public string ID;

        [Tooltip("Bloklarin oyun icindeki ismi")]
        public string DisplayName;

        [Header("Visuals & Physics")]
        [Tooltip("Bloklarin oyun icindeki prefabi")]
        public GameObject Prefab;

        [Tooltip("Bloklarin agirligi")]
        public float Mass;

        [Tooltip("Bloklarin surtunme katsayisi")]
        public PhysicsMaterial PhysicsMaterial;

        [Header("UI")]
        [Tooltip("Seçim ekranında üçlü kartlarda gösterilecek 2D görsel")]
        public Sprite Icon;
    }
}
