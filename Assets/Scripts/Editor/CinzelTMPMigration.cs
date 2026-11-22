using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Gameplay;
using CuriesCrucible.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Migrasi sekali-jalan (idempoten) untuk SampleScene:
    ///  1. Membuat aset font SDF <b>Cinzel</b> di Assets/Resources/Fonts (lihat <see cref="CinzelFontSetup"/>).
    ///  2. Mengubah seluruh komponen <see cref="Text"/> legacy di scene menjadi
    ///     <see cref="TextMeshProUGUI"/> ber-font Cinzel — posisi, ukuran, alignment, dan warna dipertahankan.
    ///  3. Menerapkan font Cinzel ke semua TMP yang sudah ada di scene.
    ///  4. Membangun kartu Quick Info Zombie + label Hotbar secara permanen di Hierarchy,
    ///     lalu menyinkronkan referensi Inspector pada <see cref="GameplayUIBridge"/> dan
    ///     <see cref="ZombieIntroSpotlight"/>.
    ///  5. Menyimpan SampleScene.
    ///
    /// Menu: Tools → Curie's Crucible → Migrate UI Text to Cinzel TMP (SampleScene).
    /// </summary>
    [InitializeOnLoad]
    public static class CinzelTMPMigration
    {
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        const string MigrationDoneKey = "CuriesCrucible.CinzelTMPMigration.v2";

        static CinzelTMPMigration()
        {
            EditorApplication.delayCall += TryAutoRun;
        }

        static void TryAutoRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoRun;
                return;
            }

            if (EditorPrefs.GetBool(MigrationDoneKey, false))
                return;

            // Jangan paksa membuka scene lain: hanya migrasi bila SampleScene memang aktif.
            if (EditorSceneManager.GetActiveScene().path != SampleScenePath)
                return;

            Run(false);
        }

        [MenuItem("Tools/Curie's Crucible/Migrate UI Text to Cinzel TMP (SampleScene)", false, 61)]
        public static void RunFromMenu()
        {
            Run(true);
        }

        /// <summary>Entry point batch untuk CI / command line.</summary>
        public static void RunBatch()
        {
            Run(true);
        }

        public static void Run(bool verbose)
        {
            TMP_FontAsset font = CinzelFontSetup.EnsureFontAsset(verbose);
            if (font == null)
            {
                Debug.LogError("[Cinzel] Migrasi dibatalkan: TMP_FontAsset Cinzel tidak tersedia.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path != SampleScenePath)
            {
                if (!System.IO.File.Exists(SampleScenePath))
                {
                    Debug.LogError("[Cinzel] SampleScene tidak ditemukan: " + SampleScenePath);
                    return;
                }

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            // 1) Kartu info zombie permanen + data teks bawaan agar tampil di Inspector.
            ZombieIntroSpotlight[] spotlights = Object.FindObjectsOfType<ZombieIntroSpotlight>(true);
            for (int i = 0; i < spotlights.Length; i++)
            {
                if (spotlights[i] == null) continue;
                spotlights[i].EditorPopulateCardDataDefaults();
                spotlights[i].EditorBuildCardPermanently();
                EditorUtility.SetDirty(spotlights[i]);
            }

            // 2) Konversi semua teks legacy uGUI menjadi TextMeshProUGUI.
            GameObject[] roots = scene.GetRootGameObjects();
            int converted = 0;
            for (int i = 0; i < roots.Length; i++)
                converted += ConvertLegacyTexts(roots[i].transform, font);

            // 3) Seragamkan font Cinzel pada semua TMP di scene.
            int styled = 0;
            for (int i = 0; i < roots.Length; i++)
                styled += ApplyFontToTmp(roots[i].transform, font);

            // 4) Sinkronkan referensi Inspector + data teks.
            GameplayUIBridge[] bridges = Object.FindObjectsOfType<GameplayUIBridge>(true);
            for (int i = 0; i < bridges.Length; i++)
            {
                if (bridges[i] == null) continue;

                bridges[i].EditorBuildHotbarTextsPermanently();
                RewireTextReferences(bridges[i]);
                SanitizeBridgeTexts(bridges[i]);
                bridges[i].SyncRecipeDrawerFromConfig();
                EditorUtility.SetDirty(bridges[i]);
            }

            for (int i = 0; i < spotlights.Length; i++)
            {
                if (spotlights[i] == null) continue;
                spotlights[i].SyncCardVisualsFromConfig();
                EditorUtility.SetDirty(spotlights[i]);
            }

            // 5) Simpan permanen ke SampleScene.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            EditorPrefs.SetBool(MigrationDoneKey, true);

            Debug.Log("[Cinzel] Migrasi SampleScene selesai — " + converted +
                      " teks legacy diubah ke TextMeshProUGUI, " + styled +
                      " komponen TMP memakai font Cinzel SDF.");
        }

        // ------------------------------------------------------------------
        // Konversi Text legacy -> TextMeshProUGUI
        // ------------------------------------------------------------------

        static int ConvertLegacyTexts(Transform root, TMP_FontAsset font)
        {
            if (root == null) return 0;

            Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
            int count = 0;

            for (int i = 0; i < legacyTexts.Length; i++)
            {
                Text legacy = legacyTexts[i];
                if (legacy == null) continue;

                GameObject go = legacy.gameObject;
                if (go == null) continue;

                // Simpan dulu seluruh pengaturan: komponen Text harus dilepas sebelum
                // TextMeshProUGUI bisa dipasang (satu GameObject hanya boleh punya satu Graphic).
                string content = legacy.text;
                Color color = legacy.color;
                float size = Mathf.Max(1f, legacy.fontSize);
                FontStyles style = ToTmpFontStyle(legacy.fontStyle);
                TextAlignmentOptions alignment = ToTmpAlignment(legacy.alignment);
                bool wrap = legacy.horizontalOverflow == HorizontalWrapMode.Wrap;
                TextOverflowModes overflow = legacy.verticalOverflow == VerticalWrapMode.Truncate
                    ? TextOverflowModes.Ellipsis
                    : TextOverflowModes.Overflow;
                float lineSpacing = (legacy.lineSpacing - 1f) * 100f;
                bool richText = legacy.supportRichText;
                bool raycastTarget = legacy.raycastTarget;
                bool autoSize = legacy.resizeTextForBestFit;
                float minSize = legacy.resizeTextMinSize;
                float maxSize = legacy.resizeTextMaxSize;

                Object.DestroyImmediate(legacy);

                TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
                if (tmp == null)
                    tmp = go.AddComponent<TextMeshProUGUI>();

                ApplyTmpSettings(tmp, font, content, color, size, style, alignment, wrap, overflow,
                    lineSpacing, richText, raycastTarget, autoSize, minSize, maxSize);

                EditorUtility.SetDirty(go);
                count++;
            }

            return count;
        }

        static void ApplyTmpSettings(
            TextMeshProUGUI tmp, TMP_FontAsset font, string content, Color color, float size,
            FontStyles style, TextAlignmentOptions alignment, bool wrap, TextOverflowModes overflow,
            float lineSpacing, bool richText, bool raycastTarget, bool autoSize, float minSize, float maxSize)
        {
            tmp.font = font;
            tmp.text = content;
            tmp.color = color;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = wrap;
            tmp.overflowMode = overflow;
            tmp.lineSpacing = lineSpacing;
            tmp.richText = richText;
            tmp.raycastTarget = raycastTarget;
            tmp.margin = Vector4.zero;

            if (autoSize)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = Mathf.Max(1f, minSize);
                tmp.fontSizeMax = Mathf.Max(1f, maxSize);
            }
            else
            {
                tmp.enableAutoSizing = false;
            }

            if (font != null && font.material != null)
                tmp.fontSharedMaterial = font.material;
        }

        static FontStyles ToTmpFontStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        // ------------------------------------------------------------------
        // Penerapan font & sinkronisasi referensi
        // ------------------------------------------------------------------

        static int ApplyFontToTmp(Transform root, TMP_FontAsset font)
        {
            if (root == null || font == null) return 0;

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            int count = 0;

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null) continue;

                if (text.font != font)
                {
                    text.font = font;
                    count++;
                }

                if (text.fontSharedMaterial == null && font.material != null)
                    text.fontSharedMaterial = font.material;

                EditorUtility.SetDirty(text);
            }

            return count;
        }

        static void RewireTextReferences(GameplayUIBridge bridge)
        {
            if (bridge == null) return;

            Transform craftRoot = bridge.craftPanel != null ? bridge.craftPanel.transform : null;

            // Kartu bahan (sisi kiri): badge stok + nilai pH.
            if (craftRoot != null)
            {
                Transform content = craftRoot.Find("Resep/Content");
                if (content == null)
                    content = craftRoot.Find("Bahan_Scroll/Content");

                if (content != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Transform card = content.Find("Card_" + (IngredientType)i);
                        if (card == null) continue;

                        Transform badgeText = card.Find("Icon_Box/Badge/Text");
                        if (badgeText == null) continue;

                        TMP_Text tmp = badgeText.GetComponent<TMP_Text>();
                        if (tmp != null)
                            bridge.craftStockBadgeTexts[i] = tmp;
                    }
                }

                Transform ph = craftRoot.Find("Craft/ph");
                if (ph == null)
                {
                    GameObject phGo = FindDeep(craftRoot, "ph");
                    if (phGo != null) ph = phGo.transform;
                }
                if (ph != null)
                    bridge.craftPHText = EnsurePhValueText(ph);
            }

            // Hotbar: slot, label 1–6, dan badge stok.
            Transform hotbar = bridge.hotbarRoot != null ? bridge.hotbarRoot.transform : null;
            if (hotbar == null && bridge.canvas != null)
            {
                GameObject found = FindDeep(bridge.canvas.transform, "Hotbar");
                if (found != null)
                    hotbar = found.transform;
            }

            if (hotbar != null)
            {
                List<RectTransform> slots = new List<RectTransform>();
                for (int i = 0; i < hotbar.childCount; i++)
                {
                    RectTransform rt = hotbar.GetChild(i) as RectTransform;
                    if (rt != null && rt.name.StartsWith("Image"))
                        slots.Add(rt);
                }

                slots.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));

                for (int i = 0; i < slots.Count && i < 6; i++)
                {
                    bridge.hotbarSlotRoots[i] = slots[i];

                    Image icon = slots[i].GetComponent<Image>();
                    if (icon != null) bridge.hotbarIcons[i] = icon;

                    Button button = slots[i].GetComponent<Button>();
                    if (button != null) bridge.hotbarButtons[i] = button;

                    Transform key = slots[i].Find("Key_Label");
                    if (key != null)
                        bridge.hotbarKeyLabels[i] = key.GetComponent<TextMeshProUGUI>();

                    Transform badge = slots[i].Find("Count_Badge");
                    if (badge != null)
                        bridge.hotbarCountBadges[i] = badge.GetComponent<TextMeshProUGUI>();
                }
            }

            // Bottom panel: stok 4 bahan dasar.
            if (bridge.bottomPanel != null)
            {
                GameObject baseGo = FindDeep(bridge.bottomPanel.transform, "Base");
                Transform baseRoot = baseGo != null ? baseGo.transform : bridge.bottomPanel.transform;

                Button[] buttons = baseRoot.GetComponentsInChildren<Button>(true);
                List<Button> ingredientButtons = new List<Button>();
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].gameObject.name.Contains("Button"))
                        ingredientButtons.Add(buttons[i]);
                }

                for (int i = 0; i < ingredientButtons.Count && i < 4; i++)
                {
                    TMP_Text tmp = ingredientButtons[i].GetComponentInChildren<TMP_Text>(true);
                    if (tmp != null)
                        bridge.stockDisplayTexts[i] = tmp;
                }
            }
        }

        /// <summary>
        /// Pastikan panel kuali punya teks nilai pH (TextMeshProUGUI Cinzel) di dalam kotak "ph".
        /// Dibuat sekali, lalu referensinya dipakai <see cref="GameplayUIBridge.craftPHText"/>.
        /// </summary>
        static TextMeshProUGUI EnsurePhValueText(Transform ph)
        {
            if (ph == null) return null;

            TextMeshProUGUI existing = ph.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existing != null)
                return existing;

            TextMeshProUGUI text = UITextUtil.CreateText(ph, "PH_Value", 20f);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f);
            rt.offsetMax = new Vector2(-6f, -4f);
            rt.localScale = Vector3.one;

            text.text = "pH: 7.0";
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.93f, 0.85f, 1f);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);

            return text;
        }

        /// <summary>Ganti glyph superscript yang tidak ada di Cinzel dengan tag rich text TMP.</summary>
        static void SanitizeBridgeTexts(GameplayUIBridge bridge)
        {
            if (bridge == null || bridge.ingredientCards == null) return;

            for (int i = 0; i < bridge.ingredientCards.Length; i++)
            {
                IngredientCardData card = bridge.ingredientCards[i];
                if (card == null || string.IsNullOrEmpty(card.ingredientTitle))
                    continue;

                card.ingredientTitle = card.ingredientTitle
                    .Replace("\u207B", "<sup>-</sup>")
                    .Replace("\u207A", "<sup>+</sup>");
            }
        }

        static GameObject FindDeep(Transform root, string name)
        {
            if (root == null) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name == name)
                    return all[i].gameObject;
            }

            return null;
        }
    }
}
