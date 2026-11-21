using System.IO;
using UnityEditor;
using UnityEngine;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Menghasilkan sprite procedural bergaya Victorian-Steampunk Brass & Wood
    /// dan menyimpannya sebagai file PNG di Assets/Art/UI/.
    /// </summary>
    public static class UISpriteAssetGenerator
    {
        public const string ArtFolder = "Assets/Art/UI";

        public static void EnsureAllSprites(bool forceOverwrite = false)
        {
            if (!Directory.Exists(ArtFolder))
                Directory.CreateDirectory(ArtFolder);

            CreatePanelWoodBrass(forceOverwrite);
            CreatePanelParchment(forceOverwrite);
            CreateButtonBrass(forceOverwrite);
            CreateButtonRoundBrass(forceOverwrite);
            CreateSlotFrame(forceOverwrite);
            CreateSlotHighlight(forceOverwrite);
            CreatePHBarGradient(forceOverwrite);
            CreateNeedleBrass(forceOverwrite);
            CreateIconPotion(forceOverwrite);
            CreateIconIngredient(forceOverwrite);
            CreateIconInfo(forceOverwrite);
            CreateIconPause(forceOverwrite);
            CreateIconTower(forceOverwrite);
            CreateIconCrafting(forceOverwrite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void SaveAndImportSprite(Texture2D tex, string fileName, Vector4 border, bool forceOverwrite)
        {
            string path = Path.Combine(ArtFolder, fileName).Replace("\\", "/");

            if (!forceOverwrite && File.Exists(path))
            {
                // Pastikan TextureImporter sudah bertipe Sprite dan isReadable
                TextureImporter existingImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (existingImporter != null &&
                    (existingImporter.textureType != TextureImporterType.Sprite || !existingImporter.isReadable))
                {
                    existingImporter.textureType = TextureImporterType.Sprite;
                    existingImporter.spriteImportMode = SpriteImportMode.Single;
                    existingImporter.spritePixelsPerUnit = 100f;
                    existingImporter.isReadable = true;
                    existingImporter.spriteBorder = border;
                    existingImporter.SaveAndReimport();
                }
                return;
            }

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.spriteBorder = border; // Left, Bottom, Right, Top
                importer.SaveAndReimport();
            }
        }

        static void CreatePanelWoodBrass(bool force)
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color brassEdge = new Color(0.85f, 0.68f, 0.32f);
            Color brassShadow = new Color(0.42f, 0.30f, 0.10f);
            Color woodBase = new Color(0.16f, 0.11f, 0.07f, 0.95f);
            Color woodGrain = new Color(0.22f, 0.15f, 0.10f, 0.95f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int distEdge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(size - 1 - x, size - 1 - y)));
                    Color c;

                    if (distEdge < 2)
                    {
                        c = brassShadow;
                    }
                    else if (distEdge < 7)
                    {
                        c = (x + y) % 4 == 0 ? brassEdge * 1.1f : brassEdge;
                    }
                    else if (distEdge < 10)
                    {
                        c = brassShadow * 0.8f;
                    }
                    else if (distEdge < 12)
                    {
                        c = brassEdge * 0.9f;
                    }
                    else
                    {
                        float grain = Mathf.Sin(y * 0.35f + Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 4f);
                        c = Color.Lerp(woodBase, woodGrain, Mathf.Clamp01(grain * 0.5f + 0.5f));
                    }

                    bool nearCorner = (x >= 8 && x <= 16 && y >= 8 && y <= 16) ||
                                      (x >= size - 17 && x <= size - 9 && y >= 8 && y <= 16) ||
                                      (x >= 8 && x <= 16 && y >= size - 17 && y <= size - 9) ||
                                      (x >= size - 17 && x <= size - 9 && y >= size - 17 && y <= size - 9);

                    if (nearCorner)
                    {
                        int cx = (x < size / 2) ? 12 : size - 13;
                        int cy = (y < size / 2) ? 12 : size - 13;
                        float r = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        if (r <= 3.8f)
                        {
                            c = (r < 2.2f) ? new Color(0.98f, 0.85f, 0.45f) : brassShadow;
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "PanelWoodBrass.png", new Vector4(20, 20, 20, 20), force);
        }

        static void CreatePanelParchment(bool force)
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color parchmentBase = new Color(0.95f, 0.91f, 0.83f, 0.98f);
            Color parchmentGrain = new Color(0.88f, 0.82f, 0.70f, 0.98f);
            Color brassTrim = new Color(0.65f, 0.50f, 0.25f);
            Color brassInner = new Color(0.80f, 0.65f, 0.35f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int distEdge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(size - 1 - x, size - 1 - y)));
                    Color c;

                    if (distEdge < 2)
                    {
                        c = brassTrim;
                    }
                    else if (distEdge == 4 || distEdge == 5)
                    {
                        c = brassInner;
                    }
                    else
                    {
                        float noise = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                        c = Color.Lerp(parchmentBase, parchmentGrain, noise * 0.6f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "PanelParchment.png", new Vector4(16, 16, 16, 16), force);
        }

        static void CreateButtonBrass(bool force)
        {
            int w = 96;
            int h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color highlight = new Color(0.98f, 0.85f, 0.50f);
            Color mid = new Color(0.78f, 0.60f, 0.25f);
            Color shadow = new Color(0.40f, 0.28f, 0.10f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int distEdge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(w - 1 - x, h - 1 - y)));
                    Color c;

                    if (distEdge < 2)
                    {
                        c = shadow;
                    }
                    else if (distEdge < 5)
                    {
                        c = (y > h / 2) ? highlight : shadow;
                    }
                    else
                    {
                        float v = (float)y / h;
                        c = Color.Lerp(shadow * 1.2f, mid * 1.15f, v);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "ButtonBrass.png", new Vector4(12, 12, 12, 12), force);
        }

        static void CreateButtonRoundBrass(bool force)
        {
            int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color outerRing = new Color(0.96f, 0.82f, 0.44f);
            Color shadowRing = new Color(0.45f, 0.32f, 0.12f);
            Color innerBase = new Color(0.24f, 0.16f, 0.08f, 0.95f);
            Color centerGem = new Color(0.85f, 0.65f, 0.25f);

            float cx = size * 0.5f;
            float cy = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    Color c = clear;

                    if (dist <= 46f)
                    {
                        if (dist >= 40f)
                            c = (y > cy) ? outerRing : shadowRing;
                        else if (dist >= 36f)
                            c = shadowRing;
                        else if (dist >= 32f)
                            c = outerRing * 0.9f;
                        else
                        {
                            float t = Mathf.Clamp01(dist / 32f);
                            c = Color.Lerp(centerGem, innerBase, t);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "ButtonRoundBrass.png", Vector4.zero, force);
        }

        static void CreateSlotFrame(bool force)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color border = new Color(0.70f, 0.52f, 0.22f);
            Color innerShadow = new Color(0.10f, 0.07f, 0.04f, 0.95f);
            Color bg = new Color(0.18f, 0.13f, 0.08f, 0.90f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dist = Mathf.Min(x, Mathf.Min(y, Mathf.Min(size - 1 - x, size - 1 - y)));
                    Color c;

                    if (dist < 3)
                        c = border;
                    else if (dist < 6)
                        c = innerShadow;
                    else
                        c = bg;

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "SlotFrame.png", new Vector4(8, 8, 8, 8), force);
        }

        static void CreateSlotHighlight(bool force)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color goldGlow = new Color(1f, 0.88f, 0.25f, 1f);
            Color softGlow = new Color(1f, 0.95f, 0.50f, 0.6f);
            Color transparent = new Color(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dist = Mathf.Min(x, Mathf.Min(y, Mathf.Min(size - 1 - x, size - 1 - y)));
                    Color c;

                    if (dist < 2)
                        c = softGlow;
                    else if (dist < 6)
                        c = goldGlow;
                    else if (dist < 8)
                        c = softGlow;
                    else
                        c = transparent;

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "SlotHighlight.png", new Vector4(10, 10, 10, 10), force);
        }

        static void CreatePHBarGradient(bool force)
        {
            int w = 256;
            int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            Color colAcid = new Color(0.92f, 0.18f, 0.18f);   // Merah Asam
            Color colWarm = new Color(0.95f, 0.65f, 0.15f);   // Transisi Hangat
            Color colNeutral = new Color(0.20f, 0.82f, 0.35f);// Hijau Netral
            Color colCyan = new Color(0.20f, 0.80f, 0.90f);   // Transisi Sejuk
            Color colBase = new Color(0.22f, 0.45f, 0.98f);   // Biru Basa
            Color border = new Color(0.85f, 0.70f, 0.35f);    // Kuningan

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y == 0 || y == h - 1 || x == 0 || x == w - 1)
                    {
                        tex.SetPixel(x, y, border);
                        continue;
                    }

                    float t = (float)x / (w - 1);
                    Color fill;

                    if (t < 0.25f)
                        fill = Color.Lerp(colAcid, colWarm, t / 0.25f);
                    else if (t < 0.50f)
                        fill = Color.Lerp(colWarm, colNeutral, (t - 0.25f) / 0.25f);
                    else if (t < 0.75f)
                        fill = Color.Lerp(colNeutral, colCyan, (t - 0.50f) / 0.25f);
                    else
                        fill = Color.Lerp(colCyan, colBase, (t - 0.75f) / 0.25f);

                    float v = (float)y / h;
                    if (v > 0.7f) fill = Color.Lerp(fill, Color.white, 0.25f);
                    if (v < 0.3f) fill = Color.Lerp(fill, Color.black, 0.20f);

                    tex.SetPixel(x, y, fill);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "PHBarGradient.png", new Vector4(2, 2, 2, 2), force);
        }

        static void CreateNeedleBrass(bool force)
        {
            int w = 32;
            int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color brass = new Color(0.96f, 0.84f, 0.45f);
            Color brassDark = new Color(0.45f, 0.32f, 0.12f);
            Color ruby = new Color(0.95f, 0.25f, 0.25f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = w * 0.5f;
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    Color c = clear;

                    if (y >= 16)
                    {
                        float maxHalfW = Mathf.Lerp(8f, 2f, (float)(y - 16) / (h - 16));
                        if (dx <= maxHalfW)
                        {
                            c = (x < cx) ? brass : brassDark;
                        }
                    }
                    else
                    {
                        float maxHalfW = Mathf.Lerp(0f, 8f, (float)y / 16f);
                        if (dx <= maxHalfW)
                        {
                            c = (dx <= 2f && y <= 6) ? ruby : (x < cx ? brass : brassDark);
                        }
                    }

                    float circleDist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, 50f));
                    if (circleDist <= 8f)
                    {
                        c = circleDist <= 4f ? ruby : (circleDist <= 7f ? brass : brassDark);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "NeedleBrass.png", Vector4.zero, force);
        }

        static void CreateIconPotion(bool force)
        {
            int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color glass = new Color(0.90f, 0.95f, 1f, 0.85f);
            Color liquid = new Color(0.40f, 0.85f, 0.40f, 0.95f);
            Color cork = new Color(0.70f, 0.45f, 0.25f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = size * 0.5f;
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    Color c = clear;

                    if (y >= 38 && y <= 44 && dx <= 5f)
                    {
                        c = cork;
                    }
                    else if (y >= 28 && y < 38 && dx <= 4f)
                    {
                        c = glass;
                    }
                    else if (y >= 6 && y < 28)
                    {
                        float maxW = Mathf.Lerp(18f, 5f, (float)(y - 6) / 22f);
                        if (dx <= maxW)
                        {
                            if (dx >= maxW - 2f || y <= 8)
                                c = glass;
                            else if (y <= 20)
                                c = liquid;
                            else
                                c = new Color(glass.r, glass.g, glass.b, 0.35f);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconPotion.png", Vector4.zero, force);
        }

        static void CreateIconIngredient(bool force)
        {
            int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color edge = new Color(0.95f, 0.90f, 0.55f);
            Color fill = new Color(0.75f, 0.65f, 0.30f, 0.9f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = size * 0.5f;
                    float cy = size * 0.5f;
                    float dist = Mathf.Abs(x - cx) + Mathf.Abs(y - cy);
                    Color c = clear;

                    if (dist <= 18f)
                    {
                        if (dist >= 15f)
                            c = edge;
                        else
                            c = fill;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconIngredient.png", Vector4.zero, force);
        }

        static void CreateIconInfo(bool force)
        {
            int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color ring = new Color(0.92f, 0.78f, 0.35f);
            Color bg = new Color(0.20f, 0.14f, 0.08f, 0.95f);
            Color white = Color.white;

            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    Color p = clear;

                    if (r <= 21f)
                    {
                        if (r >= 17f)
                            p = ring;
                        else
                            p = bg;
                    }

                    if (x >= c - 2 && x <= c + 2 && y >= 28 && y <= 32)
                        p = white;

                    if (x >= c - 2 && x <= c + 2 && y >= 14 && y <= 24)
                        p = white;

                    if (y == 24 && x >= c - 4 && x <= c + 1) p = white;
                    if (y == 14 && x >= c - 4 && x <= c + 4) p = white;

                    tex.SetPixel(x, y, p);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconInfo.png", Vector4.zero, force);
        }

        static void CreateIconPause(bool force)
        {
            int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color ring = new Color(0.92f, 0.78f, 0.35f);
            Color bg = new Color(0.20f, 0.14f, 0.08f, 0.95f);
            Color bar = new Color(0.98f, 0.88f, 0.50f);

            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                    Color p = clear;

                    if (r <= 21f)
                    {
                        if (r >= 17f)
                            p = ring;
                        else
                            p = bg;
                    }

                    if (x >= 16 && x <= 19 && y >= 15 && y <= 33)
                        p = bar;

                    if (x >= 28 && x <= 31 && y >= 15 && y <= 33)
                        p = bar;

                    tex.SetPixel(x, y, p);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconPause.png", Vector4.zero, force);
        }

        static void CreateIconTower(bool force)
        {
            int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color brass = new Color(0.92f, 0.78f, 0.35f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = size * 0.5f;
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    Color c = clear;

                    if (y >= 36 && y <= 42)
                    {
                        if (dx <= 14f && !(dx >= 3f && dx <= 7f && y >= 39))
                            c = brass;
                    }
                    else if (y >= 10 && y < 36)
                    {
                        float maxW = Mathf.Lerp(12f, 10f, (float)(y - 10) / 26f);
                        if (dx <= maxW)
                            c = brass;
                    }
                    else if (y >= 4 && y < 10)
                    {
                        if (dx <= 16f)
                            c = brass;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconTower.png", Vector4.zero, force);
        }

        static void CreateIconCrafting(bool force)
        {
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color clear = new Color(0, 0, 0, 0);
            Color brass = new Color(0.98f, 0.85f, 0.45f);
            Color cauldron = new Color(0.30f, 0.22f, 0.15f);
            Color potionBubbles = new Color(0.35f, 0.88f, 0.45f);

            float cx = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    Color c = clear;

                    // Cauldron bowl: y 14..36
                    if (y >= 14 && y <= 36)
                    {
                        float r = Mathf.Sin((float)(y - 14) / 22f * Mathf.PI) * 18f + 4f;
                        if (dx <= r)
                        {
                            if (dx >= r - 3f || y <= 16)
                                c = brass;
                            else
                                c = cauldron;
                        }
                    }
                    // Cauldron rim: y 36..40
                    else if (y > 36 && y <= 40 && dx <= 22f)
                    {
                        c = brass;
                    }
                    // Bubbles on top: y 41..48
                    else if (y > 40 && y <= 48 && dx <= 16f)
                    {
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(cx - 6, 44)) <= 4f ||
                            Vector2.Distance(new Vector2(x, y), new Vector2(cx + 6, 45)) <= 5f ||
                            Vector2.Distance(new Vector2(x, y), new Vector2(cx, 47)) <= 3f)
                        {
                            c = potionBubbles;
                        }
                    }
                    // Cauldron feet: y 8..14
                    else if (y >= 8 && y < 14)
                    {
                        if ((dx >= 10f && dx <= 16f))
                            c = brass;
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            SaveAndImportSprite(tex, "IconCrafting.png", Vector4.zero, force);
        }
    }
}
