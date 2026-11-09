using UnityEngine;
using UnityEngine.Audio;

namespace Game.Infrastructure.Save
{
    /// Oyun ayarlarının tek kaynağı: kaydeder ve uygular.
    public static class GameSettings
    {
        private const string MusicKey = "Settings.Music";
        private const string SfxKey = "Settings.Sfx";
        private const string FullscreenKey = "Settings.Fullscreen";
        private const string MusicParam = "MusicVolume";   // mixer'da expose ettiğin adlar
        private const string SfxParam = "SfxVolume";

        private static AudioMixer mixer;

        public static float Music => PlayerPrefs.GetFloat(MusicKey, 0.8f);
        public static float Sfx => PlayerPrefs.GetFloat(SfxKey, 1f);
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            ApplyAudio();
            Screen.fullScreen = Fullscreen;
        }

        /// Mixer'a kayıtlı ses seviyelerini yazar. MusicPlayer.Start de çağırır (bazı Unity sürümlerinde
        /// ilk karede SetFloat tutmayabiliyor).
        public static void ApplyAudio()
        {
            if (mixer == null) mixer = Resources.Load<AudioMixer>("Audio/MainMixer");
            if (mixer == null) { Debug.LogWarning("[Settings] Resources/Audio/MainMixer bulunamadı."); return; }

            mixer.SetFloat(MusicParam, ToDb(Music));
            mixer.SetFloat(SfxParam, ToDb(Sfx));
        }

        public static void SetMusic(float v) { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(v)); ApplyAudio(); }
        public static void SetSfx(float v)   { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(v));   ApplyAudio(); }

        public static void SetFullscreen(bool on)
        {
            PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0);
            Screen.fullScreen = on;
        }

        // Slider 0..1 -> desibel (-80..0). Sıfırda tam sessiz.
        private static float ToDb(float v) => Mathf.Log10(Mathf.Max(v, 0.0001f)) * 20f;
    }
}