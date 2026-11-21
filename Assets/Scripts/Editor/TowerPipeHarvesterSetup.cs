using System.IO;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Menu: Tools → Curie's Crucible → 6. Setup Tower Pipe Harvester
    ///
    /// Menyiapkan sistem interaktif panen bahan pipa menara laboratorium:
    ///   - Menghasilkan sprite speech bubble Victorian-steampunk jika belum ada.
    ///   - Memasang komponen TowerPipeHarvester pada Arena_25D.
    ///   - Membuat 4 titik anchor pipa dengan gelembung visual dan collider interaktif.
    ///   - Menghubungkan ke AlchemyManager dan Camera.main.
    ///   - Menandai scene dirty & menyimpan.
    /// </summary>
    public static class TowerPipeHarvesterSetup
    {
        const string ArtFolder = "Assets/Art/Arena25D";
        const string BubbleSpriteName = "HarvestBubble.png";
        const string IconIngredientPath = "Assets/Art/UI/IconIngredient.png";

        [MenuItem("Tools/Curie's Crucible/6. Setup Tower Pipe Harvester", false, 60)]
        public static void SetupHarvester()
        {
            // 1. Pastikan sprite gelembung tersedia
            Sprite bubbleSprite = EnsureBubbleSprite();
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconIngredientPath);

            // 2. Cari Arena_25D di scene aktif
            GameObject arenaGo = GameObject.Find("Arena_25D");
            if (arenaGo == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup Tower Pipe Harvester",
                    "GameObject 'Arena_25D' tidak ditemukan di scene saat ini!\n\n" +
                    "Jalankan menu berikut terlebih dahulu:\n" +
                    "Tools → Curie's Crucible → 1. Generate 2.5D Arena Hierarchy",
                    "OK");
                return;
            }

            // 3. Tambahkan atau ambil komponen TowerPipeHarvester pada Arena_25D
            TowerPipeHarvester harvester = arenaGo.GetComponent<TowerPipeHarvester>();
            if (harvester == null)
            {
                harvester = Undo.AddComponent<TowerPipeHarvester>(arenaGo);
            }

            // Hubungkan referensi
            harvester.alchemy = Object.FindObjectOfType<AlchemyManager>();
            harvester.arenaCamera = Camera.main;
            harvester.productionInterval = 7.0f;
            harvester.maxStockPerIngredient = 6;
            harvester.bubbleSortingOrder = 600;

            // 4. Cari atau buat container Tower_Harvest_Pipes
            Transform pipesContainer = arenaGo.transform.Find("Tower_Harvest_Pipes");
            if (pipesContainer == null)
            {
                GameObject pcGo = new GameObject("Tower_Harvest_Pipes");
                Undo.RegisterCreatedObjectUndo(pcGo, "Create Tower_Harvest_Pipes");
                pipesContainer = pcGo.transform;
                pipesContainer.SetParent(arenaGo.transform, false);
                pipesContainer.localPosition = Vector3.zero;
            }

            // 5. Konfigurasi 4 titik pipa laboratorium
            // Layout 4 pipa di sekitar menara:
            //   - Kiri Atas  : X = -1.75, Y = 1.90 (Basa)
            //   - Kiri Bawah : X = -1.75, Y = -0.60 (Pendingin)
            //   - Kanan Atas : X =  1.75, Y = 1.90 (Asam)
            //   - Kanan Bawah: X =  1.75, Y = -0.60 (Katalis)
            var pipeConfigs = new[]
            {
                new {
                    name = "Pipe_TopLeft_Base",
                    label = "Pipa Kiri Atas (Basa)",
                    pos = new Vector3(-1.75f, 1.90f, 0f),
                    type = IngredientType.BahanBasa_Biru
                },
                new {
                    name = "Pipe_BottomLeft_Coolant",
                    label = "Pipa Kiri Bawah (Pendingin)",
                    pos = new Vector3(-1.75f, -0.60f, 0f),
                    type = IngredientType.BahanPendingin_Cyan
                },
                new {
                    name = "Pipe_TopRight_Acid",
                    label = "Pipa Kanan Atas (Asam)",
                    pos = new Vector3(1.75f, 1.90f, 0f),
                    type = IngredientType.BahanAsam_Merah
                },
                new {
                    name = "Pipe_BottomRight_Catalyst",
                    label = "Pipa Kanan Bawah (Katalis)",
                    pos = new Vector3(1.75f, -0.60f, 0f),
                    type = IngredientType.BahanKatalis_Kuning
                }
            };

            harvester.pipes.Clear();

            for (int i = 0; i < pipeConfigs.Length; i++)
            {
                var cfg = pipeConfigs[i];

                Transform pipeAnchor = pipesContainer.Find(cfg.name);
                if (pipeAnchor == null)
                {
                    GameObject pGo = new GameObject(cfg.name);
                    Undo.RegisterCreatedObjectUndo(pGo, "Create " + cfg.name);
                    pipeAnchor = pGo.transform;
                    pipeAnchor.SetParent(pipesContainer, false);
                    pipeAnchor.localPosition = cfg.pos;
                }

                // Buat atau cari Bubble_Visual
                Transform bubbleRoot = pipeAnchor.Find("Bubble_Visual");
                if (bubbleRoot == null)
                {
                    GameObject bGo = new GameObject("Bubble_Visual");
                    Undo.RegisterCreatedObjectUndo(bGo, "Create Bubble_Visual");
                    bubbleRoot = bGo.transform;
                    bubbleRoot.SetParent(pipeAnchor, false);
                    bubbleRoot.localPosition = new Vector3(0f, 0.65f, 0f);
                }

                // Background SpriteRenderer
                SpriteRenderer bgRenderer = bubbleRoot.GetComponent<SpriteRenderer>();
                if (bgRenderer == null)
                    bgRenderer = Undo.AddComponent<SpriteRenderer>(bubbleRoot.gameObject);

                bgRenderer.sprite = bubbleSprite;
                bgRenderer.sortingOrder = harvester.bubbleSortingOrder;
                bgRenderer.color = Color.white;

                // Collider untuk deteksi klik
                CircleCollider2D col = bubbleRoot.GetComponent<CircleCollider2D>();
                if (col == null)
                    col = Undo.AddComponent<CircleCollider2D>(bubbleRoot.gameObject);

                col.radius = 0.45f;
                col.isTrigger = true;

                // Icon child
                Transform iconTrans = bubbleRoot.Find("Bubble_Icon");
                if (iconTrans == null)
                {
                    GameObject iconGo = new GameObject("Bubble_Icon");
                    Undo.RegisterCreatedObjectUndo(iconGo, "Create Bubble_Icon");
                    iconTrans = iconGo.transform;
                    iconTrans.SetParent(bubbleRoot, false);
                    iconTrans.localPosition = new Vector3(0f, 0.08f, 0f); // sedikit di atas tail
                    iconTrans.localScale = Vector3.one * 0.70f;
                }

                SpriteRenderer iconRenderer = iconTrans.GetComponent<SpriteRenderer>();
                if (iconRenderer == null)
                    iconRenderer = Undo.AddComponent<SpriteRenderer>(iconTrans.gameObject);

                iconRenderer.sprite = iconSprite;
                iconRenderer.sortingOrder = harvester.bubbleSortingOrder + 1;
                iconRenderer.color = AlchemyDatabase.IngredientColor(cfg.type);

                // Buat entry HarvestPipe
                var pipeEntry = new TowerPipeHarvester.HarvestPipe
                {
                    pipeName = cfg.label,
                    pipeAnchor = pipeAnchor,
                    ingredientType = cfg.type,
                    bubbleRoot = bubbleRoot.gameObject,
                    bubbleTransform = bubbleRoot,
                    backgroundRenderer = bgRenderer,
                    iconRenderer = iconRenderer,
                    bubbleCollider = col,
                    currentTimer = 0f,
                    isBubbleActive = false,
                    initialLocalPos = bubbleRoot.localPosition,
                    animPhaseOffset = i * 1.57f
                };

                // Di awal permainan, sembunyikan gelembung hingga timer penyulingan selesai
                bubbleRoot.gameObject.SetActive(false);

                harvester.pipes.Add(pipeEntry);
            }

            EditorUtility.SetDirty(harvester);
            EditorUtility.SetDirty(arenaGo);
            EditorSceneManager.MarkSceneDirty(arenaGo.scene);

            AssetDatabase.SaveAssets();

            string report =
                "Setup Tower Pipe Harvester Berhasil!\n\n" +
                "• Komponen TowerPipeHarvester dipasang pada 'Arena_25D'.\n" +
                "• 4 Titik Pipa Menara dibuat dengan Gizmo berwarna di Scene View:\n" +
                "   1. Kiri Atas  : Bahan Basa (Biru)\n" +
                "   2. Kiri Bawah : Bahan Pendingin (Cyan)\n" +
                "   3. Kanan Atas : Bahan Asam (Merah)\n" +
                "   4. Kanan Bawah: Bahan Katalis (Kuning)\n\n" +
                "• Gelembung speech bubble (Sorting Order = 600, Collider2D, Animasi Bobbing + Pulse) siap beroperasi.\n" +
                "• Proteksi lemparan ArenaThrowTargeting aktif otomatis.";

            EditorUtility.DisplayDialog("Setup Tower Pipe Harvester", report, "OK");
        }

        // ==================================================================
        // Prosedural Speech Bubble PNG Generator
        // ==================================================================

        public static Sprite EnsureBubbleSprite(bool forceOverwrite = false)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");

            if (!AssetDatabase.IsValidFolder(ArtFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Arena25D");

            string fullPath = Path.Combine(ArtFolder, BubbleSpriteName).Replace("\\", "/");

            if (!forceOverwrite && File.Exists(fullPath))
            {
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
                if (existing != null)
                    return existing;
            }

            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color clear = new Color(0, 0, 0, 0);
            Color brassOuter = new Color(0.86f, 0.68f, 0.28f, 1f);      // Emas kuningan steampunk
            Color brassDark = new Color(0.48f, 0.35f, 0.12f, 1f);       // Shading kuningan
            Color glassBase = new Color(0.12f, 0.18f, 0.26f, 0.88f);    // Kaca laboratorium gelap/transparan
            Color glassHighlight = new Color(1f, 1f, 1f, 0.45f);        // Kilau pantulan cahaya

            Vector2 circleCenter = new Vector2(64f, 72f);
            float circleRadius = 46f;
            float borderWidth = 4.5f;

            // Gambar speech bubble dengan ekor runcing ke bawah
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pt = new Vector2(x + 0.5f, y + 0.5f);
                    float distCenter = Vector2.Distance(pt, circleCenter);

                    // Bentuk segitiga tail di bagian bawah
                    bool insideTail = false;
                    float tailDist = 999f;
                    if (y >= 6 && y <= 40)
                    {
                        float tailProgress = (float)(y - 6) / 34f; // 0 di ujung runcing, 1 di lingkaran
                        float halfW = Mathf.Lerp(1.5f, 18f, tailProgress);
                        if (Mathf.Abs(x - 64f) <= halfW)
                        {
                            insideTail = true;
                            tailDist = Mathf.Abs(x - 64f) / Mathf.Max(0.1f, halfW);
                        }
                    }

                    Color c = clear;

                    bool inCircle = distCenter <= circleRadius;
                    bool onCircleBorder = distCenter <= circleRadius && distCenter >= (circleRadius - borderWidth);
                    bool onTailBorder = insideTail && (tailDist >= 0.72f || y <= 10);

                    if (onCircleBorder || onTailBorder)
                    {
                        // Rim / border kuningan
                        float shade = Mathf.Clamp01((y - 20f) / 90f);
                        c = Color.Lerp(brassDark, brassOuter, shade);
                    }
                    else if (inCircle || insideTail)
                    {
                        // Isi kaca
                        float glassGradient = Mathf.Clamp01((y - 30f) / 80f);
                        c = Color.Lerp(glassBase, new Color(glassBase.r * 1.3f, glassBase.g * 1.3f, glassBase.b * 1.4f, 0.92f), glassGradient);

                        // Highlight specular bulan sabit di kiri atas
                        Vector2 highlightPos = new Vector2(48f, 92f);
                        float distH = Vector2.Distance(pt, highlightPos);
                        if (distH <= 16f)
                        {
                            float hAlpha = (1f - (distH / 16f)) * 0.45f;
                            c = Color.Lerp(c, glassHighlight, hAlpha);
                        }
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;

                // Pivot di tengah gelembung
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.42f);
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        }
    }
}
