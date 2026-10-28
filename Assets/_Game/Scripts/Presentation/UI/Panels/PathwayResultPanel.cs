using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Game.Domain.Turns;

namespace Game.Presentation.UI.Panels
{
    public class PathwayResultPanel : BaseGameOverPanel
    {
        [Header("Text References")]
        public TMP_Text titleText;
        public TMP_Text scoreText;

        [Header("Stars (Yellow Overlays)")]
        [Tooltip("Boş yıldız zeminlerinin üzerine yerleştirdiğin 3 adet Sarı Yıldız (Image) objesi")]
        public GameObject[] yellowStars = new GameObject[3];
        public bool animateStarsOnLoad = true;

        [Tooltip("Yıldızların açıldığında ulaşmasını istediğin ölçek (Örn: X:1.3, Y:1.3, Z:1)")]
        [SerializeField] private Vector3 targetStarScale = new Vector3(1.3f, 1.3f, 1f);

        [Header("Navigation Buttons")]
        public Button mapButton;       // Back / Haritaya Dön putonu
        public Button nextLevelButton; // Next / Sonraki Bölüm butonu

        public override void ShowResult(GameResultData data)
        {
            gameObject.SetActive(true);
            StopAllCoroutines();

            // 1. Başlık ve Skor Metni
            if (titleText != null)
            {
                titleText.text = data.IsTowerCollapsed ? "KULE ÇÖKTÜ!" : $"BÖLÜM {data.LevelIndex} TAMAMLANDI!";
            }

            if (scoreText != null)
            {
                scoreText.text = $"SKOR: {data.ScoreOrTurns}";
            }

            // 2. Yıldız Mantığı
            int actualStars = data.IsTowerCollapsed ? 0 : data.StarsEarned;

            for (int i = 0; i < yellowStars.Length; i++)
            {
                if (yellowStars[i] == null) continue;

                bool shouldShowYellow = (i < actualStars);
                yellowStars[i].SetActive(shouldShowYellow);

                if (shouldShowYellow)
                {
                    if (animateStarsOnLoad)
                    {
                        StartCoroutine(AnimateStarPop(yellowStars[i].transform, targetStarScale, i * 0.18f));
                    }
                    else
                    {
                        yellowStars[i].transform.localScale = targetStarScale;
                    }
                }
            }

            // 3. Back (Haritaya Dön) Butonu Dinleyicisi
            if (mapButton != null)
            {
                mapButton.onClick.RemoveAllListeners();
                mapButton.onClick.AddListener(OnBackToMapClicked);
            }

            // 4. Next (Sonraki Bölüm) Butonu Dinleyicisi
            if (nextLevelButton != null)
            {
                bool canGoNext = !data.IsTowerCollapsed && actualStars > 0;
                nextLevelButton.gameObject.SetActive(canGoNext);

                if (canGoNext)
                {
                    nextLevelButton.onClick.RemoveAllListeners();
                    nextLevelButton.onClick.AddListener(() => OnNextLevelClicked(data.LevelIndex + 1));
                }
            }
        }

        private IEnumerator AnimateStarPop(Transform starTransform, Vector3 finalScale, float delay)
        {
            starTransform.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);

            float timer = 0f;
            while (timer < 0.25f)
            {
                timer += Time.deltaTime;
                float progress = timer / 0.25f;

                // Belirlediğin hedef ölçek (1.3) üzerinden Pop esnemesi yapar
                float scaleMultiplier = Mathf.Sin(progress * Mathf.PI * 0.75f) * 1.2f;
                starTransform.localScale = finalScale * scaleMultiplier;
                yield return null;
            }
            
            // Animasyon biter bitmez kesin olarak 1.3 boyutuna sabitler
            starTransform.localScale = finalScale;
        }

        private void OnBackToMapClicked()
        {
            Time.timeScale = 1f;
            PlayerPrefs.SetInt("OpenPathwayOnMenu", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("MainMenu");
        }

        private void OnNextLevelClicked(int nextLevelIndex)
        {
            Time.timeScale = 1f;
            PlayerPrefs.SetInt("SelectedLevelIndex", nextLevelIndex);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}