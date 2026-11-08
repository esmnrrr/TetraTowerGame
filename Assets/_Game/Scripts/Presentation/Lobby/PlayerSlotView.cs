using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Domain.Lobby;

namespace Game.Presentation.Lobby
{
    /// <summary>HostSlot / ClientSlot satırı: avatar + isim + (host yıldızı) + (ready tiki). Sadece gösterir.</summary>
    public class PlayerSlotView : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject hostStar;     // yıldız ikonu (client slotunda boş bırak)
        [SerializeField] private GameObject readyTick;    // tik ikonu

        public void Bind(LobbyPlayerInfo info, AvatarCatalog avatars)
        {
            avatarImage.sprite = avatars.Get(info.AvatarId);
            nameText.text = info.Name;
            if (hostStar != null) hostStar.SetActive(info.IsHost);
            readyTick.SetActive(info.IsReady);
        }
    }
}
