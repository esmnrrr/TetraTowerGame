using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;
using Game.Infrastructure.Net;

namespace Game.Presentation.Lobby
{
    /// <summary>
    /// OnlineLobbyPanel'in beyni. Servisin oyuncu listesini dinler ve ekranı o listeye göre çizer:
    /// host slotu, client slotu / waiting slot, "Players n/2", Ready tikleri, Start butonu (sadece host görür).
    /// </summary>
    public class OnlineLobbyPresenter : MonoBehaviour
    {
        [SerializeField] private NetcodeLobbyService serviceSource;
        [SerializeField] private AvatarCatalog avatars;
        [SerializeField] private MapCatalog maps;

        [Header("Sol kart")]
        [SerializeField] private TMP_Text lobbyIdText;
        //[SerializeField] private TMP_InputField ipInputField;   // salt okunur, kopyalanacak adres
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Image mapPreviewImage;

        [Header("Orta kart")]
        [SerializeField] private TMP_Text headerText;           // "Players 1 / 2"
        [SerializeField] private PlayerSlotView hostSlot;
        [SerializeField] private PlayerSlotView clientSlot;
        [SerializeField] private GameObject waitingSlot;
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonLabel;    

        [Header("Alt butonlar")]
        [SerializeField] private Button leaveButton;
        [SerializeField] private Button startButton;

        private ILobbyService service;
        private bool localReady;

        private void Awake()
        {
            service = serviceSource;
            // ipInputField.readOnly = true;

            copyCodeButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = service.LobbyCode);
            // copyCodeButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = service.Address);
            readyButton.onClick.AddListener(() => service.SetReady(!localReady));
            leaveButton.onClick.AddListener(service.Leave);
            startButton.onClick.AddListener(service.StartGame);
        }

        private void OnEnable()
        {
            service.RosterChanged += Refresh;
            service.MapChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            service.RosterChanged -= Refresh;
            service.MapChanged -= Refresh;
        }

        private void Refresh()
        {
            var roster = service.Roster;

            lobbyIdText.text = service.LobbyCode;
            // ipInputField.text = service.Address;
            var map = maps.Get(service.MapIndex);
            if (map != null && map.preview != null) mapPreviewImage.sprite = map.preview;

            bool hasHost = false, hasClient = false, everyoneReady = roster.Count == LobbyRules.MaxPlayers;
            localReady = false;

            foreach (var p in roster)
            {
                if (p.IsHost) { hostSlot.Bind(p, avatars); hasHost = true; }
                else { clientSlot.Bind(p, avatars); hasClient = true; }

                if (!p.IsReady) everyoneReady = false;
                if (p.ClientId == service.LocalClientId) localReady = p.IsReady;
            }

            hostSlot.gameObject.SetActive(hasHost);
            clientSlot.gameObject.SetActive(hasClient);
            waitingSlot.SetActive(!hasClient);                    // client yoksa "bekleniyor" satırı açık

            headerText.text = $"Players {roster.Count} / {LobbyRules.MaxPlayers}";
            if (readyButtonLabel != null) readyButtonLabel.text = localReady ? "CANCEL" : "READY!";

            startButton.gameObject.SetActive(service.IsHost);     // client Start'ı hiç görmez
            startButton.interactable = service.IsHost && everyoneReady;
        }
    }
}
