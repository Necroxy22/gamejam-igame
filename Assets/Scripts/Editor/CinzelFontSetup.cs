using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Membuat &amp; memelihara aset <see cref="TMP_FontAsset"/> untuk font <b>Cinzel</b>.
    ///
    /// Kenapa harus aset permanen:
    ///  - Teks WebGL butuh font SDF yang ikut ter-build (bukan font bitmap legacy yang buram).
    ///  - Atlas dinamis (Dynamic) membuat glyph di-rasterize sesuai kebutuhan sehingga tetap
    ///    tajam di resolusi/zoom berapa pun, tanpa membengkakkan build.
    ///  - Aset disimpan di <c>Assets/Resources/Fonts/</c> agar bisa direferensikan permanen
    ///    oleh scene, prefab, maupun <c>Resources.Load</c> saat runtime.
    ///
    /// Menu: Tools → Curie's Crucible → Ensure Cinzel TMP Font Asset.
    /// </summary>
    [InitializeOnLoad]
    public static class CinzelFontSetup
    {
        public const string FontFolder = "Assets/Resources/Fonts";
        public const string CinzelAssetPath = FontFolder + "/Cinzel SDF.asset";
        public const string FallbackAssetPath = FontFolder + "/LiberationSans Dynamic SDF.asset";

        const string CinzelSourcePath = FontFolder + "/Cinzel-VariableFont_wght.ttf";
        const string FallbackSourcePath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        static CinzelFontSetup()
        {
            EditorApplication.delayCall += AutoEnsure;
        }

        static void AutoEnsure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            EnsureFontAsset(false);
        }

        [MenuItem("Tools/Curie's Crucible/Ensure Cinzel TMP Font Asset", false, 60)]
        public static void EnsureFromMenu()
        {
            TMP_FontAsset font = EnsureFontAsset(true);
            if (font != null)
            {
                EditorUtility.DisplayDialog(
                    "Cinzel TMP Font Asset",
                    "Font SDF Cinzel siap dipakai:\n" + CinzelAssetPath +
                    "\n\nFallback: " + FallbackAssetPath,
                    "OK");
            }
        }

        /// <summary>
        /// Pastikan aset font Cinzel (atlas dinamis, SDFAA) ada dan bisa dipakai.
        /// Aman dipanggil berulang; mengembalikan null hanya bila font sumber tidak ada.
        /// </summary>
        public static TMP_FontAsset EnsureFontAsset(bool verbose)
        {
            if (!AssetDatabase.IsValidFolder(FontFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CinzelAssetPath);
            if (font == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>(CinzelSourcePath);
                if (source == null)
                {
                    Debug.LogError("[Cinzel] Font sumber tidak ditemukan: " + CinzelSourcePath);
                    return null;
                }

                font = CreateDynamicFontAsset(source, "Cinzel SDF");
                if (font == null)
                    return null;

                SaveFontAsset(font, CinzelAssetPath);

                if (verbose)
                    Debug.Log("[Cinzel] Aset TMP_FontAsset baru dibuat: " + CinzelAssetPath);
            }
            else if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                EditorUtility.SetDirty(font);
            }

            if (!font.isMultiAtlasTexturesEnabled)
            {
                font.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(font);
            }

            EnsureFallbackTable(font, verbose);

            AssetDatabase.SaveAssets();
            return font;
        }

        /// <summary>Buat TMP_FontAsset SDF dinamis (tajam, atlas 1024², multi-atlas).</summary>
        static TMP_FontAsset CreateDynamicFontAsset(Font source, string assetName)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source,
                90,                          // sampling point size (SDF tetap tajam saat di-scale)
                9,                           // padding / spread
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (font == null)
            {
                Debug.LogError("[Cinzel] Gagal membuat TMP_FontAsset dari '" + source.name +
                               "'. Pastikan 'Include Font Data' aktif di Font Import Settings.");
                return null;
            }

            font.name = assetName;
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled = true;
            return font;
        }

        static void SaveFontAsset(TMP_FontAsset font, string path)
        {
            AssetDatabase.CreateAsset(font, path);

            if (font.material != null)
            {
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            Texture2D[] atlasTextures = font.atlasTextures;
            if (atlasTextures != null)
            {
                for (int i = 0; i < atlasTextures.Length; i++)
                {
                    if (atlasTextures[i] == null)
                        continue;

                    atlasTextures[i].name = font.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlasTextures[i], font);
                }
            }

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// Tambahkan fallback dinamis (Liberation Sans) agar glyph di luar cakupan Cinzel
        /// — mis. panah &quot;←&quot; pada formula — tetap tampil tanpa warning missing glyph.
        /// </summary>
        static void EnsureFallbackTable(TMP_FontAsset font, bool verbose)
        {
            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
            if (fallback == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>(FallbackSourcePath);
                if (source != null)
                {
                    fallback = CreateDynamicFontAsset(source, "LiberationSans Dynamic SDF");
                    if (fallback != null)
                        SaveFontAsset(fallback, FallbackAssetPath);
                }
            }

            if (fallback == null || fallback == font)
                return;

            if (font.fallbackFontAssetTable == null)
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();

            if (!font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(font);

                if (verbose)
                    Debug.Log("[Cinzel] Fallback ditambahkan: " + FallbackAssetPath);
            }
        }
    }
}
