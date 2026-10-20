using System.Collections;
using UnityEngine;
using TMPro;

namespace Game.Presentation.UI
{
    public class RoundJuiceEffect : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float popScale = 1.3f;        // Ne kadar büyüsün (1.3 = %30 büyüme)
        public float animDuration = 0.35f;  // Animasyon süresi (saniye)
        public float maxTiltAngle = 12f;    // Sallanma açısı (derece)

        [Header("Optional Color Flash")]
        public TMP_Text roundText;
        public Color flashColor = new Color(1f, 0.85f, 0.2f, 1f); // Altın Sarısı
        private Color originalTextColor;

        private Vector3 originalScale;
        private Quaternion originalRotation;
        private Coroutine activeCoroutine;

        private void Awake()
        {
            originalScale = transform.localScale;
            originalRotation = transform.localRotation;

            if (roundText != null)
            {
                originalTextColor = roundText.color;
            }
        }

        public void PlayJuiceEffect()
        {
            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
            }

            activeCoroutine = StartCoroutine(JuiceRoutine());
        }

        private IEnumerator JuiceRoutine()
        {
            float elapsed = 0f;

            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / animDuration;

                // 1. POP (Elastic/Bouncing Scale) - Önce hızla büyür, sonra esneyip küçülür
                float scaleCurve = Mathf.Sin(progress * Mathf.PI); 
                float currentScale = Mathf.Lerp(1f, popScale, scaleCurve);
                transform.localScale = originalScale * currentScale;

                // 2. TILT (Sallanma) - Post-it kağıdı gibi hafif sağa sola sarsılır
                float angle = Mathf.Sin(progress * Mathf.PI * 2f) * maxTiltAngle * (1f - progress);
                transform.localRotation = originalRotation * Quaternion.Euler(0, 0, angle);

                // 3. COLOR FLASH (Renk Parıltısı)
                if (roundText != null)
                {
                    roundText.color = Color.Lerp(flashColor, originalTextColor, progress);
                }

                yield return null;
            }

            // Temizlik ve Orijinal Değerlere Dönüş
            transform.localScale = originalScale;
            transform.localRotation = originalRotation;
            if (roundText != null) roundText.color = originalTextColor;
        }
    }
}