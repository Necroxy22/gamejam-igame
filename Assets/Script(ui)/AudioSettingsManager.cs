using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Header("UI Sliders")]
    public Slider sliderMusic;
    public Slider sliderSFX;

    void Start()
    {
        // Muat volume tersimpan (default 1 = 100%)
        float savedMusic = PlayerPrefs.GetFloat("SavedMusicVol", 1f);
        float savedSFX = PlayerPrefs.GetFloat("SavedSFXVol", 1f);

        if (sliderMusic != null) sliderMusic.value = savedMusic;
        if (sliderSFX != null) sliderSFX.value = savedSFX;

        SetMusicVolume(savedMusic);
        SetSFXVolume(savedSFX);
    }

    // Hubungkan ke Slider Music -> On Value Changed (Dynamic float)
    public void SetMusicVolume(float nilaiSlider)
    {
        float dB = Mathf.Log10(Mathf.Clamp(nilaiSlider, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat("MusicVol", dB);
        PlayerPrefs.SetFloat("SavedMusicVol", nilaiSlider);
    }

    // Hubungkan ke Slider Sound Effect -> On Value Changed (Dynamic float)
    public void SetSFXVolume(float nilaiSlider)
    {
        float dB = Mathf.Log10(Mathf.Clamp(nilaiSlider, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat("SFXVol", dB);
        PlayerPrefs.SetFloat("SavedSFXVol", nilaiSlider);
    }
}