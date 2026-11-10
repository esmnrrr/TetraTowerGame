using UnityEngine;
using UnityEngine.UI;
using Game.Infrastructure.Save;

namespace Game.Presentation.UI
{
    /// Settings kartındaki kontrolleri GameSettings'e bağlar. Ana menüde ve oyunda aynı çalışır.
    public class SettingsPresenter : MonoBehaviour
    {
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;   // Screen Shake'in yerine

        private void OnEnable()
        {
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameSettings.Music);
                musicSlider.onValueChanged.AddListener(GameSettings.SetMusic);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(GameSettings.Sfx);
                sfxSlider.onValueChanged.AddListener(GameSettings.SetSfx);
            }
            if (fullscreenToggle != null)
            {
                fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
                fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
            }
        }

        private void OnDisable()
        {
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(GameSettings.SetMusic);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(GameSettings.SetSfx);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.RemoveListener(GameSettings.SetFullscreen);
        }
    }
}