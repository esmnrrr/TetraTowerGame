using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Presentation.UI
{
    public class SelectionHighlightController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Kartların üstünde gezen Kırmızı Ok UI objesi")]
        public RectTransform redArrow;

        [Tooltip("Sırasıyla 3 adet parça kartı (RectTransform)")]
        public List<RectTransform> cardCards = new List<RectTransform>();

        [Header("Animation Settings")]
        public float moveSpeed = 15f;         // Okun ve büyümenin geçiş hızı
        public float selectedScale = 1.15f;  // Seçili kartın büyüklüğü
        public float normalScale = 1.0f;     // Normal kart büyüklüğü

        [Header("Glow / Outline (Işıltı)")]
        public Color selectedOutlineColor = new Color(1f, 0.85f, 0.2f, 1f); // Altın Sarısı / Işıltı

        private int currentSelectedIndex = 0;

        private void OnEnable()
        {
            // Panel her açıldığında varsayılan olarak EN SOLDAKİ (0. kart) seçili başlasın
            SelectCard(0, true);
        }

        private void Start()
        {
            // Kartlara fare üzerine gelme (PointerEnter) dinleyicisi ekle
            for (int i = 0; i < cardCards.Count; i++)
            {
                int index = i;
                EventTrigger trigger = cardCards[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = cardCards[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => { SelectCard(index); });
                trigger.triggers.Add(entry);
            }
        }

        public void SelectCard(int index, bool instant = false)
        {
            if (index < 0 || index >= cardCards.Count) return;
            currentSelectedIndex = index;

            if (instant && redArrow != null)
            {
                // Açılışta ışınlanarak geçsin
                Vector3 targetPos = redArrow.position;
                targetPos.x = cardCards[index].position.x;
                redArrow.position = targetPos;
            }
        }

        private void Update()
        {
            if (cardCards == null || cardCards.Count == 0) return;

            // 1. Kırmızı Oku seçili kartın X pozisyonuna pürüzsüz kaydır
            if (redArrow != null)
            {
                Vector3 currentArrowPos = redArrow.position;
                float targetX = cardCards[currentSelectedIndex].position.x;
                currentArrowPos.x = Mathf.Lerp(currentArrowPos.x, targetX, Time.deltaTime * moveSpeed);
                redArrow.position = currentArrowPos;
            }

            // 2. Kartların ölçeğini ve Işıltısını (Outline) güncelle
            for (int i = 0; i < cardCards.Count; i++)
            {
                bool isSelected = (i == currentSelectedIndex);
                float targetScaleVal = isSelected ? selectedScale : normalScale;

                // Büyüme / Küçülme Animasyonu
                Vector3 currentScale = cardCards[i].localScale;
                Vector3 targetScaleVec = new Vector3(targetScaleVal, targetScaleVal, 1f);
                cardCards[i].localScale = Vector3.Lerp(currentScale, targetScaleVec, Time.deltaTime * moveSpeed);

                // Işıltı (Outline bileşeni varsa renklendir ve aç/kapat)
                Outline outline = cardCards[i].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = isSelected;
                    if (isSelected)
                    {
                        outline.effectColor = selectedOutlineColor;
                    }
                }
            }
        }
    }
}