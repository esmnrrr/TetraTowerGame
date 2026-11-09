using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Presentation.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class UITabButton : MonoBehaviour, IPointerClickHandler
    {
        [Header("Content Association")]
        [Tooltip("Bu tab seçildiğinde açılacak içerik paneli (Örn: HostContentPanel veya JoinContentPanel)")]
        public GameObject contentPanel;

        [Header("Tab Visual Settings")]
        [Tooltip("Seçili olan tab'ın yukarı kalkma miktarı (Örn: 15px)")]
        public float elevatedYOffset = 15f;
        
        [Tooltip("Pasif olan tab'ın solukluk oranı (0.4 = %40 görünürlük)")]
        public float unselectedAlpha = 0.45f;
        
        [Tooltip("Animasyon geçiş hızı")]
        public float transitionSpeed = 15f;

        [Header("Optional UI References")]
        public CanvasGroup canvasGroup;

        private RectTransform rectTransform;
        private Vector2 originalAnchoredPos;
        private bool isSelected = false;
        private System.Action<UITabButton> onTabClickedCallback;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalAnchoredPos = rectTransform.anchoredPosition;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public void Initialize(System.Action<UITabButton> onClickCallback)
        {
            onTabClickedCallback = onClickCallback;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onTabClickedCallback?.Invoke(this);
        }

        public void SetSelectedState(bool selected)
        {
            isSelected = selected;

            if (contentPanel != null)
            {
                contentPanel.SetActive(selected);
            }
        }

        private void Update()
        {
            if (rectTransform == null) return;

            // Target Y offset & Alpha
            Vector2 targetPos = isSelected 
                ? originalAnchoredPos + new Vector2(0f, elevatedYOffset) 
                : originalAnchoredPos;

            float targetAlpha = isSelected ? 1f : unselectedAlpha;

            // 1. Akıcı Y Pozisyonu Geçişi (Yukarı Kalkma)
            rectTransform.anchoredPosition = Vector2.Lerp(
                rectTransform.anchoredPosition, 
                targetPos, 
                Time.unscaledDeltaTime * transitionSpeed
            );

            // 2. Akıcı Solukluk Geçişi (Alpha Lerp)
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * transitionSpeed);
            }
        }
    }
}