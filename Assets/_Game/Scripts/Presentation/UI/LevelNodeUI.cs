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

        [Header("Stars (Empty vs Yellow)")]
        [Tooltip("Boş yıldızların üzerine koyduğun 3 adet Sarı Yıldız (Image) objesi")]
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

            // 1. KİLİT VE SAYI MANTIĞI
            if (isUnlocked)
            {
                // Bölüm AÇIKsa: Kilidi kapat, Numarayı göster, Butonu tıklanabilir yap
                if (lockIcon != null) lockIcon.SetActive(false);
                if (levelNumberText != null)
                {
                    levelNumberText.gameObject.SetActive(true);
                    levelNumberText.text = index.ToString();
                }
                nodeButton.interactable = true;
            }
            else
            {
                // Bölüm KİLİTLİyse: Kilidi aç, Numarayı gizle, Butonu kapat
                if (lockIcon != null) lockIcon.SetActive(true);
                if (levelNumberText != null) levelNumberText.gameObject.SetActive(false);
                nodeButton.interactable = false;
            }

            // 2. YILDIZ MANTIĞI
            StopAllCoroutines();
            for (int i = 0; i < yellowStars.Length; i++)
            {
                if (yellowStars[i] == null) continue;

                bool shouldShowYellow = isUnlocked && (i < starsEarned);

                if (shouldShowYellow)
                {
                    yellowStars[i].SetActive(true);
                    if (animateStarsOnLoad)
                    {
                        StartCoroutine(AnimateStarPop(yellowStars[i].transform, i * 0.15f));
                    }
                }
                else
                {
                    // Kazanılmayan veya kilitli olan yıldızların sarısı KAPALI kalır (Alttaki boş yıldız görünür)
                    yellowStars[i].SetActive(false);
                }
            }
        }

        // Yıldızların pat diye esneyerek açılması için minik Juice efekti
        private IEnumerator AnimateStarPop(Transform starTransform, float delay)
        {
            starTransform.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);

            float timer = 0f;
            while (timer < 0.25f)
            {
                timer += Time.deltaTime;
                float progress = timer / 0.25f;
                // Büyüyüp hafif esneme hareketi (Pop)
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