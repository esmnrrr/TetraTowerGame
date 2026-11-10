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

            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {data.ScoreOrTurns}";
            }

            if (data.IsWinner)
            {
                titleText.text = "WIN!";
                titleText.color = new Color32(46, 204, 113, 255); // Yeşil
            }
            else
            {
                titleText.text = "LOSE!";
                titleText.color = new Color32(231, 76, 60, 255); // Kırmızı
            }
        }
    }
}