using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Game.Domain.Turns;
using Game.Domain.Lobby;
using Game.Infrastructure.Net;

namespace Game.Presentation.UI.Panels
{
    public class MultiplayerResultPanel : BaseGameOverPanel
    {
        [Header("UI Text References")]
        public TMP_Text titleText;
        public TMP_Text scoreText;
        public TMP_Text rematchStatusText; // "Rakip bekleniyor..." uyarısı gösterilecek metin

        [Header("Buttons")]
        public Button rematchButton;
        public Button exitButton;   

        private bool hasVotedRematch = false;

        [Header("Oyuncu Bilgisi")]
        public PlayerBadgeView localBadge;      // kendi avatar + ismin
        public PlayerBadgeView opponentBadge;   // rakibin avatar + ismi
        public AvatarCatalog avatars;

        public override void ShowResult(GameResultData data)
        {
            gameObject.SetActive(true);
            hasVotedRematch = false;

            if (rematchStatusText != null) 
                rematchStatusText.text = "";

            // RESTART (Rematch) Buton Dinleyicisi
            if (rematchButton != null)
            {
                rematchButton.interactable = true;
                rematchButton.onClick.RemoveAllListeners();
                rematchButton.onClick.AddListener(OnRematchButtonClicked);
            }

            // EXIT (Çıkış) Buton Dinleyicisi
            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }

            // Kazandı / Kaybetti Başlığı
            if (titleText != null)
            {
                titleText.text = data.IsWinner ? "WIN!" : "LOSE!";
                titleText.color = data.IsWinner ? new Color32(46, 204, 113, 255) : new Color32(231, 76, 60, 255);
            }

            // Tur / Skor Metni
            if (scoreText != null)
            {
                scoreText.text = $"Score: {data.ScoreOrTurns}";
            }
            BindPlayers();
        }

        private void BindPlayers()
        {
            var lobby = FindAnyObjectByType<NetcodeLobbyService>();
            if (lobby == null || avatars == null) return;

            foreach (var p in lobby.Roster)
            {
                if (p.ClientId == lobby.LocalClientId) localBadge.Bind(p, avatars);
                else opponentBadge.Bind(p, avatars);
            }
        }

        private void OnRematchButtonClicked()
        {
            if (hasVotedRematch) return;
            hasVotedRematch = true;

            if (rematchButton != null) rematchButton.interactable = false;
            if (rematchStatusText != null) rematchStatusText.text = "SENDING...";

            // TurnCoordinator üzerinden ServerRpc tetikle
            var coordinator = FindAnyObjectByType<TurnCoordinator>();
            if (coordinator != null)
            {
                coordinator.RequestRematchServerRpc();
            }
        }

        private void OnExitButtonClicked()
        {
            // Ağ oturumunu temizce sonlandır ve Ana Menüye dön
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                Destroy(NetworkManager.Singleton.gameObject);
            }

            SceneManager.LoadScene("MainMenu");
        }

        public void UpdateRematchStatus(string statusMessage)
        {
            if (rematchStatusText != null)
            {
                rematchStatusText.text = statusMessage;
            }
        }
    }
}