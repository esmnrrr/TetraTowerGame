using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Game.Presentation.UI;
using Game.Application.UseCases;
using Game.Presentation.Input;

namespace Game.Domain.Turns
{
    public enum TurnState
    {
        Setup,              // Oyun başlatılıyor
        WaitingForSelection,// İlgili oyuncunun parça seçimi bekleniyor
        PlacingPiece,       // Parça seçildi, yerleştiriliyor
        Simulating,         // Parça bırakıldı, fizik çalışıyor (Kule sallanıyor)
        Evaluating,         // Fizik durdu, çöküş ve hedef kontrolü yapılıyor
        GameOver            // Oyun bitti
    }

    public enum GameMode
    {
        Practice,    // Solo (Pathway) Mod
        VsAI,        // Yapay Zekaya Karşı Mod
        Multiplayer  // Çok Oyunculu Mod
    }

    public enum PlayerType
    {
        Human,
        AI
    }

    /// <summary>
    /// Game State Machine ve tur orkestrasyonunu yöneten temel domain sınıfı.
    /// Practice (Pathway), VsAI ve Multiplayer modlarını tek bir çatı altında esnek ve güvenli şekilde yönetir.
    /// </summary>
    public class TurnCoordinator : NetworkBehaviour
    {
        [Header("Current State")]
        public TurnState currentState = TurnState.Setup;
        public int currentTurnNumber = 1;

        [Header("Game Configuration")]
        public GameMode currentMode = GameMode.Practice;
        public PlayerType currentPlayer = PlayerType.Human;

        [Header("Multiplayer Sync")]
        [Tooltip("Ağda sırası gelen oyuncunun ClientId'si (Yalnızca Server günceller)")]
        public NetworkVariable<ulong> ActivePlayerClientId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        [Header("References")]
        public HUDPresenter uiPresenter;
        public PiecePlacementController placementController;
        public PhysicsSettleDetector settleDetector;
        public CollapseEvaluator collapseEvaluator;

        // Observer Pattern: AI dinleyicileri için olaylar
        public event Action OnAITurnRequested;

        private PlayerType lastPlacer = PlayerType.Human;
        private ulong lastPlacerClientId = 0;

