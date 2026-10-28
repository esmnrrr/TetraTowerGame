using UnityEngine;
using TMPro;
using Game.Domain.Turns;

namespace Game.Presentation.UI.Panels
{
    public class MultiplayerResultPanel : BaseGameOverPanel
    {
        public TMP_Text titleText;
        public TMP_Text scoreText;

        public override void ShowResult(GameResultData data)
        {
            gameObject.SetActive(true);
            titleText.text = $"{data.WinnerName} KAZANDI!";
            titleText.color = new Color32(241, 196, 15, 255); // Sarı / Altın
            scoreText.text = $"Dayanılan Tur: {data.ScoreOrTurns}";
        }
    }
}