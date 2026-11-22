using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CuriesCrucible.Audio
{
    /// <summary>
    /// Singleton Audio Manager untuk mengelola BGM (smooth crossfade) dan SFX (random pitch & variation).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }

        [Header("Audio Sources (Otomatis Dibuat jika Kosong)")]
        [Tooltip("AudioSource untuk background music (looping dengan crossfade).")]
        public AudioSource bgmSource;

        [Tooltip("AudioSource untuk sound effects (PlayOneShot).")]
        public AudioSource sfxSource;

        [Header("BGM Volume & Pengaturan")]
        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 1.0f;
        [Tooltip("Durasi transisi crossfade antar lagu (detik).")]
        public float crossfadeDuration = 0.75f;

        [Header("BGM Tracks")]
        [Tooltip("Musik Menu / Persiapan (PotionMenu / simplechemistry).")]
        public AudioClip menuMusic;

        [Tooltip("Musik pertempuran Wave 1 (MariePOTIONWave1).")]
        public AudioClip wave1Music;

        [Tooltip("Musik pertempuran Wave 2 (MariePOTIONWave2).")]
        public AudioClip wave2Music;

        [Tooltip("Musik pertempuran Wave 3 & 4 (MariePOTIONWave3-4).")]
        public AudioClip wave3And4Music;

        [Tooltip("Musik pertempuran Wave 5 / Final (MariePOTIONWave5).")]
        public AudioClip wave5Music;

        [Tooltip("Musik jeda antar wave / sesi crafting (MariePOTIONinterlude).")]
        public AudioClip interludeMusic;

        [Tooltip("Musik kemenangan saat semua wave selesai (MariePOTIONEnding).")]
        public AudioClip endingMusic;

        [Tooltip("Musik kekalahan / continue saat menara hancur (MariePOTIONContinue).")]
        public AudioClip continueMusic;

        [Header("SFX Clustered Audio Clips")]
        [Tooltip("Variasi suara lempar ramuan (throw1, throw2).")]
        public AudioClip[] throwClips = new AudioClip[0];

        [Tooltip("Variasi suara botol ramuan pecah (glass1, glass2).")]
        public AudioClip[] glassBreakClips = new AudioClip[0];

        Coroutine _crossfadeRoutine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            EnsureAudioSources();

#if UNITY_EDITOR
            AutoAssignClipsFromProject();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.spatialBlend = 0f; // 2D Sound
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f; // 2D Sound
            }
        }

        // ------------------------------------------------------------------
        // BGM Playback & Crossfade Logic
        // ------------------------------------------------------------------

        public void PlayMenuBGM()
        {
            PlayBGM(menuMusic);
        }

        /// <summary>
        /// Mainkan musik wave berdasarkan nomor wave (1-based: 1, 2, 3, 4, 5).
        /// - Wave 1 -> wave1Music
        /// - Wave 2 -> wave2Music
        /// - Wave 3 & 4 -> wave3And4Music
        /// - Wave 5+ -> wave5Music
        /// </summary>
        public void PlayWaveBGM(int waveNumber)
        {
            AudioClip target = null;
            switch (waveNumber)
            {
                case 1:
                    target = wave1Music;
                    break;
                case 2:
                    target = wave2Music;
                    break;
                case 3:
                case 4:
                    target = wave3And4Music;
                    break;
                case 5:
                default:
                    target = wave5Music != null ? wave5Music : wave3And4Music;
                    break;
            }

            PlayBGM(target);
        }

        public void PlayInterludeBGM()
        {
            PlayBGM(interludeMusic);
        }

        public void PlayEndingBGM()
        {
            PlayBGM(endingMusic);
        }

        public void PlayContinueBGM()
        {
            PlayBGM(continueMusic);
        }

        /// <summary>
        /// Putar musik dengan smooth crossfade. Jika klip sama dan sedang berputar,
        /// musik dibiarkan lanjut tanpa restart.
        /// </summary>
        public void PlayBGM(AudioClip targetClip)
        {
            if (targetClip == null)
                return;

            EnsureAudioSources();

            // Hindari me-restart jika lagu yang diminta sama dan sedang berputar
            if (bgmSource.clip == targetClip && bgmSource.isPlaying)
                return;

            if (!isActiveAndEnabled)
            {
                bgmSource.clip = targetClip;
                bgmSource.volume = bgmVolume;
                bgmSource.loop = true;
                bgmSource.Play();
                return;
            }

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(CrossfadeRoutine(targetClip, crossfadeDuration));
        }

        public void StopBGM(float fadeDuration = 0.5f)
        {
            if (bgmSource == null || !bgmSource.isPlaying)
                return;

            if (_crossfadeRoutine != null)
                StopCoroutine(_crossfadeRoutine);

            _crossfadeRoutine = StartCoroutine(FadeOutRoutine(fadeDuration));
        }

        IEnumerator CrossfadeRoutine(AudioClip newClip, float duration)
        {
            float halfDuration = Mathf.Max(0.04f, duration * 0.5f);

            // 1. Fade Out lagu lama
            if (bgmSource.isPlaying && bgmSource.volume > 0.01f)
            {
                float startVol = bgmSource.volume;
                float elapsed = 0f;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                    yield return null;
                }
                bgmSource.volume = 0f;
            }

            // 2. Ganti klip & mulai play
            bgmSource.clip = newClip;
            bgmSource.loop = true;
            bgmSource.Play();

            // 3. Fade In lagu baru
            float inElapsed = 0f;
            while (inElapsed < halfDuration)
            {
                inElapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, inElapsed / halfDuration);
                yield return null;
            }

            bgmSource.volume = bgmVolume;
            _crossfadeRoutine = null;
        }

        IEnumerator FadeOutRoutine(float duration)
        {
            float startVol = bgmSource.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                yield return null;
            }
            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = bgmVolume;
            _crossfadeRoutine = null;
        }

        // ------------------------------------------------------------------
        // SFX Playback Logic (Pitch Variation & Random Selection)
        // ------------------------------------------------------------------

        /// <summary>
        /// Mainkan efek suara lemparan ramuan dengan variasi pitch 0.95f - 1.05f.
        /// </summary>
        public void PlayThrowSFX()
        {
            PlayRandomSFX(throwClips, 0.95f, 1.05f);
        }

        /// <summary>
        /// Mainkan efek suara botol ramuan pecah di target/tanah dengan variasi pitch 0.95f - 1.05f.
        /// </summary>
        public void PlayGlassBreakSFX()
        {
            PlayRandomSFX(glassBreakClips, 0.95f, 1.05f);
        }

        /// <summary>
        /// Mainkan klip acak dari array dengan variasi pitch dinamis.
        /// </summary>
        public void PlayRandomSFX(AudioClip[] clips, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clips == null || clips.Length == 0)
                return;

            EnsureAudioSources();

            // Kumpulkan klip yang valid (tidak null)
            List<AudioClip> validClips = new List<AudioClip>();
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    validClips.Add(clips[i]);
            }

            if (validClips.Count == 0)
                return;

            AudioClip chosen = validClips[Random.Range(0, validClips.Count)];
            PlaySFX(chosen, minPitch, maxPitch);
        }

        /// <summary>
        /// Mainkan satu SFX clip dengan variasi pitch.
        /// </summary>
        public void PlaySFX(AudioClip clip, float minPitch = 0.95f, float maxPitch = 1.05f)
        {
            if (clip == null)
                return;

            EnsureAudioSources();

            sfxSource.pitch = Random.Range(minPitch, maxPitch);
            sfxSource.PlayOneShot(clip, sfxVolume);
        }

