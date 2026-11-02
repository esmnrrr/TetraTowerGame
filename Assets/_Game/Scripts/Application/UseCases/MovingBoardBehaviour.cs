using UnityEngine;

namespace Game.Application.UseCases
{
    public class MovingBoardBehaviour : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveDistance = 2f; // Merkezden sağa/sola ne kadar gidecek?
        public float moveSpeed = 1.5f;  // Ne kadar hızlı gidecek?

        private Vector3 startPos;
        private Rigidbody rb;

        private void Start()
        {
            startPos = transform.position;
            rb = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            // Eğer fizik motoru (Rigidbody) varsa, Kinematic hareketi fiziksel olarak yapmalı
            float pingPong = Mathf.PingPong(Time.time * moveSpeed, moveDistance * 2) - moveDistance;
            Vector3 targetPos = startPos + new Vector3(pingPong, 0, 0);

            if (rb != null && rb.isKinematic)
            {
                rb.MovePosition(targetPos);
            }
            else
            {
                transform.position = targetPos;
            }
        }
    }
}