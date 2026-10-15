using System.Collections.Generic;
using UnityEngine;
using Game.Domain.Pieces; // PieceDefinition için 

namespace Game.Domain.Turns
{
    public class PieceSelectionService : MonoBehaviour
    {
        [Header("Piece Pool")]
        [Tooltip("Oyunda çıkabilecek tüm parçaların listesi (Inspector'dan atanacak)")]
        public List<PieceDefinition> availablePieces = new List<PieceDefinition>();

        /// <summary>
        /// Havuzdan rastgele 3 parça seçer. 
        /// Tasarım belgesi kuralı: Mümkünse aynı parçanın 3 kopyası gelmesin.
        /// Biz burada oyuncuya daha iyi strateji sunmak için 3 FARKLI parça seçeceğiz.
        /// </summary>
        public List<PieceDefinition> GetThreeRandomPieces()
        {
            List<PieceDefinition> selectedPieces = new List<PieceDefinition>();

            // Havuzda yeterli parça yoksa hata ver
            if (availablePieces == null || availablePieces.Count < 3)
            {
                Debug.LogError("PieceSelectionService: Havuzda en az 3 parça olmalı!");
                return selectedPieces;
            }

            // Orijinal listeyi bozmamak için geçici bir kopyasını oluşturuyoruz
            List<PieceDefinition> tempPool = new List<PieceDefinition>(availablePieces);

            // 3 kez rastgele parça seç
            for (int i = 0; i < 3; i++)
            {
                int randomIndex = Random.Range(0, tempPool.Count);
                selectedPieces.Add(tempPool[randomIndex]);

                // Seçilen parçayı geçici havuzdan çıkar ki bir sonraki seçimde AYNISI gelmesin
                tempPool.RemoveAt(randomIndex);
            }

            return selectedPieces;
        }
    }
}