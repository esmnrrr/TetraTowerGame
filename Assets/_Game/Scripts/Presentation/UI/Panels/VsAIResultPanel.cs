using UnityEngine;
using TMPro;
using Game.Domain.Turns;

namespace Game.Presentation.UI.Panels
{
    public class VsAIResultPanel : BaseGameOverPanel
    {
        public TMP_Text titleText;
        public TMP_Text scoreText;

        public override void ShowResult(GameResultData data)
        {
            gameObject.SetActive(true);
            scoreText.text = $"Tur: {data.ScoreOrTurns}";

            if (data.IsWinner)
            {
                titleText.text = "KAZANDIN!";
                titleText.color = new Color32(46, 204, 113, 255); // Yeşil
            }
            else
            {
                titleText.text = "KAYBETTİN!";
                titleText.color = new Color32(231, 76, 60, 255); // Kırmızı
            }
        }
    }
}