using UnityEngine;
using System;
using System.Collections;
using Game.Presentation.UI;       // HUDPresenter için
using Game.Application.UseCases;  // PhysicsSettleDetector, CollapseEvaluator için
using Game.Presentation.Input;    // PiecePlacementController için

namespace Game.Domain.Turns
{
    // Oyunun içindeki anlık durumları temsil eden numaralandırma (State Machine)
    public enum TurnState
    {
        Setup,               // Oyun başlıyor
        WaitingForSelection, // İNSAN: UI açık, 3 parçadan birini seçmesi bekleniyor
        PlacingPiece,        // Parça seçildi, yerleştiriliyor (insan fareyle, AI kendi koluyla)
        Simulating,          // Parça bırakıldı, fizik çalışıyor
        Evaluating,          // Fizik durdu, çöküş kontrol ediliyor
        GameOver             // Kule çöktü
    }

    public enum GameMode
    {
        Practice, // Tek kişilik endless mod (sıra hep insanda)
        VsAI      // Yapay zekaya karşı
    }

    public enum PlayerType
    {
        Human,
        AI
    }

    public class TurnCoordinator : MonoBehaviour
    {
        [Header("Current State")]
        public TurnState currentState = TurnState.Setup;
        public int currentTurnNumber = 1;

        [Header("Game Configuration")]
        public GameMode currentMode = GameMode.Practice;
        public PlayerType currentPlayer = PlayerType.Human;

        [Header("References")]
        public HUDPresenter uiPresenter;
        public PiecePlacementController placementController;
        public PhysicsSettleDetector settleDetector;
        public CollapseEvaluator collapseEvaluator;

        /// <summary>
        /// AI sırası geldiğinde yayınlanır. TurnCoordinator AI'ı tanımaz; AIBot bu olaya abone olur.
        /// </summary>
        public event Action OnAITurnRequested;

        // Parçayı en son bırakan oyuncu: kule çökerse kaybeden odur.
        private PlayerType lastPlacer = PlayerType.Human;

        private void Start()
        {
            if (settleDetector != null) settleDetector.OnPhysicsSettled += OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped += OnHumanPieceDropped;

            StartTurn();
        }

        private void OnDestroy()
        {
            if (settleDetector != null) settleDetector.OnPhysicsSettled -= OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped -= OnHumanPieceDropped;
        }

        /// <summary>Yeni bir turu başlatır.</summary>
        public void StartTurn()
        {
            if (currentState == TurnState.GameOver) return;

            bool isAITurn = currentMode == GameMode.VsAI && currentPlayer == PlayerType.AI;

            Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI | MOD: {currentMode} | SIRA: {currentPlayer} ---");

            // AI kendi parçasını kendisi yerleştirir, o yüzden doğrudan PlacingPiece'te başlar.
            currentState = isAITurn ? TurnState.PlacingPiece : TurnState.WaitingForSelection;

            if (uiPresenter != null)
            {
                uiPresenter.UpdateRoundText(currentTurnNumber);
                uiPresenter.ShowPieceSelection();                       // 3 yeni parça üretir
                uiPresenter.SetHumanInputInteractable(!isAITurn);       // AI sırasında insan kartlara basamaz
            }

            if (isAITurn)
            {
                OnAITurnRequested?.Invoke();
            }
        }

        /// <summary>HUDPresenter'da insan bir kart seçtiğinde çağrılır.</summary>
        public void OnPieceSelectedFromUI()
        {
            // AI sırasında UI'dan gelen çağrıları yok say
            if (currentMode == GameMode.VsAI && currentPlayer != PlayerType.Human) return;

            if (currentState == TurnState.WaitingForSelection)
            {
                currentState = TurnState.PlacingPiece;
                Debug.Log("Durum: Parça Yerleştiriliyor...");
            }
        }

        /// <summary>İnsanın parça bırakma olayı. Tek giriş noktasına yönlendirir.</summary>
        private void OnHumanPieceDropped()
        {
            BeginSimulation();
        }

        /// <summary>
        /// İNSAN VE AI AYNI NOKTADAN GEÇER. Parça bırakıldığında çağrılır:
        /// durumu Simulating yapar ve fizik sakinleşme takibini başlatır.
        /// </summary>
        public void BeginSimulation()
        {
            if (currentState != TurnState.PlacingPiece) return;

            lastPlacer = currentPlayer;
            currentState = TurnState.Simulating;
            Debug.Log($"Durum: Fizik Simülasyonu (Kule sallanıyor)... Bırakan: {lastPlacer}");

            if (settleDetector != null) settleDetector.StartDetecting();
        }

        private void OnPhysicsSettled()
        {
            if (currentState != TurnState.Simulating) return;

            currentState = TurnState.Evaluating;
            Debug.Log("Durum: Kule durdu. Çöküş kontrolü yapılıyor...");

            if (collapseEvaluator != null && collapseEvaluator.HasTowerCollapsed)
            {
                currentState = TurnState.GameOver;
                Debug.Log($"OYUN BİTTİ! Kule yıkıldı. Kaybeden: {lastPlacer}");

                if (uiPresenter != null)
                {
                    if (currentMode == GameMode.VsAI)
                        uiPresenter.ShowVersusGameOver(lastPlacer == PlayerType.AI, currentTurnNumber);
                    else
                        uiPresenter.ShowGameOver(currentTurnNumber - 1);
                }
            }
            else
            {
                StartCoroutine(EndTurnRoutine());
            }
        }

        private IEnumerator EndTurnRoutine()
        {
            yield return new WaitForSeconds(1f);

            currentTurnNumber++;

            // Sadece VsAI modunda sıra el değiştirir
            currentPlayer = currentMode == GameMode.VsAI
                ? (currentPlayer == PlayerType.Human ? PlayerType.AI : PlayerType.Human)
                : PlayerType.Human;

            StartTurn();
        }
    }
}