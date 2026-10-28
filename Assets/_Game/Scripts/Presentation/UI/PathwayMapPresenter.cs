using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Infrastructure.Save;
using Game.Domain.Progression;

namespace Game.Presentation.UI
{
    public class PathwayMapPresenter : MonoBehaviour
    {
        [Header("Level Nodes in Map (1 to 10)")]
        [Tooltip("Sahnede elle dizdiğin 10 adet LevelNode objesi")]
        public LevelNodeUI[] levelNodes;

        [Header("Header Elements")]
        [Tooltip("Sağ üstteki '0/30' yıldız metni")]
        public TMP_Text totalStarsText;
        public int totalWorldStarsMax = 30; // 10 bölüm x 3 yıldız = 30

        [Header("Footer Worlds")]
        public Button kitchenButton;
        public Button bathroomButton;
        public Button livingRoomButton;
        public Button bedroomButton;

        [Header("Save Repository")]
        public ProgressRepository progressRepository;

        private void Start()
        {
            RefreshMapUI();
            SetupFooterButtons();
        }

        public void RefreshMapUI()
        {
            PlayerProgress progress = (progressRepository != null) 
                ? progressRepository.LoadProgress() 
                : new PlayerProgress();

            int accumulatedStars = 0;

            for (int i = 0; i < levelNodes.Length; i++)
            {
                int levelIndex = i + 1;
                string levelID = $"level_kitchen_{levelIndex:D2}"; // Örn: level_kitchen_01

                // Kayıtlı veride bu seviye var mı?
                var result = progress.levelResults.Find(r => r.levelID == levelID);

                // Kural: 1. seviye HER ZAMAN açık başlar. Sonraki seviyeler kayıtlı "isUnlocked" bilgisine bakar.
                bool isUnlocked = (i == 0) || (result != null && result.isUnlocked);
                int starsEarned = (result != null) ? result.starsEarned : 0;

                if (isUnlocked)
                {
                    accumulatedStars += starsEarned;
                }

                // Düğümü ayarla
                if (levelNodes[i] != null)
                {
                    levelNodes[i].SetupNode(levelIndex, isUnlocked, starsEarned, OnLevelSelected);
                }
            }

            // Üstteki Yıldız Sayacını Güncelle (Örn: 5/30)
            if (totalStarsText != null)
            {
                totalStarsText.text = $"{accumulatedStars}/{totalWorldStarsMax}";
            }
        }

        private void SetupFooterButtons()
        {
            // Sadece Kitchen tıklanabilir, diğer dünyalar kilitli
            if (kitchenButton != null) kitchenButton.interactable = true;
            if (bathroomButton != null) bathroomButton.interactable = false;
            if (livingRoomButton != null) livingRoomButton.interactable = false;
            if (bedroomButton != null) bedroomButton.interactable = false;
        }

        private void OnLevelSelected(int levelIndex)
        {
            Debug.Log($"Seviye {levelIndex} seçildi! Oyun sahnesi yükleniyor...");

            // 1. Seçilen seviye numarasını hafızaya kaydet (Oyun sahnesi açılınca hangi bölüm olduğunu anlasın)
            PlayerPrefs.SetInt("SelectedLevelIndex", levelIndex);
            PlayerPrefs.Save();

            // 2. Oyun sahnesini yükle 
            UnityEngine.SceneManagement.SceneManager.LoadScene("Map_Kitchen"); 
        }
    }
}