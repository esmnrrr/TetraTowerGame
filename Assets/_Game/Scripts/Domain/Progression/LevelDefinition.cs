using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Pieces;

namespace Game.Domain.Progression
{
    public enum LevelGoalType
    {
        ReachPieceCount, // Belirli sayıda parçayı kuleye dizme
        ReachHeight,     // Belirli bir kule yüksekliğine ulaşma
        Survival         // Sınırlı düşüşle belirli tur hayatta kalma
    }

    [CreateAssetMenu(fileName = "Level_01", menuName = "Tower Panic/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string levelID = "level_world1_01";
        public string worldID = "world_1_classic";
        public int levelIndex = 1; // 1, 2, 3...
        public string displayName = "Bölüm 1 - İlk Denge";

        [Header("Board & Rules")]
        [Tooltip("Bu seviyede sahneye yüklenecek Denge Tahtası prefab'ı veya tanımı")]
        public GameObject boardPrefab;
        
        [Tooltip("Bu seviyede çıkabilecek parça havuzu")]
        public List<PieceDefinition> piecePool;

        [Header("Objectives")]
        public LevelGoalType goalType = LevelGoalType.ReachPieceCount;
        public int targetPieceCount = 10;
        public float targetHeight = 5f;

        [Header("Star Criteria")]
        [Tooltip("1 Yıldız (Tamamlama): Temel hedefi başarma")]
        public int star1TargetCount = 10;
        
        [Tooltip("2 Yıldız: En fazla bu kadar düşen parçayla bitirme")]
        public int maxAllowedDropsForStar2 = 1;
        
        [Tooltip("3 Yıldız: Parça hiç düşürmeden tamamlama")]
        public int maxAllowedDropsForStar3 = 0;
    }
}