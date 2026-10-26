using UnityEngine;
using Game.Domain.Progression;

namespace Game.Infrastructure.Save
{
    public class ProgressRepository : MonoBehaviour
    {
        private const string SAVE_KEY = "TOWER_PANIC_PLAYER_PROGRESS";

        public PlayerProgress LoadProgress()
        {
            if (PlayerPrefs.HasKey(SAVE_KEY))
            {
                string json = PlayerPrefs.GetString(SAVE_KEY);
                return JsonUtility.FromJson<PlayerProgress>(json);
            }
            
            // Varsayılan İlk Kayıt: İlk seviye açık, diğerleri kilitli
            PlayerProgress defaultProgress = new PlayerProgress();
            defaultProgress.levelResults.Add(new LevelResultData { levelID = "level_world1_01", starsEarned = 0, isUnlocked = true });
            return defaultProgress;
        }

        public void SaveProgress(PlayerProgress progress)
        {
            string json = JsonUtility.ToJson(progress);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }
    }
}