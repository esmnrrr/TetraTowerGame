using UnityEngine;
using Game.Domain.Progression;
using Game.Infrastructure.Save;
using Game.Presentation.UI;

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

        public int PlacedPieceCount => placedPieceCount;
        public int DroppedPieceCount => droppedPieceCount;

        private void Start()
        {
            // 1. Oyuncunun haritadan hangi seviyeyi seçtiğini öğren
            int selectedLevelIndex = PlayerPrefs.GetInt("SelectedLevelIndex", 1);
            
            // 2. O seviyenin veri dosyasını (ScriptableObject) Resources klasöründen yükle
            currentLevelDefinition = Resources.Load<LevelDefinition>($"Levels/Level_{selectedLevelIndex}");

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
        }

        public void RegisterPieceDropped()
        {
            droppedPieceCount++;
        }

        /// <summary>
        /// Seviye bittiğinde (hedef tamamlandığında veya kule durduğunda) çalışır.
        /// </summary>
        public void EvaluateAndFinishLevel(bool isTowerCollapsed)
        {
            if (currentLevelDefinition == null)
            {
                Debug.LogWarning("LevelDefinition atanmamış! Varsayılan mantık çalışıyor.");
                return;
            }

            // 1. Yıldızları Hesapla
            int starsEarned = 0;
            if (!isTowerCollapsed)
            {
                starsEarned = StarEvaluator.EvaluateStars(currentLevelDefinition, placedPieceCount, droppedPieceCount);
            }

            // 2. İlerlemeyi Hafızaya Kaydet
            SaveLevelProgress(currentLevelDefinition.levelIndex, starsEarned);

            // 3. UI'da Sonuç Ekranını Göster
            if (hudPresenter != null)
            {
                hudPresenter.ShowLevelResults(currentLevelDefinition.levelIndex, starsEarned, isTowerCollapsed);
            }
        }

        private void SaveLevelProgress(int levelIndex, int starsEarned)
        {
            if (progressRepository == null) progressRepository = FindAnyObjectByType<ProgressRepository>();
            if (progressRepository == null) return;

            PlayerProgress progress = progressRepository.LoadProgress();
            
            string currentLevelID = $"level_kitchen_{levelIndex:D2}";
            string nextLevelID = $"level_kitchen_{(levelIndex + 1):D2}";

            // Mevcut seviyenin verisini güncelle veya ekle
            var currentResult = progress.levelResults.Find(r => r.levelID == currentLevelID);
            if (currentResult == null)
            {
                currentResult = new LevelResultData { levelID = currentLevelID, starsEarned = starsEarned, isUnlocked = true };
                progress.levelResults.Add(currentResult);
            }
            else
            {
                // Daha yüksek yıldız aldıysa güncelle
                if (starsEarned > currentResult.starsEarned)
                {
                    currentResult.starsEarned = starsEarned;
                }
            }

            // En az 1 yıldız kazandıysa BİR SONRAKİ SEVİYENİN KİLİDİNİ AÇ!
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

            // Kaydet
            progressRepository.SaveProgress(progress);
        }
    }
}