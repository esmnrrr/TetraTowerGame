using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Domain.Turns;

namespace Game.Presentation.UI.Panels
{
    public class PathwayResultPanel : BaseGameOverPanel
    {
        public TMP_Text titleText;
        public TMP_Text scoreText;
        public GameObject[] yellowStars = new GameObject[3];
        public Button nextLevelButton;

        public override void ShowResult(GameResultData data)
        {
            gameObject.SetActive(true);
            titleText.text = data.IsTowerCollapsed ? "KULE ÇÖKTÜ!" : $"BÖLÜM {data.LevelIndex} TAMAMLANDI!";
            scoreText.text = $"Skor: {data.ScoreOrTurns}";

            for (int i = 0; i < yellowStars.Length; i++)
            {
                if (yellowStars[i] != null) yellowStars[i].SetActive(i < data.StarsEarned);
            }

            if (nextLevelButton != null)
                nextLevelButton.gameObject.SetActive(data.StarsEarned > 0);
        }
    }
}