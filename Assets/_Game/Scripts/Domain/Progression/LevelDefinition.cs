using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Pieces;

namespace Game.Domain.Progression
{
    [CreateAssetMenu(fileName = "Level_01", menuName = "Tower Panic/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Level Identity")]
        [Tooltip("Bölüm sıra numarası (Örn: 1, 2, 3)")]
        public int levelIndex = 1;

        [Tooltip("UI'da görünecek bölüm adı (Örn: Kitchen - Level 1)")]
        public string levelName = "Level 1";

        [Tooltip("Hangi dünyaya ait olduğu")]
        public int worldNumber = 1;

        [Header("Piece Pool")]
        [Tooltip("Bu seviyede rasgele gelebilecek parça havuzu")]
        public List<PieceDefinition> allowedPieces = new List<PieceDefinition>();

        [Header("Objectives & Targets")]
        [Tooltip("Bölümü kazanmak için kuleye koyulması gereken toplam parça sayısı")]
        public int targetPieceCount = 10;

        [Header("Star Goals")]
        [Tooltip("2 Yıldız için maksimum kaç parça düşürülebilir?")]
        public int star2_MaxAllowedDrops = 1;

        [Tooltip("3 Yıldız için maksimum kaç parça düşürülebilir?")]
        public int star3_MaxAllowedDrops = 0;

        [Header("Unlock Requirements")]
        [Tooltip("Bu bölümün kilitli kalmaması için toplamda kaç yıldız gerekiyor?")]
        public int requiredTotalStarsToUnlock = 0;
    }
}