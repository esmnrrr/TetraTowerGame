using UnityEngine;

namespace Game.Application.UseCases
{
    public class WindForceController : MonoBehaviour
    {
        [Header("Wind Settings")]
        public Vector3 windForce = new Vector3(3f, 0, 0); // Sağ tarafa esen rüzgar
        public float windGustInterval = 5f; // Kaç saniyede bir şiddetli essin?
        
        private float timer = 0f;

        private void FixedUpdate()
        {
            timer += Time.fixedDeltaTime;
            bool isGusting = (timer % windGustInterval) < 1.5f; // 1.5 saniyelik sert rüzgar

            if (isGusting)
            {
                // Sahnede Rigidbody'si olan ve kuleye yerleştirilmiş düşen nesnelere güç uygula
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