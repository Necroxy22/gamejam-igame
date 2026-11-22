using CuriesCrucible.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Generator permanen Laci Resep (Recipe_Drawer) + Kartu Bahan Dasar ke SampleScene.
    ///
    /// Menjalankan builder runtime lewat API publik <see cref="GameplayUIBridge"/> sehingga
    /// hasilnya SAMA persis dengan yang dibangun saat Play Mode, lalu menyimpannya ke scene.
    /// Setelah dijalankan, semua objek bisa diklik di Hierarchy, Source Image diganti PNG,
    /// dan teks TextMeshProUGUI diedit langsung — tanpa mengubah kode.
    ///
    /// Menu: Tools → Curie's Crucible → Generate Recipe Drawer UI (SampleScene).
    /// </summary>
    public static class RecipeDrawerSetup
    {
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Curie's Crucible/Generate Recipe Drawer UI (SampleScene)", false, 62)]
        public static void Generate()
        {
            GenerateInternal(true);
        }

        /// <summary>
        /// Entry point batch (tanpa dialog) untuk CI / command line:
        ///   Unity.exe -batchmode -quit -projectPath "D:\tes" \
        ///     -executeMethod CuriesCrucible.EditorTools.RecipeDrawerSetup.GenerateBatch
        /// </summary>
        public static void GenerateBatch()
        {
            GenerateInternal(false);
        }

        static void GenerateInternal(bool showDialogs)
        {
            if (!EnsureSampleSceneOpen())
            {
                Fail("Tidak bisa membuka " + SampleScenePath + ". Buka SampleScene manual lalu jalankan lagi.", showDialogs);
                return;
            }

            Canvas canvas = FindGameplayCanvas();
            if (canvas == null)
            {
                Fail("Canvas dengan GameplayUIBridge tidak ditemukan di SampleScene.", showDialogs);
                return;
            }

            GameplayUIBridge bridge = canvas.GetComponent<GameplayUIBridge>();
            if (bridge == null)
            {
                Fail("Komponen GameplayUIBridge tidak ada di Canvas. Jalankan dulu 'Setup Gameplay UI Bridge'.", showDialogs);
                return;
            }

            // 1) Pastikan referensi manager & panel terisi (tanpa mengubah input bridge).
            bridge.canvas = canvas;
            if (bridge.alchemy == null) bridge.alchemy = Object.FindObjectOfType<CuriesCrucible.Alchemy.AlchemyManager>();
            if (bridge.tower == null) bridge.tower = Object.FindObjectOfType<TowerHealthManager>();
            if (bridge.waves == null) bridge.waves = Object.FindObjectOfType<WaveManager>();

            if (bridge.craftPanel == null)
            {
                Transform found = FindDeep(canvas.transform, "Craft_panel");
                if (found != null) bridge.craftPanel = found.gameObject;
            }

            if (bridge.craftPanel == null)
            {
                Fail("Objek 'Craft_panel' tidak ditemukan di bawah Canvas.", showDialogs);
                return;
            }

            // 2) Bangun kartu bahan + laci resep permanen.
            bridge.EditorBuildIngredientCardsPermanently();
            bridge.EditorBuildRecipeDrawerPermanently();
            bridge.SyncRecipeDrawerFromConfig();

            // 3) Simpan ke scene.
            EditorUtility.SetDirty(bridge);
            if (bridge.craftPanel != null)
                EditorUtility.SetDirty(bridge.craftPanel);

            var scene = canvas.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // 4) Laporan.
            int cards = CountChildren(bridge.craftPanel.transform, "Recipe_Drawer");
            string report =
                "Laci Resep berhasil dibuat permanen di SampleScene.\n\n" +
                "Lokasi : Canvas → Craft_panel → Recipe_Drawer_Window → Recipe_Drawer\n" +
                "Kartu  : 6 Recipe_Card_0..5 + 4 Card_<Bahan>\n" +
                "Objek laci ditemukan: " + cards + "\n\n" +
                "Langkah kustomisasi:\n" +
                "• Klik objek kartu di Hierarchy → ganti Source Image bingkai (PNG).\n" +
                "• Edit teks TextMeshProUGUI langsung di Inspector.\n" +
                "• Atur teks/sprite global di komponen GameplayUIBridge (group 'Laci Resep').";

            Debug.Log("[RecipeDrawer] " + report.Replace("\n", " | "));
            if (showDialogs)
                EditorUtility.DisplayDialog("Generate Recipe Drawer UI", report, "OK");
        }

        /// <summary>Laporkan kegagalan: dialog di Editor interaktif, error log di batchmode.</summary>
        static void Fail(string message, bool showDialogs)
        {
            Debug.LogError("[RecipeDrawer] " + message);
            if (showDialogs)
                EditorUtility.DisplayDialog("Generate Recipe Drawer UI", message, "OK");
            else if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        // ------------------------------------------------------------------

        static bool EnsureSampleSceneOpen()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == SampleScenePath)
                return true;

            if (!System.IO.File.Exists(SampleScenePath))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            return true;
        }

        static Canvas FindGameplayCanvas()
        {
            Canvas[] all = Object.FindObjectsOfType<Canvas>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetComponent<GameplayUIBridge>() != null)
                    return all[i];
            }
            return all.Length > 0 ? all[0] : null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        static int CountChildren(Transform root, string name)
        {
            int count = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    count++;
            }
            return count;
        }
    }
}
