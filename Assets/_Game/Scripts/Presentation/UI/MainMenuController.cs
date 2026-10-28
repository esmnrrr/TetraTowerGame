using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels / Cards")]
        public GameObject mainCard;
        public GameObject modeSelectionCard;
        public GameObject settingsCard;
        [Tooltip("Yeni eklediğimiz 10 düğümlü Pathway (Harita) paneli")]
        public GameObject pathwayCard; 

        [Header("Presenters")]
        [Tooltip("Haritadaki yıldızları ve kilitleri tazeleyecek olan Presenter")]
        public PathwayMapPresenter pathwayPresenter;

        [Header("Scene Settings")]
        public string gameplaySceneName = "Test_Physics"; // Gameplay veya Test_Physics sahnenin adı

        private void Start()
        {
            // Oyun sahnesinden (Back butonuyla) dönülüp dönülmediğini kontrol et
            if (PlayerPrefs.GetInt("OpenPathwayOnMenu", 0) == 1)
            {
                // Bayrağı temizle ki tekrar girişlerde normal başlasın
                PlayerPrefs.DeleteKey("OpenPathwayOnMenu");

                // Doğrudan Pathway (Harita) kartını ve verilerini aç
                SelectPracticeMode();
            }
            else
            {
                // Normal açılış düzeni
                if (mainCard != null) mainCard.SetActive(true);
                if (modeSelectionCard != null) modeSelectionCard.SetActive(false);
                if (settingsCard != null) settingsCard.SetActive(false);
                if (pathwayCard != null) pathwayCard.SetActive(false);
            }
        }

        // --- Panel Geçişleri ---
        public void OpenModeSelection()
        {
            if (mainCard != null) mainCard.SetActive(false);
            if (modeSelectionCard != null) modeSelectionCard.SetActive(true);
        }

        public void OpenSettings()
        {
            if (mainCard != null) mainCard.SetActive(false);
            if (settingsCard != null) settingsCard.SetActive(true);
        }

        /// <summary>
        /// Mod Seçiminden Practice (Solo) tıklandığında doğrudan sahneye gitmek YERİNE
        /// 10 Düğümlü Pathway Harita Kartını açar!
        /// </summary>
        public void SelectPracticeMode()
        {
            PlayerPrefs.SetString("SelectedGameMode", "Practice");
            PlayerPrefs.Save();

            if (modeSelectionCard != null) modeSelectionCard.SetActive(false);
            if (mainCard != null) mainCard.SetActive(false);

            if (pathwayCard != null)
            {
                pathwayCard.SetActive(true);
                // Harita açılır açılmaz hafızadaki yıldız ve kilit durumunu tazele
                if (pathwayPresenter != null)
                {
                    pathwayPresenter.RefreshMapUI();
                }
            }
        }

        public void BackToMainMenu()
        {
            if (modeSelectionCard != null) modeSelectionCard.SetActive(false);
            if (settingsCard != null) settingsCard.SetActive(false);
            if (pathwayCard != null) pathwayCard.SetActive(false); // Pathway panelini de kapatır
            
            if (mainCard != null) mainCard.SetActive(true);
        }

        // --- Diğer Modlar (Doğrudan Sahneye Geçer) ---
        public void SelectVsAIMode()
        {
            PlayerPrefs.SetString("SelectedGameMode", "VsAI");
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void SelectMultiplayerMode()
        {
            PlayerPrefs.SetString("SelectedGameMode", "Multiplayer");
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void QuitGame()
        {
            //Application.Quit();
        }
    }
}