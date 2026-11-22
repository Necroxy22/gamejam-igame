using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// Jembatan antara Canvas UI HUD minimalis dan logika gameplay (AlchemyManager,
    /// TowerHealthManager, WaveManager).
    ///
    /// Desain HUD Minimalis:
    ///   1. Pojok Kiri Bawah (Bottom_panel/Base):
    ///      Display jumlah stok 4 bahan dasar (Basa/Biru, Pendingin/Cyan, Asam/Merah, Katalis/Kuning)
    ///      dalam format "<jumlah>x" (contoh: 3x, 1x, 0x). Murni display, tidak dipakai untuk klik racik.
    ///
    ///   2. Tengah Bawah (Hotbar — 6 Slot Tetap):
    ///      Image (1) s/d Image (6) diurutkan dari kiri ke kanan berdasarkan anchoredPosition.x.
    ///      Setiap slot mewakili 1 dari 6 jenis ramuan campuran secara tetap:
    ///        - Label Shortcut Keyboard (1–6) di tengah atas slot.
    ///        - Badge Jumlah Ramuan ("0x", "1x", "2x") di pojok samping slot.
    ///        - Highlight visual (skala 1.15x + warna terang) pada slot aktif (1–6).
    ///        - Tombol R atau klik tombol lingkaran tengah Hotbar meracik 1x resep slot aktif.
    ///        - Klik kiri di jalanan arena melempar ramuan dari slot aktif (jika stok >= 1x).
    ///
    ///   3. Proteksi & Kompatibilitas:
    ///      Klik pada Hotbar tidak tembus melempar ramuan ke jalanan (IsPointerOverHotbar).
    ///      Tanpa Coroutine, 100% kompatibel WebGL, 0 error / 0 warning.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayUIBridge : MonoBehaviour
    {
        public static GameplayUIBridge Instance { get; private set; }

        [Header("Referensi (auto / manual)")]
        public AlchemyManager alchemy;
        public TowerHealthManager tower;
        public WaveManager waves;
        public Canvas canvas;

        [Header("Panel")]
        public GameObject bottomPanel;
        public GameObject topPanel;
        public GameObject craftPanel;
        public GameObject hotbarRoot;
        public GameObject optionsPanel;
        public GameObject pauseMenu;
        public GameObject recipePanel;

        [Header("Bottom — Display Stok 4 Bahan Dasar")]
        [Tooltip("4 elemen display stok bahan (0=Basa/Biru, 1=Pendingin/Cyan, 2=Asam/Merah, 3=Katalis/Kuning).")]
        public Component[] stockDisplayTexts = new Component[4];
        public Button[] ingredientButtons = new Button[4];

        [Header("Hotbar — 6 Slot Tetap Ramuan")]
        [Tooltip("6 slot ramuan berurutan dari kiri ke kanan berdasarkan posisi X.")]
        public RectTransform[] hotbarSlotRoots = new RectTransform[6];
        public Image[] hotbarIcons = new Image[6];
        public Button[] hotbarButtons = new Button[6];
        public TMPro.TextMeshProUGUI[] hotbarKeyLabels = new TMPro.TextMeshProUGUI[6];
        public TMPro.TextMeshProUGUI[] hotbarCountBadges = new TMPro.TextMeshProUGUI[6];

        [Tooltip("Tombol lingkaran tengah Hotbar untuk meracik ramuan slot aktif.")]
        public Button centerBrewButton;

        [Header("Crafting Panel Components")]
        public Button craftCloseButton;
        public Button craftBrewButton;    // Simpan
        public Button craftClearButton;   // Ulang / Clear
        public Image craftSlot1Image;
        public Image craftSlot2Image;
        public Image craftResultImage;
        public Component craftPHText;

        [Header("Crafting Panel — Panel Stok 4 Bahan Dasar (Kiri)")]
        public TMPro.TMP_Text[] craftStockBadgeTexts = new TMPro.TMP_Text[4];
        public Image[] craftStockIconImages = new Image[4];
        public CraftIngredientDraggable[] craftStockDraggables = new CraftIngredientDraggable[4];

        [Header("Top — Status")]
        public Component towerHpText;
        public Component waveText;
        public Button recipeButton;

        [Header("Pause / Opsi")]
        public Button pauseOpenButton;
        public Button continueButton;
        public Button optionsOpenButton;
        public Button optionsCloseButton;
        public Button quitButton;
        public Slider musicSlider;
        public Slider sfxSlider;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource sfxSource;

        [Header("Perilaku")]
        [Tooltip("Nonaktifkan input keyboard bawaan AlchemyManager (bridge mengambil alih 1-6/QE/R/F).")]
        public bool takeOverKeyboard = true;

        [Header("Crafting Panel — Laci Buku Resep Geser")]
        [Tooltip("Lebar laci resep saat terbuka keluar ke kiri (px).")]
        public float recipeDrawerWidth = 300f;

        [Tooltip("Kecepatan geser laci resep (px/detik, memakai unscaledDeltaTime).")]
        public float recipeDrawerSpeed = 1600f;

        [Tooltip("Nama scene menu utama untuk tombol Quit.")]
        public string mainMenuScene = "Main_Menu";

        [Header("Laci Resep — Tampilan (Bisa Dikustomisasi)")]
        [Tooltip("Sprite & warna bingkai laci, bookmark, dan kartu. Semua bisa diganti dari Inspector.")]
        public RecipeDrawerVisualStyle drawerStyle = new RecipeDrawerVisualStyle();

        [Tooltip("Data 6 kartu resep (judul, ikon, deskripsi, target, warna teks). Urutan = slot hotbar 1..6.")]
        public RecipeCardData[] recipeCards = new RecipeCardData[6];

        [Tooltip("Data 4 kartu bahan dasar (judul, ikon, info kimia, warna tema). Urutan = Basa, Pendingin, Asam, Katalis.")]
        public IngredientCardData[] ingredientCards = new IngredientCardData[4];

        [Header("Laci Resep — Ukuran Teks (TextMeshPro)")]
        public float drawerTitleFontSize = 18f;
        public float drawerDescriptionFontSize = 13f;
        public float drawerTargetFontSize = 14f;
        public float drawerFormulaGlyphFontSize = 22f;
        public float drawerTabFontSize = 15f;
        public float ingredientCardTitleFontSize = 16f;
        public float ingredientCardInfoFontSize = 13f;

        // ------------------------------------------------------------------
        // Single Source of Truth — Sprite Ikon Ramuan
        // ------------------------------------------------------------------

        [Header("Sprite Ikon — 4 Bahan Dasar (Sumber Tunggal)")]
        [Tooltip("Urutan WAJIB: 0 = Basa/Biru, 1 = Pendingin/Cyan, 2 = Asam/Merah, 3 = Katalis/Kuning.")]
        public Sprite[] basePotionSprites = new Sprite[4];

        [Tooltip("Urutan WAJIB: 0 = Gel Sabun Beku, 1 = Kabut Asam Dingin, 2 = Ledakan Es Instan, " +
                 "3 = Asam Pelarut Karat, 4 = Bom Netralisasi, 5 = Busa Sabun Mengembang.\n\n" +
                 "Elemen yang MASIH KOSONG (null) dibiarkan memakai warna placeholder lama. " +
                 "Isi PNG-nya agar ikon otomatis masuk ke Hotbar & kartu resep.")]
        public Sprite[] compoundPotionSprites = new Sprite[6];

        [Tooltip("Redupkan ikon Hotbar saat stok ramuan 0x.")]
        public bool dimEmptyHotbarIcons = true;

        [Range(0f, 1f)]
        public float emptyIconDimAmount = 0.55f;

        [Header("Sprite Ikon — Referensi UI (assign manual di Inspector)")]
        [Tooltip("4 Image ikon stok bahan dasar (Bottom_panel/Base). Hanya Image.sprite yang diubah.")]
        [SerializeField] Image[] baseStockIconImages = new Image[4];

        [Tooltip("4 Image Icon_Box di panel buku resep (Card_Bahan…).")]
        [SerializeField] Image[] ingredientCardIconImages = new Image[4];

        [Tooltip("Kartu resep campuran Recipe_Card_0…5 (root transform).")]
        [SerializeField] RectTransform[] recipeCardRoots = new RectTransform[6];

        [Header("Sprite Ikon — Meja Racik & Indikator pH (assign manual)")]
        [Tooltip("Craft Slot 1 Image & Craft Slot 2 Image pada Meja Racik. " +
                 "Terisi → sprite dari basePotionSprites; kosong → warna Color.clear (node tetap aktif).")]
        [SerializeField] private Image[] mixingInputIcons = new Image[2];

        [Tooltip("Craft Result Image: pratinjau hasil dari compoundPotionSprites[recipeIndex].")]
        [SerializeField] private Image mixingResultIcon;

        [Tooltip("Ikon bahan/ramuan di panel atau indikator pH. Indeks = ID bahan " +
                 "(0=Basa/Biru, 1=Pendingin/Cyan, 2=Asam/Merah, 3=Katalis/Kuning); " +
                 "indeks > 3 boleh diisi ikon ramuan campuran dari compoundPotionSprites.")]
        [SerializeField] private Image[] phIndicatorIcons = new Image[4];

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        bool _paused;
        float _musicVolume = 1f;
        float _sfxVolume = 1f;

        // Cache teks (hindari rebuild Canvas tiap frame)
        string _lastHp = "";
        string _lastWave = "";
        readonly int[] _lastIngredientStock = { -1, -1, -1, -1 };
        readonly int[] _lastPotionStock = { -1, -1, -1, -1, -1, -1 };
        int _lastSelectedSlot = -1;

        // Cache status kuali untuk Meja Racik (hindari sync sprite tiap frame)
        int _lastMixSlot1 = -1;
        int _lastMixSlot2 = -1;

        // Laci Buku Resep Geser (dibangun sekali; data dibaca dari field Inspector di atas)
        RectTransform _recipeDrawer;
        RectTransform _recipeDrawerTab;
        TMPro.TextMeshProUGUI _recipeDrawerTabLabel;
        float _recipeDrawerClosedX;
        bool _recipeDrawerOpen;
        bool _recipeDrawerBuilt;
        bool _drawerIsPrefabInstance;

        // Kartu/bingkai yang dibangun runtime, agar bisa di-sync ulang dengan Inspector.
        Image _recipeDrawerBackground;      // Image latar laci
        Image _recipeDrawerBookmarkImage;   // Image sprite bookmark
        Image[] _recipeCardFrames;          // Background 6 kartu resep
        Image[] _recipeResultIcons;         // Kotak ikon hasil (kiri formula)
        TMPro.TextMeshProUGUI[] _ingredientCardTitles;
        TMPro.TextMeshProUGUI[] _ingredientCardInfos;

        // Ikon bahan (Ing1_Box / Ing2_Box) kartu resep yang dibangun runtime.
        Image[] _recipeIngredientAIcons;
        Image[] _recipeIngredientBIcons;

        const string PrefMusic = "MusicVolume";
        const string PrefSfx = "SFXVolume";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            // Bridge ini mengasumsikan Canvas gameplay (HUD + hotbar). Scene Main_Menu /
            // Credits punya Canvas sendiri dan tidak boleh disentuh.
            if (!IsGameplayScene(SceneManager.GetActiveScene().name))
                return;

            if (FindObjectOfType<GameplayUIBridge>() != null)
                return;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                canvas.gameObject.AddComponent<GameplayUIBridge>();
        }

        /// <summary>
        /// Scene gameplay = punya WaveManager/AlchemyManager. Dipakai agar bootstrap
        /// tidak menyentuh scene menu yang Canvas-nya bukan HUD.
        /// </summary>
        static bool IsGameplayScene(string sceneName)
        {
            if (FindObjectOfType<WaveManager>() != null || AlchemyManager.Instance != null)
                return true;

            // Cadangan: nama scene gameplay bawaan proyek ini.
            return sceneName == "SampleScene";
        }

        // ------------------------------------------------------------------
        // Unity Lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ResolveManagers();
            AutoWire();
            LoadAudioPrefs();

            ApplyKeyboardTakeover();

            if (craftPanel != null) craftPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (pauseMenu != null) pauseMenu.SetActive(false);
            Time.timeScale = 1f;

            // Sumber tunggal sprite tetap aman bila komponen lain membangun ulang ikon
            // pada frame pertama (sebelum Start() milik bridge berjalan).
            if (isActiveAndEnabled)
                ApplyAllPotionSprites();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            ResolveManagers();

            // Sprite siap sebelum wiring agar ikon yang dibuat ulang oleh Wire*()
            // langsung memakai sprite dari sumber tunggal.
            AutoAssignBaseSpritesFromProject();

            AutoWire();

            WireIngredients();
            WireHotbar();
            WireCraftPanel();
            WireButtons();
            WireSliders();

            ApplyAllPotionSprites();
            RefreshUI(true);
        }

        /// <summary>
        /// Dipanggil Unity saat nilai Inspector berubah. Semua operasi di sini aman
        /// di Edit Mode: tanpa event bracketing, coroutine, atau pembuatan Canvas Play-only.
        /// </summary>
        void OnValidate()
        {
            if (Application.isPlaying && Instance != null && Instance != this)
                return;

            AutoAssignBaseSpritesFromProject();

            // Objek yang belum ada (mis. laci resep) di-skip dulu; dibangun saat
            // Start()/Edit Mode builder memakai sprite yang sudah terisi.
            ApplyAllPotionSprites();
        }

        void Update()
        {
            HandleKeyboard();
            UpdateRecipeDrawer();
            RefreshUI(false);
        }

        void ResolveManagers()
        {
            if (alchemy == null) alchemy = AlchemyManager.Instance.Ensure(() => FindObjectOfType<AlchemyManager>());
            if (tower == null) tower = TowerHealthManager.Instance.Ensure(() => FindObjectOfType<TowerHealthManager>());
            if (waves == null) waves = FindObjectOfType<WaveManager>();
        }

        void ApplyKeyboardTakeover()
        {
            if (takeOverKeyboard && alchemy != null)
                alchemy.keyboardInputEnabled = false;
        }

        // ------------------------------------------------------------------
        // Input Keyboard (1-6, R, Q, E, C/Tab, Esc) — Tanpa Coroutine
        // ------------------------------------------------------------------

        void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (optionsPanel != null && optionsPanel.activeSelf)
                    CloseOptions();
                else if (craftPanel != null && craftPanel.activeSelf)
                    CloseCraftPanel();
                else
                    TogglePause();
                return;
            }

            // C / Tab: Buka / tutup Craft_panel pop-up
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleCraftPanel();
            }

            if (_paused || !takeOverKeyboard)
                return;

            // 1-6: Pilih slot ramuan aktif secara instan
            for (int i = 0; i < 6; i++)
            {
                KeyCode num = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode pad = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(num) || Input.GetKeyDown(pad))
                {
                    SelectSlot(i);
                    break;
                }
            }

            // R: Saat Craft_panel terbuka -> geser Laci Buku Resep; jika tertutup -> racik slot aktif.
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (craftPanel != null && craftPanel.activeSelf)
                    ToggleRecipeDrawer();
                else
                    DoBrewSelected();
            }

            // Q / E: Pilih slot sebelumnya / berikutnya
            if (Input.GetKeyDown(KeyCode.Q))
                SelectRelative(-1);
            if (Input.GetKeyDown(KeyCode.E))
                SelectRelative(1);
        }

        // ------------------------------------------------------------------
        // Aksi Gameplay
        // ------------------------------------------------------------------

        public void SelectSlot(int index)
        {
            if (alchemy == null) return;
            alchemy.SelectSlot(index);
        }

        public void SelectRelative(int delta)
        {
            if (alchemy == null) return;
            if (delta > 0) alchemy.SelectNext();
            else if (delta < 0) alchemy.SelectPrevious();
        }

        public void DoBrewSelected()
        {
            if (alchemy == null) return;
            alchemy.BrewSelectedSlot();
        }

        // ------------------------------------------------------------------
        // Craft Panel Aksi & Helper
        // ------------------------------------------------------------------

        public void ToggleCraftPanel()
        {
            if (craftPanel == null)
            {
                Transform root = canvas != null ? canvas.transform : transform.root;
                craftPanel = FindByName(root, "Craft_panel");
            }
            if (craftPanel == null) return;

            bool nextState = !craftPanel.activeSelf;
            if (!nextState)
            {
                CloseCraftPanel();
                return;
            }

            craftPanel.SetActive(true);

            RectTransform rt = craftPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(800f, 520f);
                rt.localScale = Vector3.one;
            }

            // Pastikan seluruh subpanel utama di dalamnya aktif
            for (int i = 0; i < craftPanel.transform.childCount; i++)
            {
                craftPanel.transform.GetChild(i).gameObject.SetActive(true);
            }

            craftPanel.transform.SetAsLastSibling();
            WireCraftPanel();
            ApplyAllPotionSprites();
            RefreshCraftPanel();
        }

        public void OpenCraftPanel()
        {
            if (craftPanel == null)
            {
                Transform root = canvas != null ? canvas.transform : transform.root;
                craftPanel = FindByName(root, "Craft_panel");
            }
            if (craftPanel != null)
            {
                craftPanel.SetActive(true);
                RectTransform rt = craftPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(800f, 520f);
                    rt.localScale = Vector3.one;
                }

                for (int i = 0; i < craftPanel.transform.childCount; i++)
                {
                    craftPanel.transform.GetChild(i).gameObject.SetActive(true);
                }

                craftPanel.transform.SetAsLastSibling();
                WireCraftPanel();
                ApplyAllPotionSprites();
                RefreshCraftPanel();
            }
        }

        public void CloseCraftPanel()
        {
            if (craftPanel != null)
                craftPanel.SetActive(false);

            // Laci resep selalu ikut kembali ke posisi tertutup (sinkron, tanpa coroutine).
            _recipeDrawerOpen = false;
            if (_recipeDrawer != null)
            {
                Vector2 closedPos = _recipeDrawer.anchoredPosition;
                closedPos.x = _recipeDrawerClosedX;
                _recipeDrawer.anchoredPosition = closedPos;
            }
            RefreshRecipeDrawerTab();
        }

        public void DoBrewFromCauldron()
        {
            if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
            if (alchemy == null) return;

            // Jika sedang berada di tutorial prolog, pastikan selalu mengeksekusi peracikan tutorial
            PrologueTutorialManager ptm = PrologueTutorialManager.Instance ?? FindObjectOfType<PrologueTutorialManager>();
            if (ptm != null && ptm.CurrentState != PrologueState.Completed && ptm.CurrentState != PrologueState.NotStarted)
            {
                ptm.ExecuteTutorialBrew();
                return;
            }

            if (alchemy.slot1Filled && alchemy.slot2Filled)
            {
                alchemy.Brew();
            }
            else
            {
                alchemy.BrewSelectedSlot();
            }

            RefreshCraftPanel();
            RefreshUI(true);
        }

        public void DoClearCauldron()
        {
            if (alchemy == null) return;
            alchemy.ClearCauldron(true);
            RefreshCraftPanel();
            RefreshUI(true);
        }

        public void DoSelectOrBrewRecipe(int recipeIndex)
        {
            if (alchemy == null) return;

            SelectSlot(recipeIndex);

            IngredientType a, b;
            AlchemyManager.GetSlotIngredients(recipeIndex, out a, out b);

            if (!alchemy.slot1Filled && !alchemy.slot2Filled)
            {
                if (alchemy.AddIngredient(a))
                    alchemy.AddIngredient(b);
            }
            else
            {
                alchemy.BrewSlot(recipeIndex);
            }

            RefreshCraftPanel();
            RefreshUI(true);
        }

        // ------------------------------------------------------------------
        // Proteksi Lemparan: IsPointerOverHotbar & IsPointerOverCraftPanel
        // ------------------------------------------------------------------

        public static bool IsCraftPanelOpen()
        {
            return Instance != null && Instance.craftPanel != null && Instance.craftPanel.activeSelf;
        }

        public static bool IsPointerOverCraftPanel()
        {
            if (!IsCraftPanelOpen())
                return false;

            GameplayUIBridge bridge = Instance;

            RectTransform rt = bridge.craftPanel.GetComponent<RectTransform>();
            if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null))
                return true;

            // Laci resep yang meluncur keluar ke kiri (di luar area panel utama).
            RectTransform drawer = bridge._recipeDrawer;
            if (drawer != null && RectTransformUtility.RectangleContainsScreenPoint(drawer, Input.mousePosition, null))
                return true;

            // Tab bookmark di tepi kiri Craft_panel.
            RectTransform tab = bridge._recipeDrawerTab;
            if (tab != null && RectTransformUtility.RectangleContainsScreenPoint(tab, Input.mousePosition, null))
                return true;

            return false;
        }

        /// <summary>
        /// Mengecek apakah kursor mouse saat ini sedang berada di atas area Hotbar
        /// sehingga ArenaThrowTargeting tidak mengeksekusi lemparan ramuan.
        /// </summary>
        public static bool IsPointerOverHotbar()
        {
            if (Instance == null || Instance.hotbarRoot == null)
                return false;

            RectTransform rt = Instance.hotbarRoot.GetComponent<RectTransform>();
            if (rt == null)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
        }

        /// <summary>
        /// Mengecek apakah kursor mouse saat ini sedang berada di atas panel interaktif yang aktif
        /// (Craft_panel, Hotbar, Bottom_panel, Top_panel, Pause_Menu, Options_Panel).
        /// Digunakan oleh ArenaThrowTargeting agar lemparan tidak terblokir oleh canvas transparan.
        /// </summary>
        public static bool IsPointerOverInteractiveUI()
        {
            if (Instance == null) return false;
            Vector2 mousePos = Input.mousePosition;

            // 1. Craft_panel & laci resep
            if (IsPointerOverCraftPanel()) return true;

            // 2. Hotbar
            if (IsPointerOverHotbar()) return true;

            // 3. Bottom Panel (Stok 4 bahan dasar)
            if (Instance.bottomPanel != null && Instance.bottomPanel.activeInHierarchy)
            {
                RectTransform rt = Instance.bottomPanel.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                    return true;
            }

            // 4. Top Panel (HP Menara, Wave, Tombol Pause)
            if (Instance.topPanel != null && Instance.topPanel.activeInHierarchy)
            {
                RectTransform rt = Instance.topPanel.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                    return true;
            }

            // 5. Pause Menu
            if (Instance.pauseMenu != null && Instance.pauseMenu.activeInHierarchy)
            {
                RectTransform rt = Instance.pauseMenu.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                    return true;
            }

            // 6. Options Panel
            if (Instance.optionsPanel != null && Instance.optionsPanel.activeInHierarchy)
            {
                RectTransform rt = Instance.optionsPanel.GetComponent<RectTransform>();
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, null))
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // UI Refresh (Perubahan Nilai Saja)
        // ------------------------------------------------------------------

        public void RefreshUI(bool force)
        {
            ResolveManagers();

            // 1. Top Panel: HP Menara & Wave
            if (tower != null && towerHpText != null)
            {
                string hp = tower.Health + " / " + tower.maxHealth;
                if (force || _lastHp != hp)
                {
                    _lastHp = hp;
                    SetText(towerHpText, hp);
                }
            }

            if (waveText != null)
            {
                string w;
                if (waves == null || waves.TotalWaves == 0)
                {
                    w = "Wave -";
                }
                else
                {
                    string name = waves.CurrentWave != null ? waves.CurrentWave.waveName : "";
                    string state = waves.IsWaveActive ? "AKTIF" : (waves.IsWaitingForNextWave ? "SEBAR" : "-");
                    w = "Wave " + (waves.CurrentWaveIndex + 1) + "/" + waves.TotalWaves +
                        (string.IsNullOrEmpty(name) ? "" : "  •  " + name) + "  [" + state + "]";
                }

                if (force || _lastWave != w)
                {
                    _lastWave = w;
                    SetText(waveText, w);
                }
            }

            // 2. Bottom Panel: Display Stok 4 Bahan Dasar ("<jumlah>x")
            if (alchemy != null && alchemy.ingredientStock != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    int stock = (i < alchemy.ingredientStock.Length) ? alchemy.ingredientStock[i] : 0;
                    if (force || _lastIngredientStock[i] != stock)
                    {
                        _lastIngredientStock[i] = stock;
                        string stockStr = stock + "x";
                        if (stockDisplayTexts != null && i < stockDisplayTexts.Length && stockDisplayTexts[i] != null)
                            SetText(stockDisplayTexts[i], stockStr);
                    }
                }
            }

            // 3. Hotbar: 6 Slot Tetap Ramuan
            if (alchemy != null)
            {
                alchemy.EnsurePotionStock();
                int currentSelected = alchemy.selectedSlotIndex;
                bool selectedChanged = force || (_lastSelectedSlot != currentSelected);

                for (int i = 0; i < 6; i++)
                {
                    int stock = (alchemy.potionStock != null && i < alchemy.potionStock.Length) ? alchemy.potionStock[i] : 0;
                    bool stockChanged = force || (_lastPotionStock[i] != stock);
                    bool hasSourceSprite = HotbarHasSourceSprite(i);

                    if (stockChanged)
                    {
                        _lastPotionStock[i] = stock;

                        // Badge jumlah ("0x", "1x", "2x")
                        if (hotbarCountBadges != null && i < hotbarCountBadges.Length && hotbarCountBadges[i] != null)
                        {
                            hotbarCountBadges[i].text = stock + "x";
                            hotbarCountBadges[i].color = stock > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.85f);
                        }
                    }

                    // Warna slot ramuan:
                    //   - Bila PNG sumber tunggal sudah diisi, sprite yang menentukan warna
                    //     (hanya diredupkan saat stok 0x, bukan diwarnai ulang).
                    //   - Bila belum ada PNG, placeholder lama tetap memakai warna ramuan.
                    if (hotbarIcons != null && i < hotbarIcons.Length && hotbarIcons[i] != null)
                    {
                        if (hasSourceSprite)
                        {
                            if (stockChanged) RefreshHotbarSourceTint(i, stock);
                        }
                        else if (stockChanged)
                        {
                            Color potionCol = AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]);
                            if (stock > 0)
                            {
                                hotbarIcons[i].color = potionCol;
                            }
                            else
                            {
                                // Redup jika belum ada stok
                                hotbarIcons[i].color = new Color(potionCol.r * 0.45f, potionCol.g * 0.45f, potionCol.b * 0.45f, 0.65f);
                            }
                        }
                    }

                    // Highlight visual pada slot terpilih (skala membesar 1.15x)
                    if (selectedChanged && hotbarSlotRoots != null && i < hotbarSlotRoots.Length && hotbarSlotRoots[i] != null)
                    {
                        bool isSel = (i == currentSelected);
                        hotbarSlotRoots[i].localScale = isSel ? Vector3.one * 1.15f : Vector3.one;

                        // Tambahkan kecerahan jika terpilih (hanya untuk placeholder tanpa PNG)
                        if (!hasSourceSprite && hotbarIcons != null && i < hotbarIcons.Length && hotbarIcons[i] != null && stock > 0)
                        {
                            Color potionCol = AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]);
                            hotbarIcons[i].color = isSel ? Color.Lerp(potionCol, Color.white, 0.35f) : potionCol;
                        }
                    }
                }

                _lastSelectedSlot = currentSelected;
            }

            // 4. Meja Racik: sync hanya saat isi kuali berubah (bukan tiap frame).
            if (alchemy != null)
            {
                int s1 = alchemy.slot1Filled ? (int)alchemy.slot1 : -1;
                int s2 = alchemy.slot2Filled ? (int)alchemy.slot2 : -1;

                bool slotsChanged = force || s1 != _lastMixSlot1 || s2 != _lastMixSlot2;
                if (slotsChanged)
                {
                    _lastMixSlot1 = s1;
                    _lastMixSlot2 = s2;
                    ApplySpritesToMixingTable();
                }
            }
        }

        // ------------------------------------------------------------------
        // Single Source of Truth — Sinkronisasi Sprite Ikon Ramuan
        // ------------------------------------------------------------------

        // Penanda pencarian sprite project sudah dijalankan (hemat I/O saat Play Mode).
        bool _baseSpritesResolved;

        /// <summary>
        /// Terapkan seluruh sprite (4 bahan dasar + 6 ramuan campuran) ke slot ikon yang sudah
        /// di-assign di Inspector. Hanya <see cref="Image.sprite"/>, <c>color</c>, dan
        /// <c>preserveAspect</c> yang diubah: posisi, ukuran, struktur child, dan status
        /// GameObject wadah/bingkai tidak pernah disentuh.
        /// Idempoten: aman dipanggil dari Awake, Start, OnValidate, dan setiap refresh UI.
        /// </summary>
        public void ApplyAllPotionSprites()
        {
            EnsureSpriteArrays();

            ApplyBaseSpritesToHudStock();
            ApplyBaseSpritesToIngredientCards();
            ApplySpritesToRecipeCards();
            ApplyCompoundSpritesToRecipeResults();
            ApplyCompoundSpritesToHotbar();
            ApplySpritesToPhIndicators();
            ApplySpritesToMixingTable();
        }

        /// <summary>
        /// Lengkapi array sprite agar selalu berukuran 4 (bahan) dan 6 (campuran).
        /// Elemen lama dipertahankan; kekurangan diisi null.
        /// </summary>
        void EnsureSpriteArrays()
        {
            if (basePotionSprites == null || basePotionSprites.Length != 4)
            {
                Sprite[] old = basePotionSprites;
                basePotionSprites = new Sprite[4];
                if (old != null)
                {
                    for (int i = 0; i < old.Length && i < 4; i++)
                        basePotionSprites[i] = old[i];
                }
            }

            if (compoundPotionSprites == null || compoundPotionSprites.Length != 6)
            {
                Sprite[] old = compoundPotionSprites;
                compoundPotionSprites = new Sprite[6];
                if (old != null)
                {
                    for (int i = 0; i < old.Length && i < 6; i++)
                        compoundPotionSprites[i] = old[i];
                }
            }

            if (baseStockIconImages == null || baseStockIconImages.Length != 4)
                baseStockIconImages = KeepFour(baseStockIconImages);
            if (ingredientCardIconImages == null || ingredientCardIconImages.Length != 4)
                ingredientCardIconImages = KeepFour(ingredientCardIconImages);
            if (recipeCardRoots == null || recipeCardRoots.Length != 6)
                recipeCardRoots = KeepSix(recipeCardRoots);
        }

        static Image[] KeepFour(Image[] old)
        {
            Image[] arr = new Image[4];
            if (old != null)
            {
                for (int i = 0; i < old.Length && i < 4; i++)
                    arr[i] = old[i];
            }
            return arr;
        }

        static RectTransform[] KeepSix(RectTransform[] old)
        {
            RectTransform[] arr = new RectTransform[6];
            if (old != null)
            {
                for (int i = 0; i < old.Length && i < 6; i++)
                    arr[i] = old[i];
            }
            return arr;
        }

        /// <summary>
        /// Cari sprite bahan dasar di Assets/Sprites/Potion_ sesuai nama file
        /// ("base potion biru/cyan/merah/kuning"). Hanya mengisi slot yang masih null,
        /// jadi sprite yang sudah di-assign manual di Inspector tidak pernah ditimpa.
        /// </summary>
        public void AutoAssignBaseSpritesFromProject()
        {
            EnsureSpriteArrays();

            bool allFilled = true;
            for (int i = 0; i < 4; i++)
            {
                if (basePotionSprites[i] == null) allFilled = false;
            }
            if (allFilled) return;

            if (Application.isPlaying && _baseSpritesResolved) return;
            _baseSpritesResolved = true;

#if UNITY_EDITOR
            string[] keywords = { "base potion biru", "base potion cyan", "base potion merah", "base potion kuning" };

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/Potion_" });
            for (int g = 0; g < guids.Length; g++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[g]);
                Sprite spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr == null) continue;

                string fileNoExt = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                for (int i = 0; i < 4; i++)
                {
                    if (basePotionSprites[i] != null) continue;
                    if (fileNoExt.Contains(keywords[i]))
                        basePotionSprites[i] = spr;
                }
            }
