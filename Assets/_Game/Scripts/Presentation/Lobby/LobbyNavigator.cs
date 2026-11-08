using UnityEngine;
using Game.Domain.Lobby;
using Game.Infrastructure.Net;
using Game.Presentation.UI;

namespace Game.Presentation.Lobby
{
    /// <summary>
    /// SADECE lobi ekranları arası geçişi yönetir (Setup paneli <-> Lobby paneli <-> ana menü).
    /// Ağ olaylarına tepki verir, iş mantığı içermez.
    /// </summary>
    public class LobbyNavigator : MonoBehaviour
    {
        [SerializeField] private NetcodeLobbyService serviceSource;   // LobbyNetworkManager objesi
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private GameObject setupPanel;               // MultiplayerSetupPanel
        [SerializeField] private GameObject lobbyPanel;               // OnlineLobbyPanel

        private ILobbyService service;

        private void Awake()
        {
            service = serviceSource;
            setupPanel.SetActive(false);
            lobbyPanel.SetActive(false);
        }

        private void OnEnable()
        {
            service.LobbyOpened += OnLobbyOpened;
            service.LobbyClosed += OnLobbyClosed;
        }

        private void OnDisable()
        {
            service.LobbyOpened -= OnLobbyOpened;
            service.LobbyClosed -= OnLobbyClosed;
        }

        /// <summary>ModeSelection'daki Multiplayer butonu çağırır.</summary>
        public void OpenSetup()
        {
            setupPanel.SetActive(true);
            lobbyPanel.SetActive(false);
        }

        /// <summary>Setup panelindeki Back butonu çağırır.</summary>
        public void BackFromSetup()
        {
            setupPanel.SetActive(false);
            mainMenu.OpenModeSelection();
        }

        private void OnLobbyOpened()
        {
            setupPanel.SetActive(false);
            lobbyPanel.SetActive(true);
        }

        private void OnLobbyClosed(LobbyCloseReason reason, string message)
        {
            setupPanel.SetActive(false);
            lobbyPanel.SetActive(false);
            mainMenu.BackToMainMenu();
            if (reason != LobbyCloseReason.LeftByUser)
                Debug.Log($"[Lobby] Lobi kapandı: {reason} {message}");   // istersen buraya toast/uyarı UI'ı bağla
        }
    }
}
