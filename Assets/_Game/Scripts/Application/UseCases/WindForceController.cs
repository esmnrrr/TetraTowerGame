using UnityEngine;

namespace Game.Application.UseCases
{
    public class WindForceController : MonoBehaviour
    {
        [Header("Wind Settings")]
        public Vector3 windForce = new Vector3(3f, 0, 0); // Sağ tarafa esen rüzgar
        public float windGustInterval = 5f; // Kaç saniyede bir şiddetli essin?
        
        [Header("Visual Effects")]
        [Tooltip("Rüzgar eserken çalışacak Particle System (WindParticles objesi)")]
        public ParticleSystem windParticles;

        private float timer = 0f;

        private void Start()
        {
            if (windParticles != null)
            {
                // Parçacık sisteminin yönünü rüzgar kuvvetine doğru çevir
                if (windForce != Vector3.zero)
                {
                    windParticles.transform.rotation = Quaternion.LookRotation(windForce);
                }

                // Başlangıçta rüzgar parçacıklarını kapalı tut
                var emission = windParticles.emission;
                emission.enabled = false;
            }
        }

        private void FixedUpdate()
        {
            timer += Time.fixedDeltaTime;
            
            // 1.5 saniyelik sert rüzgar periyodu
            bool isGusting = (timer % windGustInterval) < 1.5f; 

            // Görsel efekti rüzgar anına göre aç/kapat
            if (windParticles != null)
            {
                var emission = windParticles.emission;
                emission.enabled = isGusting;
            }

            if (isGusting)
            {
                // Sahnede kuleye yerleştirilmiş fiziksel parçalara rüzgar uygula
                GameObject[] pieces = GameObject.FindGameObjectsWithTag("Piece");
                foreach (var piece in pieces)
                {
                    Rigidbody rb = piece.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic)
                    {
                        rb.AddForce(windForce, ForceMode.Force);
                    }
                }
            }
        }
    }
}