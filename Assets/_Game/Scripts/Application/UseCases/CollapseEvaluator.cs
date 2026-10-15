using UnityEngine;

namespace Game.Application.UseCases
{
    public class CollapseEvaluator : MonoBehaviour
    {
        [Tooltip("Kulenin çökük sayılması için kaç parçanın aşağı düşmesi gerekiyor? (Zorluk için 0 veya 1 yapabilirsin)")]
        public int allowedDrops = 0;
        
        private int currentDrops = 0;
        public bool HasTowerCollapsed { get; private set; } = false;

        // Görünmez trigger alanına bir obje girdiğinde çalışır
        private void OnTriggerEnter(Collider other)
        {
            // Eğer giren objenin etiketi "Piece" ise (Yani tetris bloğuysa)
            if (other.CompareTag("Piece"))
            {
                currentDrops++;
                Debug.Log($"Bir parça düştü! Toplam düşen: {currentDrops}");

                if (currentDrops > allowedDrops)
                {
                    HasTowerCollapsed = true;
                    Debug.Log("KULE ÇÖKTÜ! OYUN BİTMELİ.");
                }
            }
        }
    }
}