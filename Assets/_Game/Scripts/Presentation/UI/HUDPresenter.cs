using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Domain.Pieces;
using Game.Domain.Turns;
using Game.Presentation.Input;
using UnityEngine.SceneManagement;
using Game.Presentation.UI.Panels;

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

        private void OnPieceSelected(PieceDefinition selectedPiece)
        {
            selectionPanel.SetActive(false);

            if (placementController != null) placementController.SelectPiece(selectedPiece);

            TurnCoordinator coordinator = UnityEngine.Object.FindAnyObjectByType<TurnCoordinator>();
            if (coordinator != null) coordinator.OnPieceSelectedFromUI();
        }

        public void UpdateRoundText(int currentTurn)
        {
            if (roundInfoText != null) roundInfoText.text = $"Turn {currentTurn}";
            if (roundJuice != null) roundJuice.PlayJuiceEffect();
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

        // --- PAUSE & NAVİGASYON METOTLARI ---
        public void PauseGame()
        {
            isPaused = true;
            if (pauseContainer != null) pauseContainer.SetActive(true);
            if (pauseMainCard != null) pauseMainCard.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            Time.timeScale = 0f;
        }

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
    }
}