using UnityEngine;

namespace Game.Presentation.Audio
{
    public class PieceCollisionAudio : MonoBehaviour
    {
        public AudioSource audioSource;
        public AudioClip impactSound;
        
        [Tooltip("Sesin çalması için gereken minimum çarpışma hızı")]
        public float minImpactVelocity = 1.5f;

        private void OnCollisionEnter(Collision collision)
        {
            // Sadece sert çarpmalarda ses çalsın (Sürtünme/yavaş temaslarda çalmasın)
            if (collision.relativeVelocity.magnitude >= minImpactVelocity)
            {
                if (audioSource != null && impactSound != null)
                {
                    // Çarpma hızına göre ses seviyesini hafifçe dinamik yapalım (Daha doğal hissettirir)
                    float volume = Mathf.Clamp01(collision.relativeVelocity.magnitude / 10f);
                    audioSource.PlayOneShot(impactSound, volume);
                }
            }
        }
    }
}