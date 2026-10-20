using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro kullanıyorsan bunu ekle
using Game.Domain.Pieces;
using Game.Domain.Turns; // PieceSelectionService'in bulunduğu namespace
using Game.Presentation.Input; // PiecePlacementController'ın bulunduğu namespace
using UnityEngine.SceneManagement;

namespace Game.Presentation.UI
{
    public class HUDPresenter : MonoBehaviour
    {
        [Header("Services & Controllers")]
        [Tooltip("Üçlü parçaları seçecek servis")]
        public PieceSelectionService selectionService;
        
        [Tooltip("Seçilen parçayı yerleştirecek controller")]
        public PiecePlacementController placementController;

        [Header("UI Elements")]
        [Tooltip("Butonların bulunduğu ana panel (Seçim yapıldıktan sonra gizlemek için)")]
        public GameObject selectionPanel;

        [Tooltip("3 adet seçilebilir UI butonu")]
        public Button[] pieceButtons = new Button[3];

        [Tooltip("Butonların içindeki parça isimlerini gösterecek text objeleri (Sırayla butonlara karşılık gelsin)")]
        public TMP_Text[] pieceNameTexts = new TMP_Text[3]; // Klasik Text ise TMP_Text yerine Text kullan

        [Tooltip("Butonların içindeki parça ikonlarını gösterecek image objeleri")]
        public Image[] pieceIconImages = new Image[3];

        [Header("Game Over UI")]
        public GameObject gameOverPanel;
        public TMP_Text scoreText; 

        [Header("In-Game HUD Elements")]
        public GameObject controlsHint; 
        public GameObject roundInfo;
        public TMP_Text roundInfoText;   

        public RoundJuiceEffect roundJuice;

        [Header("Pause UI Elements")]
        public GameObject pauseContainer;
        public GameObject settingsPanel;
        public GameObject pauseMainCard; // Pause menüsünün ana kartı

        private bool isPaused = false;

        // O an sunulan 3 parçayı hafızada tutmak için
        private List<PieceDefinition> currentOptions;

        private void Start()
        {
            // Oyun başlar başlamaz UI'ı test etmek için ilk seçim ekranını gösteriyoruz.
            // İleride bu TurnCoordinator tarafından çağrılacak!
            ShowPieceSelection();
        }

        private void Update()
        {
            // Oyuncu ESC tuşuna bastığında Duraklatma menüsünü aç/kapat
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        /// <summary>
        /// PieceSelectionService'ten 3 yeni parça alır ve UI'ı doldurur.
        /// </summary>
        public void ShowPieceSelection()
        {
            if (selectionService == null)
            {
                Debug.LogError("HUDPresenter: PieceSelectionService atanmamış!");
                return;
            }

            // 1. Servisten 3 rastgele parça iste
            currentOptions = selectionService.GetThreeRandomPieces();

            if (currentOptions == null || currentOptions.Count < 3)
            {
                Debug.LogWarning("HUDPresenter: Yeterli parça gelmedi!");
                return;
            }

            // 2. UI panelini görünür yap
            selectionPanel.SetActive(true);

            // 3. Her bir buton için UI bilgilerini güncelle ve tıklama olaylarını bağla
            for (int i = 0; i < 3; i++)
            {
                PieceDefinition pieceDef = currentOptions[i];
                
                // İsim ve ikonu güncelle
                if (pieceNameTexts[i] != null)
                    pieceNameTexts[i].text = pieceDef.DisplayName;
                
                if (pieceIconImages[i] != null && pieceDef.Icon != null)
                    pieceIconImages[i].sprite = pieceDef.Icon;

                // Eski buton dinleyicilerini temizle (üst üste binmemesi için)
                pieceButtons[i].onClick.RemoveAllListeners();

                // Butona tıklandığında ne olacağını belirle
                // Döngü değişkenini (i) lambda içinde doğrudan kullanmak hatalara yol açabilir, bu yüzden yerel kopya (index) alıyoruz.
                int index = i; 
                pieceButtons[index].onClick.AddListener(() => OnPieceSelected(currentOptions[index]));
            }
        }

        /// <summary>
        /// Kullanıcı 3 butondan birine tıkladığında çalışır.
        /// </summary>
        private void OnPieceSelected(PieceDefinition selectedPiece)
        {
            Debug.Log($"UI'dan parça seçildi: {selectedPiece.DisplayName}");

            // 1. Seçim UI'ını gizle (Artık oyuncu yerleştirmeye odaklanmalı)
            selectionPanel.SetActive(false);

            // 2. Seçilen parçayı PlacementController'a gönder ki farenin ucunda belirebilsin!
            if (placementController != null)
            {
                placementController.SelectPiece(selectedPiece);
            }
            else
            {
                Debug.LogError("HUDPresenter: PiecePlacementController atanmamış!");
            }

            // Orkestra şefine (TurnCoordinator) durumun değiştiğini bildir
            TurnCoordinator coordinator = UnityEngine.Object.FindAnyObjectByType<TurnCoordinator>();
            if (coordinator != null)
            {
                coordinator.OnPieceSelectedFromUI();
            }
        }

        // Tur Yazısını Güncelleyen Metod
        public void UpdateRoundText(int currentTurn)
        {
            if (roundInfoText != null)
            {
                roundInfoText.text = $"Turn {currentTurn}";
            }

            if (roundJuice != null)
            {
                roundJuice.PlayJuiceEffect();
            }
        }

        // Game Over ekranını gösteren metod
        public void ShowGameOver(int survivedTurns)
        {
            // Seçim menüsünü tamamen kapat
            if (selectionPanel != null) selectionPanel.SetActive(false);
            if (controlsHint != null) controlsHint.SetActive(false);
            if (roundInfo != null) roundInfo.gameObject.SetActive(false);
            
            // Game Over ekranını aç
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                if (scoreText != null)
                {
                    scoreText.text = $"Tur Skorun: {survivedTurns}";
                }
            }
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Oyunu Duraklatır
        public void PauseGame()
        {
            isPaused = true;
            if (pauseContainer != null) pauseContainer.SetActive(true);
            
            // Her duraklatıldığında ana menü kartı açık, ayarlar paneli kapalı başlasın!
            if (pauseMainCard != null) pauseMainCard.SetActive(true);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            Time.timeScale = 0f;
        }

        // Oyuna Devam Etmesini Sağlar
        public void ResumeGame()
        {
            isPaused = false;
            if (pauseContainer != null) pauseContainer.SetActive(false);

            Time.timeScale = 1f;
        }

        // Oyunu Sıfırlar (Restart Button OnClick)
        public void RestartGameFromPause()
        {
            // Sahneler arası veya restart atarken zamanın dondurulmuş kalmaması şarttır!
            Time.timeScale = 1f; 
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // Pause ekranındaki "Settings" butonuna basılınca çalışır
        public void OpenSettings()
        {
            if (pauseMainCard != null) pauseMainCard.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        // Settings ekranındaki "Back" butonuna basılınca çalışır
        public void CloseSettings()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pauseMainCard != null) pauseMainCard.SetActive(true);
        }

        // Ana Menüye Dönüş (Exit Button OnClick)
        public void ExitToMainMenu()
        {
            Time.timeScale = 1f;
            // İleride Ana Menü sahnen olunca onun indexini veya adını yazabilirsin:
            // SceneManager.LoadScene("MainMenu");
            Debug.Log("Ana Menüye Dönülüyor...");
        }
    }
}