using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Presentation.UI
{
    public class LevelNodeUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button nodeButton;
        [SerializeField] private TMP_Text levelNumberText;
        [SerializeField] private GameObject lockIcon;
        
        [Tooltip("Kilitli seviyelerde boş yıldızları tamamen gizlemek için ana kapsayıcı")]
        [SerializeField] private GameObject starsContainer; 

        [Header("Stars (Yellow Overlays)")]
        [Tooltip("Star1, Star2, Star3 objelerinin içinde duran 3 adet YellowStar objesi")]
        [SerializeField] private GameObject[] yellowStars = new GameObject[3];

        [Header("Juice / Pop Settings")]
        [SerializeField] private bool animateStarsOnLoad = true;

        private int levelIndex;
        private Action<int> onClickCallback;

        private void Awake()
        {
            if (nodeButton == null) nodeButton = GetComponent<Button>();
            nodeButton.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// Düğümün durumunu (Kilitli/Açık, Yıldızlar, Numaralar) veriye göre ayarlar.
        /// </summary>
        public void SetupNode(int index, bool isUnlocked, int starsEarned, Action<int> callback)
        {
            levelIndex = index;
            onClickCallback = callback;

            StopAllCoroutines();

            if (isUnlocked)
            {
                // --- 1. BÖLÜM AÇIKSA ---
                if (lockIcon != null) lockIcon.SetActive(false);
                
                if (levelNumberText != null)
                {
                    levelNumberText.gameObject.SetActive(true);
                    levelNumberText.text = index.ToString();
                }

                if (nodeButton != null) nodeButton.interactable = true;

                // Boş yıldız zeminlerini (Star1, Star2, Star3) görünür yap
                if (starsContainer != null) starsContainer.SetActive(true);

                // Kazanılan yıldız sayısına göre sarı katmanları aç/kapat
                for (int i = 0; i < yellowStars.Length; i++)
                {
                    if (yellowStars[i] == null) continue;

                    bool shouldShowYellow = (i < starsEarned);
                    yellowStars[i].SetActive(shouldShowYellow);

                    if (shouldShowYellow && animateStarsOnLoad)
                    {
                        StartCoroutine(AnimateStarPop(yellowStars[i].transform, i * 0.15f));
                    }
                }
            }
            else
            {
                // --- 2. BÖLÜM KİLİTLİYSE ---
                if (lockIcon != null) lockIcon.SetActive(true);
                if (levelNumberText != null) levelNumberText.gameObject.SetActive(false);
                if (nodeButton != null) nodeButton.interactable = false;

                // Yıldız alanını tamamen kapat (Boş yıldızlar dahil hiçbir şey görünmez)
                if (starsContainer != null) starsContainer.SetActive(false);

                // Tüm sarı yıldızları kapat
                for (int i = 0; i < yellowStars.Length; i++)
                {
                    if (yellowStars[i] != null) yellowStars[i].SetActive(false);
                }
            }
        }

        private IEnumerator AnimateStarPop(Transform starTransform, float delay)
        {
            starTransform.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);

            float timer = 0f;
            while (timer < 0.25f)
            {
                timer += Time.deltaTime;
                float progress = timer / 0.25f;
                float scale = Mathf.Sin(progress * Mathf.PI * 0.75f) * 1.2f;
                starTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            starTransform.localScale = Vector3.one;
        }

        private void OnClicked()
        {
            onClickCallback?.Invoke(levelIndex);
        }
    }
}