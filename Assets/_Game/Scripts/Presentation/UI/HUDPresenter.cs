using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro kullanıyorsan bunu ekle (Eğer klasik Text kullanıyorsan UnityEngine.UI yeterlidir)
using Game.Domain.Pieces; // Kendi namespace'ine göre güncelle
using Game.Domain.Turns; // PieceSelectionService'in bulunduğu namespace
using Game.Presentation.Input; // PiecePlacementController'ın bulunduğu namespace

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

        // O an sunulan 3 parçayı hafızada tutmak için
        private List<PieceDefinition> currentOptions;

        private void Start()
        {
            // Oyun başlar başlamaz UI'ı test etmek için ilk seçim ekranını gösteriyoruz.
            // İleride bu TurnCoordinator tarafından çağrılacak!
            ShowPieceSelection();
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
    }
}