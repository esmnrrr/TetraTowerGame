using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;

namespace Game.Presentation.Lobby
{
    /// <summary>
    /// "Avatar resmi + isim kutusu" ikilisi. Host ve Join sekmelerinin ikisinde de aynı component kullanılır.
    /// Avatar menü açıldığında rastgele atanır ve iki sekmede de aynı kalır; isim PlayerPrefs'te hatırlanır.
    /// </summary>
    public class PlayerIdentityView : MonoBehaviour
    {
        private const string NameKey = "PlayerName";
        private static int sessionAvatarId = -1;           // iki sekme aynı avatarı görsün

        [SerializeField] private AvatarCatalog avatars;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_InputField nameInput;

        public int AvatarId { get; private set; }

        public PlayerProfile Profile
        {
            get
            {
                string n = nameInput != null ? nameInput.text.Trim() : "";
                if (n.Length == 0) n = "Player" + Random.Range(100, 999);
                PlayerPrefs.SetString(NameKey, n);
                return new PlayerProfile(n, AvatarId);
            }
        }

        private void Awake()
        {
            if (sessionAvatarId < 0) sessionAvatarId = avatars.RandomId();
            AvatarId = sessionAvatarId;
            avatarImage.sprite = avatars.Get(AvatarId);

            nameInput.characterLimit = LobbyRules.MaxNameLength;
            nameInput.text = PlayerPrefs.GetString(NameKey, "");
        }
    }
}
