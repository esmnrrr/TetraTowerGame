using UnityEngine;

namespace Game.Presentation.Camera
{
    public class TowerCameraTracker : MonoBehaviour
    {
        [Header("Tracking Settings")]
        [Tooltip("Kamera harekete geçmeden önce kule Y ekseninde hangi yüksekliğe ulaşmalı?")]
        public float heightThreshold = 5f;
        
        [Tooltip("Kameranın yukarı doğru takip etme yumuşaklığı (Düşük = Daha sinematik/yavaş)")]
        public float smoothSpeed = 3f;

        private float initialY;

        private void Start()
        {
            // Kameranın başlangıçtaki dikey (Y) yüksekliğini kaydediyoruz
            initialY = transform.position.y;
        }

        private void LateUpdate()
        {
            // Sahnede "Piece" etiketine sahip tüm objeleri bul
            GameObject[] pieces = GameObject.FindGameObjectsWithTag("Piece");
            
            if (pieces.Length == 0) return;

            float highestY = initialY; 
            bool foundPlacedPiece = false;

            foreach (GameObject piece in pieces)
            {
                // BUG FIX: Eğer bu obje farenin ucunda gezen hayalet (Ghost) parçaysa VEYA 
                // henüz bırakılmamış/isKinematic olan bir parçaysa bunu kamera takibine ALMA!
                Rigidbody rb = piece.GetComponentInParent<Rigidbody>();
                if (rb != null && rb.isKinematic)
                {
                    continue; // Hayalet veya taşınma aşamasındaki objeyi atla!
                }

                // Objeye ait Renderer üzerinden en üst tepe noktasını (bounds.max.y) al
                Renderer r = piece.GetComponentInChildren<Renderer>();
                float pieceTopY = (r != null) ? r.bounds.max.y : piece.transform.position.y;

                if (pieceTopY > highestY)
                {
                    highestY = pieceTopY;
                    foundPlacedPiece = true;
                }
            }

            // Hedef Y pozisyonunu hesapla
            float targetY = initialY;

            // Eğer platformda gerçekten bırakılmış bir parça varsa ve eşiği geçtiyse
            if (foundPlacedPiece && highestY > heightThreshold)
            {
                targetY = initialY + (highestY - heightThreshold);
            }

            // Kamerayı sadece Z ve X'i koruyup Y ekseninde yumuşakça kaydır
            Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);
        }
    }
}