using TMPro;
using UnityEngine;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Helper null-check untuk UnityEngine.Object.
    ///
    /// Kenapa perlu: operator `??` mengabaikan overload `==` milik UnityEngine.Object.
    /// Objek yang sudah di-Destroy tetap "non-null" di sisi C#, sehingga
    /// `instance ?? FindObjectOfType&lt;T&gt;()` mengembalikan referensi mati dan
    /// tidak pernah jatuh ke fallback pencarian.
    ///
    /// Gunakan Ensure(ref field) agar pola fallback selalu aman.
    /// </summary>
    public static class UnityObjectUtil
    {
        /// <summary>Isi <paramref name="value"/> dengan hasil <paramref name="resolve"/> bila null (mengikuti semantik Unity).</summary>
        public static T Ensure<T>(this T value, System.Func<T> resolve) where T : Object
        {
            return value != null ? value : (resolve != null ? resolve() : null);
        }
    }

    /// <summary>
    /// Utilitas Teks Tajam (TextMeshProUGUI) untuk seluruh UI proyek.
    ///
    /// Teks legacy uGUI (<c>Text</c>) dirender dari bitmap font dinamis sehingga buram saat
    /// di-scale oleh CanvasScaler. TextMeshPro merender glyph SDF sehingga tetap tajam di
    /// resolusi berapa pun (termasuk WebGL canvas yang diperbesar user).
    ///
    /// Font proyek adalah <b>Cinzel</b> (TMP_FontAsset SDF dinamis di
    /// <c>Assets/Resources/Fonts/Cinzel SDF.asset</c>). <see cref="ApplySharpDefaults"/>
    /// menyetel properti anti-buram yang paling sering terlupa: material SDF, raytarget,
    /// overflow Wrap, dan mode auto-size yang non-destruktif (tidak menimpa fontSize yang
    /// sudah dikustomisasi user di Inspector).
    /// </summary>
    public static class UITextUtil
    {
        /// <summary>Path aset font SDF Cinzel di dalam folder Resources.</summary>
        public const string ProjectFontResourcePath = "Fonts/Cinzel SDF";

        static TMP_FontAsset _cachedFont;
        static TMP_FontAsset _cachedProjectFont;

        /// <summary>Font SDF default proyek (TMP Settings) sebagai fallback.</summary>
        public static TMP_FontAsset DefaultFontAsset
        {
            get
            {
                if (_cachedFont != null)
                    return _cachedFont;

                // 1. TMP Settings (LiberationSans SDF) — selalu ada saat TMP essentials diimpor.
                if (TMP_Settings.defaultFontAsset != null)
                {
                    _cachedFont = TMP_Settings.defaultFontAsset;
                    return _cachedFont;
                }

                // 2. Load langsung dari path bawaan paket TextMeshPro.
                _cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

                // 3. Aset SDF apa pun yang ada di project.
                if (_cachedFont == null)
                {
                    TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != null)
                        {
                            _cachedFont = all[i];
                            break;
                        }
                    }
                }

                return _cachedFont;
            }
        }

        /// <summary>
        /// Font utama UI proyek: Cinzel SDF (Victorian, tajam kristal).
        /// Bila aset belum dibuat, jatuh ke font default TMP agar tidak pernah null.
        /// </summary>
        public static TMP_FontAsset ProjectFontAsset
        {
            get
            {
                if (_cachedProjectFont != null)
                    return _cachedProjectFont;

                _cachedProjectFont = Resources.Load<TMP_FontAsset>(ProjectFontResourcePath);
                if (_cachedProjectFont != null)
                    return _cachedProjectFont;

                // Cadangan: cari aset TMP berisi "Cinzel" di folder Resources/Fonts.
                TMP_FontAsset[] fonts = Resources.LoadAll<TMP_FontAsset>("Fonts");
                for (int i = 0; i < fonts.Length; i++)
                {
                    if (fonts[i] != null &&
                        fonts[i].name.IndexOf("Cinzel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _cachedProjectFont = fonts[i];
                        break;
                    }
                }

                if (_cachedProjectFont == null)
                    _cachedProjectFont = DefaultFontAsset;

                return _cachedProjectFont;
            }
        }

        /// <summary>Terapkan font Cinzel + material SDF-nya ke komponen teks apa pun.</summary>
        public static void ApplyProjectFont(TMP_Text text)
        {
            if (text == null)
                return;

            TMP_FontAsset font = ProjectFontAsset;
            if (font == null)
                return;

            if (text.font != font)
                text.font = font;

            if (text.fontSharedMaterial == null && font.material != null)
                text.fontSharedMaterial = font.material;
        }

        /// <summary>
        /// Setel properti teks agar tajam &amp; konsisten. Aman dipanggil berulang;
        /// hanya mengisi nilai yang belum diatur (font / material) dan memaksa
        /// mode render yang tajam. <paramref name="fontSize"/> hanya dipakai bila
        /// teks masih memakai ukuran default TMP (36).
        /// </summary>
        public static void ApplySharpDefaults(TMP_Text text, float fontSize = 0f)
        {
            if (text == null)
                return;

            ApplyProjectFont(text);

            // Hindari auto-size (memicu re-layout tiap frame & bikin blur saat transisi).
            text.enableAutoSizing = false;

            // Wrap agar teks panjang tidak terpotong; overflow diatur per-kasus di pemanggil.
            text.horizontalAlignment = HorizontalAlignmentOptions.Left;
            text.verticalAlignment = VerticalAlignmentOptions.Top;

            text.raycastTarget = false;

            if (fontSize > 0f && Mathf.Approximately(text.fontSize, 36f))
                text.fontSize = fontSize;
        }

        /// <summary>
        /// Buat GameObject berisi <see cref="TextMeshProUGUI"/> dengan konfigurasi tajam standar.
        /// TextMeshProUGUI ditambahkan lewat <c>RequireComponent</c> RectTransform pada Unity 2022.
        /// </summary>
        public static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize = 0f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = 5;
            if (parent != null)
                go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            ApplySharpDefaults(text, fontSize);
            return text;
        }

        /// <summary>Ambil (atau tambahkan) TextMeshProUGUI pada GameObject yang sudah ada.</summary>
        public static TextMeshProUGUI EnsureText(GameObject go, float fontSize = 0f)
        {
            if (go == null)
                return null;

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (text == null)
                text = go.AddComponent<TextMeshProUGUI>();

            ApplySharpDefaults(text, fontSize);
            return text;
        }
    }
}
