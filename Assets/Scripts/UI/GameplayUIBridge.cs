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
        public Text[] hotbarKeyLabels = new Text[6];
        public Text[] hotbarCountBadges = new Text[6];

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
        public Text[] craftStockBadgeTexts = new Text[4];
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

        // Laci Buku Resep Geser (runtime, dibangun sekali di dalam Craft_panel)
        RectTransform _recipeDrawer;
        RectTransform _recipeDrawerTab;
        Text _recipeDrawerTabLabel;
        float _recipeDrawerClosedX;
        bool _recipeDrawerOpen;
        bool _recipeDrawerBuilt;

        const string PrefMusic = "MusicVolume";
        const string PrefSfx = "SFXVolume";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindObjectOfType<GameplayUIBridge>() != null)
                return;

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                canvas.gameObject.AddComponent<GameplayUIBridge>();
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
            AutoWire();

            WireIngredients();
            WireHotbar();
            WireCraftPanel();
            WireButtons();
            WireSliders();
            RefreshUI(true);
        }

        void Update()
        {
            HandleKeyboard();
            UpdateRecipeDrawer();
            RefreshUI(false);
        }

        void ResolveManagers()
        {
            if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
            if (tower == null) tower = TowerHealthManager.Instance ?? FindObjectOfType<TowerHealthManager>();
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
            if (alchemy == null) return;

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
                        if (stockDisplayTexts[i] != null)
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

                    if (stockChanged)
                    {
                        _lastPotionStock[i] = stock;

                        // Badge jumlah ("0x", "1x", "2x")
                        if (hotbarCountBadges[i] != null)
                        {
                            hotbarCountBadges[i].text = stock + "x";
                            hotbarCountBadges[i].color = stock > 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.85f);
                        }

                        // Warna slot ramuan
                        if (hotbarIcons[i] != null)
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
                    if (selectedChanged && hotbarSlotRoots[i] != null)
                    {
                        bool isSel = (i == currentSelected);
                        hotbarSlotRoots[i].localScale = isSel ? Vector3.one * 1.15f : Vector3.one;

                        // Tambahkan kecerahan jika terpilih
                        if (hotbarIcons[i] != null && stock > 0)
                        {
                            Color potionCol = AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]);
                            hotbarIcons[i].color = isSel ? Color.Lerp(potionCol, Color.white, 0.35f) : potionCol;
                        }
                    }
                }

                _lastSelectedSlot = currentSelected;
            }
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

                // Terapkan warna bahan ke Image button
                Image img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = AlchemyDatabase.IngredientColor((IngredientType)i);
                }

                // Cari teks stok di child button (TMP_Text atau Text)
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

            Font safeFont = ZombieIntroSpotlight.GetSafeFont();
            if (safeFont == null)
                safeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (safeFont == null)
                safeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

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
                    Destroy(keyLabelTrans.gameObject);
                    keyLabelTrans = null;
                }

                if (keyLabelTrans == null)
                {
                    GameObject go = new GameObject("Key_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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

                CanvasRenderer keyCr = keyLabelTrans.GetComponent<CanvasRenderer>() ?? keyLabelTrans.gameObject.AddComponent<CanvasRenderer>();

                Text keyText = keyLabelTrans.GetComponent<Text>() ?? keyLabelTrans.gameObject.AddComponent<Text>();
                keyText.text = (i + 1).ToString();
                if (safeFont != null) keyText.font = safeFont;
                keyText.fontSize = 15;
                keyText.fontStyle = FontStyle.Bold;
                keyText.alignment = TextAnchor.MiddleCenter;
                keyText.color = new Color(1f, 0.88f, 0.45f, 1f); // Kuningan terang
                keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
                keyText.verticalOverflow = VerticalWrapMode.Overflow;
                keyText.raycastTarget = false;
                hotbarKeyLabels[i] = keyText;

                var keyOutline = keyLabelTrans.GetComponent<Outline>() ?? keyLabelTrans.gameObject.AddComponent<Outline>();
                keyOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                keyOutline.effectDistance = new Vector2(1f, -1f);

                // 3. Badge Jumlah Ramuan ("0x", "1x") di pojok samping/kanan bawah
                Transform badgeTrans = rt.Find("Count_Badge");
                if (badgeTrans != null && !(badgeTrans is RectTransform))
                {
                    Destroy(badgeTrans.gameObject);
                    badgeTrans = null;
                }

                if (badgeTrans == null)
                {
                    GameObject go = new GameObject("Count_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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

                CanvasRenderer badgeCr = badgeTrans.GetComponent<CanvasRenderer>() ?? badgeTrans.gameObject.AddComponent<CanvasRenderer>();

                Text badgeText = badgeTrans.GetComponent<Text>() ?? badgeTrans.gameObject.AddComponent<Text>();
                badgeText.text = "0x";
                if (safeFont != null) badgeText.font = safeFont;
                badgeText.fontSize = 14;
                badgeText.fontStyle = FontStyle.Bold;
                badgeText.alignment = TextAnchor.MiddleRight;
                badgeText.color = Color.white;
                badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                badgeText.verticalOverflow = VerticalWrapMode.Overflow;
                badgeText.raycastTarget = false;
                hotbarCountBadges[i] = badgeText;

                var badgeOutline = badgeTrans.GetComponent<Outline>() ?? badgeTrans.gameObject.AddComponent<Outline>();
                badgeOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                badgeOutline.effectDistance = new Vector2(1f, -1f);
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

            Font safeFont = ZombieIntroSpotlight.GetSafeFont();
            if (safeFont == null) safeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (safeFont == null) safeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Data 4 Bahan Dasar (Basa, Pendingin, Asam, Katalis)
            var ingData = new []
            {
                new {
                    type = IngredientType.BahanBasa_Biru,
                    title = "Cairan Basa (OH⁻)",
                    desc = "pH tinggi (pH 12), licin seperti sabun, menetralkan asam.",
                    color = AlchemyDatabase.IngredientColor(IngredientType.BahanBasa_Biru)
                },
                new {
                    type = IngredientType.BahanPendingin_Cyan,
                    title = "Cairan Pendingin (-ΔH)",
                    desc = "Reaksi endotermik, menyerap kalor, membekukan kobaran api.",
                    color = AlchemyDatabase.IngredientColor(IngredientType.BahanPendingin_Cyan)
                },
                new {
                    type = IngredientType.BahanAsam_Merah,
                    title = "Cairan Asam (H⁺)",
                    desc = "pH rendah (pH 2), sangat korosif, melarutkan zirah besi.",
                    color = AlchemyDatabase.IngredientColor(IngredientType.BahanAsam_Merah)
                },
                new {
                    type = IngredientType.BahanKatalis_Kuning,
                    title = "Bubuk Katalis",
                    desc = "Menurunkan energi aktivasi, melipatgandakan laju reaksi kimia.",
                    color = AlchemyDatabase.IngredientColor(IngredientType.BahanKatalis_Kuning)
                }
            };

            RectTransform craftRt = craftPanel != null ? craftPanel.GetComponent<RectTransform>() : null;

            for (int i = 0; i < 4; i++)
            {
                var data = ingData[i];
                string cardName = "Card_" + data.type;
                Transform cardTrans = contentTrans.Find(cardName);
                if (cardTrans == null)
                {
                    GameObject cardGo = new GameObject(cardName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                    cardGo.layer = 5;
                    cardGo.transform.SetParent(contentTrans, false);
                    cardTrans = cardGo.transform;
                }

                LayoutElement le = cardTrans.GetComponent<LayoutElement>() ?? cardTrans.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 90f;
                le.minHeight = 90f;

                Image cardBg = cardTrans.GetComponent<Image>() ?? cardTrans.gameObject.AddComponent<Image>();
                cardBg.color = new Color(0.14f, 0.17f, 0.23f, 0.95f);

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
                iconImg.color = data.color;
                iconImg.raycastTarget = true;
                craftStockIconImages[i] = iconImg;

                var iconOutline = iconTrans.GetComponent<Outline>() ?? iconTrans.gameObject.AddComponent<Outline>();
                iconOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                iconOutline.effectDistance = new Vector2(1f, -1f);

                CraftIngredientDraggable draggable = iconTrans.GetComponent<CraftIngredientDraggable>() ?? iconTrans.gameObject.AddComponent<CraftIngredientDraggable>();
                draggable.source = CraftDragSource.Stock;
                draggable.ingredientType = data.type;
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
                    GameObject btGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    btGo.layer = 5;
                    btGo.transform.SetParent(badgeTrans, false);
                    badgeTextTrans = btGo.transform;
                }

                RectTransform btRt = (RectTransform)badgeTextTrans;
                btRt.anchorMin = Vector2.zero;
                btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero;
                btRt.offsetMax = Vector2.zero;

                Text badgeText = badgeTextTrans.GetComponent<Text>() ?? badgeTextTrans.gameObject.AddComponent<Text>();
                badgeText.text = "0x";
                if (safeFont != null) badgeText.font = safeFont;
                badgeText.fontSize = 12;
                badgeText.fontStyle = FontStyle.Bold;
                badgeText.alignment = TextAnchor.MiddleCenter;
                badgeText.color = Color.white;
                badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                badgeText.verticalOverflow = VerticalWrapMode.Overflow;
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

                // Title
                Transform titleTrans = textContTrans.Find("Title");
                if (titleTrans == null)
                {
                    GameObject tGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    tGo.layer = 5;
                    tGo.transform.SetParent(textContTrans, false);
                    titleTrans = tGo.transform;
                }

                RectTransform titleRt = (RectTransform)titleTrans;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, 0f);
                titleRt.sizeDelta = new Vector2(0f, 22f);

                Text titleText = titleTrans.GetComponent<Text>() ?? titleTrans.gameObject.AddComponent<Text>();
                titleText.text = data.title;
                if (safeFont != null) titleText.font = safeFont;
                titleText.fontSize = 14;
                titleText.fontStyle = FontStyle.Bold;
                titleText.alignment = TextAnchor.MiddleLeft;
                titleText.color = new Color(1f, 0.88f, 0.45f, 1f); // Kuningan emas terang
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Overflow;
                titleText.raycastTarget = false;

                // Desc
                Transform descTrans = textContTrans.Find("Desc");
                if (descTrans == null)
                {
                    GameObject dGo = new GameObject("Desc", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    dGo.layer = 5;
                    dGo.transform.SetParent(textContTrans, false);
                    descTrans = dGo.transform;
                }

                RectTransform descRt = (RectTransform)descTrans;
                descRt.anchorMin = new Vector2(0f, 0f);
                descRt.anchorMax = new Vector2(1f, 1f);
                descRt.pivot = new Vector2(0f, 1f);
                descRt.offsetMin = new Vector2(0f, 0f);
                descRt.offsetMax = new Vector2(0f, -22f);

                Text descText = descTrans.GetComponent<Text>() ?? descTrans.gameObject.AddComponent<Text>();
                descText.text = data.desc;
                if (safeFont != null) descText.font = safeFont;
                descText.fontSize = 11;
                descText.fontStyle = FontStyle.Normal;
                descText.alignment = TextAnchor.UpperLeft;
                descText.color = new Color(0.80f, 0.84f, 0.90f, 0.95f);
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Overflow;
                descText.raycastTarget = false;
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

        void RefreshRecipeDrawerTab()
        {
            if (_recipeDrawerTabLabel == null)
                return;

            _recipeDrawerTabLabel.text = _recipeDrawerOpen ? "RESEP\n>" : "<\nRESEP";
        }

        void BuildRecipeDrawer(RectTransform craftRt)
        {
            if (_recipeDrawerBuilt || craftRt == null)
                return;
            _recipeDrawerBuilt = true;

            Font font = ZombieIntroSpotlight.GetSafeFont();
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            float panelWidth = craftRt.sizeDelta.x;
            if (panelWidth < 100f) panelWidth = 800f;
            float drawerWidth = Mathf.Min(Mathf.Clamp(recipeDrawerWidth, 240f, 420f), panelWidth * 0.55f);
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
            drawerBg.color = new Color(0.09f, 0.07f, 0.14f, 0.97f);
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
                "Efektif: Zombie Api \uD83D\uDD25",
                "Efektif: Zombie Besi \uD83D\uDEE1\uFE0F",
                "Efektif: Zombie Asam \uD83E\uDDEA",
                "Efektif: Zombie Besi \uD83D\uDEE1\uFE0F",
                "Efektif: Zombie Basa \uD83E\uDED7",
                "Efektif: Zombie Asam \uD83E\uDDEA"
            };

            for (int i = 0; i < 6; i++)
            {
                IngredientType ingA;
                IngredientType ingB;
                AlchemyManager.GetSlotIngredients(i, out ingA, out ingB);

                GameObject cardGo = new GameObject("Recipe_Card_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(Button));
                cardGo.layer = 5;
                cardGo.transform.SetParent(contentRt, false);

                LayoutElement cardLayout = cardGo.GetComponent<LayoutElement>();
                cardLayout.preferredHeight = 172f;
                cardLayout.minHeight = 172f;

                Image cardBg = cardGo.GetComponent<Image>();
                cardBg.color = Color.white;

                Outline cardOutline = cardGo.AddComponent<Outline>();
                cardOutline.effectColor = new Color(0.58f, 0.42f, 0.88f, 0.90f);
                cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

                Button cardButton = cardGo.GetComponent<Button>();
                cardButton.targetGraphic = cardBg;

                Color cardNormal = new Color(0.22f, 0.15f, 0.34f, 0.96f);
                ColorBlock cardColors = cardButton.colors;
                cardColors.normalColor = cardNormal;
                cardColors.highlightedColor = new Color(0.34f, 0.24f, 0.50f, 1f);
                cardColors.pressedColor = new Color(0.15f, 0.10f, 0.24f, 1f);
                cardColors.selectedColor = cardNormal;
                cardColors.disabledColor = new Color(0.20f, 0.20f, 0.20f, 0.50f);
                cardColors.colorMultiplier = 1f;
                cardColors.fadeDuration = 0.08f;
                cardButton.colors = cardColors;

                int slotIndex = i;
                cardButton.onClick.AddListener(delegate { QuickFillRecipe(slotIndex); });

                // Baris 1: Nama ramuan
                CreateCardText(cardGo.transform, "Name", font, 10f, 8f, 10f, 22f, 0f,
                    AlchemyDatabase.PotionName(AlchemyManager.FixedSlotPotions[i]),
                    14, new Color(1f, 0.88f, 0.48f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);

                // Baris 2: Formula visual  [hasil] <- [bahan1] + [bahan2]
                CreateColorBox(cardGo.transform, "Result_Box", new Vector2(10f, -36f), 44f,
                    AlchemyDatabase.PotionColor(AlchemyManager.FixedSlotPotions[i]));

                CreateCardText(cardGo.transform, "Arrow", font, 56f, 36f, 0f, 44f, 26f,
                    "\u2190", 20, new Color(0.95f, 0.92f, 0.86f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

                CreateColorBox(cardGo.transform, "Ing1_Box", new Vector2(86f, -38f), 40f,
                    AlchemyDatabase.IngredientColor(ingA));

                CreateCardText(cardGo.transform, "Plus", font, 130f, 36f, 0f, 44f, 18f,
                    "+", 20, new Color(0.95f, 0.92f, 0.86f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

                CreateColorBox(cardGo.transform, "Ing2_Box", new Vector2(152f, -38f), 40f,
                    AlchemyDatabase.IngredientColor(ingB));

                // Baris 3: Deskripsi reaksi ilmiah singkat
                CreateCardText(cardGo.transform, "Desc", font, 10f, 84f, 10f, 52f, 0f,
                    reactions[i], 11, new Color(0.80f, 0.84f, 0.92f, 0.96f), TextAnchor.UpperLeft, FontStyle.Normal);

                // Baris 4: Target efektif / kegunaan monster
                CreateCardText(cardGo.transform, "Target", font, 10f, 140f, 10f, 24f, 0f,
                    targets[i], 12, new Color(0.55f, 1f, 0.62f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            // --- Tab bookmark krem di tepi kiri Craft_panel (selalu terlihat) ---
            GameObject tabGo = new GameObject("Recipe_Drawer_Tab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            tabGo.layer = 5;
            tabGo.transform.SetParent(craftRt, false);

            _recipeDrawerTab = (RectTransform)tabGo.transform;
            _recipeDrawerTab.anchorMin = new Vector2(0f, 0.5f);
            _recipeDrawerTab.anchorMax = new Vector2(0f, 0.5f);
            _recipeDrawerTab.pivot = new Vector2(1f, 0.5f);
            _recipeDrawerTab.anchoredPosition = new Vector2(-2f, 0f);
            _recipeDrawerTab.sizeDelta = new Vector2(DrawerTabWidth, DrawerTabHeight);
            _recipeDrawerTab.localScale = Vector3.one;

            Image tabBg = tabGo.GetComponent<Image>();
            tabBg.color = new Color(0.961f, 0.886f, 0.722f, 1f); // #F5E2B8

            Outline tabOutline = tabGo.AddComponent<Outline>();
            tabOutline.effectColor = new Color(0.35f, 0.26f, 0.10f, 0.90f);
            tabOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Button tabButton = tabGo.GetComponent<Button>();
            tabButton.targetGraphic = tabBg;
            tabButton.onClick.AddListener(ToggleRecipeDrawer);

            GameObject tabLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            tabLabelGo.layer = 5;
            tabLabelGo.transform.SetParent(tabGo.transform, false);

            RectTransform tabLabelRt = (RectTransform)tabLabelGo.transform;
            tabLabelRt.anchorMin = Vector2.zero;
            tabLabelRt.anchorMax = Vector2.one;
            tabLabelRt.offsetMin = new Vector2(2f, 6f);
            tabLabelRt.offsetMax = new Vector2(-2f, -6f);
            tabLabelRt.localScale = Vector3.one;

            _recipeDrawerTabLabel = tabLabelGo.GetComponent<Text>();
            _recipeDrawerTabLabel.font = font;
            _recipeDrawerTabLabel.fontSize = 13;
            _recipeDrawerTabLabel.fontStyle = FontStyle.Bold;
            _recipeDrawerTabLabel.alignment = TextAnchor.MiddleCenter;
            _recipeDrawerTabLabel.color = new Color(0.24f, 0.17f, 0.06f, 1f);
            _recipeDrawerTabLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _recipeDrawerTabLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _recipeDrawerTabLabel.raycastTarget = false;

            RefreshRecipeDrawerTab();
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

        static Image CreateColorBox(Transform parent, string name, Vector2 topLeftPos, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        static void CreateCardText(Transform parent, string name, Font font,
            float left, float top, float right, float height, float fixedWidth,
            string content, int fontSize, Color color, TextAnchor alignment, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = 5;
            go.transform.SetParent(parent, false);

            RectTransform rt = (RectTransform)go.transform;
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

            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
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
            if (comp is Text uiText) { uiText.text = val; return; }
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

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int k = 0; k < keywords.Length; k++)
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i].gameObject.name.IndexOf(keywords[k], System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return texts[i];

            if (tmps.Length > 0) return tmps[0];
            if (texts.Length > 0) return texts[0];
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