        private void Start()
        {
            string savedMode = PlayerPrefs.GetString("SelectedGameMode", "Practice");
            if (Enum.TryParse(savedMode, out GameMode parsedMode))
                currentMode = parsedMode;
            else
                currentMode = GameMode.Practice;

            if (settleDetector != null) settleDetector.OnPhysicsSettled += OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped += OnHumanPieceDropped;

            // Multiplayer dışındaki yerel modlarda (Pathway & VsAI) turu doğrudan başlatır
            if (currentMode != GameMode.Multiplayer)
            {
                StartTurn();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (currentMode == GameMode.Multiplayer)
            {
                ActivePlayerClientId.OnValueChanged += OnMultiplayerTurnPlayerChanged;

                if (IsServer)
                {
                    // Sunucu ilk oyuncuyu varsayılan olarak Host (0) seçer
                    ActivePlayerClientId.Value = NetworkManager.Singleton.ConnectedClientsIds[0];
                    StartTurn();
                }
                else
                {
                    UpdateUIForMultiplayerTurn(ActivePlayerClientId.Value);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (currentMode == GameMode.Multiplayer)
            {
                ActivePlayerClientId.OnValueChanged -= OnMultiplayerTurnPlayerChanged;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (settleDetector != null) settleDetector.OnPhysicsSettled -= OnPhysicsSettled;
            if (placementController != null) placementController.OnPieceDropped -= OnHumanPieceDropped;
        }

        public void StartTurn()
        {
            if (currentState == TurnState.GameOver) return;

            Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI | MOD: {currentMode} | SIRA: {currentPlayer} ---");

            // --- MULTIPLAYER AKIŞI ---
            // Tur başlatma ve parça seçimi kararlarını yalnızca Server (Host) alır
            if (IsServer)
            {
                currentState = TurnState.WaitingForSelection;
                Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI | SIRA PLAYER ID: {ActivePlayerClientId.Value} ---");

                // Tüm istemcilerin (Client) ekranındaki UI'ı güncellenmesi için ClientRpc çağrılır
                SyncTurnToClientsClientRpc(currentTurnNumber, ActivePlayerClientId.Value);
            }

            if (currentMode == GameMode.Multiplayer)
                {
                    currentState = TurnState.WaitingForSelection;

                    // DÜZELTME: Kartları sadece Server'da değil, HER İKİ EKRANDA DA (Host & Client) göster!
                    if (uiPresenter != null)
                    {
                        uiPresenter.ShowPieceSelection();
                    }

                    // Sıra kimdeyse onun kartlarını açar, diğerinikini kilitler
                    UpdateUIForMultiplayerTurn(ActivePlayerClientId.Value);
                    return;
                }

            // --- PRACTICE (PATHWAY) & VS AI AKIŞI ---
            bool isAITurn = (currentMode == GameMode.VsAI && currentPlayer == PlayerType.AI);
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
        
            [ClientRpc]
            private void SyncTurnToClientsClientRpc(int turnNumber, ulong activePlayerId)
            {
                currentTurnNumber = turnNumber;

                if (uiPresenter != null)
                {
                    // Hem Host hem Client kendi ekranında 3 kart seçeneğini yeniler
                    uiPresenter.ShowPieceSelection();
                    uiPresenter.UpdateRoundText(turnNumber);

                    // Sıra bu bilgisayardaki oyuncuda mı kontrol edilir
                    bool isMyTurn = (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == activePlayerId);
                    uiPresenter.SetHumanInputInteractable(isMyTurn);
                }
            }

        private void OnMultiplayerTurnPlayerChanged(ulong previousPlayerId, ulong newPlayerId)
        {
            Debug.Log($"[TurnCoordinator] Sıra Değişti -> Aktif Player ID: {newPlayerId}");
            UpdateUIForMultiplayerTurn(newPlayerId);
        }

        private void UpdateUIForMultiplayerTurn(ulong activePlayerId)
        {
            if (uiPresenter == null) return;

            bool isMyTurn = (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == activePlayerId);
            uiPresenter.UpdateRoundText(currentTurnNumber);
            uiPresenter.SetHumanInputInteractable(isMyTurn);
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
            if (NetworkManager.Singleton != null)
            {
                lastPlacerClientId = NetworkManager.Singleton.LocalClientId;
            }

            currentState = TurnState.Simulating;
            Debug.Log($"Durum: Fizik Simülasyonu (Kule sallanıyor)... Bırakan: {lastPlacer}");

            if (settleDetector != null) settleDetector.StartDetecting();
        }

        private void OnPhysicsSettled()
        {
            // Multiplayer modunda değerlendirme ve karar yetkisi yalnızca Sunucuya (Server) aittir
            if (currentMode == GameMode.Multiplayer && !IsServer) return;

            if (currentState == TurnState.Simulating)
            {
                currentState = TurnState.Evaluating;
                Debug.Log("Durum: Fizik durdu. Çöküş ve Seviye kontrolü yapılıyor...");

                var levelManager = FindAnyObjectByType<LevelManager>();

                // 1. KULE ÇÖKTÜ MÜ?
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
                        bool isWinner = false;
                        string winnerName = "";

                        if (currentMode == GameMode.Multiplayer)
                        {
                            var clientIds = NetworkManager.Singleton.ConnectedClientsIds;
                            ulong winnerId = (lastPlacerClientId == clientIds[0] && clientIds.Count > 1) ? clientIds[1] : clientIds[0];
                            isWinner = (NetworkManager.Singleton.LocalClientId == winnerId);
                            winnerName = (winnerId == 0) ? "Host (Oyuncu 1)" : "Client (Oyuncu 2)";
                        }
                        else if (currentMode == GameMode.VsAI)
                        {
                            isWinner = (lastPlacer != PlayerType.Human);
                            winnerName = (lastPlacer == PlayerType.Human) ? "Yapay Zeka" : "Oyuncu 1";
                        }

                        GameResultData resultData = new GameResultData
                        {
                            Mode = currentMode,
                            ScoreOrTurns = currentTurnNumber - 1,
                            IsTowerCollapsed = true,
                            IsWinner = isWinner,
                            WinnerName = winnerName
                        };

                        uiPresenter.DisplayGameOver(resultData);
                    }
                }
                else
                {
                    // 2. KULE SAĞLAM - PATHWAY SEVİYE KONTROLÜ
                    if (currentMode == GameMode.Practice && levelManager != null)
                    {
                        levelManager.RegisterPiecePlaced();
                        int currentCount = levelManager.PlacedPieceCount;
                        int targetCount = levelManager.currentLevelDefinition != null ? levelManager.currentLevelDefinition.targetPieceCount : 1;

                        if (currentCount >= targetCount)
                        {
                            currentState = TurnState.GameOver;
                            Debug.Log("SEVİYE BAŞARIYLA TAMAMLANDI!");
                            levelManager.EvaluateAndFinishLevel(isTowerCollapsed: false);
                            return;
                        }
                    }

                    StartCoroutine(EndTurnRoutine());
                }
            }
        }

        private IEnumerator EndTurnRoutine()
        {
            yield return new WaitForSeconds(1f);

            currentTurnNumber++;

            if (currentMode == GameMode.Multiplayer)
            {
                if (IsServer)
                {
                    var clientIds = NetworkManager.Singleton.ConnectedClientsIds;
                    if (clientIds.Count > 1)
                    {
                        ulong nextPlayer = (ActivePlayerClientId.Value == clientIds[0]) ? clientIds[1] : clientIds[0];
                        ActivePlayerClientId.Value = nextPlayer;
                    }
                }
            }
            else if (currentMode == GameMode.VsAI)
            {
                currentPlayer = (currentPlayer == PlayerType.Human) ? PlayerType.AI : PlayerType.Human;
            }
            else
            {
                currentPlayer = PlayerType.Human;
            }

            StartTurn();
        }
    }
}