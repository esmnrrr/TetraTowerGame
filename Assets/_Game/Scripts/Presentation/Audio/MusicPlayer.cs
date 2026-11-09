using UnityEngine;
using Game.Infrastructure.Save;

namespace Game.Presentation.Audio
{
    /// Ana menüde bir kez oluşur, sahne değişse de çalmaya devam eder. Host/client/AI/practice hepsinde
    /// kendi bilgisayarında yerel çalar, ağla ilgisi yok.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        private static MusicPlayer instance;

        private void Awake()
        {
            if (instance != null) { Destroy(gameObject); return; }   // menüye dönünce kopya oluşmasın
            instance = this;
            DontDestroyOnLoad(gameObject);

            var source = GetComponent<AudioSource>();
            source.loop = true;
            if (!source.isPlaying) source.Play();
        }

        private void Start() => GameSettings.ApplyAudio();
    }
}