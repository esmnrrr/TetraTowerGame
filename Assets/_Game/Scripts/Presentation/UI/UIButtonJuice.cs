using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.UI
{
    public class UIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Juice Settings")]
        [Tooltip("Basıldığında butonun küçülme oranı (0.92 = %8 küçülme)")]
        public float pressedScale = 0.92f;

        [Tooltip("Basıldığında butonun aşağı kayma miktarı (Y piksel)")]
        public float pressedYOffset = -4f;

        public float animationSpeed = 25f;

        private Vector3 originalScale;
        private Vector3 originalPosition;
        private RectTransform rectTransform;
        private bool isPressed = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalScale = rectTransform.localScale;
                originalPosition = rectTransform.anchoredPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPressed = false;
        }

        private void Update()
        {
            if (rectTransform == null) return;

            Vector3 targetScale = isPressed ? originalScale * pressedScale : originalScale;
            Vector3 targetPos = isPressed ? originalPosition + new Vector3(0, pressedYOffset, 0) : originalPosition;

            // Pürüzsüz esneme hareketi
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
            rectTransform.anchoredPosition = Vector3.Lerp(rectTransform.anchoredPosition, targetPos, Time.unscaledDeltaTime * animationSpeed);
        }
    }
}