#if UNITY_EDITOR
        void Reset()
        {
            AutoAssignClipsFromProject();
        }

        [ContextMenu("Auto Assign Audio Clips")]
        public void AutoAssignClipsFromProject()
        {
            string basePath = "Assets/Sound/OST+Soundeffects/";

            if (menuMusic == null)
            {
                menuMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "PotionMenu.wav")
                            ?? UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "simplechemistry.mp3");
            }

            if (wave1Music == null)
                wave1Music = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONWave1.mp3");

            if (wave2Music == null)
                wave2Music = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONWave2.mp3");

            if (wave3And4Music == null)
                wave3And4Music = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONWave3-4.mp3");

            if (wave5Music == null)
                wave5Music = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONWave5.mp3");

            if (interludeMusic == null)
                interludeMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONinterlude.mp3");

            if (endingMusic == null)
                endingMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONEnding.mp3");

            if (continueMusic == null)
                continueMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "MariePOTIONContinue.mp3");

            if (throwClips == null || throwClips.Length == 0)
            {
                var t1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "throw1/throw1.MP3");
                var t2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "throw2/throw2.MP3");
                var list = new List<AudioClip>();
                if (t1 != null) list.Add(t1);
                if (t2 != null) list.Add(t2);
                throwClips = list.ToArray();
            }

            if (glassBreakClips == null || glassBreakClips.Length == 0)
            {
                var g1 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "glass1/glass1.MP3");
                var g2 = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(basePath + "glass2/glass2.MP3");
                var list = new List<AudioClip>();
                if (g1 != null) list.Add(g1);
                if (g2 != null) list.Add(g2);
                glassBreakClips = list.ToArray();
            }
        }
#endif
    }
}
