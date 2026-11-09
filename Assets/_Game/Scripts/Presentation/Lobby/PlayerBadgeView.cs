using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;

namespace Game.Presentation.UI.Panels
{
    /// Sonuç ekranındaki "avatar + isim" kutusu. Sadece gösterir.
    public class PlayerBadgeView : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text nameText;

        public void Bind(LobbyPlayerInfo info, AvatarCatalog avatars)
        {
            avatarImage.sprite = avatars.Get(info.AvatarId);
            nameText.text = info.Name;
        }
    }
}