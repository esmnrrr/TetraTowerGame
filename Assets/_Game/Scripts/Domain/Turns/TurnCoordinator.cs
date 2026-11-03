using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Game.Presentation.UI;
using Game.Application.UseCases;
using Game.Presentation.Input;
using Game.Domain.Pieces;
using System.Collections.Generic;

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
                private string[] currentTurnPieceNames = new string[3];


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
                    // Sunucuya yeni bir oyuncu bağlandığında çalışacak olayı dinle
                    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

                    // Eğer Client zaten bağlanmışsa (örn. Lobi/Sahne geçişinde) maçı başlat
                    if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
                    {
                        SelectRandomStartingPlayerAndStart();
                    }
                }
                else
                {
                    // Client bağlandığı an Sunucudan mevcut durumu ve kartları ister
                    RequestCurrentTurnStateRpc();
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (currentMode == GameMode.Multiplayer)
            {
                ActivePlayerClientId.OnValueChanged -= OnMultiplayerTurnPlayerChanged;

                if (IsServer && NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                }
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;

            // Odada 2 oyuncu tamamlandığı an (Host + Client) rastgele seçimi yap ve maçı başlat!
            if (NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
            {
                SelectRandomStartingPlayerAndStart();
            }
        }

        private void SelectRandomStartingPlayerAndStart()
        {
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
            if (connectedClients.Count > 0)
            {
                // 2 oyuncu arasından (0: Host, 1: Client) %50 şansla rastgele ilk hamle yapanı seç
                int randomIndex = UnityEngine.Random.Range(0, connectedClients.Count);
                ulong startingPlayerId = connectedClients[randomIndex];

                ActivePlayerClientId.Value = startingPlayerId;
                Debug.Log($"[TurnCoordinator] 2 Oyuncu Bağlandı! Maça başlayan rastgele oyuncu ClientId: {startingPlayerId}");
            }

            StartTurn();
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
            if (currentMode == GameMode.Multiplayer)
            {
                if (IsServer)
                {
                    currentState = TurnState.WaitingForSelection;

                    // 1. Sunucu TEK BİR KEZ 3 rastgele parça belirler ve hafızaya kaydeder
                    if (uiPresenter != null && uiPresenter.selectionService != null)
                    {
                        var pieces = uiPresenter.selectionService.GetThreeRandomPieces();
                        if (pieces != null && pieces.Count >= 3)
                        {
                            currentTurnPieceNames[0] = pieces[0].name;
                            currentTurnPieceNames[1] = pieces[1].name;
                            currentTurnPieceNames[2] = pieces[2].name;
                        }
                    }

                    Debug.Log($"--- TUR {currentTurnNumber} BAŞLADI | SIRA PLAYER ID: {ActivePlayerClientId.Value} ---");

                    // 2. Hem Host hem Client için BİREBİR AYNI 3 kartı senkronize et
                    SyncTurnToClientsClientRpc(currentTurnNumber, ActivePlayerClientId.Value, currentTurnPieceNames[0], currentTurnPieceNames[1], currentTurnPieceNames[2]);
                }
                return;
            }

            // --- PRACTICE (SOLO) & VS AI AKIŞI ---
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
        private void SyncTurnToClientsClientRpc(int turnNumber, ulong activePlayerId, string p1, string p2, string p3)
        {
            currentTurnNumber = turnNumber;

            // İsmi verilen 3 parçayı projeden / hafızadan çek
            List<PieceDefinition> syncedPieces = new List<PieceDefinition>
            {
                GetPieceDefByName(p1),
                GetPieceDefByName(p2),
                GetPieceDefByName(p3)
            };

            // Hem Host hem Client ekranında BİREBİR AYNI 3 kartı göster
            if (uiPresenter != null)
            {
                uiPresenter.ShowSpecificPieceSelection(syncedPieces);
                uiPresenter.UpdateRoundText(turnNumber);

                // Tıklanabilirlik Durumu: Sıra bu bilgisayardaki oyuncuda mı?
                bool isMyTurn = (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == activePlayerId);
                uiPresenter.SetHumanInputInteractable(isMyTurn);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestCurrentTurnStateRpc()
        {
            // Yalnızca Sunucu (Host) yanıt verir ve hafızadaki 3 kart bilgisini istemciye gönderir
            if (IsServer && currentTurnPieceNames != null && currentTurnPieceNames.Length >= 3 && !string.IsNullOrEmpty(currentTurnPieceNames[0]))
            {
                SyncTurnToClientsClientRpc(
                    currentTurnNumber, 
                    ActivePlayerClientId.Value, 
                    currentTurnPieceNames[0], 
                    currentTurnPieceNames[1], 
                    currentTurnPieceNames[2]
                );
            }
        }

        private PieceDefinition GetPieceDefByName(string pieceName)
        {
            if (string.IsNullOrEmpty(pieceName)) return null;

            // 1. HUDPresenter üzerindeki mevcut parçalar listesinden ara
            if (uiPresenter != null && uiPresenter.selectionService != null && uiPresenter.selectionService.availablePieces != null)
            {
                foreach (var def in uiPresenter.selectionService.availablePieces)
                {
                    if (def != null && (def.name.Equals(pieceName, StringComparison.OrdinalIgnoreCase) || 
                                        def.DisplayName.Equals(pieceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return def;
                    }
                }
            }

            // 2. Bulunamadıysa Resources/Pieces/ klasöründen yükle
            PieceDefinition loaded = Resources.Load<PieceDefinition>($"Pieces/{pieceName}");
            if (loaded != null) return loaded;

            // 3. Klasördeki tüm parçaları tarayarak tam eşleşme sağla
            var allPieces = Resources.LoadAll<PieceDefinition>("Pieces");
            foreach (var def in allPieces)
            {
                if (def != null && (def.name.Equals(pieceName, StringComparison.OrdinalIgnoreCase) || 
                                    def.DisplayName.Equals(pieceName, StringComparison.OrdinalIgnoreCase)))
                {
                    return def;
                }
            }

            return null;
        }
        
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestSpawnPieceServerRpc(string pieceDefName, Vector3 position, Quaternion rotation)
        {
            PieceDefinition pieceDef = GetPieceDefByName(pieceDefName);

            if (pieceDef != null)
            {
                if (placementController != null && placementController.pieceFactory != null)
                {
                    // 1. Gerçek parçayı Sunucu üzerinde doğur
                    placementController.pieceFactory.CreateRealPiece(pieceDef, position, rotation);
                    Debug.Log($"[TurnCoordinator] {pieceDefName} başarıyla Sunucuda doğuruldu!");

                    // 2. KİLİT NOKTA: Sunucuda durum güncellemesi yapıp Fizik Sakinleşme Takibini başlat
                    if (currentState == TurnState.WaitingForSelection || currentState == TurnState.PlacingPiece)
                    {
                        currentState = TurnState.PlacingPiece;
                        BeginSimulation();
                    }
                }
            }
            else
            {
                Debug.LogError($"[TurnCoordinator] {pieceDefName} isimli PieceDefinition bulunamadı!");
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

                    if (currentMode == GameMode.Multiplayer)
                    {
                        // Sunucu, kuleyi düşüren kişinin ID'sini ağdaki tüm istemcilere bildirir
                        TriggerMultiplayerGameOverClientRpc(lastPlacerClientId);
                    }
                    else if (currentMode == GameMode.Practice && levelManager != null)
                    {
                        levelManager.EvaluateAndFinishLevel(isTowerCollapsed: true);
                    }
                    else if (uiPresenter != null)
                    {
                        bool isWinner = (lastPlacer != PlayerType.Human);
                        string winnerName = (lastPlacer == PlayerType.Human) ? "Yapay Zeka" : "Oyuncu 1";

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

        [ClientRpc]
        private void TriggerMultiplayerGameOverClientRpc(ulong loserClientId)
        {
            currentState = TurnState.GameOver;

            if (uiPresenter != null && NetworkManager.Singleton != null)
            {
                // Yerel oyuncu kuleyi düşüren kişi DEĞİLSE maçı kazanmıştır
                bool amIWinner = (NetworkManager.Singleton.LocalClientId != loserClientId);

                GameResultData resultData = new GameResultData
                {
                    Mode = GameMode.Multiplayer,
                    ScoreOrTurns = currentTurnNumber - 1,
                    IsTowerCollapsed = true,
                    IsWinner = amIWinner,
                    WinnerName = amIWinner ? "WIN!" : "LOSE!"
                };

                uiPresenter.DisplayGameOver(resultData);
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