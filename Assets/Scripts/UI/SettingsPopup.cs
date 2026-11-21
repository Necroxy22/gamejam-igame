using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Popup Pengaturan Suara (BGM & SFX):
    /// Mengatur AudioListener global dan menyimpan preferensi ke PlayerPrefs.
    /// </summary>
    public class SettingsPopup : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;

        [Header("Slider")]
        public Slider bgmSlider;
        public Text bgmValueText;
        public Slider sfxSlider;
        public Text sfxValueText;

        [Header("Tombol")]
        public Button closeButton;

        const string PrefKeyBGM = "CuriesCrucible_BGM";
        const string PrefKeySFX = "CuriesCrucible_SFX";

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (bgmSlider != null)
                bgmSlider.onValueChanged.AddListener(OnBGMChanged);

            if (sfxSlider != null)
                sfxSlider.onValueChanged.AddListener(OnSFXChanged);

            LoadSettings();
        }

        public void Open()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        void LoadSettings()
        {
            float bgm = PlayerPrefs.GetFloat(PrefKeyBGM, 0.8f);
            float sfx = PlayerPrefs.GetFloat(PrefKeySFX, 0.9f);

            if (bgmSlider != null) bgmSlider.value = bgm;
            if (sfxSlider != null) sfxSlider.value = sfx;

            UpdateAudio(bgm, sfx);
        }

        void OnBGMChanged(float val)
        {
            PlayerPrefs.SetFloat(PrefKeyBGM, val);
            UpdateAudio(val, sfxSlider != null ? sfxSlider.value : 0.9f);
        }

        void OnSFXChanged(float val)
        {
            PlayerPrefs.SetFloat(PrefKeySFX, val);
            UpdateAudio(bgmSlider != null ? bgmSlider.value : 0.8f, val);
        }

        void UpdateAudio(float bgm, float sfx)
        {
            if (bgmValueText != null)
                bgmValueText.text = Mathf.RoundToInt(bgm * 100f) + "%";

            if (sfxValueText != null)
                sfxValueText.text = Mathf.RoundToInt(sfx * 100f) + "%";

            // Atur volume master AudioListener untuk WebGL
            AudioListener.volume = Mathf.Max(bgm, sfx);
        }
    }
}
