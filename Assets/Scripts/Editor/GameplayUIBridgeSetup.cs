using CuriesCrucible.Alchemy;
using CuriesCrucible.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Auto-setup GameplayUIBridge pada Canvas di SampleScene:
    ///   - Membersihkan komponen "Missing (Mono Script)" di seluruh anak Canvas.
    ///   - Menamb/mengisi komponen GameplayUIBridge + referensi (Canvas, managers).
    ///   - Auto-wire tombol/slider/teks berdasarkan nama/struktur.
    ///   - Memastikan panel Opsi & Pause Menu nonaktif saat mulai.
    ///
    /// Menu: Tools → Curie's Crucible → Setup Gameplay UI Bridge.
    /// </summary>
    public static class GameplayUIBridgeSetup
    {
        const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Curie's Crucible/Setup Gameplay UI Bridge", false, 60)]
        public static void Setup()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Setup Gameplay UI Bridge",
                    "Canvas tidak ditemukan. Pastikan SampleScene sudah dibuka dan Canvas ada di dalamnya.",
                    "OK");
                return;
            }

            // 1) Bersihkan missing script di seluruh anak Canvas.
            int removed = CleanMissingScripts(canvas.gameObject);

            // 2) Tamb / ambil komponen bridge.
            GameplayUIBridge bridge = canvas.GetComponent<GameplayUIBridge>();
            bool created = false;
            if (bridge == null)
            {
                bridge = canvas.gameObject.AddComponent<GameplayUIBridge>();
                created = true;
            }

            // 3) Isi referensi utama.
            bridge.canvas = canvas;
            bridge.alchemy = Object.FindObjectOfType<AlchemyManager>();
            bridge.tower = Object.FindObjectOfType<TowerHealthManager>();
            bridge.waves = Object.FindObjectOfType<WaveManager>();

            // 4) Auto-wire.
            bridge.AutoWire();

            // 5) Panel modal & Craft_panel nonaktif saat mulai.
            if (bridge.craftPanel != null) bridge.craftPanel.SetActive(false);
            if (bridge.optionsPanel != null) bridge.optionsPanel.SetActive(false);
            if (bridge.pauseMenu != null) bridge.pauseMenu.SetActive(false);

            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorSceneManager.SaveScene(canvas.gameObject.scene);
            AssetDatabase.SaveAssets();

            // 6) Laporan.
            string report =
                "Setup Gameplay UI Bridge selesai.\n\n" +
                "Missing script dibersihkan : " + removed + "\n" +
                "Bridge komponen            : " + (created ? "dibuat" : "sudah ada") + "\n" +
                "Panel ditemukan            : " +
                    Has(bridge.bottomPanel) + " Bottom, " +
                    Has(bridge.topPanel) + " Top, " +
                    Has(bridge.craftPanel) + " Craft, " +
                    Has(bridge.hotbarRoot) + " Hotbar, " +
                    Has(bridge.optionsPanel) + " Opsi, " +
                    Has(bridge.pauseMenu) + " Pause\n" +
                "Tombol bahan (0..3)        : " + CountButtons(bridge.ingredientButtons) + "/4\n" +
                "Slot hotbar                : " + CountButtons(bridge.hotbarButtons) + "/6\n" +
                "Slider (Music/SFX)         : " + (bridge.musicSlider != null ? "Y" : "-") + "/" + (bridge.sfxSlider != null ? "Y" : "-") + "\n" +
                "Tombol (cont/opt/close/quit): " +
                    (bridge.continueButton != null ? "Y" : "-") + "/" +
                    (bridge.optionsOpenButton != null ? "Y" : "-") + "/" +
                    (bridge.optionsCloseButton != null ? "Y" : "-") + "/" +
                    (bridge.quitButton != null ? "Y" : "-") + "\n\n" +
                "Jika ada 'tidak ditemukan', atur manual di Inspector komponen GameplayUIBridge pada Canvas.";

            Debug.Log("[UIBridge] " + report.Replace("\n", " | "));
            EditorUtility.DisplayDialog("Setup Gameplay UI Bridge", report, "OK");
        }

        [MenuItem("Tools/Curie's Crucible/Clean Missing Scripts on Canvas", false, 61)]
        public static void CleanOnly()
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Clean Missing Scripts", "Canvas tidak ditemukan.", "OK");
                return;
            }

            int removed = CleanMissingScripts(canvas.gameObject);
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            EditorUtility.DisplayDialog("Clean Missing Scripts",
                "Komponen hilang dibersihkan: " + removed, "OK");
        }

        // ------------------------------------------------------------------

        static int CleanMissingScripts(GameObject root)
        {
            int removed = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(all[i].gameObject);
            return removed;
        }

        static Canvas FindCanvas()
        {
            Canvas[] all = Object.FindObjectsOfType<Canvas>(true);
            if (all != null && all.Length > 0)
                return all[0];

            // Coba buka SampleScene bila belum ada Canvas di scene aktif.
            if (System.IO.File.Exists(SampleScenePath))
            {
                EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
                all = Object.FindObjectsOfType<Canvas>(true);
                if (all != null && all.Length > 0)
                    return all[0];
            }

            return null;
        }

        static bool Has(Object o) { return o != null; }

        static int CountButtons(Button[] arr)
        {
            if (arr == null) return 0;
            int c = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i] != null) c++;
            return c;
        }
    }
}