#endif
        }

        // --- Target A: 4 slot stok bahan (Bottom_panel/Base) -------------------

        void ApplyBaseSpritesToHudStock()
        {
            if (baseStockIconImages == null) return;

            for (int i = 0; i < 4 && i < baseStockIconImages.Length; i++)
                ApplyIconSprite(baseStockIconImages[i], SafeBaseSprite(i));
        }

        // --- Target B: 4 Icon_Box di panel buku resep --------------------------

        void ApplyBaseSpritesToIngredientCards()
        {
            EnsureDrawerData();

            if (ingredientCardIconImages == null) return;

            for (int i = 0; i < 4 && i < ingredientCardIconImages.Length; i++)
            {
                Sprite overrideSprite = ingredientCards != null && i < ingredientCards.Length && ingredientCards[i] != null
                    ? ingredientCards[i].customIngredientIcon
                    : null;

                Sprite spr = overrideSprite != null ? overrideSprite : SafeBaseSprite(i);
                ApplyIconSprite(ingredientCardIconImages[i], spr);
            }
        }

        // --- Target C: ikon bahan (Icon_A / Icon_B) di 6 kartu resep -----------

        void ApplySpritesToRecipeCards()
        {
            if (recipeCardRoots == null) return;

            for (int i = 0; i < 6; i++)
            {
                RectTransform card = i < recipeCardRoots.Length ? recipeCardRoots[i] : null;
                if (card == null) continue;

                IngredientType ingA;
                IngredientType ingB;
                AlchemyManager.GetSlotIngredients(i, out ingA, out ingB);

                RecipeCardData data = recipeCards != null && i < recipeCards.Length ? recipeCards[i] : null;

                Sprite spriteA = data != null && data.customIngredientAIcon != null
                    ? data.customIngredientAIcon
                    : SafeBaseSprite((int)ingA);
                Sprite spriteB = data != null && data.customIngredientBIcon != null
                    ? data.customIngredientBIcon
                    : SafeBaseSprite((int)ingB);

                ApplyIconSprite(FindCardIcon(card, "Ing1_Box", "Icon_A", ref _recipeIngredientAIcons, i), spriteA);
                ApplyIconSprite(FindCardIcon(card, "Ing2_Box", "Icon_B", ref _recipeIngredientBIcons, i), spriteB);
            }
        }

        /// <summary>
        /// Ambil kotak ikon bahan di kartu resep ("Ing1_Box"/"Icon_A", "Ing2_Box"/"Icon_B").
        /// Tidak pernah membuat objek baru: kotak yang tidak ada di scene dianggap tidak ada.
        /// </summary>
        Image FindCardIcon(RectTransform card, string primaryName, string aliasName, ref Image[] cache, int index)
        {
            Transform t = card.Find(primaryName);
            if (t == null) t = card.Find(aliasName);

            Image img = t != null ? t.GetComponent<Image>() : null;

            if (cache == null || cache.Length < 6) cache = new Image[6];
            cache[index] = img;
            return img;
        }

        // --- Target B campuran: Result_Box 6 kartu resep ----------------------

        void ApplyCompoundSpritesToRecipeResults()
        {
            if (recipeCardRoots == null) return;

            for (int i = 0; i < 6; i++)
            {
                RectTransform card = i < recipeCardRoots.Length ? recipeCardRoots[i] : null;
                if (card == null) continue;

                Transform result = card.Find("Result_Box");
                if (result == null) continue;

                Image img = result.GetComponent<Image>();
                if (img == null) continue;

                if (_recipeResultIcons == null || _recipeResultIcons.Length < 6) _recipeResultIcons = new Image[6];
                _recipeResultIcons[i] = img;

                RecipeCardData data = recipeCards != null && i < recipeCards.Length ? recipeCards[i] : null;
                Sprite overrideSprite = data != null ? data.customPotionIcon : null;
                Sprite spr = overrideSprite != null ? overrideSprite : SafeCompoundSprite(i);

                ApplyIconSprite(img, spr);
            }
        }

        // --- Target A campuran: 6 slot Hotbar --------------------------------

        void ApplyCompoundSpritesToHotbar()
        {
            if (hotbarIcons == null) return;

            for (int i = 0; i < 6 && i < hotbarIcons.Length; i++)
            {
                // Hanya sprite yang diubah; warna placeholder tanpa PNG diatur RefreshUI().
                ApplyIconSprite(hotbarIcons[i], SafeCompoundSprite(i));
            }
        }

        // --- Meja Racik (2 kotak bahan + pratinjau hasil, di atas BREW) ------

        /// <summary>
        /// Sinkronkan ikon Meja Racik dari status kuali:
        ///   - mixingInputIcons[0..1] ← basePotionSprites[id bahan slot1/slot2].
        ///     Slot kosong → image.color = Color.clear (node wadah tetap aktif).
        ///   - mixingResultIcon ← compoundPotionSprites[recipeIndex] saat kombinasi valid;
        ///     warna lama dipertahankan bila sprite belum tersedia.
        /// Tidak ada pembuatan/destroy objek dan tidak ada SetActive().
        /// </summary>
        void ApplySpritesToMixingTable()
        {
            bool hasAlchemy = alchemy != null;

            // 1. Dua kotak bahan input.
            if (mixingInputIcons != null)
            {
                for (int i = 0; i < mixingInputIcons.Length && i < 2; i++)
                {
                    Image img = mixingInputIcons[i];
                    if (img == null) continue;

                    bool filled = hasAlchemy && (i == 0 ? alchemy.slot1Filled : alchemy.slot2Filled);

                    if (!filled)
                    {
                        // Edit Mode tanpa AlchemyManager: jangan sembunyikan layout.
                        if (hasAlchemy) img.color = Color.clear;
                        continue;
                    }

                    int ingId = i == 0 ? (int)alchemy.slot1 : (int)alchemy.slot2;
                    Sprite spr = SafeBaseSprite(ingId);

                    if (spr != null)
                    {
                        img.sprite = spr;
                        img.color = Color.white;
                        img.preserveAspect = true;
                    }
                    else if (hasAlchemy)
                    {
                        // Bahan terisi tetapi PNG belum ada → jangan tampilkan gambar lama.
                        img.color = Color.clear;
                    }
                }
            }

            // 2. Kotak pratinjau hasil racikan.
            if (mixingResultIcon != null)
            {
                Sprite resultSprite = null;

                if (hasAlchemy && alchemy.slot1Filled && alchemy.slot2Filled)
                {
                    int recipeIndex = ResolveMixingRecipeIndex();
                    if (recipeIndex >= 0)
                        resultSprite = SafeCompoundSprite(recipeIndex);
                }

                // Sprite null → warna placeholder lama dibiarkan apa adanya.
                ApplyIconSprite(mixingResultIcon, resultSprite);
            }
        }

        /// <summary>
        /// Nomor resep (0..5 = indeks FixedSlotPotions / compoundPotionSprites) untuk isi kuali
        /// saat ini, atau -1 bila kombinasi tidak valid. Dicocokkan lewat PotionRecipeSO;
        /// bila SO resep belum tersedia, dicocokkan lewat pasangan bahan tetap di AlchemyManager.
        /// </summary>
        int ResolveMixingRecipeIndex()
        {
            if (alchemy == null) return -1;
            if (!alchemy.slot1Filled || !alchemy.slot2Filled) return -1;

            // A. Lewat resep (menghormati rentang pH).
            if (alchemy.recipes != null)
            {
                PotionRecipeSO rec = alchemy.FindRecipe(alchemy.slot1, alchemy.slot2, alchemy.CurrentPH);
                if (rec != null)
                {
                    for (int i = 0; i < AlchemyManager.FixedSlotPotions.Length; i++)
                    {
                        if (AlchemyManager.FixedSlotPotions[i] == rec.potionType)
                            return i;
                    }
                }
            }

            // B. Cadangan: cocokkan pasangan bahan (bisa dibalik).
            for (int i = 0; i < 6; i++)
            {
                IngredientType a, b;
                AlchemyManager.GetSlotIngredients(i, out a, out b);

                bool direct = alchemy.slot1 == a && alchemy.slot2 == b;
                bool swapped = alchemy.slot1 == b && alchemy.slot2 == a;
                if (direct || swapped)
                    return i;
            }

            return -1;
        }

        // --- Panel / Indikator pH --------------------------------------------

        /// <summary>
        /// Sinkronkan ikon bahan di panel/indikator pH dari basePotionSprites.
        /// Indeks i = ID bahan (0..3). Bila array lebih panjang dari 4, indeks
        /// tambahan diisi dari compoundPotionSprites (ramuan campuran).
        /// </summary>
        void ApplySpritesToPhIndicators()
        {
            if (phIndicatorIcons == null) return;

            for (int i = 0; i < phIndicatorIcons.Length; i++)
            {
                Sprite spr = i < 4 ? SafeBaseSprite(i) : SafeCompoundSprite(i - 4);
                ApplyIconSprite(phIndicatorIcons[i], spr);
            }
        }

        Sprite SafeBaseSprite(int index)
        {
            if (basePotionSprites == null || index < 0 || index >= basePotionSprites.Length) return null;
            return basePotionSprites[index];
        }

        Sprite SafeCompoundSprite(int index)
        {
            if (compoundPotionSprites == null || index < 0 || index >= compoundPotionSprites.Length) return null;
            return compoundPotionSprites[index];
        }

        /// <summary>true bila slot hotbar ini sudah memakai PNG campuran dari sumber tunggal.</summary>
        bool HotbarHasSourceSprite(int index)
        {
            return SafeCompoundSprite(index) != null &&
                   hotbarIcons != null && index < hotbarIcons.Length && hotbarIcons[index] != null;
        }

        // ------------------------------------------------------------------
        // Helper Sprite
        // ------------------------------------------------------------------

        /// <summary>
        /// Ganti gambar sebuah slot ikon. Satu-satunya hal yang diubah adalah
        /// <see cref="Image.sprite"/>, <c>color</c>, dan <c>preserveAspect</c>.
        /// Posisi, ukuran, struktur child, dan status GameObject TIDAK pernah diubah.
        ///   sprite != null → sprite dipasang, color = Color.white, preserveAspect = true.
        ///   sprite == null → dibiarkan apa adanya (sprite &amp; warna bawaan scene tetap utuh).
        /// </summary>
        static void ApplyIconSprite(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        /// <summary>
        /// Redupkan ikon Hotbar yang stoknya 0x (opsional, lihat <see cref="dimEmptyHotbarIcons"/>).
        /// </summary>
        void RefreshHotbarSourceTint(int index, int stock)
        {
            if (!dimEmptyHotbarIcons) return;
            if (!HotbarHasSourceSprite(index)) return;

            float dim = Mathf.Clamp01(emptyIconDimAmount);
            hotbarIcons[index].color = stock > 0 ? Color.white : new Color(dim, dim, dim, 1f);
        }

        // ------------------------------------------------------------------
        // Auto-Wire
        // ------------------------------------------------------------------

        public void AutoWire()
        {
            if (canvas == null) canvas = GetComponent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (canvas == null) return;

            Transform root = canvas.transform;

            if (bottomPanel == null) bottomPanel = FindByName(root, "Bottom_panel");
            if (topPanel == null) topPanel = FindByName(root, "Top_panel");
            if (craftPanel == null) craftPanel = FindByName(root, "Craft_panel");
            if (hotbarRoot == null) hotbarRoot = FindByName(root, "Hotbar");
            if (optionsPanel == null) optionsPanel = FindByName(root, "Opsi", "Options", "Settings");
            if (pauseMenu == null) pauseMenu = FindByName(root, "Pause Menu", "Pause", "PauseMenu");

            WireIngredients();
            WireHotbar();
            WireTop();
            WireModal();
            WireAudio();
            WireCraftPanel();
        }

        void WireIngredients()
        {
            GameObject baseGo = bottomPanel != null ? FindByName(bottomPanel.transform, "Base") : null;
            Transform baseRoot = baseGo != null ? baseGo.transform : (bottomPanel != null ? bottomPanel.transform : null);

            if (baseRoot == null) return;

            // Cari 4 tombol di Base
            Button[] btns = baseRoot.GetComponentsInChildren<Button>(true);
            List<Button> btnList = new List<Button>();
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i].gameObject.name.Contains("Button"))
                    btnList.Add(btns[i]);
            }

            // Sesuai 4 jenis bahan dasar:
            //   0: Basa (Biru)
            //   1: Pendingin (Cyan)
            //   2: Asam (Merah)
            //   3: Katalis (Kuning)
            for (int i = 0; i < btnList.Count && i < 4; i++)
            {
                Button btn = btnList[i];
                ingredientButtons[i] = btn;

                btn.interactable = true;
                int ingIdx = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    if (alchemy != null && craftPanel != null && craftPanel.activeSelf)
                    {
                        alchemy.AddIngredient((IngredientType)ingIdx);
                        RefreshCraftPanel();
                        RefreshUI(true);
                    }
                });

                // Terapkan warna bahan ke Image button.
                // Bila PNG sumber tunggal sudah diisi, warna ditangani ApplyBaseSpritesToHudStock()
                // (putih + preserveAspect) agar tidak tertimpa warna kotak placeholder.
                Image img = btn.GetComponent<Image>();
                if (img != null && SafeBaseSprite(i) == null)
                {
                    img.color = AlchemyDatabase.IngredientColor((IngredientType)i);
                }

                // Cari teks stok di child button (TMP_Text atau Text)
                if (stockDisplayTexts == null || stockDisplayTexts.Length < 4)
                    stockDisplayTexts = new Component[4];

                Component txtComp = FindTextComponentIn(btn.gameObject);
                if (txtComp != null)
                    stockDisplayTexts[i] = txtComp;
            }
        }

        void WireHotbar()
        {
            if (hotbarRoot == null)
            {
                Transform root = canvas != null ? canvas.transform : transform.root;
                hotbarRoot = FindByName(root, "Hotbar");
            }
            if (hotbarRoot == null) return;

            // 1. Cari 6 slot Image (Image (1) s/d Image (6))
            List<RectTransform> slotRects = new List<RectTransform>();
            for (int i = 0; i < hotbarRoot.transform.childCount; i++)
            {
                Transform child = hotbarRoot.transform.GetChild(i);
                if (child.name.StartsWith("Image"))
                {
                    RectTransform rt = child as RectTransform;
                    if (rt != null)
                        slotRects.Add(rt);
                }
            }

            // Urutkan dari kiri ke kanan berdasarkan posisi X
            slotRects.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));

            for (int i = 0; i < slotRects.Count && i < 6; i++)
            {
                RectTransform rt = slotRects[i];
                hotbarSlotRoots[i] = rt;

                Image img = rt.GetComponent<Image>();
                if (img == null) img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = true;
                hotbarIcons[i] = img;

                Button btn = rt.GetComponent<Button>();
                if (btn == null) btn = rt.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.interactable = true;
                hotbarButtons[i] = btn;

                // Wire click listener
                int slotIndex = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectSlot(slotIndex));

                // 2. Label Shortcut Keyboard (1–6) di bagian tengah atas slot
                Transform keyLabelTrans = rt.Find("Key_Label");
                if (keyLabelTrans != null && !(keyLabelTrans is RectTransform))
                {
                    if (Application.isPlaying) Destroy(keyLabelTrans.gameObject);
                    else DestroyImmediate(keyLabelTrans.gameObject);
                    keyLabelTrans = null;
                }

                if (keyLabelTrans == null)
                {
                    GameObject go = new GameObject("Key_Label", typeof(RectTransform), typeof(CanvasRenderer));
                    go.layer = 5;
                    go.transform.SetParent(rt, false);
                    keyLabelTrans = go.transform;
                }

                RectTransform keyRt = (RectTransform)keyLabelTrans;
                keyRt.anchorMin = new Vector2(0.5f, 1f);
                keyRt.anchorMax = new Vector2(0.5f, 1f);
                keyRt.pivot = new Vector2(0.5f, 1f);
                keyRt.anchoredPosition = new Vector2(0f, -4f);
                keyRt.sizeDelta = new Vector2(30f, 20f);
                keyRt.localScale = Vector3.one;

                TMPro.TextMeshProUGUI keyText = UITextUtil.EnsureText(keyLabelTrans.gameObject, 15f);
                keyText.text = (i + 1).ToString();
                keyText.fontSize = 15f;
                keyText.fontStyle = TMPro.FontStyles.Bold;
                keyText.alignment = TMPro.TextAlignmentOptions.Center;
                keyText.color = new Color(1f, 0.88f, 0.45f, 1f); // Kuningan terang
                keyText.enableWordWrapping = false;
                keyText.overflowMode = TMPro.TextOverflowModes.Overflow;
                keyText.raycastTarget = false;
                hotbarKeyLabels[i] = keyText;

                var keyOutline = keyLabelTrans.GetComponent<Outline>() ?? keyLabelTrans.gameObject.AddComponent<Outline>();
                keyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                keyOutline.effectDistance = new Vector2(1f, -1f);

                // 3. Badge Jumlah Ramuan ("0x", "1x") di pojok samping/kanan bawah
                Transform badgeTrans = rt.Find("Count_Badge");
                if (badgeTrans != null && !(badgeTrans is RectTransform))
                {
                    if (Application.isPlaying) Destroy(badgeTrans.gameObject);
                    else DestroyImmediate(badgeTrans.gameObject);
                    badgeTrans = null;
                }

                if (badgeTrans == null)
                {
                    GameObject go = new GameObject("Count_Badge", typeof(RectTransform), typeof(CanvasRenderer));
                    go.layer = 5;
                    go.transform.SetParent(rt, false);
                    badgeTrans = go.transform;
                }

                RectTransform badgeRt = (RectTransform)badgeTrans;
                badgeRt.anchorMin = new Vector2(1f, 0f);
                badgeRt.anchorMax = new Vector2(1f, 0f);
                badgeRt.pivot = new Vector2(1f, 0f);
                badgeRt.anchoredPosition = new Vector2(-4f, 4f);
                badgeRt.sizeDelta = new Vector2(40f, 22f);
                badgeRt.localScale = Vector3.one;

                TMPro.TextMeshProUGUI badgeText = UITextUtil.EnsureText(badgeTrans.gameObject, 14f);
                badgeText.text = "0x";
                badgeText.fontSize = 14f;
                badgeText.fontStyle = TMPro.FontStyles.Bold;
                badgeText.alignment = TMPro.TextAlignmentOptions.MidlineRight;
                badgeText.color = Color.white;
                badgeText.enableWordWrapping = false;
                badgeText.overflowMode = TMPro.TextOverflowModes.Overflow;
                badgeText.raycastTarget = false;
                hotbarCountBadges[i] = badgeText;

                var badgeOutline = badgeTrans.GetComponent<Outline>() ?? badgeTrans.gameObject.AddComponent<Outline>();
                badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                badgeOutline.effectDistance = new Vector2(1f, -1f);
            }

            // Pastikan click listener untuk seluruh slot button terhubung
            for (int i = 0; i < 6; i++)
            {
                if (hotbarButtons != null && i < hotbarButtons.Length && hotbarButtons[i] != null)
                {
                    int slotIndex = i;
                    hotbarButtons[i].onClick.RemoveAllListeners();
                    hotbarButtons[i].onClick.AddListener(() => SelectSlot(slotIndex));
                }
            }

            // 4. Tombol lingkaran tengah Hotbar (Hotbar -> Button) untuk membuka/menutup Craft_panel
            if (centerBrewButton == null && hotbarRoot != null)
            {
                Button[] allHotbarBtns = hotbarRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < allHotbarBtns.Length; i++)
                {
                    if (allHotbarBtns[i].gameObject.name.Equals("Button"))
                    {
                        centerBrewButton = allHotbarBtns[i];
                        break;
                    }
                }
            }

            if (centerBrewButton != null)
            {
                Image btnImg = centerBrewButton.GetComponent<Image>();
                if (btnImg != null)
                {
                    btnImg.raycastTarget = true;
                    centerBrewButton.targetGraphic = btnImg;
                }

                Graphic[] childGraphics = centerBrewButton.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < childGraphics.Length; i++)
                {
                    if (childGraphics[i].gameObject != centerBrewButton.gameObject)
                        childGraphics[i].raycastTarget = false;
                }

                centerBrewButton.onClick.RemoveAllListeners();
                centerBrewButton.onClick.AddListener(ToggleCraftPanel);
            }
        }

        void WireTop()
        {
            if (topPanel == null) return;

            if (towerHpText == null) towerHpText = FindTextComponentIn(topPanel, "HP", "Health", "Darah", "Tower");
            if (waveText == null) waveText = FindTextComponentIn(topPanel, "Wave", "Gelombang");
            if (recipeButton == null) recipeButton = FindButton(topPanel.transform, "Recipe", "Resep", "Guide", "Panduan");

            if (pauseOpenButton == null)
            {
                Transform topRow = topPanel.transform.Find("Toprow_group");
                if (topRow != null)
                    pauseOpenButton = topRow.GetComponentInChildren<Button>(true);

                if (pauseOpenButton == null)
                    pauseOpenButton = FindButton(topPanel.transform, "Button", "Pause", "||", "Jeda");
            }
        }

        void WireModal()
        {
            Transform scope = pauseMenu != null ? pauseMenu.transform : transform.root;

            if (continueButton == null) continueButton = FindButton(scope, "continue", "Continue", "Lanjut", "Resume", "Main");
            if (quitButton == null) quitButton = FindButton(scope, "Quit", "quit", "Keluar", "Exit");

            if (optionsOpenButton == null)
            {
                Button[] btns = scope.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < btns.Length; i++)
                {
                    if (optionsPanel != null && btns[i].transform.IsChildOf(optionsPanel.transform)) continue;
                    string n = btns[i].gameObject.name.ToLowerInvariant();
                    if (n.Contains("opsi") || n.Contains("option") || n.Contains("setting") || n.Contains("pengaturan"))
                    { optionsOpenButton = btns[i]; break; }
                }
            }

            Transform optScope = optionsPanel != null ? optionsPanel.transform : transform.root;
            if (optionsCloseButton == null) optionsCloseButton = FindButton(optScope, "Close", "close", "Tutup", "X");

            if (musicSlider == null) musicSlider = FindSlider(optScope, "Music", "Musik", "BGM");
            if (sfxSlider == null) sfxSlider = FindSlider(optScope, "SFX", "Sound", "Suara", "Effect");
        }

        void WireCraftPanel()
        {
            if (craftPanel == null)
            {
                Transform root = canvas != null ? canvas.transform : transform.root;
                craftPanel = FindByName(root, "Craft_panel");
            }

            if (craftPanel == null) return;

            Transform cTrans = craftPanel.transform;
            RectTransform craftRt = craftPanel.GetComponent<RectTransform>();

            // Tombol Close
            if (craftCloseButton == null)
                craftCloseButton = FindButton(cTrans, "Close", "close", "Tutup", "X");
            if (craftCloseButton != null)
            {
                craftCloseButton.onClick.RemoveAllListeners();
                craftCloseButton.onClick.AddListener(CloseCraftPanel);
            }

            // Subpanel "Craft" (Kanan)
            Transform craftSub = cTrans.Find("Craft");
            if (craftSub != null)
            {
                // Tombol Simpan (Brew)
                Transform simpanTrans = craftSub.Find("Simpan");
                if (simpanTrans != null)
                    craftBrewButton = simpanTrans.GetComponent<Button>() ?? simpanTrans.GetComponentInChildren<Button>(true);
                if (craftBrewButton == null)
                    craftBrewButton = FindButton(craftSub, "Simpan", "simpan", "Brew", "Racik", "Craft");
                if (craftBrewButton != null)
                {
                    craftBrewButton.onClick.RemoveAllListeners();
                    craftBrewButton.onClick.AddListener(DoBrewFromCauldron);
                }

                // Tombol Clear / Ulang
                Transform clearTrans = craftSub.Find("Clear");
                if (clearTrans != null)
                    craftClearButton = clearTrans.GetComponent<Button>() ?? clearTrans.GetComponentInChildren<Button>(true);
                if (craftClearButton == null)
                    craftClearButton = FindButton(craftSub, "Clear", "clear", "Ulang", "Reset", "Kosongkan");
                if (craftClearButton != null)
                {
                    craftClearButton.onClick.RemoveAllListeners();
                    craftClearButton.onClick.AddListener(DoClearCauldron);

                    // Ubah label teks tombol menjadi "Ulang"
                    Component clearTxt = FindTextComponentIn(craftClearButton.gameObject);
                    if (clearTxt != null)
                    {
                        SetText(clearTxt, "Ulang");
                    }
                }

                // slot1 (Drag & Drop + Drop Zone)
                Transform s1 = craftSub.Find("slot1");
                if (s1 != null)
                {
                    craftSlot1Image = s1.GetComponent<Image>() ?? s1.gameObject.AddComponent<Image>();
                    craftSlot1Image.raycastTarget = true;

                    CauldronSlotDropZone drop1 = s1.GetComponent<CauldronSlotDropZone>() ?? s1.gameObject.AddComponent<CauldronSlotDropZone>();
                    drop1.slotIndex = 0;

                    CraftIngredientDraggable drag1 = s1.GetComponent<CraftIngredientDraggable>() ?? s1.gameObject.AddComponent<CraftIngredientDraggable>();
                    drag1.source = CraftDragSource.Cauldron;
                    drag1.cauldronSlot = 0;
                    drag1.craftPanelRect = craftRt;
                    drag1.rootCanvas = canvas;
                }

                // slot2 (Drag & Drop + Drop Zone)
                Transform s2 = craftSub.Find("slot2");
                if (s2 != null)
                {
                    craftSlot2Image = s2.GetComponent<Image>() ?? s2.gameObject.AddComponent<Image>();
                    craftSlot2Image.raycastTarget = true;

                    CauldronSlotDropZone drop2 = s2.GetComponent<CauldronSlotDropZone>() ?? s2.gameObject.AddComponent<CauldronSlotDropZone>();
                    drop2.slotIndex = 1;

                    CraftIngredientDraggable drag2 = s2.GetComponent<CraftIngredientDraggable>() ?? s2.gameObject.AddComponent<CraftIngredientDraggable>();
                    drag2.source = CraftDragSource.Cauldron;
                    drag2.cauldronSlot = 1;
                    drag2.craftPanelRect = craftRt;
                    drag2.rootCanvas = canvas;
                }

                // Hasil
                Transform h = craftSub.Find("Hasil");
                if (h != null)
                    craftResultImage = h.GetComponent<Image>();

                // ph
                Transform phTrans = craftSub.Find("ph");
                if (phTrans != null)
                    craftPHText = FindTextComponentIn(phTrans.gameObject);
            }

            // Subpanel Kiri: Panel Stok 4 Bahan Dasar (Scrollable ke Bawah)
            Transform leftSub = cTrans.Find("Resep") ?? cTrans.Find("Bahan_Scroll");
            if (leftSub != null)
            {
                SetupCraftIngredientsScroll(leftSub);
            }

            // Laci Buku Resep Geser menempel di sisi kiri Craft_panel.
            BuildRecipeDrawer(craftRt);
        }

        void SetupCraftIngredientsScroll(Transform leftRoot)
        {
            if (leftRoot == null) return;

            // Sembunyikan child lama resep yang bertumpuk jika ada
            List<Transform> oldItems = new List<Transform>();
            for (int i = 0; i < leftRoot.childCount; i++)
            {
                Transform c = leftRoot.GetChild(i);
                if (c.name.StartsWith("Item") || c.name.Contains("Resep") || c.name.StartsWith("Image"))
                {
                    oldItems.Add(c);
                }
            }
            for (int i = 0; i < oldItems.Count; i++)
            {
                oldItems[i].gameObject.SetActive(false);
            }

            RectTransform leftRt = leftRoot as RectTransform;
            if (leftRt != null)
            {
                leftRt.anchorMin = new Vector2(0f, 0f);
                leftRt.anchorMax = new Vector2(0.5f, 1f);
                leftRt.pivot = new Vector2(0.5f, 0.5f);
                leftRt.offsetMin = new Vector2(16f, 16f);
                leftRt.offsetMax = new Vector2(-10f, -16f);
            }

            Image bgImg = leftRoot.GetComponent<Image>() ?? leftRoot.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.10f, 0.14f, 0.88f);
            bgImg.raycastTarget = true;

            // Masker RectMask2D agar kartu bahan terpotong rapi saat di-scroll
            RectMask2D mask = leftRoot.GetComponent<RectMask2D>() ?? leftRoot.gameObject.AddComponent<RectMask2D>();

            // ScrollRect vertikal
            ScrollRect scroll = leftRoot.GetComponent<ScrollRect>() ?? leftRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            // Content container
            Transform contentTrans = leftRoot.Find("Content");
            if (contentTrans == null)
            {
                GameObject cGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                cGo.layer = 5;
                cGo.transform.SetParent(leftRoot, false);
                contentTrans = cGo.transform;
            }

            RectTransform contentRt = (RectTransform)contentTrans;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            scroll.content = contentRt;

            VerticalLayoutGroup vlg = contentTrans.GetComponent<VerticalLayoutGroup>() ?? contentTrans.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 8, 8);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentTrans.GetComponent<ContentSizeFitter>() ?? contentTrans.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            EnsureDrawerData();

            // Data 4 Bahan Dasar (Basa, Pendingin, Asam, Katalis) — dari Inspector.
            RectTransform craftRt = craftPanel != null ? craftPanel.GetComponent<RectTransform>() : null;
            _ingredientCardTitles = new TMPro.TextMeshProUGUI[4];
            _ingredientCardInfos = new TMPro.TextMeshProUGUI[4];

            for (int i = 0; i < 4; i++)
            {
                IngredientCardData data = ingredientCards[i];
                IngredientType type = (IngredientType)i;
                string cardName = "Card_" + type;
                Transform cardTrans = contentTrans.Find(cardName);
                if (cardTrans == null)
                {
                    GameObject cardGo = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                    cardGo.layer = 5;
                    cardGo.transform.SetParent(contentTrans, false);
                    cardTrans = cardGo.transform;
                }

                LayoutElement le = cardTrans.GetComponent<LayoutElement>() ?? cardTrans.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 96f;
                le.minHeight = 96f;

                // Latar belakang kartu bahan dasar: Image.color maupun ApplyFrame dihentikan
                // agar warna dan sprite kartu yang sudah diatur manual di Unity Editor tetap tampil apa adanya saat Play Mode.
                if (cardTrans.GetComponent<Image>() == null)
                    cardTrans.gameObject.AddComponent<Image>();

                // Outline halus pada kartu
                var cardOutline = cardTrans.GetComponent<Outline>() ?? cardTrans.gameObject.AddComponent<Outline>();
                cardOutline.effectColor = new Color(0.25f, 0.28f, 0.35f, 0.7f);
                cardOutline.effectDistance = new Vector2(1f, -1f);

                // --- Sisi Kiri: Icon Box (Draggable) ---
                Transform iconTrans = cardTrans.Find("Icon_Box");
                if (iconTrans == null)
                {
                    GameObject iconGo = new GameObject("Icon_Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CraftIngredientDraggable));
                    iconGo.layer = 5;
                    iconGo.transform.SetParent(cardTrans, false);
                    iconTrans = iconGo.transform;
                }

                RectTransform iconRt = (RectTransform)iconTrans;
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(62f, 62f);

                Image iconImg = iconTrans.GetComponent<Image>() ?? iconTrans.gameObject.AddComponent<Image>();
                craftStockIconImages[i] = iconImg;

                // Sumber tunggal: basePotionSprites (override per-kartu opsional).
                // Tanpa sprite → placeholder warna tema lama tetap dipakai.
                Sprite ingSprite = data.customIngredientIcon != null ? data.customIngredientIcon : SafeBaseSprite(i);
                if (ingSprite != null)
                {
                    ApplyIconSprite(iconImg, ingSprite);
                }
                else
                {
                    // Tanpa PNG: biarkan sprite bawaan Icon_Box tetap terpasang,
                    // cukup tint warnanya (jangan di-reset ke sprite kosong/putih polos).
                    iconImg.color = data.themeColor;
                }
                iconImg.raycastTarget = true;

                if (ingredientCardIconImages == null || ingredientCardIconImages.Length < 4)
                    ingredientCardIconImages = new Image[4];
                ingredientCardIconImages[i] = iconImg;

                var iconOutline = iconTrans.GetComponent<Outline>() ?? iconTrans.gameObject.AddComponent<Outline>();
                iconOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                iconOutline.effectDistance = new Vector2(1f, -1f);

                CraftIngredientDraggable draggable = iconTrans.GetComponent<CraftIngredientDraggable>() ?? iconTrans.gameObject.AddComponent<CraftIngredientDraggable>();
                draggable.source = CraftDragSource.Stock;
                draggable.ingredientType = type;
                draggable.craftPanelRect = craftRt;
                draggable.rootCanvas = canvas;
                craftStockDraggables[i] = draggable;

                // Badge Stok di pojok atas Icon Box
                Transform badgeTrans = iconTrans.Find("Badge");
                if (badgeTrans == null)
                {
                    GameObject badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    badgeGo.layer = 5;
                    badgeGo.transform.SetParent(iconTrans, false);
                    badgeTrans = badgeGo.transform;
                }

                RectTransform badgeRt = (RectTransform)badgeTrans;
                badgeRt.anchorMin = new Vector2(1f, 1f);
                badgeRt.anchorMax = new Vector2(1f, 1f);
                badgeRt.pivot = new Vector2(1f, 1f);
                badgeRt.anchoredPosition = new Vector2(4f, 4f);
                badgeRt.sizeDelta = new Vector2(30f, 18f);

                Image badgeBg = badgeTrans.GetComponent<Image>() ?? badgeTrans.gameObject.AddComponent<Image>();
                badgeBg.color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
                badgeBg.raycastTarget = false;

                Transform badgeTextTrans = badgeTrans.Find("Text");
                if (badgeTextTrans == null)
                {
                    GameObject btGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
                    btGo.layer = 5;
                    btGo.transform.SetParent(badgeTrans, false);
                    badgeTextTrans = btGo.transform;
                }

                RectTransform btRt = (RectTransform)badgeTextTrans;
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero;
                btRt.offsetMax = Vector2.zero;

                TMPro.TextMeshProUGUI badgeText = UITextUtil.EnsureText(badgeTextTrans.gameObject, 12f);
                badgeText.text = "0x";
                badgeText.fontSize = 12f;
                badgeText.fontStyle = TMPro.FontStyles.Bold;
                badgeText.alignment = TMPro.TextAlignmentOptions.Center;
                badgeText.color = Color.white;
                badgeText.enableWordWrapping = false;
                badgeText.overflowMode = TMPro.TextOverflowModes.Overflow;
                badgeText.raycastTarget = false;
                craftStockBadgeTexts[i] = badgeText;

                // --- Sisi Kanan: Text Container (Title & Desc) ---
                Transform textContTrans = cardTrans.Find("Text_Container");
                if (textContTrans == null)
                {
                    GameObject tcGo = new GameObject("Text_Container", typeof(RectTransform));
                    tcGo.layer = 5;
                    tcGo.transform.SetParent(cardTrans, false);
                    textContTrans = tcGo.transform;
                }

                RectTransform tcRt = (RectTransform)textContTrans;
                tcRt.anchorMin = Vector2.zero;
                tcRt.anchorMax = Vector2.one;
                tcRt.offsetMin = new Vector2(80f, 6f);
                tcRt.offsetMax = new Vector2(-8f, -6f);

                // Title (TextMeshPro)
                Transform titleTrans = textContTrans.Find("Title");
                if (titleTrans == null)
                {
                    GameObject tGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                    tGo.layer = 5;
                    tGo.transform.SetParent(textContTrans, false);
                    titleTrans = tGo.transform;
                }

                RectTransform titleRt = (RectTransform)titleTrans;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, 0f);
                titleRt.sizeDelta = new Vector2(0f, 24f);

                TMPro.TextMeshProUGUI titleText = UITextUtil.EnsureText(titleTrans.gameObject, ingredientCardTitleFontSize);
                titleText.text = data.ingredientTitle;
                titleText.fontStyle = TMPro.FontStyles.Bold;
                titleText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                titleText.color = new Color(1f, 0.88f, 0.45f, 1f);
                titleText.enableWordWrapping = true;
                titleText.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                titleText.raycastTarget = false;
                _ingredientCardTitles[i] = titleText;

                // Desc (TextMeshPro)
                Transform descTrans = textContTrans.Find("Desc");
                if (descTrans == null)
                {
                    GameObject dGo = new GameObject("Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                    dGo.layer = 5;
                    dGo.transform.SetParent(textContTrans, false);
                    descTrans = dGo.transform;
                }

                RectTransform descRt = (RectTransform)descTrans;
                descRt.anchorMin = new Vector2(0f, 0f);
                descRt.anchorMax = new Vector2(1f, 1f);
                descRt.pivot = new Vector2(0f, 1f);
                descRt.offsetMin = new Vector2(0f, 0f);
                descRt.offsetMax = new Vector2(0f, -24f);

                TMPro.TextMeshProUGUI descText = UITextUtil.EnsureText(descTrans.gameObject, ingredientCardInfoFontSize);
                descText.text = data.chemicalInfoText;
                descText.fontStyle = TMPro.FontStyles.Normal;
                descText.alignment = TMPro.TextAlignmentOptions.TopLeft;
                descText.color = new Color(0.80f, 0.84f, 0.90f, 0.95f);
                descText.enableWordWrapping = true;
                descText.overflowMode = TMPro.TextOverflowModes.Overflow;
                descText.raycastTarget = false;
                _ingredientCardInfos[i] = descText;
            }
        }

        // ------------------------------------------------------------------
        // Laci Buku Resep Geser (Slide-Out Recipe Drawer) — Tanpa Coroutine
        // ------------------------------------------------------------------

        const float DrawerTabWidth = 46f;
        const float DrawerTabHeight = 132f;

        /// <summary>true bila laci resep sedang terbuka (meluncur keluar ke kiri).</summary>
        public bool IsRecipeDrawerOpen { get { return _recipeDrawerOpen; } }

        void UpdateRecipeDrawer()
        {
            if (_recipeDrawer == null)
                return;

            float targetX = _recipeDrawerOpen ? -(DrawerTabWidth + 4f) : _recipeDrawerClosedX;
            Vector2 pos = _recipeDrawer.anchoredPosition;
            pos.x = Mathf.MoveTowards(pos.x, targetX, recipeDrawerSpeed * Time.unscaledDeltaTime);
            _recipeDrawer.anchoredPosition = pos;
        }

        /// <summary>Buka / tutup laci resep (tab bookmark atau shortcut R saat crafting).</summary>
        public void ToggleRecipeDrawer()
        {
            if (craftPanel == null || !craftPanel.activeSelf)
                return;

            if (!_recipeDrawerBuilt)
                BuildRecipeDrawer(craftPanel.GetComponent<RectTransform>());

            _recipeDrawerOpen = !_recipeDrawerOpen;
            RefreshRecipeDrawerTab();
        }

        public void OpenRecipeDrawer()
        {
            if (craftPanel == null || !craftPanel.activeSelf)
                return;

            if (!_recipeDrawerBuilt)
                BuildRecipeDrawer(craftPanel.GetComponent<RectTransform>());

            _recipeDrawerOpen = true;
            RefreshRecipeDrawerTab();
        }

        public void CloseRecipeDrawer()
        {
            if (_recipeDrawerOpen)
            {
                _recipeDrawerOpen = false;
                RefreshRecipeDrawerTab();
            }
        }

        /// <summary>
        /// Data resep default — hanya dipakai sebagai nilai AWAL untuk field Inspector
        /// yang masih kosong. Mengubah isi Inspector tidak pernah ditimpa oleh ini.
        /// </summary>
        static RecipeCardData[] CreateDefaultRecipeCards()
        {
            string[] titles =
            {
                "Gel Sabun Beku",
                "Kabut Asam Dingin",
                "Ledakan Es Instan",
                "Asam Pelarut Karat",
                "Bom Netralisasi",
                "Busa Sabun Mengembang"
            };

            string[] reactions =
            {
                "Reaksi saponifikasi & pendinginan eksotermik menghasilkan gel kriogenik yang membekukan pergerakan.",
                "Asam yang diuapkan lalu didinginkan mengembun menjadi kabut korosif yang melapisi zirah sasaran.",
                "Pendingin dan katalis memicu kristalisasi kilat: ledakan kristal es yang menghentikan laju reaksi.",
                "Asam pekat berbantu katalis melarutkan lapisan karat dan zirah besi dalam sekejap.",
                "Netralisasi asam-basa kuat melepaskan gelombang kejut CO2 dan uap panas.",
                "Busa sabun mengembang oleh katalis: gelembung kimia mendorong sekaligus memperlambat."
            };

            string[] targets =
            {
                "Efektif: Zombie Api",
                "Efektif: Zombie Besi",
                "Efektif: Zombie Asam",
                "Efektif: Zombie Besi",
                "Efektif: Zombie Basa",
                "Efektif: Zombie Asam"
            };

            var cards = new RecipeCardData[6];
            for (int i = 0; i < 6; i++)
            {
                cards[i] = new RecipeCardData
                {
                    recipeTitle = titles[i],
                    scientificDescription = reactions[i],
                    effectiveTargetText = targets[i]
                };
            }
            return cards;
        }

        static IngredientCardData[] CreateDefaultIngredientCards()
        {
            return new[]
            {
                new IngredientCardData
                {
                    ingredientTitle = "Cairan Basa (OH<sup>-</sup>)",
                    chemicalInfoText = "pH tinggi (pH 12), licin seperti sabun, menetralkan asam.",
                    themeColor = AlchemyDatabase.IngredientColor(IngredientType.BahanBasa_Biru)
                },
                new IngredientCardData
                {
                    ingredientTitle = "Cairan Pendingin (-\u0394H)",
                    chemicalInfoText = "Reaksi endotermik, menyerap kalor, membekukan kobaran api.",
                    themeColor = AlchemyDatabase.IngredientColor(IngredientType.BahanPendingin_Cyan)
                },
                new IngredientCardData
                {
                    ingredientTitle = "Cairan Asam (H<sup>+</sup>)",
                    chemicalInfoText = "pH rendah (pH 2), sangat korosif, melarutkan zirah besi.",
                    themeColor = AlchemyDatabase.IngredientColor(IngredientType.BahanAsam_Merah)
                },
                new IngredientCardData
                {
                    ingredientTitle = "Bubuk Katalis",
                    chemicalInfoText = "Menurunkan energi aktivasi, melipatgandakan laju reaksi kimia.",
                    themeColor = AlchemyDatabase.IngredientColor(IngredientType.BahanKatalis_Kuning)
                }
            };
        }

        /// <summary>Pastikan array data Inspector punya panjang benar (6 resep, 4 bahan) & tidak null.</summary>
        void EnsureDrawerData()
        {
            if (drawerStyle == null)
                drawerStyle = new RecipeDrawerVisualStyle();

            RecipeCardData[] defaults = CreateDefaultRecipeCards();

            if (recipeCards == null || recipeCards.Length != 6)
            {
                RecipeCardData[] old = recipeCards;
                recipeCards = new RecipeCardData[6];
                if (old != null)
                {
                    for (int i = 0; i < old.Length && i < 6; i++)
                        recipeCards[i] = old[i];
                }
            }

            for (int i = 0; i < 6; i++)
            {
                if (recipeCards[i] == null)
                    recipeCards[i] = defaults[i];
                else
                    FillEmptyRecipeFields(recipeCards[i], defaults[i]);
            }

            IngredientCardData[] ingDefaults = CreateDefaultIngredientCards();

            if (ingredientCards == null || ingredientCards.Length != 4)
            {
                IngredientCardData[] oldIng = ingredientCards;
                ingredientCards = new IngredientCardData[4];
                if (oldIng != null)
                {
                    for (int i = 0; i < oldIng.Length && i < 4; i++)
                        ingredientCards[i] = oldIng[i];
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (ingredientCards[i] == null)
                    ingredientCards[i] = ingDefaults[i];
                else
                    FillEmptyIngredientFields(ingredientCards[i], ingDefaults[i]);
            }
        }

        static void FillEmptyRecipeFields(RecipeCardData card, RecipeCardData fallback)
        {
            if (string.IsNullOrEmpty(card.recipeTitle)) card.recipeTitle = fallback.recipeTitle;
            if (string.IsNullOrEmpty(card.scientificDescription)) card.scientificDescription = fallback.scientificDescription;
            if (string.IsNullOrEmpty(card.effectiveTargetText)) card.effectiveTargetText = fallback.effectiveTargetText;
            if (string.IsNullOrEmpty(card.arrowText)) card.arrowText = fallback.arrowText;
            if (string.IsNullOrEmpty(card.plusText)) card.plusText = fallback.plusText;
        }

        static void FillEmptyIngredientFields(IngredientCardData card, IngredientCardData fallback)
        {
            if (string.IsNullOrEmpty(card.ingredientTitle)) card.ingredientTitle = fallback.ingredientTitle;
            if (string.IsNullOrEmpty(card.chemicalInfoText)) card.chemicalInfoText = fallback.chemicalInfoText;
            if (card.themeColor.a <= 0f) card.themeColor = fallback.themeColor;
        }

        /// <summary>
        /// API Editor: bangun Recipe_Drawer + 6 kartu permanen di bawah Canvas -> Craft_panel
        /// (dipakai menu Tools agar objek tersimpan ke SampleScene, bukan hanya saat Play).
        /// </summary>
        public void EditorBuildRecipeDrawerPermanently()
        {
            if (craftPanel == null)
                craftPanel = FindByName(canvas != null ? canvas.transform : transform.root, "Craft_panel");

            if (craftPanel == null)
                return;

            RectTransform craftRt = craftPanel.GetComponent<RectTransform>();
            if (craftRt == null)
                return;

            // Jika sudah ada sisa dari Play Mode / run sebelumnya, bersihkan agar tidak dobel.
            RemoveExistingDrawerObjects(craftRt);

            _recipeDrawerBuilt = false;
            _recipeDrawer = null;
            _recipeDrawerTab = null;
            _recipeDrawerTabLabel = null;

            BuildRecipeDrawer(craftRt);
        }

        /// <summary>Hapus objek laci lama (window, drawer, tab) supaya hasil generate tidak dobel.</summary>
        void RemoveExistingDrawerObjects(RectTransform craftRt)
        {
            if (craftRt == null)
                return;

            string[] names = { "Recipe_Drawer_Window", "Recipe_Drawer", "Recipe_Drawer_Tab" };
            for (int n = 0; n < names.Length; n++)
            {
                Transform found = craftRt.Find(names[n]);
                if (found != null)
                {
                    if (Application.isPlaying) Destroy(found.gameObject);
                    else DestroyImmediate(found.gameObject);
                }
            }
        }

        /// <summary>
        /// API Editor: bangun ulang 4 kartu bahan (sisi kiri Craft_panel) secara permanen
        /// memakai data Inspector <see cref="ingredientCards"/>.
        /// </summary>
        public void EditorBuildIngredientCardsPermanently()
        {
            if (craftPanel == null)
                craftPanel = FindByName(canvas != null ? canvas.transform : transform.root, "Craft_panel");

            if (craftPanel == null)
                return;

            Transform leftSub = craftPanel.transform.Find("Resep");
            if (leftSub == null)
                leftSub = craftPanel.transform.Find("Bahan_Scroll");
            if (leftSub == null)
                return;

            EnsureDrawerData();
            SetupCraftIngredientsScroll(leftSub);
        }

        /// <summary>
        /// API Editor: pastikan label shortcut (1–6) dan badge stok (0x/1x/…) pada Hotbar
        /// memakai TextMeshProUGUI ber-font Cinzel dan tersimpan permanen di SampleScene
        /// (bukan hanya dibuat saat Play Mode).
        /// </summary>
        public void EditorBuildHotbarTextsPermanently()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (hotbarRoot == null && canvas != null)
                hotbarRoot = FindByName(canvas.transform, "Hotbar");
            if (hotbarRoot == null)
                return;

            WireHotbar();
        }

        void RefreshRecipeDrawerTab()
        {
            if (_recipeDrawerTabLabel == null)
                return;

            string arrow = _recipeDrawerOpen ? ">" : "<";
            string format = drawerStyle != null && !string.IsNullOrEmpty(drawerStyle.bookmarkTabLabelFormat)
                ? drawerStyle.bookmarkTabLabelFormat
                : "RESEP\n{0}";

            _recipeDrawerTabLabel.text = string.Format(format, arrow);
        }

        /// <summary>
        /// Adopsi laci yang sudah ada di scene (hasil menu Editor). Mengembalikan false bila
        /// tidak ditemukan, sehingga builder runtime akan membuatnya dari nol.
        /// Referensi dikumpulkan berdasarkan struktur nama (window → drawer → content → kartu).
        /// </summary>
        bool TryAdoptExistingDrawer(RectTransform craftRt)
        {
            Transform window = craftRt.Find("Recipe_Drawer_Window");
            Transform drawer = window != null ? window.Find("Recipe_Drawer") : craftRt.Find("Recipe_Drawer");
            if (drawer == null)
                return false;

            RectTransform drawerRt = drawer as RectTransform;
            if (drawerRt == null)
                return false;

            _recipeDrawer = drawerRt;
            _recipeDrawerClosedX = drawerRt.anchoredPosition.x;

            _recipeDrawerBackground = drawer.GetComponent<Image>();

            Transform tab = craftRt.Find("Recipe_Drawer_Tab");
            if (tab != null)
            {
                _recipeDrawerTab = tab as RectTransform;
                _recipeDrawerBookmarkImage = tab.GetComponent<Image>();

                Transform label = tab.Find("Label");
                if (label != null)
                    _recipeDrawerTabLabel = label.GetComponent<TMPro.TextMeshProUGUI>();
            }

            Transform content = drawer.Find("Content");
            if (content != null)
            {
                _recipeCardFrames = new Image[6];
                _recipeResultIcons = new Image[6];
                _recipeIngredientAIcons = new Image[6];
                _recipeIngredientBIcons = new Image[6];
                recipeCardRoots = new RectTransform[6];

                for (int i = 0; i < 6; i++)
                {
                    Transform card = content.Find("Recipe_Card_" + i);
                    if (card == null) continue;

                    recipeCardRoots[i] = card as RectTransform;
                    _recipeCardFrames[i] = card.GetComponent<Image>();

                    Transform result = card.Find("Result_Box");
                    if (result != null)
                        _recipeResultIcons[i] = result.GetComponent<Image>();

                    Transform ing1 = card.Find("Ing1_Box") ?? card.Find("Icon_A");
                    if (ing1 != null) _recipeIngredientAIcons[i] = ing1.GetComponent<Image>();

                    Transform ing2 = card.Find("Ing2_Box") ?? card.Find("Icon_B");
                    if (ing2 != null) _recipeIngredientBIcons[i] = ing2.GetComponent<Image>();
                }
            }

            // Kartu bahan di sisi kiri (bila ada) di-adopsi juga agar teks Inspector ikut sync.
            _ingredientCardTitles = new TMPro.TextMeshProUGUI[4];
            _ingredientCardInfos = new TMPro.TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                Transform card = craftRt.Find("Card_" + (IngredientType)i);
                if (card == null) continue;

                Transform title = card.Find("Text_Container/Title");
                if (title != null)
                    _ingredientCardTitles[i] = title.GetComponent<TMPro.TextMeshProUGUI>();

                Transform desc = card.Find("Text_Container/Desc");
                if (desc != null)
                    _ingredientCardInfos[i] = desc.GetComponent<TMPro.TextMeshProUGUI>();
            }

            _recipeDrawerBuilt = true;
            return true;
        }

        void BuildRecipeDrawer(RectTransform craftRt)
        {
            if (craftRt == null)
                return;

            EnsureDrawerData();

            if (_recipeDrawerBuilt)
            {
                // Sudah ada instance di scene: jangan bangun ulang, cukup sinkronkan
                // data yang mungkin diubah user (tanpa menimpa teks/warna hardcoded).
                SyncRecipeDrawerFromConfig();
                return;
            }

            // Kalau Recipe_Drawer sudah dipasang PERMANEN di scene (lewat menu Editor),
            // gunakan objek itu apa adanya: re-bind referensi & baca ukuran dari RectTransform.
            if (TryAdoptExistingDrawer(craftRt))
            {
                SyncRecipeDrawerFromConfig();
                return;
            }

            _recipeDrawerBuilt = true;

            float panelWidth = craftRt.sizeDelta.x;
            if (panelWidth < 100f) panelWidth = 800f;
            float drawerWidth = Mathf.Min(Mathf.Clamp(recipeDrawerWidth, 240f, 520f), panelWidth * 0.55f);
            _recipeDrawerClosedX = drawerWidth + 2f;

            // --- Jendela kliping (RectMask2D) menempel di tepi kiri Craft_panel ---
            GameObject winGo = new GameObject("Recipe_Drawer_Window", typeof(RectTransform), typeof(RectMask2D));
            winGo.layer = 5;
            winGo.transform.SetParent(craftRt, false);
            winGo.transform.SetAsFirstSibling();

            RectTransform winRt = (RectTransform)winGo.transform;
            winRt.anchorMin = new Vector2(0f, 0f);
            winRt.anchorMax = new Vector2(0f, 1f);
            winRt.pivot = new Vector2(1f, 0.5f);
            winRt.anchoredPosition = Vector2.zero;
            winRt.sizeDelta = new Vector2(drawerWidth + DrawerTabWidth + 20f, -20f);
            winRt.localScale = Vector3.one;

            // --- Root laci: ScrollRect vertikal + latar ---
            GameObject drawerGo = new GameObject("Recipe_Drawer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            drawerGo.layer = 5;
            drawerGo.transform.SetParent(winRt, false);

            _recipeDrawer = (RectTransform)drawerGo.transform;
            _recipeDrawer.anchorMin = new Vector2(1f, 0f);
            _recipeDrawer.anchorMax = new Vector2(1f, 1f);
            _recipeDrawer.pivot = new Vector2(1f, 0.5f);
            _recipeDrawer.sizeDelta = new Vector2(drawerWidth, -8f);
            _recipeDrawer.anchoredPosition = new Vector2(_recipeDrawerClosedX, 0f);
            _recipeDrawer.localScale = Vector3.one;

            Image drawerBg = drawerGo.GetComponent<Image>();
            _recipeDrawerBackground = drawerBg;
            ApplyFrame(drawerStyle.drawerBackgroundSprite, drawerStyle.drawerBackgroundColor,
                drawerBg, Color.white);
            drawerBg.raycastTarget = true;

            ScrollRect scroll = drawerGo.GetComponent<ScrollRect>();
            scroll.viewport = _recipeDrawer;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.decelerationRate = 0.12f;

            // --- Content (6 kartu resep) ---
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.layer = 5;
            contentGo.transform.SetParent(_recipeDrawer, false);

            RectTransform contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;
            contentRt.localScale = Vector3.one;

            scroll.content = contentRt;

            VerticalLayoutGroup vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 12, 12);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _recipeCardFrames = new Image[6];
            _recipeResultIcons = new Image[6];
            _recipeIngredientAIcons = new Image[6];
            _recipeIngredientBIcons = new Image[6];
            recipeCardRoots = new RectTransform[6];

            for (int i = 0; i < 6; i++)
            {
                BuildRecipeCard(contentRt, i);
            }

            BuildDrawerBookmark(craftRt);

            RefreshRecipeDrawerTab();
            SyncRecipeDrawerFromConfig();

            // Terapkan sprite dari sumber tunggal (bahan + ramuan) setelah kartu terbangun.
            ApplyAllPotionSprites();
        }

        void BuildDrawerBookmark(RectTransform craftRt)
        {
            // --- Tab bookmark di tepi kiri Craft_panel (selalu terlihat) ---
            GameObject tabGo = new GameObject("Recipe_Drawer_Tab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            tabGo.layer = 5;
            tabGo.transform.SetParent(craftRt, false);

            _recipeDrawerTab = (RectTransform)tabGo.transform;
            _recipeDrawerTab.anchorMin = new Vector2(0f, 0.5f);
            _recipeDrawerTab.anchorMax = new Vector2(0f, 0.5f);
            _recipeDrawerTab.pivot = new Vector2(1f, 0.5f);
            _recipeDrawerTab.anchoredPosition = new Vector2(-2f, 0f);
            _recipeDrawerTab.sizeDelta = new Vector2(DrawerTabWidth, DrawerTabHeight);
            _recipeDrawerTab.localScale = Vector3.one;

            _recipeDrawerBookmarkImage = tabGo.GetComponent<Image>();
            ApplyFrame(drawerStyle.bookmarkTabSprite, drawerStyle.bookmarkTabColor,
                _recipeDrawerBookmarkImage, Color.white);

            Outline tabOutline = tabGo.GetComponent<Outline>();
            tabOutline.effectColor = drawerStyle.bookmarkTabOutlineColor;
            tabOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Button tabButton = tabGo.GetComponent<Button>();
            tabButton.targetGraphic = _recipeDrawerBookmarkImage;
            tabButton.onClick.AddListener(ToggleRecipeDrawer);

            TMPro.TextMeshProUGUI label = UITextUtil.CreateText(tabGo.transform, "Label", drawerTabFontSize);
            RectTransform tabLabelRt = label.rectTransform;
            tabLabelRt.anchorMin = Vector2.zero;
            tabLabelRt.anchorMax = Vector2.one;
            tabLabelRt.offsetMin = new Vector2(2f, 6f);
            tabLabelRt.offsetMax = new Vector2(-2f, -6f);
            tabLabelRt.localScale = Vector3.one;

            label.fontStyle = TMPro.FontStyles.Bold;
            label.alignment = TMPro.TextAlignmentOptions.Center;
            label.color = new Color(0.24f, 0.17f, 0.06f, 1f);
            label.enableWordWrapping = true;
            label.raycastTarget = false;

            _recipeDrawerTabLabel = label;
        }

        void BuildRecipeCard(RectTransform contentRt, int i)
        {
            RecipeCardData data = recipeCards[i];

            IngredientType ingA;
            IngredientType ingB;
            AlchemyManager.GetSlotIngredients(i, out ingA, out ingB);

            GameObject cardGo = new GameObject("Recipe_Card_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(Button), typeof(Outline));
            cardGo.layer = 5;
            cardGo.transform.SetParent(contentRt, false);

            if (recipeCardRoots == null || recipeCardRoots.Length < 6) recipeCardRoots = new RectTransform[6];
            recipeCardRoots[i] = (RectTransform)cardGo.transform;

            LayoutElement cardLayout = cardGo.GetComponent<LayoutElement>();
            cardLayout.preferredHeight = 196f;
            cardLayout.minHeight = 196f;

            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.raycastTarget = true;

            Outline cardOutline = cardGo.GetComponent<Outline>();
            cardOutline.effectColor = drawerStyle.cardOutlineColor;
            cardOutline.effectDistance = drawerStyle.cardOutlineDistance;

            Button cardButton = cardGo.GetComponent<Button>();
            cardButton.targetGraphic = cardBg;

            ColorBlock cardColors = cardButton.colors;
            cardColors.normalColor = Color.white;
            cardColors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            cardColors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cardColors.selectedColor = Color.white;
            cardColors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cardColors.colorMultiplier = 1f;
            cardColors.fadeDuration = 0.08f;
            cardButton.colors = cardColors;

            int slotIndex = i;
            cardButton.onClick.AddListener(delegate { QuickFillRecipe(slotIndex); });

            // Baris 1: Judul ramuan
            CreateCardText(cardGo.transform, "Title",
                10f, 8f, 10f, 26f, 0f,
                data.recipeTitle, drawerTitleFontSize, data.titleColor,
                TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);

            // Baris 2: Formula [hasil] <- [bahan1] + [bahan2]
            _recipeResultIcons[i] = CreateIconBox(cardGo.transform, "Result_Box", new Vector2(10f, -40f), 50f,
                AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]));

            CreateCardText(cardGo.transform, "Arrow",
                64f, 38f, 0f, 50f, 30f,
                data.arrowText, drawerFormulaGlyphFontSize, new Color(0.95f, 0.92f, 0.86f, 1f),
                TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            CreateIconBox(cardGo.transform, "Ing1_Box", new Vector2(100f, -44f), 44f,
                AlchemyDatabase.IngredientColor(ingA));

            CreateCardText(cardGo.transform, "Plus",
                148f, 38f, 0f, 50f, 24f,
                data.plusText, drawerFormulaGlyphFontSize, new Color(0.95f, 0.92f, 0.86f, 1f),
                TMPro.TextAlignmentOptions.Center, TMPro.FontStyles.Bold);

            CreateIconBox(cardGo.transform, "Ing2_Box", new Vector2(176f, -44f), 44f,
                AlchemyDatabase.IngredientColor(ingB));

            // Simpan referensi ikon bahan agar sprite dari sumber tunggal bisa diterapkan.
            if (_recipeIngredientAIcons == null || _recipeIngredientAIcons.Length < 6) _recipeIngredientAIcons = new Image[6];
            if (_recipeIngredientBIcons == null || _recipeIngredientBIcons.Length < 6) _recipeIngredientBIcons = new Image[6];
            Transform ing1T = cardGo.transform.Find("Ing1_Box");
            Transform ing2T = cardGo.transform.Find("Ing2_Box");
            if (ing1T != null) _recipeIngredientAIcons[i] = ing1T.GetComponent<Image>();
            if (ing2T != null) _recipeIngredientBIcons[i] = ing2T.GetComponent<Image>();

            // Baris 3: Deskripsi ilmiah
            CreateCardText(cardGo.transform, "Description",
                10f, 96f, 10f, 56f, 0f,
                data.scientificDescription, drawerDescriptionFontSize, data.descriptionColor,
                TMPro.TextAlignmentOptions.TopLeft, TMPro.FontStyles.Normal);

            // Baris 4: Target efektif
            CreateCardText(cardGo.transform, "Target",
                10f, 158f, 10f, 28f, 0f,
                data.effectiveTargetText, drawerTargetFontSize, data.targetColor,
                TMPro.TextAlignmentOptions.MidlineLeft, TMPro.FontStyles.Bold);

            _recipeCardFrames[i] = cardBg;
        }

        /// <summary>
        /// Sinkronkan sprite, warna, dan teks laci dengan nilai Inspector saat ini.
        /// Aman dipanggil berulang; tidak menyentuh posisi/ukuran yang diatur user di Scene.
        /// </summary>
        public void SyncRecipeDrawerFromConfig()
        {
            EnsureDrawerData();

            if (_recipeDrawerBackground != null)
                ApplyFrame(drawerStyle.drawerBackgroundSprite, drawerStyle.drawerBackgroundColor,
                    _recipeDrawerBackground, Color.white);

            if (_recipeDrawerBookmarkImage != null)
                ApplyFrame(drawerStyle.bookmarkTabSprite, drawerStyle.bookmarkTabColor,
                    _recipeDrawerBookmarkImage, Color.white);

            if (_recipeCardFrames != null)
            {
                for (int i = 0; i < _recipeCardFrames.Length && i < recipeCards.Length; i++)
                {
                    if (_recipeCardFrames[i] == null) continue;

                    ApplyFrame(drawerStyle.recipeCardFrameSprite, drawerStyle.recipeCardFrameColor,
                        _recipeCardFrames[i], Color.white);

                    if (_recipeResultIcons != null && i < _recipeResultIcons.Length && _recipeResultIcons[i] != null)
                    {
                        // Prioritas: override per-kartu → compoundPotionSprites (sumber tunggal) → warna.
                        Sprite icon = recipeCards[i] != null && recipeCards[i].customPotionIcon != null
                            ? recipeCards[i].customPotionIcon
                            : SafeCompoundSprite(i);

                        if (icon != null)
                        {
                            ApplyIconSprite(_recipeResultIcons[i], icon);
                        }
                        else
                        {
                            // Sprite campuran masih null → pertahankan sprite bawaan scene
                            // (mis. frame "Kotak ux"); jangan di-reset jadi kotak putih polos.
                            _recipeResultIcons[i].color = AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]);
                        }
                    }

                    ApplyRecipeCardText(i);
                }
            }

            ApplyIngredientCardText();
            RefreshRecipeDrawerTab();
        }

        void ApplyRecipeCardText(int i)
        {
            if (_recipeCardFrames == null || i >= _recipeCardFrames.Length || _recipeCardFrames[i] == null)
                return;

            Transform card = _recipeCardFrames[i].transform;
            RecipeCardData data = recipeCards[i];

            ApplyText(card, "Title", data.recipeTitle, drawerTitleFontSize, data.titleColor);
            ApplyText(card, "Description", data.scientificDescription, drawerDescriptionFontSize, data.descriptionColor);
            ApplyText(card, "Target", data.effectiveTargetText, drawerTargetFontSize, data.targetColor);
            ApplyText(card, "Arrow", data.arrowText, drawerFormulaGlyphFontSize, new Color(0.95f, 0.92f, 0.86f, 1f));
            ApplyText(card, "Plus", data.plusText, drawerFormulaGlyphFontSize, new Color(0.95f, 0.92f, 0.86f, 1f));
        }

        static void ApplyText(Transform parent, string childName, string content, float fontSize, Color color)
        {
            Transform child = parent.Find(childName);
            if (child == null) return;

            TMPro.TextMeshProUGUI text = child.GetComponent<TMPro.TextMeshProUGUI>();
            if (text == null) return;

            if (content != null)
                text.text = content;
            if (fontSize > 0f)
                text.fontSize = fontSize;
            text.color = color;
        }

        void ApplyIngredientCardText()
        {
            if (_ingredientCardTitles == null) return;

            for (int i = 0; i < _ingredientCardTitles.Length && i < ingredientCards.Length; i++)
            {
                if (_ingredientCardTitles[i] != null)
                {
                    _ingredientCardTitles[i].text = ingredientCards[i].ingredientTitle;
                    _ingredientCardTitles[i].fontSize = ingredientCardTitleFontSize;
                }

                if (_ingredientCardInfos != null && i < _ingredientCardInfos.Length && _ingredientCardInfos[i] != null)
                {
                    _ingredientCardInfos[i].text = ingredientCards[i].chemicalInfoText;
                    _ingredientCardInfos[i].fontSize = ingredientCardInfoFontSize;
                }
            }
        }

        /// <summary>
        /// Terapkan sprite bingkai + warna. Sprite null = warna solid; sprite ada = sprite
        /// di-tint dengan warna tersebut (9-slice bila border sprite tersedia).
        /// </summary>
        static void ApplyFrame(Sprite sprite, Color backgroundColor, Image target, Color fallbackColor)
        {
            if (target == null) return;

            if (sprite != null)
            {
                target.sprite = sprite;
                target.type = sprite.border.sqrMagnitude > 0.0f ? Image.Type.Sliced : Image.Type.Simple;
                target.color = backgroundColor;
            }
            else
            {
                target.sprite = null;
                target.type = Image.Type.Simple;
                target.color = backgroundColor;
            }
        }

        /// <summary>Isi otomatis slot1 &amp; slot2 kuali dari kartu resep yang diklik (jika stok cukup).</summary>
        void QuickFillRecipe(int slot)
        {
            if (alchemy == null) return;

            IngredientType ingA;
            IngredientType ingB;
            AlchemyManager.GetSlotIngredients(slot, out ingA, out ingB);

            int[] stock = alchemy.ingredientStock;
            int idxA = (int)ingA;
            int idxB = (int)ingB;

            if (stock == null || idxA < 0 || idxB < 0 || idxA >= stock.Length || idxB >= stock.Length)
                return;

            // Stok efektif = stok dasar + bahan yang otomatis di-refund dari kuali.
            int availA = stock[idxA];
            int availB = stock[idxB];

            if (alchemy.slot1Filled && (int)alchemy.slot1 == idxA) availA++;
            if (alchemy.slot2Filled && (int)alchemy.slot2 == idxA) availA++;
            if (alchemy.slot1Filled && (int)alchemy.slot1 == idxB) availB++;
            if (alchemy.slot2Filled && (int)alchemy.slot2 == idxB) availB++;

            if (availA < 1 || availB < 1)
            {
                Debug.Log("[RecipeDrawer] Stok bahan tidak cukup untuk meracik " +
                          AlchemyDatabase.PotionName(AlchemyManager.FixedSlotPotions[slot]));
                return;
            }

            SelectSlot(slot);
            alchemy.SetCauldronSlot(0, ingA);
            alchemy.SetCauldronSlot(1, ingB);

            RefreshCraftPanel();
            RefreshUI(true);
        }

        /// <summary>
        /// Kotak ikon formula (bisa sprite bila nanti diganti user di Inspector).
        /// Kotak hasil resep juga dipakai untuk ikon ramuan kustom.
        /// </summary>
        static Image CreateIconBox(Transform parent, string name, Vector2 topLeftPos, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            go.layer = 5;
            go.transform.SetParent(parent, false);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeftPos;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.one;

            Image img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        /// <summary>
        /// Teks kartu memakai TextMeshProUGUI (SDF) agar tajam di semua resolusi.
        /// Font SDF berasal dari project (TMP Settings), bukan font bitmap legacy.
        /// </summary>
        static void CreateCardText(Transform parent, string name,
            float left, float top, float right, float height, float fixedWidth,
            string content, float fontSize, Color color,
            TMPro.TextAlignmentOptions alignment, TMPro.FontStyles style)
        {
            TMPro.TextMeshProUGUI txt = UITextUtil.CreateText(parent, name, fontSize);
            RectTransform rt = txt.rectTransform;
            rt.pivot = new Vector2(0f, 1f);
            rt.localScale = Vector3.one;

            if (fixedWidth > 0f)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(left, -top);
                rt.sizeDelta = new Vector2(fixedWidth, height);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(left, -(top + height));
                rt.offsetMax = new Vector2(-right, -top);
            }

            txt.text = content ?? string.Empty;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.enableWordWrapping = true;
            txt.overflowMode = TMPro.TextOverflowModes.Overflow;
            txt.raycastTarget = false;
        }

        public void RefreshCraftPanel()
        {
            if (craftPanel == null || !craftPanel.activeSelf || alchemy == null)
                return;

            // 1. Refresh 4 Kartu Stok Bahan Dasar di sisi kiri
            if (craftStockBadgeTexts != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    int stock = (alchemy.ingredientStock != null && i < alchemy.ingredientStock.Length) ? alchemy.ingredientStock[i] : 0;
                    if (craftStockBadgeTexts[i] != null)
                    {
                        craftStockBadgeTexts[i].text = stock + "x";
                        craftStockBadgeTexts[i].color = stock > 0 ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
                    }

                    if (craftStockIconImages[i] != null)
                    {
                        // Bila PNG sumber tunggal sudah diisi: jangan recolor (warna PNG asli
                        // dipertahankan, hanya diredupkan saat stok 0).
                        if (SafeBaseSprite(i) != null)
                        {
                            float dim = stock > 0 ? 1f : 0.5f;
                            craftStockIconImages[i].color = new Color(dim, dim, dim, 1f);
                        }
                        else
                        {
                            Color baseColor = AlchemyDatabase.IngredientColor((IngredientType)i);
                            if (stock > 0)
                            {
                                craftStockIconImages[i].color = baseColor;
                            }
                            else
                            {
                                // Redup jika stok habis
                                craftStockIconImages[i].color = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 0.45f);
                            }
                        }
                    }
                }
            }

            // 2. Refresh Slot 1 di Kuali
            if (craftSlot1Image != null)
            {
                if (alchemy.slot1Filled)
                {
                    craftSlot1Image.color = AlchemyDatabase.IngredientColor(alchemy.slot1);
                }
                else
                {
                    craftSlot1Image.color = new Color(0.35f, 0.35f, 0.35f, 0.45f);
                }
            }

            // 3. Refresh Slot 2 di Kuali
            if (craftSlot2Image != null)
            {
                if (alchemy.slot2Filled)
                {
                    craftSlot2Image.color = AlchemyDatabase.IngredientColor(alchemy.slot2);
                }
                else
                {
                    craftSlot2Image.color = new Color(0.35f, 0.35f, 0.35f, 0.45f);
                }
            }

            // 4. Refresh Preview Hasil Ramuan
            if (craftResultImage != null)
            {
                if (alchemy.slot1Filled && alchemy.slot2Filled)
                {
                    PotionRecipeSO rec = alchemy.FindRecipe(alchemy.slot1, alchemy.slot2, alchemy.CurrentPH);
                    if (rec != null)
                    {
                        craftResultImage.color = AlchemyDatabase.PotionColor(rec.potionType);
                    }
                    else
                    {
                        craftResultImage.color = new Color(0.5f, 0.2f, 0.2f, 0.6f);
                    }
                }
                else
                {
                    craftResultImage.color = new Color(0.25f, 0.25f, 0.25f, 0.35f);
                }
            }

            // 5. Refresh Angka pH
            if (craftPHText != null)
            {
                SetText(craftPHText, "pH: " + alchemy.CurrentPH.ToString("0.0"));
            }

            // 6. Meja Racik (2 bahan + pratinjau hasil) & ikon panel pH
            ApplySpritesToMixingTable();
            ApplySpritesToPhIndicators();
        }

        void WireAudio()
        {
            if (musicSource != null && sfxSource != null) return;

            AudioSource[] sources = FindObjectsOfType<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                string n = sources[i].gameObject.name.ToLowerInvariant();
                if (musicSource == null && (n.Contains("music") || n.Contains("musik") || n.Contains("bgm")))
                    musicSource = sources[i];
                else if (sfxSource == null && (n.Contains("sfx") || n.Contains("sound") || n.Contains("effect")))
                    sfxSource = sources[i];
            }
        }

        void WireButtons()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(Resume);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(QuitToMenu);
            }
            if (optionsOpenButton != null)
            {
                optionsOpenButton.onClick.RemoveAllListeners();
                optionsOpenButton.onClick.AddListener(OpenOptions);
            }
            if (optionsCloseButton != null)
            {
                optionsCloseButton.onClick.RemoveAllListeners();
                optionsCloseButton.onClick.AddListener(CloseOptions);
            }
            if (recipeButton != null)
            {
                recipeButton.onClick.RemoveAllListeners();
                recipeButton.onClick.AddListener(ToggleRecipe);
            }
            if (pauseOpenButton != null)
            {
                pauseOpenButton.onClick.RemoveAllListeners();
                pauseOpenButton.onClick.AddListener(TogglePause);
            }
        }

        void WireSliders()
        {
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }
        }

        // ------------------------------------------------------------------
        // Audio
        // ------------------------------------------------------------------

        void LoadAudioPrefs()
        {
            _musicVolume = PlayerPrefs.GetFloat(PrefMusic, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(PrefSfx, 1f);

            if (musicSlider != null)
            {
                musicSlider.minValue = 0f; musicSlider.maxValue = 1f;
                musicSlider.value = _musicVolume;
            }
            if (sfxSlider != null)
            {
                sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
                sfxSlider.value = _sfxVolume;
            }

            ApplyAudio();
        }

        void ApplyAudio()
        {
            if (musicSource != null) musicSource.volume = _musicVolume;
            if (sfxSource != null) sfxSource.volume = _sfxVolume;
        }

        void OnMusicChanged(float v)
        {
            _musicVolume = v;
            PlayerPrefs.SetFloat(PrefMusic, v);
            if (musicSource != null) musicSource.volume = v;
        }

        void OnSfxChanged(float v)
        {
            _sfxVolume = v;
            PlayerPrefs.SetFloat(PrefSfx, v);
            if (sfxSource != null) sfxSource.volume = v;
        }

        // ------------------------------------------------------------------
        // Pause / Options / Quit
        // ------------------------------------------------------------------

        void TogglePause()
        {
            if (_paused) Resume();
            else Pause();
        }

        void Pause()
        {
            if (pauseMenu == null) return;
            _paused = true;
            Time.timeScale = 0f;
            pauseMenu.SetActive(true);
        }

        void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;
            if (pauseMenu != null) pauseMenu.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        void OpenOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(true);
        }

        void CloseOptions()
        {
            if (optionsPanel != null) optionsPanel.SetActive(false);
        }

        void QuitToMenu()
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(mainMenuScene) && Application.CanStreamedLevelBeLoaded(mainMenuScene))
                SceneManager.LoadScene(mainMenuScene);
            else
                Debug.LogWarning("[UIBridge] Scene '" + mainMenuScene + "' tidak ada di Build Settings.");
        }

        void ToggleRecipe()
        {
            if (recipePanel == null) return;
            recipePanel.SetActive(!recipePanel.activeSelf);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        public static void SetText(Component comp, string val)
        {
            if (comp == null) return;
            if (comp is TMPro.TMP_Text tmp) { tmp.text = val; return; }
            if (comp is Text legacy) { legacy.text = val; }
        }

        static Component FindTextComponentIn(GameObject go, params string[] keywords)
        {
            if (go == null) return null;
            Transform root = go.transform;

            TMPro.TMP_Text[] tmps = root.GetComponentsInChildren<TMPro.TMP_Text>(true);
            for (int k = 0; k < keywords.Length; k++)
                for (int i = 0; i < tmps.Length; i++)
                    if (tmps[i].gameObject.name.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return tmps[i];

            if (tmps.Length > 0) return tmps[0];

            // Cadangan: Text legacy (dipakai slot stok bahan di Bottom_panel/Base).
            Text[] legacies = root.GetComponentsInChildren<Text>(true);
            for (int k = 0; k < keywords.Length; k++)
                for (int i = 0; i < legacies.Length; i++)
                    if (legacies[i].gameObject.name.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return legacies[i];

            if (legacies.Length > 0) return legacies[0];
            return null;
        }

        static GameObject FindByName(Transform root, params string[] names)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].gameObject.name.IndexOf(names[n], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return all[i].gameObject;
                }
            }
            return null;
        }

        static Button FindButton(Transform root, params string[] keywords)
        {
            if (root == null) return null;
            Button[] btns = root.GetComponentsInChildren<Button>(true);
            for (int k = 0; k < keywords.Length; k++)
                for (int i = 0; i < btns.Length; i++)
                    if (btns[i].gameObject.name.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return btns[i];
            return null;
        }

        static Slider FindSlider(Transform root, params string[] keywords)
        {
            if (root == null) return null;
            Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
            for (int k = 0; k < keywords.Length; k++)
                for (int i = 0; i < sliders.Length; i++)
                    if (sliders[i].gameObject.name.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return sliders[i];
            return null;
        }
    }
}

namespace CuriesCrucible.Gameplay
{
    // Sprite ikon kini selalu dipasang langsung ke Image.sprite milik slot yang di-assign
    // di Inspector; penempatan sebagai child tidak lagi didukung.
}
