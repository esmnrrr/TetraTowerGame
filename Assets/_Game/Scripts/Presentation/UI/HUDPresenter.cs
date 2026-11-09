using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Domain.Pieces;
using Game.Domain.Turns;
using Game.Presentation.Input;
using UnityEngine.SceneManagement;
using Game.Presentation.UI.Panels;
using Unity.Netcode;

namespace Game.Presentation.UI
{
    public class HUDPresenter : MonoBehaviour
    {
        [Header("Services & Controllers")]
        public PieceSelectionService selectionService;        
        public PiecePlacementController placementController;

        [Header("Selection UI Elements")]
        public GameObject selectionPanel;
        public Button[] pieceButtons = new Button[3];
        public TMP_Text[] pieceNameTexts = new TMP_Text[3]; 
        public Image[] pieceIconImages = new Image[3];

        [Header("In-Game HUD Elements")]
        public GameObject controlsHint; 
        public GameObject roundInfo;
        public TMP_Text roundInfoText;   
        public RoundJuiceEffect roundJuice;

        [Header("Pause UI Elements")]
        public GameObject pauseContainer;
        public GameObject settingsPanel;
        public GameObject pauseMainCard;
        public Button pauseRestartButton;
        public Button pauseSurrenderButton;

        private bool isPaused = false;
        private List<PieceDefinition> currentOptions;

        [Header("Input Control")]
        public CanvasGroup selectionCanvasGroup;
        public SelectionHighlightController highlightController;

        public IReadOnlyList<PieceDefinition> CurrentOptions => currentOptions;

        [Header("Game Over Strategy Panels (Strateji Panelleri)")]
        [Tooltip("Sahnede üzerine ilgili scriptler takılmış olan Container objelerini buraya sürükle")]
        public PathwayResultPanel pathwayPanel;
        public VsAIResultPanel vsAIPanel;
        public MultiplayerResultPanel multiplayerPanel;
        
        public AudioClip gameOverSound;

        [Header("Settings")]
        public Button settingsBackButton;      

