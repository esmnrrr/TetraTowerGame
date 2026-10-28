using UnityEngine;
using Game.Domain.Turns;

namespace Game.Application.UseCases
{
    public class CollapseEvaluator : MonoBehaviour
    {
        [Header("Practice Mode Settings")]
        [Tooltip("Practice/Pathway modunda kulesinin çökmesi için kaç parçanın düşmesi gerektiği")]
        public int practiceAllowedDrops = 2;

        private int currentDrops = 0;
        public bool HasTowerCollapsed { get; private set; } = false;

        private void Start()
        {
            ResetEvaluator();
        }

        /// <summary>
        /// Yeni maç başladığında sayacı ve durumu sıfırlar.
        /// </summary>
        public void ResetEvaluator()
        {
            currentDrops = 0;
            HasTowerCollapsed = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Piece"))
            {
                currentDrops++;
                Debug.Log($"[KillZone] Bir parça düştü! Toplam düşen: {currentDrops}");

                // 1. LevelManager'a düşen parçayı bildir (Yıldız hesabı için)
                var levelManager = FindAnyObjectByType<LevelManager>();
                if (levelManager != null)
                {
                    levelManager.RegisterPieceDropped();
                }

                // 2. Aktif Oyun Modunu TurnCoordinator'dan Öğren
                var coordinator = FindAnyObjectByType<TurnCoordinator>();
                GameMode currentMode = coordinator != null ? coordinator.currentMode : GameMode.Practice;

                // 3. MODA GÖRE ÇÖKÜŞ KONTROLÜ
                if (currentMode == GameMode.VsAI || currentMode == GameMode.Multiplayer)
                {
                    // VS AI ve Multiplayer modunda 1 TEK PARÇA bile düşerse KULE ANINDA ÇÖKER!
                    HasTowerCollapsed = true;
                    Debug.Log($"[KillZone] {currentMode} modunda parça düştü! OYUN BİTTİ.");
                }
                else
                {
                    // Practice / Pathway modunda belirlenen düşüş toleransı geçerlidir
                    if (currentDrops > practiceAllowedDrops)
                    {
                        HasTowerCollapsed = true;
                        Debug.Log($"[KillZone] Practice sınır aşıldı! Düşen: {currentDrops} > İzin Verilen: {practiceAllowedDrops}");
                    }
                }
            }
        }
    }
}