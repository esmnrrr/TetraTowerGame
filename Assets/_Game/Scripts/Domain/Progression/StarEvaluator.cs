using UnityEngine;

namespace Game.Domain.Progression
{
    public static class StarEvaluator
    {
        public static int EvaluateStars(LevelDefinition levelDef, int placedPieceCount, int droppedPieceCount)
        {
            int stars = 0;

            // 1. Yıldız: Temel Hedefe Ulaşıldı mı?
            bool goalReached = false;
            if (levelDef.goalType == LevelGoalType.ReachPieceCount)
            {
                goalReached = placedPieceCount >= levelDef.targetPieceCount;
            }

            if (!goalReached) return 0; // Hedef başaramadıysa 0 yıldız

            stars = 1; // Başarılı tamamlama

            // 2. Yıldız: Düşen parça sınırı kontrolü
            if (droppedPieceCount <= levelDef.maxAllowedDropsForStar2)
            {
                stars = 2;
            }

            // 3. Yıldız: Mükemmel performans (Hiç düşürmeme vb.)
            if (droppedPieceCount <= levelDef.maxAllowedDropsForStar3)
            {
                stars = 3;
            }

            return stars;
        }
    }
}