        private void Awake()
        {
            if (settingsBackButton != null)
                settingsBackButton.onClick.AddListener(CloseSettings);
        } 

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused) ResumeGame();
                else PauseGame();
            }
        }

        public void SetHumanInputInteractable(bool isInteractable)
        {
            if (selectionCanvasGroup != null)
            {
                selectionCanvasGroup.interactable = isInteractable;
                selectionCanvasGroup.blocksRaycasts = isInteractable;
            }
            else
            {
                foreach (var btn in pieceButtons)
                    if (btn != null) btn.interactable = isInteractable;
            }
        }

        public void HighlightCardForAI(int cardIndex)
        {
            if (highlightController != null) highlightController.SelectCard(cardIndex);
        }

        public void ShowPieceSelection()
        {
            if (selectionService == null)
            {
                Debug.LogError("HUDPresenter: PieceSelectionService atanmamış!");
                return;
            }

            SetHumanInputInteractable(true);
            currentOptions = selectionService.GetThreeRandomPieces();

            if (currentOptions == null || currentOptions.Count < 3) return;

            selectionPanel.SetActive(true);

            for (int i = 0; i < 3; i++)
            {
                PieceDefinition pieceDef = currentOptions[i];
                
                if (pieceNameTexts[i] != null) pieceNameTexts[i].text = pieceDef.DisplayName;
                if (pieceIconImages[i] != null && pieceDef.Icon != null) pieceIconImages[i].sprite = pieceDef.Icon;

                pieceButtons[i].onClick.RemoveAllListeners();
                int index = i; 
                pieceButtons[index].onClick.AddListener(() => OnPieceSelected(currentOptions[index]));
            }
        }

        /// <summary>
        /// Sunucudan (Host) gelen veya dışarıdan verilen spesifik 3 parçayı UI kartlarına basar.
        /// Solo / Vs AI modlarını etkilemez, Multiplayer senkronizasyonu için kullanılır.
        /// </summary>
        public void ShowSpecificPieceSelection(List<PieceDefinition> pieces)
        {
            if (pieces == null || pieces.Count < 3) return;

            SetHumanInputInteractable(true);
            currentOptions = pieces;

            if (selectionPanel != null) selectionPanel.SetActive(true);

            for (int i = 0; i < 3; i++)
            {
                PieceDefinition pieceDef = currentOptions[i];

                if (pieceNameTexts[i] != null && pieceDef != null)
                    pieceNameTexts[i].text = pieceDef.DisplayName;

                if (pieceIconImages[i] != null && pieceDef != null && pieceDef.Icon != null)
                    pieceIconImages[i].sprite = pieceDef.Icon;

                if (pieceButtons[i] != null)
                {
                    pieceButtons[i].onClick.RemoveAllListeners();
                    int index = i;
                    pieceButtons[index].onClick.AddListener(() => OnPieceSelected(currentOptions[index]));
                }
            }
        }

        private void OnPieceSelected(PieceDefinition selectedPiece)
        {
            selectionPanel.SetActive(false);

            if (placementController != null) placementController.SelectPiece(selectedPiece);

            TurnCoordinator coordinator = UnityEngine.Object.FindAnyObjectByType<TurnCoordinator>();
            if (coordinator != null) coordinator.OnPieceSelectedFromUI();
        }

        public void UpdateRoundText(int currentTurn)
        {
            // 1. Oyun bittiğinde kapanmış olabilecek roundInfo objesini tekrar aktif yap
            if (roundInfo != null)
            {
                roundInfo.SetActive(true);
            }

            if (roundInfoText != null)
            {
                roundInfoText.text = $"Turn {currentTurn}";
            }

            // 2. Yalnızca obje sahnede aktifse Juice efektini başlat (Coroutine hatasını önler)
            if (roundJuice != null && roundJuice.gameObject.activeInHierarchy)
            {
                roundJuice.PlayJuiceEffect();
            }
        }

        /// <summary>
        /// Oyun bittiğinde TurnCoordinator burayı çağırır. Mod tipine göre doğru strateji panelini açar.
        /// </summary>
        public void DisplayGameOver(GameResultData resultData)
        {
            // 1. Oyun içi UI öğelerini gizle
            if (selectionPanel != null) selectionPanel.SetActive(false);
            if (controlsHint != null) controlsHint.SetActive(false);
            if (roundInfo != null) roundInfo.SetActive(false);

            // 2. Game Over Sesini Çal
            AudioSource audio = GetComponent<AudioSource>();
            if (audio != null && gameOverSound != null) audio.PlayOneShot(gameOverSound);

            // 3. Mevcut panelleri kapat
            if (pathwayPanel != null) pathwayPanel.HidePanel();
            if (vsAIPanel != null) vsAIPanel.HidePanel();
            if (multiplayerPanel != null) multiplayerPanel.HidePanel();

            // 4. Strateji Paneline Yönlendir
            switch (resultData.Mode)
            {
                case GameMode.Practice:
                    if (pathwayPanel != null) pathwayPanel.ShowResult(resultData);
                    break;

                case GameMode.VsAI:
                    if (vsAIPanel != null) vsAIPanel.ShowResult(resultData);
                    break;

                case GameMode.Multiplayer:
                    if (multiplayerPanel != null) multiplayerPanel.ShowResult(resultData);
                    break;
            }
        }

        /// <summary>
        /// Pause menüsündeki "Pes Et / Çık" butonuna bağlanacak metot.
        /// </summary>
        public void SurrenderMultiplayerMatch()
        {
            Time.timeScale = 1f;

            if (isPaused) 
            {
                isPaused = false;
                if (pauseContainer != null) pauseContainer.SetActive(false);
            }

            // Ağ oturumunu temizce kapat
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // Ana menüye güvenli dönüş yap
            SceneManager.LoadScene("MainMenu");
        }

        // --- PAUSE & NAVİGASYON METOTLARI ---
        public void PauseGame()
        {
            isPaused = true;
            if (pauseContainer != null) pauseContainer.SetActive(true);
            if (pauseMainCard != null) pauseMainCard.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            TurnCoordinator coordinator = UnityEngine.Object.FindAnyObjectByType<TurnCoordinator>();
            bool isMultiplayer = (coordinator != null && coordinator.currentMode == GameMode.Multiplayer);

            if (isMultiplayer)
            {
                // 1. Multiplayer'da zaman akmaya devam eder
                Time.timeScale = 1f;

                // 2. Solo Restart butonunu gizle, Pes Et butonunu göster
                if (pauseRestartButton != null) pauseRestartButton.gameObject.SetActive(false);
                if (pauseSurrenderButton != null) pauseSurrenderButton.gameObject.SetActive(true);
            }
            else
            {
                // Solo modlarda zamanı dondur, Restart'ı göster, Pes Et'i gizle
                Time.timeScale = 0f;
                if (pauseRestartButton != null) pauseRestartButton.gameObject.SetActive(true);
                if (pauseSurrenderButton != null) pauseSurrenderButton.gameObject.SetActive(false);
            }
        }

        // Oyuna Devam Et (Resume)
        public void ResumeGame()
        {
            isPaused = false;
            if (pauseContainer != null) pauseContainer.SetActive(false);
            Time.timeScale = 1f;
        }

        public void OpenSettings()
        {
            if (pauseMainCard != null) pauseMainCard.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        public void CloseSettings()
        {
            Debug.Log($"[HUD] CloseSettings çağrıldı. settingsPanel={settingsPanel}, pauseMainCard={pauseMainCard}");
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pauseMainCard != null) pauseMainCard.SetActive(true);
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMap()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        // Pause menüsündeki "EXIT" Butonuna Bağlanacak Metot (Pes Et / Çık)
        public void ExitToMainMenu()
        {
            Time.timeScale = 1f;

            if (isPaused)
            {
                isPaused = false;
                if (pauseContainer != null) pauseContainer.SetActive(false);
            }

            // Eğer Multiplayer modundaysak ağ oturumunu kapat (Rakip hükmen kazanır)
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SceneManager.LoadScene("MainMenu");
        }
    }
}