using UnityEngine;
using System;
using System.Collections;
using Game.Presentation.UI;       
using Game.Application.UseCases;  
using Game.Presentation.Input;    

namespace Game.Domain.Turns
{
    public enum TurnState
    {
        Setup,              
        WaitingForSelection, 
        PlacingPiece,        
        Simulating,          
        Evaluating,          
        GameOver             
    }

    public enum GameMode
    {
        Practice,    // Solo (Pathway) Mod
        VsAI,        // Yapay Zekaya Karşı
        Multiplayer  // Çok Oyunculu
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

        public event Action OnAITurnRequested;
        private PlayerType lastPlacer = PlayerType.Human;

        private void Start()
        {
            string savedMode = PlayerPrefs.GetString("SelectedGameMode", "Practice");
            // Enum'u string'den güvenle parse ediyoruz (Multiplayer da eklendiği için)
            if (Enum.TryParse(savedMode, out GameMode parsedMode))
                currentMode = parsedMode;
            else
                currentMode = GameMode.Practice;
            
            if (settleDetector != null) settleDetector.OnPhysicsSettled += OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped += OnHumanPieceDropped;

            StartTurn();
        }

        private void OnDestroy()
        {
            if (settleDetector != null) settleDetector.OnPhysicsSettled -= OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped -= OnHumanPieceDropped;
        }

        public void StartTurn()
        {
            if (currentState == TurnState.GameOver) return;

            bool isAITurn = currentMode == GameMode.VsAI && currentPlayer == PlayerType.AI;

            Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI | MOD: {currentMode} | SIRA: {currentPlayer} ---");

            currentState = isAITurn ? TurnState.PlacingPiece : TurnState.WaitingForSelection;

            if (uiPresenter != null)
            {
                uiPresenter.UpdateRoundText(currentTurnNumber);
                uiPresenter.ShowPieceSelection();                       
                uiPresenter.SetHumanInputInteractable(!isAITurn);       
            }

            if (isAITurn)
            {
                OnAITurnRequested?.Invoke();
            }
        }

        public void OnPieceSelectedFromUI()
        {
            if (currentMode == GameMode.VsAI && currentPlayer != PlayerType.Human) return;

            if (currentState == TurnState.WaitingForSelection)
            {
                currentState = TurnState.PlacingPiece;
                Debug.Log("Durum: Parça Yerleştiriliyor...");
            }
        }

        private void OnHumanPieceDropped()
        {
            BeginSimulation();
        }

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
            if (currentState == TurnState.Simulating)
            {
                currentState = TurnState.Evaluating;
                Debug.Log("Durum: Fizik durdu. Çöküş ve Seviye kontrolü yapılıyor...");

                var levelManager = FindAnyObjectByType<Game.Application.UseCases.LevelManager>();

                // 1. DURUM: KULE ÇÖKTÜ MÜ?
                if (collapseEvaluator != null && collapseEvaluator.HasTowerCollapsed)
                {
                    currentState = TurnState.GameOver;
                    Debug.Log("OYUN BİTTİ! Kule yıkıldı.");

                    if (currentMode == GameMode.Practice && levelManager != null)
                    {
                        levelManager.EvaluateAndFinishLevel(isTowerCollapsed: true);
                    }
                    else if (uiPresenter != null) 
                    {
                        GameResultData resultData = new GameResultData
                        {
                            Mode = currentMode,
                            ScoreOrTurns = currentTurnNumber - 1,
                            IsTowerCollapsed = true,
                            IsWinner = (lastPlacer != PlayerType.Human),
                            WinnerName = (lastPlacer == PlayerType.Human) ? "Oyuncu 2 / AI" : "Oyuncu 1"
                        };

                        uiPresenter.DisplayGameOver(resultData);
                    }
                }
                else
                {
                    // 2. DURUM: KULE SAĞLAM - SAYACI ARTIR VE KONTROL ET
                    if (levelManager != null)
                    {
                        levelManager.RegisterPiecePlaced();
                        
                        // Güncel sayıyı doğrudan LevelManager'dan okuyoruz
                        int currentCount = levelManager.PlacedPieceCount;
                        int targetCount = levelManager.currentLevelDefinition != null ? levelManager.currentLevelDefinition.targetPieceCount : 1;

                        Debug.Log($"[TurnCoordinator] Güncel Parça Sayısı: {currentCount} / {targetCount}");

                        // HEDEF TAMAMLANDI MI? (Örn: 1 >= 1)
                        if (currentCount >= targetCount)
                        {
                            currentState = TurnState.GameOver; 
                            Debug.Log("SEVİYE BAŞARIYLA TAMAMLANDI!");
                            levelManager.EvaluateAndFinishLevel(isTowerCollapsed: false);
                            return; // Tur 2'ye geçmesini engeller
                        }
                    }

                    // Hedefe henüz ulaşılmadıysa bir sonraki tura geç
                    StartCoroutine(EndTurnRoutine());
                }
            }
        }

        private IEnumerator EndTurnRoutine()
        {
            yield return new WaitForSeconds(1f);

            currentTurnNumber++;

            currentPlayer = currentMode == GameMode.VsAI || currentMode == GameMode.Multiplayer
                ? (currentPlayer == PlayerType.Human ? PlayerType.AI : PlayerType.Human)
                : PlayerType.Human;

            StartTurn();
        }
    }
}