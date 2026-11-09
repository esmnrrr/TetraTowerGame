using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;
using Game.Infrastructure.Net;

namespace Game.Presentation.Lobby
{
    /// <summary>
    /// MultiplayerSetupPanel'in beyni: Host/Join sekmeleri, "Create Lobby" ve "Join Lobby" butonları.
    /// Ekranı değiştirmez (onu LobbyNavigator yapar); sadece servise "kur" / "katıl" der.
    /// </summary>
    public class MultiplayerSetupPresenter : MonoBehaviour
    {
        [SerializeField] private NetcodeLobbyService serviceSource;

        [Header("Sekmeler")]
        [SerializeField] private Button hostTabButton;
        [SerializeField] private Button joinTabButton;
        [SerializeField] private GameObject hostContentPanel;
        [SerializeField] private GameObject joinContentPanel;

        [Header("Host sekmesi")]
        [SerializeField] private PlayerIdentityView hostIdentity;
        [SerializeField] private MapSelectorView mapSelector;
        [SerializeField] private Button createLobbyButton;

        [Header("Join sekmesi")]
        [SerializeField] private PlayerIdentityView joinIdentity;
        [SerializeField] private TMP_InputField addressInput;
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private TMP_Text statusText;          // opsiyonel: "Bağlanılıyor..." / hata

        [Header("Geri")]
        [SerializeField] private Button backButton;            // opsiyonel
        [SerializeField] private LobbyNavigator navigator;

        private ILobbyService service;

        private void Awake()
        {
            service = serviceSource;

            hostTabButton.onClick.AddListener(() => ShowTab(true));
            joinTabButton.onClick.AddListener(() => ShowTab(false));
            createLobbyButton.onClick.AddListener(OnCreateClicked);
            joinLobbyButton.onClick.AddListener(OnJoinClicked);
            if (backButton != null) backButton.onClick.AddListener(navigator.BackFromSetup);
        }

        private void OnEnable()
        {
            service.JoinFailed += OnJoinFailed;
            SetBusy(false);
            SetStatus("");
            ShowTab(true);
        }

        private void OnDisable() => service.JoinFailed -= OnJoinFailed;

        private void ShowTab(bool host)
        {
            hostContentPanel.SetActive(host);
            joinContentPanel.SetActive(!host);
        }

        private void OnCreateClicked()
        {
            SetBusy(true);
            service.Host(hostIdentity.Profile, mapSelector.SelectedIndex);
        }

        private void OnJoinClicked()
        {
            SetBusy(true);
            SetStatus("Bağlanılıyor...");
            service.Join(addressInput.text, joinIdentity.Profile);
        }

        private void OnJoinFailed(string message)
        {
            SetBusy(false);
            SetStatus(message);
        }

        private void SetBusy(bool busy)
        {
            createLobbyButton.interactable = !busy;
            joinLobbyButton.interactable = !busy;
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
