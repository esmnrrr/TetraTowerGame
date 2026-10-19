using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.UI
{
    public class UIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("Juice Settings")]
        [Tooltip("Basıldığında butonun küçülme oranı (0.92 = %8 küçülme)")]
        public float pressedScale = 0.92f;
        public float animationSpeed = 25f;

        private Vector3 originalScale;
        private RectTransform rectTransform;
        private bool isPressed = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                originalScale = rectTransform.localScale;
                // originalPosition takibini sildik, pozisyonu Layout Group yönetsin!
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

            // Sadece esneme/küçülme hareketi (Pozisyonu Layout Group'a bıraktık)
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
        }
    }
}