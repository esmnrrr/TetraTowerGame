using UnityEngine;
using System;

namespace Game.Application.UseCases
{
    public class PhysicsSettleDetector : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Hızın bu değerin altına düşmesini bekliyoruz (Örn: 0.05)")]
        public float velocityThreshold = 0.05f;

        [Tooltip("Fiziğin tamamen durduğuna ikna olmamız için geçmesi gereken süre (Saniye)")]
        public float settleDuration = 1.5f;

        [Header("Live State")]
        public bool isWaitingForSettle = false;
        private float currentSettleTime = 0f;

        // Fiziğin sakinleştiğini diğer sistemlere (TurnCoordinator) haber verecek olan Event
        public event Action OnPhysicsSettled;

        private void Update()
        {
            // Sadece bir parça bırakıldığında ve bekleme modundaysak çalışır
            if (!isWaitingForSettle) return;

            if (CheckIfAllRigidbodiesAreSettled())
            {
                // Her şey durgun, sakinlik süresini artır
                currentSettleTime += Time.deltaTime;

                if (currentSettleTime >= settleDuration)
                {
                    // Fizik belirlenen süre boyunca hareketsiz kaldı! (Settled)
                    isWaitingForSettle = false;
                    Debug.Log("Fizik Sakinleşti! Sonraki tura geçilebilir.");
                    OnPhysicsSettled?.Invoke();
                }
            }
            else
            {
                // Kulenin bir parçası veya platform hala sallanıyor, sayacı sıfırla
                currentSettleTime = 0f;
            }
        }

        /// <summary>
        /// Oyuncu parçayı yerleştirip bıraktığında dışarıdan çağrılır.
        /// </summary>
        public void StartDetecting()
        {
            isWaitingForSettle = true;
            currentSettleTime = 0f;
            Debug.Log("Fizik Sakinleşme Kontrolü Başladı...");
        }

        private bool CheckIfAllRigidbodiesAreSettled()
        {
            // Sahnedeki tüm 3D Rigidbody'leri bul
            // Unity 6 için FindObjectsByType kullanımı performansa uygundur
            Rigidbody[] allRigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);

            // Performans için karekök (magnitude) yerine karesini (sqrMagnitude) kullanıyoruz
            float sqrThreshold = velocityThreshold * velocityThreshold;

            foreach (Rigidbody rb in allRigidbodies)
            {
                // Eğer obje kinematikse (kodla yönetiliyorsa) veya fizik motoru tarafından uyutulduysa (Sleeping), o obje zaten durmuştur
                if (rb.IsSleeping() || rb.isKinematic) continue;

                // Hız veya dönüş hızı eşiğin üzerindeyse, sistem henüz sakinleşmemiştir
                if (rb.linearVelocity.sqrMagnitude > sqrThreshold || rb.angularVelocity.sqrMagnitude > sqrThreshold)
                {
                    return false; 
                }
            }

            return true; // Hiçbir obje belirlenen eşikten daha hızlı hareket etmiyor
        }
    }
}