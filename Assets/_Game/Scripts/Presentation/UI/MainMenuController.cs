using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Presentation.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject mainCard;
        public GameObject modeSelectionCard;
        public GameObject settingsCard;

        [Header("Scene Settings")]
        public string gameplaySceneName = "Gameplay"; // Asıl oyun sahnenin adı

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

        public void BackToMainMenu()
        {
            if (modeSelectionCard != null) modeSelectionCard.SetActive(false);
            if (settingsCard != null) settingsCard.SetActive(false);
            if (mainCard != null) mainCard.SetActive(true);
        }

        // --- Mod Seçimi ve Sahneye Geçiş ---
        public void SelectPracticeMode()
        {
            PlayerPrefs.SetString("SelectedGameMode", "Practice");
            PlayerPrefs.Save();
            SceneManager.LoadScene(gameplaySceneName);
        }

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