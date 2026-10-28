using UnityEngine;
using Game.Domain.Progression;
using Game.Infrastructure.Save;
using Game.Presentation.UI;
using Game.Domain.Turns;

namespace Game.Application.UseCases
{
    public class LevelManager : MonoBehaviour
    {
        [Header("Current Level Data")]
        public LevelDefinition currentLevelDefinition;
        
        [Header("References")]
        public ProgressRepository progressRepository;
        public HUDPresenter hudPresenter;

        private int placedPieceCount = 0;
        private int droppedPieceCount = 0;

        // Dışarıdan okunduğunda doğrudan private değişkeni döndürmeli
        public int PlacedPieceCount => placedPieceCount;
        public int DroppedPieceCount => droppedPieceCount;

        private void Start()
        {
            int selectedLevelIndex = PlayerPrefs.GetInt("SelectedLevelIndex", 1);
            
            currentLevelDefinition = Resources.Load<LevelDefinition>($"Levels/Level_{selectedLevelIndex}");
            if (currentLevelDefinition == null)
            {
                currentLevelDefinition = Resources.Load<LevelDefinition>($"Levels/Level_{selectedLevelIndex:D2}");
            }

            if (currentLevelDefinition != null)
            {
                Debug.Log($"Şu an Seviye {selectedLevelIndex} oynanıyor. Hedef: {currentLevelDefinition.targetPieceCount} parça");
            }
            else
            {
                Debug.LogError($"Level_{selectedLevelIndex} bulunamadı! Lütfen Resources/Levels/ klasörüne ekleyin.");
            }
        }

        public void RegisterPiecePlaced()
        {
            placedPieceCount++;
            Debug.Log($"[LevelManager] Parça sayısı başarıyla artırıldı! Yeni Sayı: {placedPieceCount}");
        }

        public void RegisterPieceDropped()
        {
            droppedPieceCount++;
        }

        public void EvaluateAndFinishLevel(bool isTowerCollapsed)
        {
            if (currentLevelDefinition == null) return;

            int starsEarned = 0;
            if (!isTowerCollapsed)
            {
                starsEarned = StarEvaluator.EvaluateStars(currentLevelDefinition, placedPieceCount, droppedPieceCount);
            }

            SaveLevelProgress(currentLevelDefinition.levelIndex, starsEarned);

            if (hudPresenter != null)
            {
                GameResultData resultData = new GameResultData
                {
                    Mode = GameMode.Practice,
                    LevelIndex = currentLevelDefinition.levelIndex,
                    StarsEarned = starsEarned,
                    IsTowerCollapsed = isTowerCollapsed,
                    ScoreOrTurns = placedPieceCount
                };

                hudPresenter.DisplayGameOver(resultData);
            }
        }

        private void SaveLevelProgress(int levelIndex, int starsEarned)
        {
            if (progressRepository == null) progressRepository = FindAnyObjectByType<ProgressRepository>();
            if (progressRepository == null) return;

            PlayerProgress progress = progressRepository.LoadProgress();
            
            string currentLevelID = $"level_kitchen_{levelIndex:D2}";
            string nextLevelID = $"level_kitchen_{(levelIndex + 1):D2}";

            var currentResult = progress.levelResults.Find(r => r.levelID == currentLevelID);
            if (currentResult == null)
            {
                currentResult = new LevelResultData { levelID = currentLevelID, starsEarned = starsEarned, isUnlocked = true };
                progress.levelResults.Add(currentResult);
            }
            else if (starsEarned > currentResult.starsEarned)
            {
                currentResult.starsEarned = starsEarned;
            }

            if (starsEarned > 0)
            {
                var nextResult = progress.levelResults.Find(r => r.levelID == nextLevelID);
                if (nextResult == null)
                {
                    progress.levelResults.Add(new LevelResultData { levelID = nextLevelID, starsEarned = 0, isUnlocked = true });
                }
                else
                {
                    nextResult.isUnlocked = true;
                }
            }

            progressRepository.SaveProgress(progress);
        }
    }
}