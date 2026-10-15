using UnityEngine;
using System.Collections;
using Game.Presentation.UI; // HUDPresenter için
using Game.Application.UseCases; // PhysicsSettleDetector için
using Game.Presentation.Input; // PiecePlacementController için

namespace Game.Domain.Turns
{
    // Oyunun içindeki anlık durumları temsil eden numaralandırma (State Machine)
    public enum TurnState
    {
        Setup,              // Oyun başlıyor, ayarlar yapılıyor
        WaitingForSelection,// UI açık, oyuncunun 3 parçadan birini seçmesi bekleniyor
        PlacingPiece,       // Oyuncu parçayı seçti, fareyle yerleştiriyor
        Simulating,         // Parça bırakıldı, fizik motoru devrede (kule sallanıyor)
        Evaluating,         // Fizik durdu, kulenin çöküp çökmediği kontrol ediliyor
        GameOver            // Kule çöktü, oyun bitti
    }

    public class TurnCoordinator : MonoBehaviour
    {
        [Header("Current State")]
        public TurnState currentState = TurnState.Setup;
        public int currentTurnNumber = 1;

        [Header("References")]
        public HUDPresenter uiPresenter;
        public PiecePlacementController placementController;
        public PhysicsSettleDetector settleDetector;

        public CollapseEvaluator collapseEvaluator;
        
        // TODO: İleride CollapseEvaluator (Kule çöktü mü kontrolü) buraya eklenecek

        private void Start()
        {
            // Oyun başladığında gerekli bağlantıları kur
            if (settleDetector != null)
            {
                // SettleDetector "Fizik Durdu" dediğinde, bizim OnPhysicsSettled metodumuzu çalıştıracak
                settleDetector.OnPhysicsSettled += OnPhysicsSettled;
            }

            if (placementController != null)
            {
                // PlacementController "Parça Bırakıldı" dediğinde, bizim OnPieceDropped metodumuzu çalıştıracak
                placementController.OnPieceDropped += OnPieceDropped;
            }

            // İlk turu başlat
            StartTurn();
        }

        private void OnDestroy()
        {
            // Obje silinirse (sahne değişirse vb.) bağlantıları temizlemek (Memory leak önlemek için)
            if (settleDetector != null) settleDetector.OnPhysicsSettled -= OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped -= OnPieceDropped;
        }

        /// <summary>
        /// Yeni bir turu başlatır. (TurnStart)
        /// </summary>
        public void StartTurn()
        {
            if (currentState == TurnState.GameOver) return;

            Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI ---");
            currentState = TurnState.WaitingForSelection;

            // UI'a "Yeni 3 parça göster" emrini ver
            if (uiPresenter != null)
            {
                uiPresenter.ShowPieceSelection();
            }
        }

        /// <summary>
        /// HUDPresenter'da (UI) bir butona basıldığında çağrılır (HUDPresenter içinden manuel olarak bağlayacağız veya event ile tetikleyeceğiz).
        /// Ancak şu an HUDPresenter direkt PlacementController'a gönderiyor. 
        /// Biz sadece State'i güncelliyoruz.
        /// </summary>
        public void OnPieceSelectedFromUI()
        {
            if (currentState == TurnState.WaitingForSelection)
            {
                currentState = TurnState.PlacingPiece;
                Debug.Log("Durum: Parça Yerleştiriliyor...");
            }
        }

        /// <summary>
        /// PiecePlacementController, sol tıka basıp parçayı fırlattığında bunu çağırır.
        /// </summary>
        private void OnPieceDropped()
        {
            if (currentState == TurnState.PlacingPiece)
            {
                currentState = TurnState.Simulating;
                Debug.Log("Durum: Fizik Simülasyonu (Kule sallanıyor)...");
                
                // SettleDetector'a "Takibi başlat, kule durduğunda haber ver" diyoruz
                if (settleDetector != null)
                {
                    settleDetector.StartDetecting();
                }
            }
        }

        /// <summary>
        /// PhysicsSettleDetector, her şeyin durduğuna ikna olunca bu metodu tetikler.
        /// </summary>
        private void OnPhysicsSettled()
        {
            if (currentState == TurnState.Simulating)
            {
                currentState = TurnState.Evaluating;
                Debug.Log("Durum: Kule durdu. Çöküş kontrolü yapılıyor...");

                // ÇÖKÜŞ KONTROLÜ BURADA YAPILIYOR!
                if (collapseEvaluator != null && collapseEvaluator.HasTowerCollapsed)
                {
                    currentState = TurnState.GameOver;
                    Debug.Log("OYUN BİTTİ! Kule yıkıldı.");
                    
                    if (uiPresenter != null) 
                    {
                        uiPresenter.ShowGameOver(currentTurnNumber - 1); // Son tamamlanan turu gönder
                    }
                }
                else
                {
                    // Kule sağlamsa bir sonraki tura geç
                    StartCoroutine(EndTurnRoutine());
                }
            }
        }

        private IEnumerator EndTurnRoutine()
        {
            // Kule durduktan sonra oyuncuya durumu kavraması için kısa bir es (örneğin 1 saniye) veriyoruz
            yield return new WaitForSeconds(1f);

            currentTurnNumber++;
            StartTurn(); // Yeni 3 parça ile döngü tekrar başlıyor!
        }
    }
}