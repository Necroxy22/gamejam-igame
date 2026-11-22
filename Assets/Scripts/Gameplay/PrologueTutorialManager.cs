using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Player;
using CuriesCrucible.UI;

namespace CuriesCrucible.Gameplay
{
    public enum PrologueState
    {
        NotStarted,
        Phase0_CleanScreen,          // Sembunyikan HUD & pemain, tahan WaveManager
        Phase1_PanZoomToBalcony,     // Kamera zoom halus ke pintu balkon
        Phase1_MarieAppear,          // Marie fade-in dan melangkah maju
        Phase2_SpawnZombie,          // Spawn 1 Zombie Asam pelan
        Phase2_ZoomZombie,           // Kamera zoom ke zombie
        Phase2_MarieSpeechBubble,    // Gelembung dialog narasi ilmiah Marie
        Step1_CameraPan,             // Petunjuk tombol A & D untuk pan kamera
        Step2_HarvestPipes,          // Munculkan gelembung Basa & Pendingin, sorot pipa
        Step3_IntroHUD_BottomPanel,  // Sorot Bottom_panel (stok 4 bahan)
        Step3_IntroHUD_Hotbar,       // Sorot 6 slot Hotbar
        Step4_ClickCraftButton,      // Sorot tombol Crafting di tengah Hotbar
        Step5_Tour_Stock,            // 5a. Sorot panel bahan dasar (kiri)
        Step5_Tour_Cauldron,         // 5b. Sorot slot1 & slot2 kuali
        Step5_Tour_PHResult,         // 5c. Sorot indikator pH & Hasil
        Step5_Tour_RecipeDrawer,     // 5d. Buka laci resep otomatis & sorot kartu resep
        Step6_DragIngredients,       // 6a. Instruksikan drag 1x Basa + 1x Pendingin ke kuali
        Step6_ClickBrew,             // 6b. Begitu slot terisi, sorot tombol Simpan (Brew)
        Conclusion,                  // Pesan penutup sebelum pertarungan
        Completed                    // Tutorial selesai, kontrol normal
    }

    /// <summary>
    /// Interactive Cinematic Opening & Guided Tutorial Manager (PrologueTutorialManager).
    /// Memandu pemain langkah demi langkah dari awal scene hingga siap memulai Wave 1.
    ///
    /// Aturan Teknis:
    ///   - Tanpa Coroutine (menggunakan state machine berbasis Update + Time.deltaTime / unscaledDeltaTime).
    ///   - Teks tajam menggunakan TextMeshProUGUI dengan font Cinzel SDF (UITextUtil.ApplyProjectFont).
    ///   - Cutout Spotlight dinamis (4 dimmer panels) mengisolasi target interaksi.
    ///   - Tombol skip [ESC] di pojok kanan atas untuk fleksibilitas pengujian.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrologueTutorialManager : MonoBehaviour
    {
        public static PrologueTutorialManager Instance { get; private set; }

        [Header("Pengaturan Prologue")]
        [Tooltip("Apakah tutorial prolog otomatis berjalan saat scene dimulai")]
        public bool runTutorialOnStart = true;

        [Header("Referensi Scene (Auto-wire)")]
        public MarieCurieController marie;
        [Tooltip("Karakter Wizard di balkon")]
        public GameObject wizard;
        public Camera mainCamera;
        public Arena25DCameraRig cameraRig;
        public WaveManager waveManager;
        public TowerPipeHarvester harvestPipes;
        public GameplayUIBridge uiBridge;
        public AlchemyManager alchemy;
        public ArenaThrowTargeting throwTargeting;

        [Header("Prefab Zombie Tutorial")]
        [Tooltip("Prefab Zombie Asam untuk opening sinematik")]
        public ZombieChemicalEntity tutorialZombiePrefab;
        public float tutorialZombieMoveSpeed = 0.35f;

        [Header("Pengaturan Sinematik Kamera")]
        public const float FixedCameraZ = -10f;
        public float balconyZoomSize = 3.50f;
        public float zombieZoomSize = 3.60f;
        public float defaultCameraSize = 4.75f;
        public float cameraPanSpeed = 2.8f;
        public float cameraZoomSpeed = 3.2f;

        [Header("Referensi Background Arena")]
        public SpriteRenderer arenaBackgroundRenderer;

        [Header("Narasi & Dialog (Cinzel SDF)")]
        [TextArea(3, 6)]
        public string marieDialogueText =
            "Oh tidak! Itu spesimen mutasi Asam (H<size=70%><sup>+</sup></size>) milik Mr. Seven yang kabur dari laboratorium pusat kemarin!\n\n" +
            "Korosinya bisa melarutkan fondasi menara - aku harus menetralkannya dengan senyawa Basa kuat (OH<size=70%><sup>-</sup></size>) dan pendingin!";

        [Header("Teks Langkah Tutorial (Cinzel SDF)")]
        public string step1Title = "GERAKAN KARAKTER & BALKON";
        [TextArea(2, 4)]
        public string step1Instruction = "Gunakan tombol [A] dan [D] (atau Panah Kiri/Kanan) untuk menggerakkan Marie Curie di sepanjang balkon laboratorium!";

        public string step2Title = "PIPA DISTILASI MENARA";
        [TextArea(2, 4)]
        public string step2Instruction = "Pipa distilasi menara memproduksi bahan kimia dasar secara berkala. Klik gelembung Basa (Biru) dan Pendingin (Cyan) untuk memanennya!";

        public string step3aTitle = "DISPLAY BAHAN DASAR";
        [TextArea(2, 4)]
        public string step3aInstruction = "Pojok Kiri Bawah: Menampilkan persediaan 4 bahan kimia dasar (Basa, Pendingin, Asam, Katalis).";

        public string step3bTitle = "HOTBAR RAMUAN (SLOT 1-6)";
        [TextArea(2, 4)]
        public string step3bInstruction = "Hotbar: Tempat menyimpan 6 jenis ramuan siap lempar. Tekan angka [1]-[6] untuk memilih ramuan aktif!";

        public string step4Title = "MEJA PERACIKAN RAMUAN";
        [TextArea(2, 4)]
        public string step4Instruction = "Klik tombol lingkaran [Crafting] di tengah Hotbar (atau tekan tombol [C] / [Tab]) untuk membuka meja peracikan!";

        public string step5aTitle = "DAFTAR BAHAN DASAR";
        [TextArea(2, 4)]
        public string step5aInstruction = "1. Sisi Kiri: Daftar persediaan bahan dasar mentah yang siap ditarik (drag) ke dalam kuali.";

        public string step5bTitle = "SLOT KUALI PENCAMPUR";
        [TextArea(2, 4)]
        public string step5bInstruction = "2. Slot Kuali (slot1 & slot2): Tempat mencampur 2 bahan kimia untuk bereaksi membentuk ramuan baru.";

        public string step5cTitle = "INDIKATOR PH & HASIL";
        [TextArea(2, 4)]
        public string step5cInstruction = "3. Indikator pH & Hasil: Memantau derajat keasaman (pH 0-14) dan pratinjau ramuan yang terbentuk.";

        public string step5dTitle = "TAB RESEP LABORATORIUM";
        [TextArea(2, 4)]
        public string step5dInstruction = "4. Tab RESEP: Membuka laci panduan formula ramuan dan kelemahan monster. Klik kartu atau tab untuk membaca panduan!";

        public string step6DragTitle = "PRAKTEK RACIK RAMUAN";
        [TextArea(2, 4)]
        public string step6DragInstruction = "Tarik (drag) 1x Bahan Basa (Biru) dan 1x Bahan Pendingin (Cyan) ke dalam kedua slot kuali (slot1 & slot2)!";

        public string step6BrewTitle = "SIMPAN RAMUAN KE HOTBAR";
        [TextArea(2, 4)]
        public string step6BrewInstruction = "Campuran siap bereaksi! Klik tombol [Simpan] untuk meracik Gel Sabun Beku ke dalam Hotbar!";

        public string conclusionTitle = "PERSIAPAN SELESAI!";
        [TextArea(2, 4)]
        public string conclusionInstruction = "Ramuan Gel Sabun Beku siap di Hotbar Slot 1! Pilih slot 1 lalu klik jalur arena untuk melemparkannya ke arah Zombie Asam!";

        [Header("Warna Tema Spotlight & UI")]
        public Color goldAccentColor = new Color(1f, 0.88f, 0.45f, 1f); // #FFE073
        public Color darkPanelColor = new Color(0.08f, 0.10f, 0.14f, 0.94f);
        [Range(0f, 0.45f)]
        public float spotlightDimmerAlpha = 0.38f;
        public Color dimmerColor = new Color(0.02f, 0.02f, 0.04f, 0.38f);

        // ------------------------------------------------------------------
        // Runtime State
        // ------------------------------------------------------------------

        public PrologueState CurrentState { get; private set; } = PrologueState.NotStarted;

        float _stateTimer;
        Vector3 _camStartPos;
        Vector3 _camTargetPos;
        float _camStartSize;
        float _camTargetSize;
        bool _isCamMoving;

        ZombieChemicalEntity _spawnedScoutZombie;
        PathFollower25D _zombiePathFollower;

        // Tracking aksi pemain untuk melangkah
        bool _playerPressedA;
        bool _playerPressedD;
        bool _harvestedBaseOrCoolant;

        // UI Overlay Elements
        Canvas _tutorialCanvas;
        RectTransform _canvasRt;
        RectTransform _dimmerTop;
        RectTransform _dimmerBottom;
        RectTransform _dimmerLeft;
        RectTransform _dimmerRight;
        RectTransform _dimmerFull;
        RectTransform _spotlightFrameRt;
        Image[] _spotlightBorders;

        // Speech Bubble Marie
        GameObject _speechBubbleRoot;
        RectTransform _speechBubbleRt;
        TextMeshProUGUI _speechBubbleTitleText;
        TextMeshProUGUI _speechBubbleBodyText;
        Button _speechBubbleContinueButton;

        // Instruction Box
        GameObject _instructionBoxRoot;
        RectTransform _instructionBoxRt;
        TextMeshProUGUI _instructionStepBadge;
        TextMeshProUGUI _instructionTitleText;
        TextMeshProUGUI _instructionBodyText;
        Button _instructionContinueButton;
        TextMeshProUGUI _instructionContinueLabel;

        // Skip Button
        GameObject _skipButtonRoot;
        Button _skipButton;

        RectTransform _currentSpotlightTarget;
        bool _spotlightActive;
        float _spotlightPulseTimer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureArenaActive();
            ResolveReferences();
            HideMariePlaceholder();
        }

        void Start()
        {
            EnsureArenaActive();
            ResolveReferences();
            HideMariePlaceholder();

            if (runTutorialOnStart)
            {
                StartPrologueTutorial();
            }
        }

        void Update()
        {
            if (CurrentState == PrologueState.NotStarted || CurrentState == PrologueState.Completed)
                return;

            EnsureArenaActive();

            // Kunci mutlak Z kamera pada -10f di setiap frame
            if (mainCamera != null && Mathf.Abs(mainCamera.transform.position.z - FixedCameraZ) > 0.001f)
            {
                Vector3 camPos = mainCamera.transform.position;
                camPos.z = FixedCameraZ;
                mainCamera.transform.position = camPos;
            }

            float dt = Time.unscaledDeltaTime;
            _stateTimer += dt;

            HandleCameraAnimation(dt);
            HandleSpotlightPulse(dt);
            HandleGlobalInput();

            UpdateStateMachine(dt);
        }

        void LateUpdate()
        {
            if (CurrentState == PrologueState.NotStarted || CurrentState == PrologueState.Completed)
                return;

            ApplyCameraBoundsConstraint();
        }

        public void ResolveReferences()
        {
            if (wizard == null) wizard = GameObject.Find("Wizard");
            if (mainCamera == null) mainCamera = Camera.main;
            if (cameraRig == null) cameraRig = FindObjectOfType<Arena25DCameraRig>();
            if (marie == null) marie = FindObjectOfType<MarieCurieController>();
            if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
            if (harvestPipes == null) harvestPipes = FindObjectOfType<TowerPipeHarvester>();
            if (uiBridge == null) uiBridge = GameplayUIBridge.Instance ?? FindObjectOfType<GameplayUIBridge>();
            if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
            if (throwTargeting == null) throwTargeting = FindObjectOfType<ArenaThrowTargeting>();
            if (arenaBackgroundRenderer == null) arenaBackgroundRenderer = GetArenaBackgroundRenderer();

            if (tutorialZombiePrefab == null && waveManager != null && waveManager.waves != null && waveManager.waves.Count > 0)
            {
                if (waveManager.waves[0].entries != null && waveManager.waves[0].entries.Length > 0)
                {
                    tutorialZombiePrefab = waveManager.waves[0].entries[0].prefab;
                }
            }

            EnsureTutorialUI();
        }

        // ------------------------------------------------------------------
        // Inisialisasi Tutorial
        // ------------------------------------------------------------------

        public void EnsureArenaActive()
        {
            GameObject arenaGo = GameObject.Find("Arena_25D");
            if (arenaGo == null)
            {
                ArenaThrowTargeting targeting = FindObjectOfType<ArenaThrowTargeting>(true);
                if (targeting != null) arenaGo = targeting.gameObject;
            }
            if (arenaGo != null)
            {
                arenaGo.SetActive(true);
                Transform env = arenaGo.transform.Find("Environment_Layers");
                if (env != null)
                {
                    env.gameObject.SetActive(true);
                    for (int i = 0; i < env.childCount; i++)
                    {
                        Transform child = env.GetChild(i);
                        if (child.name == "BG_Arena" || child.name == "Tower_Body" || child.name == "Tower_Balcony")
                        {
                            child.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        public void StartPrologueTutorial()
        {
            EnsureArenaActive();
            ResolveReferences();
            EnsureTutorialUI();

            // Tahan ZombieIntroSpotlight agar tidak muncul mendahului/menimpa tutorial prolog
            ZombieIntroSpotlight.IsPausedByTutorial = true;

            // Pastikan Z Main Camera terkunci pada -10f di awal tutorial dan berada di dalam batas BG_Arena
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                float safeSize = ClampCameraZoom(defaultCameraSize);
                mainCamera.orthographicSize = safeSize;
                mainCamera.transform.position = ClampCameraPositionToBounds(mainCamera.transform.position, safeSize);
            }

            // Pastikan seluruh dimmer spotlight nonaktif total (alpha = 0f) di awal sinematik
            ClearCutoutSpotlight();

            // Tahan WaveManager
            if (waveManager != null)
            {
                waveManager.isPausedByTutorial = true;
            }

            // Matikan lemparan sembarangan selama prolog
            if (throwTargeting != null)
            {
                throwTargeting.enabled = false;
            }

            // Matikan pergerakan manual Marie Curie di awal
            if (marie != null)
            {
                marie.enableInput = false;
            }

            // Sembunyikan UI HUD utama
            if (uiBridge != null)
            {
                if (uiBridge.bottomPanel != null) uiBridge.bottomPanel.SetActive(false);
                if (uiBridge.topPanel != null) uiBridge.topPanel.SetActive(false);
                if (uiBridge.hotbarRoot != null) uiBridge.hotbarRoot.SetActive(false);
                if (uiBridge.craftPanel != null) uiBridge.craftPanel.SetActive(false);
            }

            // Sembunyikan pipa panen sementara
            if (harvestPipes != null)
            {
                harvestPipes.gameObject.SetActive(false);
            }

            // Sembunyikan / nonaktifkan placeholder kotak merah Marie
            HideMariePlaceholder();

            // Sembunyikan karakter Wizard (alpha = 0)
            SetWizardAlpha(0f);

            // Mulai Fase 0
            ChangeState(PrologueState.Phase0_CleanScreen);
        }

        // ------------------------------------------------------------------
        // State Machine Loop
        // ------------------------------------------------------------------

        void UpdateStateMachine(float dt)
        {
            switch (CurrentState)
            {
                case PrologueState.Phase0_CleanScreen:
                    if (_stateTimer >= 0.8f)
                    {
                        // Kamera zoom in ke arah balkon Marie Curie
                        Vector3 balconyPos = GetBalconyWorldPos();
                        balconyPos.z = -10f;
                        StartCameraTransition(balconyPos, balconyZoomSize, 1.4f);
                        ChangeState(PrologueState.Phase1_PanZoomToBalcony);
                    }
                    break;

                case PrologueState.Phase1_PanZoomToBalcony:
                    if (!_isCamMoving && _stateTimer >= 1.4f)
                    {
                        ChangeState(PrologueState.Phase1_MarieAppear);
                    }
                    break;

                case PrologueState.Phase1_MarieAppear:
                    // Fade-in Wizard (alpha 0 -> 1 dalam 1 detik)
                    float wizardFadeAlpha = Mathf.Clamp01(_stateTimer / 1.0f);
                    SetWizardAlpha(wizardFadeAlpha);

                    if (_stateTimer >= 1.2f)
                    {
                        // Spawn Scout Zombie
                        SpawnTutorialZombie();
                        ChangeState(PrologueState.Phase2_SpawnZombie);
                    }
                    break;

                case PrologueState.Phase2_SpawnZombie:
                    // Balik scale.x Wizard menjadi negatif agar menoleh ke kiri (arah jalur datang zombie)
                    SetWizardFacingLeft();

                    if (_stateTimer >= 1.0f)
                    {
                        // Kamera geser membidik Zombie Asam yang merayap masuk
                        if (_spawnedScoutZombie != null)
                        {
                            Vector3 zombiePos = _spawnedScoutZombie.transform.position;
                            zombiePos.z = -10f;
                            StartCameraTransition(zombiePos, zombieZoomSize, 1.5f);
                            ChangeState(PrologueState.Phase2_ZoomZombie);
                        }
                        else
                        {
                            ChangeState(PrologueState.Phase2_MarieSpeechBubble);
                        }
                    }
                    break;

                case PrologueState.Phase2_ZoomZombie:
                    if (!_isCamMoving && _stateTimer >= 1.2f)
                    {
                        // Hentikan gerak zombie asam sementara
                        if (_zombiePathFollower != null)
                        {
                            _zombiePathFollower.moveSpeed = 0f;
                        }

                        // Munculkan gelembung dialog Marie Curie
                        ShowMarieSpeechBubble(marieDialogueText, () => {
                            // Sembunyikan zombie asam sementara agar pemain fokus belajar UI
                            if (_spawnedScoutZombie != null)
                            {
                                _spawnedScoutZombie.gameObject.SetActive(false);
                            }

                            // Kamera kembali ke framing normal arena
                            StartCameraTransition(new Vector3(0f, 0f, -10f), defaultCameraSize, 1.2f);
                            ChangeState(PrologueState.Step1_CameraPan);
                        });

                        ChangeState(PrologueState.Phase2_MarieSpeechBubble);
                    }
                    break;

                case PrologueState.Phase2_MarieSpeechBubble:
                    // Menunggu tombol Lanjut / Space pada speech bubble
                    UpdateSpeechBubblePosition();
                    break;

                case PrologueState.Step1_CameraPan:
                    if (!_isCamMoving)
                    {
                        // Buka kunci pergerakan horizontal Marie Curie di balkon
                        if (marie != null)
                        {
                            marie.enableInput = true;
                        }

                        // Tampilkan Top_panel HUD
                        if (uiBridge != null && uiBridge.topPanel != null)
                            uiBridge.topPanel.SetActive(true);

                        // Pantau input A / D serta panah kiri/kanan dan axis horizontal
                        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetAxisRaw("Horizontal") < -0.1f)
                            _playerPressedA = true;
                        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || Input.GetAxisRaw("Horizontal") > 0.1f)
                            _playerPressedD = true;

                        ShowInstructionBox(1, 6, step1Title, step1Instruction, false, null);

                        if (_playerPressedA && _playerPressedD && _stateTimer >= 1.2f)
                        {
                            ChangeState(PrologueState.Step2_HarvestPipes);
                        }
                    }
                    break;

                case PrologueState.Step2_HarvestPipes:
                    // Aktifkan Tower_Harvest_Pipes dan paksa spawn gelembung Basa & Pendingin
                    if (harvestPipes != null)
                    {
                        harvestPipes.gameObject.SetActive(true);
                        harvestPipes.ForceSpawnBubble(IngredientType.BahanBasa_Biru);
                        harvestPipes.ForceSpawnBubble(IngredientType.BahanPendingin_Cyan);
                    }

                    // Sorot area pipa menara
                    HighlightWorldPosition(GetBalconyWorldPos() + new Vector3(0f, -0.2f, 0f), new Vector2(280f, 220f));
                    ShowInstructionBox(2, 6, step2Title, step2Instruction, false, null);

                    // Dengarkan panen via event atau pengecekan stok
                    if (_harvestedBaseOrCoolant || (alchemy != null && (alchemy.ingredientStock[0] > 0 || alchemy.ingredientStock[1] > 0)))
                    {
                        ChangeState(PrologueState.Step3_IntroHUD_BottomPanel);
                    }
                    break;

                case PrologueState.Step3_IntroHUD_BottomPanel:
                    // Munculkan Bottom_panel & Hotbar
                    if (uiBridge != null)
                    {
                        if (uiBridge.bottomPanel != null) uiBridge.bottomPanel.SetActive(true);
                        if (uiBridge.hotbarRoot != null) uiBridge.hotbarRoot.SetActive(true);
                    }

                    // Sorot pojok kiri bawah (Bottom_panel)
                    if (uiBridge != null && uiBridge.bottomPanel != null)
                    {
                        HighlightRectTransform(uiBridge.bottomPanel.GetComponent<RectTransform>());
                    }
                    ShowInstructionBox(3, 6, step3aTitle, step3aInstruction, true, () => {
                        ChangeState(PrologueState.Step3_IntroHUD_Hotbar);
                    });
                    break;

                case PrologueState.Step3_IntroHUD_Hotbar:
                    // Sorot Hotbar
                    if (uiBridge != null && uiBridge.hotbarRoot != null)
                    {
                        HighlightRectTransform(uiBridge.hotbarRoot.GetComponent<RectTransform>());
                    }
                    ShowInstructionBox(3, 6, step3bTitle, step3bInstruction, true, () => {
                        ChangeState(PrologueState.Step4_ClickCraftButton);
                    });
                    break;

                case PrologueState.Step4_ClickCraftButton:
                    // Sorot tombol lingkaran Crafting di tengah Hotbar
                    if (uiBridge != null && uiBridge.centerBrewButton != null)
                    {
                        HighlightRectTransform(uiBridge.centerBrewButton.GetComponent<RectTransform>());
                    }
                    ShowInstructionBox(4, 6, step4Title, step4Instruction, false, null);

                    // Wajib tunggu pemain membuka Craft_panel
                    if (uiBridge != null && uiBridge.craftPanel != null && uiBridge.craftPanel.activeSelf)
                    {
                        ChangeState(PrologueState.Step5_Tour_Stock);
                    }
                    break;

                case PrologueState.Step5_Tour_Stock:
                    // Sorot panel bahan dasar di kiri Craft_panel
                    Transform leftSub = uiBridge != null && uiBridge.craftPanel != null ? uiBridge.craftPanel.transform.Find("Resep") : null;
                    if (leftSub != null) HighlightRectTransform(leftSub as RectTransform);

                    ShowInstructionBox(5, 6, step5aTitle, step5aInstruction, true, () => {
                        ChangeState(PrologueState.Step5_Tour_Cauldron);
                    });
                    break;

                case PrologueState.Step5_Tour_Cauldron:
                    // Sorot slot1 & slot2 kuali di kanan
                    Transform s1 = uiBridge != null && uiBridge.craftPanel != null ? uiBridge.craftPanel.transform.Find("Craft/slot1") : null;
                    if (s1 != null) HighlightRectTransform(s1 as RectTransform, new Vector2(180f, 110f));

                    ShowInstructionBox(5, 6, step5bTitle, step5bInstruction, true, () => {
                        ChangeState(PrologueState.Step5_Tour_PHResult);
                    });
                    break;

                case PrologueState.Step5_Tour_PHResult:
                    // Sorot indikator pH & Hasil
                    Transform ph = uiBridge != null && uiBridge.craftPanel != null ? uiBridge.craftPanel.transform.Find("Craft/Hasil") : null;
                    if (ph != null) HighlightRectTransform(ph as RectTransform, new Vector2(160f, 130f));

                    ShowInstructionBox(5, 6, step5cTitle, step5cInstruction, true, () => {
                        ChangeState(PrologueState.Step5_Tour_RecipeDrawer);
                    });
                    break;

                case PrologueState.Step5_Tour_RecipeDrawer:
                    // Buka Laci Resep otomatis
                    if (uiBridge != null && !uiBridge.IsRecipeDrawerOpen)
                    {
                        uiBridge.OpenRecipeDrawer();
                    }

                    Transform drawerWin = uiBridge != null && uiBridge.craftPanel != null ? uiBridge.craftPanel.transform.Find("Recipe_Drawer_Window") : null;
                    if (drawerWin != null) HighlightRectTransform(drawerWin as RectTransform);

                    ShowInstructionBox(5, 6, step5dTitle, step5dInstruction, true, () => {
                        // Tutup laci resep
                        if (uiBridge != null) uiBridge.CloseRecipeDrawer();
                        ChangeState(PrologueState.Step6_DragIngredients);
                    });
                    break;

                case PrologueState.Step6_DragIngredients:
                    // Instruksikan drag Basa + Pendingin ke kuali
                    ClearCutoutSpotlight();
                    ShowInstructionBox(6, 6, step6DragTitle, step6DragInstruction, true, () => {
                        if (alchemy != null)
                        {
                            if (!alchemy.slot1Filled && alchemy.ingredientStock != null && alchemy.ingredientStock[0] > 0)
                                alchemy.SetCauldronSlot(0, IngredientType.BahanBasa_Biru);
                            if (!alchemy.slot2Filled && alchemy.ingredientStock != null && alchemy.ingredientStock[1] > 0)
                                alchemy.SetCauldronSlot(1, IngredientType.BahanPendingin_Cyan);
                            alchemy.selectedSlotIndex = 0;
                        }
                        if (uiBridge != null)
                        {
                            uiBridge.RefreshCraftPanel();
                            uiBridge.RefreshUI(true);
                        }
                        ChangeState(PrologueState.Step6_ClickBrew);
                    });

                    if (alchemy != null && alchemy.slot1Filled && alchemy.slot2Filled)
                    {
                        ChangeState(PrologueState.Step6_ClickBrew);
                    }
                    break;

                case PrologueState.Step6_ClickBrew:
                    // Sorot tombol Simpan (Brew)
                    if (uiBridge != null && uiBridge.craftBrewButton != null)
                    {
                        HighlightRectTransform(uiBridge.craftBrewButton.GetComponent<RectTransform>());
                    }
                    ShowInstructionBox(6, 6, step6BrewTitle, step6BrewInstruction, false, null);

                    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                    {
                        ExecuteTutorialBrew();
                        break;
                    }

                    // Wajib tunggu pemain meracik hingga masuk ke Hotbar
                    if (alchemy != null && alchemy.potionStock != null && alchemy.potionStock[0] > 0)
                    {
                        ChangeState(PrologueState.Conclusion);
                    }
                    break;

                case PrologueState.Conclusion:
                    ClearCutoutSpotlight();

                    // Tutup Craft_panel
                    if (uiBridge != null)
                    {
                        uiBridge.CloseCraftPanel();
                    }

                    ShowInstructionBox(6, 6, conclusionTitle, conclusionInstruction, true, () => {
                        FinishPrologueTutorial();
                    });
                    break;
            }
        }

        /// <summary>
        /// Mengeksekusi peracikan ramuan tutorial (Gel Sabun Beku) ke slot 1 Hotbar
        /// dan memajukan tutorial ke tahap Conclusion.
        /// Dipanggil saat tombol [Simpan] diklik atau tombol konfirmasi ditekan.
        /// </summary>
        public void ExecuteTutorialBrew()
        {
            if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
            if (alchemy != null)
            {
                if (!alchemy.slot1Filled || !alchemy.slot2Filled)
                {
                    alchemy.SetCauldronSlot(0, IngredientType.BahanBasa_Biru);
                    alchemy.SetCauldronSlot(1, IngredientType.BahanPendingin_Cyan);
                }

                alchemy.Brew();

                if (alchemy.potionStock != null)
                {
                    alchemy.potionStock[0] = Mathf.Max(1, alchemy.potionStock[0]);
                    alchemy.SyncMixedPotionsList();
                }

                alchemy.selectedSlotIndex = 0;
            }

            if (uiBridge != null)
            {
                uiBridge.RefreshCraftPanel();
                uiBridge.RefreshUI(true);
            }

            ChangeState(PrologueState.Conclusion);
        }

        void ChangeState(PrologueState newState)
        {
            CurrentState = newState;
            _stateTimer = 0f;

            if (newState == PrologueState.Phase2_SpawnZombie)
            {
                SetWizardFacingLeft();
            }

            if (newState == PrologueState.Step6_ClickBrew)
            {
                if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
                if (alchemy != null)
                {
                    if (!alchemy.slot1Filled || !alchemy.slot2Filled)
                    {
                        alchemy.SetCauldronSlot(0, IngredientType.BahanBasa_Biru);
                        alchemy.SetCauldronSlot(1, IngredientType.BahanPendingin_Cyan);
                    }
                    alchemy.selectedSlotIndex = 0;
                }
                if (uiBridge != null)
                {
                    if (uiBridge.craftBrewButton != null)
                    {
                        uiBridge.craftBrewButton.onClick.RemoveAllListeners();
                        uiBridge.craftBrewButton.onClick.AddListener(ExecuteTutorialBrew);
                    }
                    uiBridge.RefreshCraftPanel();
                    uiBridge.RefreshUI(true);
                }
            }
        }

        // ------------------------------------------------------------------
        // Cutscene & Kamera Animasi (Lerp Tanpa Coroutine & Clamped ke BG_Arena)
        // ------------------------------------------------------------------

        public SpriteRenderer GetArenaBackgroundRenderer()
        {
            if (arenaBackgroundRenderer != null) return arenaBackgroundRenderer;

            GameObject bg = GameObject.Find("BG_Arena");
            if (bg != null)
            {
                SpriteRenderer sr = bg.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    arenaBackgroundRenderer = sr;
                    return sr;
                }
            }

            GameObject arenaGo = GameObject.Find("Arena_25D");
            if (arenaGo != null)
            {
                Transform env = arenaGo.transform.Find("Environment_Layers");
                if (env != null)
                {
                    Transform bgT = env.Find("BG_Arena");
                    if (bgT != null)
                    {
                        SpriteRenderer sr = bgT.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            arenaBackgroundRenderer = sr;
                            return sr;
                        }
                    }
                }
            }

            SpriteRenderer[] allSrs = FindObjectsOfType<SpriteRenderer>(true);
            for (int i = 0; i < allSrs.Length; i++)
            {
                if (allSrs[i].gameObject.name.Equals("BG_Arena", StringComparison.OrdinalIgnoreCase))
                {
                    arenaBackgroundRenderer = allSrs[i];
                    return arenaBackgroundRenderer;
                }
            }

            return null;
        }

        public float ClampCameraZoom(float requestedSize)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            float aspect = (mainCamera != null && mainCamera.aspect > 0.01f) ? mainCamera.aspect : (16f / 9f);

            float minSize = 3.5f;
            float maxSize = 4.79f;

            SpriteRenderer bg = GetArenaBackgroundRenderer();
            if (bg != null)
            {
                Bounds b = bg.bounds;
                if (b.size.x > 1f && b.size.y > 1f)
                {
                    float maxAllowed = Mathf.Min(b.extents.y, b.extents.x / aspect);
                    maxSize = Mathf.Max(minSize, maxAllowed);
                }
            }

            return Mathf.Clamp(requestedSize, minSize, maxSize);
        }

        public Vector3 ClampCameraPositionToBounds(Vector3 targetPos, float orthoSize)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return new Vector3(targetPos.x, targetPos.y, FixedCameraZ);

            SpriteRenderer bg = GetArenaBackgroundRenderer();
            if (bg == null)
            {
                return new Vector3(targetPos.x, targetPos.y, FixedCameraZ);
            }

            Bounds b = bg.bounds;
            if (b.size.x < 1f || b.size.y < 1f)
            {
                return new Vector3(targetPos.x, targetPos.y, FixedCameraZ);
            }

            float aspect = (mainCamera.aspect > 0.01f) ? mainCamera.aspect : (16f / 9f);
            float halfHeight = orthoSize;
            float halfWidth = halfHeight * aspect;

            float minCamX = b.min.x + halfWidth;
            float maxCamX = b.max.x - halfWidth;
            float clampedX;
            if (minCamX >= maxCamX)
                clampedX = b.center.x;
            else
                clampedX = Mathf.Clamp(targetPos.x, minCamX, maxCamX);

            float minCamY = b.min.y + halfHeight;
            float maxCamY = b.max.y - halfHeight;
            float clampedY;
            if (minCamY >= maxCamY)
                clampedY = b.center.y;
            else
                clampedY = Mathf.Clamp(targetPos.y, minCamY, maxCamY);

            return new Vector3(clampedX, clampedY, FixedCameraZ);
        }

        public void ApplyCameraBoundsConstraint()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            // Pastikan orthographicSize selalu dalam rentang aman [3.5f .. max]
            float clampedSize = ClampCameraZoom(mainCamera.orthographicSize);
            if (Mathf.Abs(mainCamera.orthographicSize - clampedSize) > 0.001f)
            {
                mainCamera.orthographicSize = clampedSize;
            }

            // Clamp posisi kamera (x, y) agar view frustum tidak pernah keluar dari BG_Arena
            Vector3 camPos = mainCamera.transform.position;
            Vector3 clampedPos = ClampCameraPositionToBounds(camPos, mainCamera.orthographicSize);
            clampedPos.z = FixedCameraZ;
            mainCamera.transform.position = clampedPos;
        }

        void StartCameraTransition(Vector3 targetPos, float targetSize, float duration)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            if (cameraRig != null)
            {
                cameraRig.enabled = false;
            }

            float safeTargetSize = ClampCameraZoom(targetSize);
            Vector3 safeTargetPos = ClampCameraPositionToBounds(targetPos, safeTargetSize);

            Vector3 currentPos = mainCamera.transform.position;
            currentPos.z = FixedCameraZ;
            mainCamera.transform.position = currentPos;

            _camStartPos = currentPos;
            _camTargetPos = safeTargetPos;
            _camStartSize = ClampCameraZoom(mainCamera.orthographicSize);
            _camTargetSize = safeTargetSize;
            _isCamMoving = true;
        }

        void HandleCameraAnimation(float dt)
        {
            if (!_isCamMoving || mainCamera == null) return;

            Vector3 currentPos = mainCamera.transform.position;
            currentPos.z = FixedCameraZ;

            float nextSize = Mathf.Lerp(mainCamera.orthographicSize, _camTargetSize, dt * cameraZoomSpeed);
            mainCamera.orthographicSize = ClampCameraZoom(nextSize);

            Vector3 rawNextPos = Vector3.Lerp(currentPos, _camTargetPos, dt * cameraPanSpeed);
            Vector3 nextPos = ClampCameraPositionToBounds(rawNextPos, mainCamera.orthographicSize);
            nextPos.z = FixedCameraZ;
            mainCamera.transform.position = nextPos;

            if (Vector2.Distance(new Vector2(nextPos.x, nextPos.y), new Vector2(_camTargetPos.x, _camTargetPos.y)) < 0.05f &&
                Mathf.Abs(mainCamera.orthographicSize - _camTargetSize) < 0.03f)
            {
                Vector3 finalPos = ClampCameraPositionToBounds(_camTargetPos, _camTargetSize);
                finalPos.z = FixedCameraZ;
                mainCamera.transform.position = finalPos;
                mainCamera.orthographicSize = _camTargetSize;
                _isCamMoving = false;
            }
        }

        Vector3 GetBalconyWorldPos()
        {
            if (wizard == null) wizard = GameObject.Find("Wizard");
            if (wizard != null) return wizard.transform.position;
            if (marie != null) return marie.transform.position;
            GameObject b = GameObject.Find("Tower_Balcony");
            if (b != null) return b.transform.position;
            return Vector3.zero;
        }

        void SpawnTutorialZombie()
        {
            if (tutorialZombiePrefab == null || waveManager == null) return;

            Transform spawnPath = waveManager.leftPath != null ? waveManager.leftPath : waveManager.rightPath;
            if (spawnPath == null) return;

            Vector3 spawnPos = spawnPath.childCount > 0 ? spawnPath.GetChild(0).position : Vector3.left * 8f;

            _spawnedScoutZombie = Instantiate(tutorialZombiePrefab, spawnPos, Quaternion.identity);
            _spawnedScoutZombie.name = "Tutorial_Scout_Zombie";

            _zombiePathFollower = _spawnedScoutZombie.GetComponent<PathFollower25D>();
            if (_zombiePathFollower != null)
            {
                List<Transform> pts = new List<Transform>();
                for (int i = 0; i < spawnPath.childCount; i++) pts.Add(spawnPath.GetChild(i));
                _zombiePathFollower.waypoints = pts.ToArray();
                _zombiePathFollower.moveSpeed = tutorialZombieMoveSpeed;
            }
        }

        // ------------------------------------------------------------------
        // Cutout Spotlight (4 Dimmer Panels)
        // ------------------------------------------------------------------

        public void HighlightRectTransform(RectTransform targetRt, Vector2? customPadding = null)
        {
            _currentSpotlightTarget = targetRt;
            _spotlightActive = true;

            if (_spotlightFrameRt != null)
                _spotlightFrameRt.gameObject.SetActive(true);

            UpdateCutoutDimmer(customPadding ?? new Vector2(16f, 16f));
        }

        public void HighlightWorldPosition(Vector3 worldPos, Vector2 screenBoxSize)
        {
            _currentSpotlightTarget = null;
            _spotlightActive = true;

            if (_spotlightFrameRt != null)
                _spotlightFrameRt.gameObject.SetActive(true);

            Vector2 screenPoint = mainCamera != null ? (Vector2)mainCamera.WorldToScreenPoint(worldPos) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ApplyCutoutRect(new Rect(screenPoint.x - screenBoxSize.x * 0.5f, screenPoint.y - screenBoxSize.y * 0.5f, screenBoxSize.x, screenBoxSize.y));
        }

        public void SetDimmerAlpha(float alpha)
        {
            float safeAlpha = Mathf.Clamp(alpha, 0f, 0.45f);
            Color c = new Color(dimmerColor.r, dimmerColor.g, dimmerColor.b, safeAlpha);
            if (_dimmerTop != null) { Image img = _dimmerTop.GetComponent<Image>(); if (img != null) img.color = c; }
            if (_dimmerBottom != null) { Image img = _dimmerBottom.GetComponent<Image>(); if (img != null) img.color = c; }
            if (_dimmerLeft != null) { Image img = _dimmerLeft.GetComponent<Image>(); if (img != null) img.color = c; }
            if (_dimmerRight != null) { Image img = _dimmerRight.GetComponent<Image>(); if (img != null) img.color = c; }
            if (_dimmerFull != null) { Image img = _dimmerFull.GetComponent<Image>(); if (img != null) img.color = c; }
        }

        public static string SanitizeTutorialText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("⁻", "<size=70%><sup>-</sup></size>")
                       .Replace("\u207B", "<size=70%><sup>-</sup></size>")
                       .Replace("⁺", "<size=70%><sup>+</sup></size>")
                       .Replace("\u207A", "<size=70%><sup>+</sup></size>")
                       .Replace("—", " - ")
                       .Replace("–", "-")
                       .Replace("✦", "")
                       .Replace("✧", "");
        }

        public void ClearCutoutSpotlight()
        {
            _spotlightActive = false;
            _currentSpotlightTarget = null;

            if (_spotlightFrameRt != null)
                _spotlightFrameRt.gameObject.SetActive(false);

            SetDimmerAlpha(0f);

            // Tutup cutout: nonaktifkan seluruh panel dimmer
            if (_dimmerFull != null) _dimmerFull.gameObject.SetActive(false);
            if (_dimmerTop != null) _dimmerTop.gameObject.SetActive(false);
            if (_dimmerBottom != null) _dimmerBottom.gameObject.SetActive(false);
            if (_dimmerLeft != null) _dimmerLeft.gameObject.SetActive(false);
            if (_dimmerRight != null) _dimmerRight.gameObject.SetActive(false);
        }

        void UpdateCutoutDimmer(Vector2 padding)
        {
            if (_currentSpotlightTarget == null || mainCamera == null)
            {
                ClearCutoutSpotlight();
                return;
            }

            Vector3[] corners = new Vector3[4];
            _currentSpotlightTarget.GetWorldCorners(corners);

            Vector2 min = corners[0];
            Vector2 max = corners[2];

            // Jika canvas screen-space overlay
            Rect screenRect = new Rect(min.x - padding.x, min.y - padding.y, (max.x - min.x) + padding.x * 2f, (max.y - min.y) + padding.y * 2f);
            ApplyCutoutRect(screenRect);
        }

        void ApplyCutoutRect(Rect holeRect)
        {
            if (_canvasRt == null) return;

            float screenW = Screen.width;
            float screenH = Screen.height;

            // Pastikan alpha dimmer lembut (maksimal 0.45f) saat spotlight aktif
            SetDimmerAlpha(Mathf.Min(spotlightDimmerAlpha, 0.45f));

            if (_dimmerFull != null) _dimmerFull.gameObject.SetActive(false);
            if (_dimmerTop != null) _dimmerTop.gameObject.SetActive(true);
            if (_dimmerBottom != null) _dimmerBottom.gameObject.SetActive(true);
            if (_dimmerLeft != null) _dimmerLeft.gameObject.SetActive(true);
            if (_dimmerRight != null) _dimmerRight.gameObject.SetActive(true);

            // Dimmer Top: di atas lubang
            SetScreenRect(_dimmerTop, 0f, holeRect.yMax, screenW, screenH - holeRect.yMax);
            // Dimmer Bottom: di bawah lubang
            SetScreenRect(_dimmerBottom, 0f, 0f, screenW, holeRect.yMin);
            // Dimmer Left: sisi kiri lubang
            SetScreenRect(_dimmerLeft, 0f, holeRect.yMin, holeRect.xMin, holeRect.height);
            // Dimmer Right: sisi kanan lubang
            SetScreenRect(_dimmerRight, holeRect.xMax, holeRect.yMin, screenW - holeRect.xMax, holeRect.height);

            // Posisi Frame Spotlight Emas
            if (_spotlightFrameRt != null)
            {
                Vector2 localPos;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, holeRect.center, null, out localPos))
                {
                    _spotlightFrameRt.anchoredPosition = localPos;
                    // Sesuaikan ukuran ke canvas scale
                    float scaleFactor = _canvasRt.rect.width / screenW;
                    _spotlightFrameRt.sizeDelta = new Vector2(holeRect.width * scaleFactor, holeRect.height * scaleFactor);
                }
            }
        }

        void SetScreenRect(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null || _canvasRt == null) return;

            w = Mathf.Max(0f, w);
            h = Mathf.Max(0f, h);

            float scaleFactor = _canvasRt.rect.width / Mathf.Max(1f, Screen.width);
            Vector2 localMin;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, new Vector2(x, y), null, out localMin);

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = localMin;
            rt.sizeDelta = new Vector2(w * scaleFactor, h * scaleFactor);
        }

        void HandleSpotlightPulse(float dt)
        {
            if (!_spotlightActive || _spotlightFrameRt == null) return;

            _spotlightPulseTimer += dt * 3.5f;
            float pulse = 1f + Mathf.Sin(_spotlightPulseTimer) * 0.03f;
            _spotlightFrameRt.localScale = Vector3.one * pulse;

            float alpha = 0.85f + Mathf.Sin(_spotlightPulseTimer) * 0.15f;
            Color gold = new Color(1f, 0.835f, 0.31f, alpha);
            if (_spotlightBorders != null)
            {
                for (int i = 0; i < _spotlightBorders.Length; i++)
                {
                    if (_spotlightBorders[i] != null)
                        _spotlightBorders[i].color = gold;
                }
            }
        }

        // ------------------------------------------------------------------
        // Speech Bubble & Instruction Box UI
        // ------------------------------------------------------------------

        void ShowMarieSpeechBubble(string text, Action onContinue)
        {
            ClearCutoutSpotlight();

            if (_speechBubbleRoot != null)
            {
                _speechBubbleRoot.SetActive(true);
            }

            if (_speechBubbleBodyText != null)
            {
                _speechBubbleBodyText.text = SanitizeTutorialText(text);
                UITextUtil.ApplyProjectFont(_speechBubbleBodyText);
            }

            if (_speechBubbleContinueButton != null)
            {
                _speechBubbleContinueButton.onClick.RemoveAllListeners();
                _speechBubbleContinueButton.onClick.AddListener(() => {
                    if (_speechBubbleRoot != null) _speechBubbleRoot.SetActive(false);
                    onContinue?.Invoke();
                });
            }

            UpdateSpeechBubblePosition();
        }

        void SetWizardAlpha(float alpha)
        {
            if (wizard == null) wizard = GameObject.Find("Wizard");
            if (wizard == null) return;

            SpriteRenderer[] renderers = wizard.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = renderers[i].color;
                    c.a = alpha;
                    renderers[i].color = c;
                }
            }
        }

        void SetWizardFacingLeft()
        {
            if (wizard == null) wizard = GameObject.Find("Wizard");
            if (wizard == null) return;

            Vector3 s = wizard.transform.localScale;
            s.x = -Mathf.Abs(s.x);
            wizard.transform.localScale = s;
        }

        public void HideMariePlaceholder()
        {
            if (marie != null)
            {
                if (marie.spriteRenderer != null)
                {
                    marie.spriteRenderer.enabled = false;
                    Color c = marie.spriteRenderer.color;
                    c.a = 0f;
                    marie.spriteRenderer.color = c;
                }

                SpriteRenderer[] childRenderers = marie.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < childRenderers.Length; i++)
                {
                    if (childRenderers[i] != null)
                    {
                        childRenderers[i].enabled = false;
                        Color c = childRenderers[i].color;
                        c.a = 0f;
                        childRenderers[i].color = c;
                    }
                }

                marie.gameObject.SetActive(false);
            }

            string[] candidateNames = new string[]
            {
                "marieRenderer",
                "mariePlaceholder",
                "MarieCurie_Placeholder",
                "Marie_Placeholder",
                "MariePlaceholder",
                "MarieCurie",
                "Marie_Curie",
                "Placeholder_Marie",
                "Marie"
            };

            for (int i = 0; i < candidateNames.Length; i++)
            {
                GameObject obj = GameObject.Find(candidateNames[i]);
                if (obj != null && (wizard == null || (obj != wizard && !obj.transform.IsChildOf(wizard.transform))))
                {
                    var r = obj.GetComponent<Renderer>();
                    if (r != null) r.enabled = false;
                    obj.SetActive(false);
                }
            }

            GameObject arena = GameObject.Find("Arena_25D");
            if (arena != null)
            {
                Transform[] transforms = arena.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform t = transforms[i];
                    if (t == null) continue;
                    if (wizard != null && (t.gameObject == wizard || t.IsChildOf(wizard.transform)))
                        continue;

                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("marie") || lower.Contains("placeholder"))
                    {
                        var r = t.GetComponent<Renderer>();
                        if (r != null) r.enabled = false;
                        t.gameObject.SetActive(false);
                    }
                }
            }
        }

        void UpdateSpeechBubblePosition()
        {
            if (_speechBubbleRt == null || mainCamera == null) return;

            if (wizard == null) wizard = GameObject.Find("Wizard");
            if (wizard == null) return;

            // Pastikan acuan posisi speech bubble hanya memakai objek Wizard (+1.2f world unit dari pusat Wizard)
            Vector3 worldPos = wizard.transform.position + Vector3.up * 1.2f;

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, mainCamera.WorldToScreenPoint(worldPos), null, out localPoint))
            {
                // Clamp agar tetap berada di dalam layar
                float halfW = _speechBubbleRt.sizeDelta.x * 0.5f;
                float clampedX = Mathf.Clamp(localPoint.x, -_canvasRt.rect.width * 0.5f + halfW + 20f, _canvasRt.rect.width * 0.5f - halfW - 20f);
                float clampedY = Mathf.Clamp(localPoint.y, -_canvasRt.rect.height * 0.5f + 100f, _canvasRt.rect.height * 0.5f - 100f);
                _speechBubbleRt.anchoredPosition = new Vector2(clampedX, clampedY);
            }
        }

        void ShowInstructionBox(int stepNum, int totalSteps, string title, string instruction, bool showContinueBtn, Action onContinue)
        {
            if (_instructionBoxRoot != null)
                _instructionBoxRoot.SetActive(true);

            if (_instructionStepBadge != null)
            {
                _instructionStepBadge.text = $"LANGKAH {stepNum} DARI {totalSteps}";
                UITextUtil.ApplyProjectFont(_instructionStepBadge);
            }

            if (_instructionTitleText != null)
            {
                _instructionTitleText.text = SanitizeTutorialText(title);
                UITextUtil.ApplyProjectFont(_instructionTitleText);
            }

            if (_instructionBodyText != null)
            {
                _instructionBodyText.text = SanitizeTutorialText(instruction);
                UITextUtil.ApplyProjectFont(_instructionBodyText);
            }

            if (_instructionContinueButton != null)
            {
                _instructionContinueButton.gameObject.SetActive(showContinueBtn);
                if (showContinueBtn)
                {
                    _instructionContinueButton.onClick.RemoveAllListeners();
                    _instructionContinueButton.onClick.AddListener(() => {
                        onContinue?.Invoke();
                    });
                }
            }
        }

        // ------------------------------------------------------------------
        // Input Global & Skip
        // ------------------------------------------------------------------

        void HandleGlobalInput()
        {
            // Tombol Escape -> Lewati Tutorial
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SkipTutorial();
                return;
            }

            // Tombol Space -> Lanjut jika tombol continue aktif
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (_speechBubbleRoot != null && _speechBubbleRoot.activeSelf && _speechBubbleContinueButton != null)
                {
                    _speechBubbleContinueButton.onClick.Invoke();
                }
                else if (_instructionBoxRoot != null && _instructionBoxRoot.activeSelf && _instructionContinueButton != null && _instructionContinueButton.gameObject.activeSelf)
                {
                    _instructionContinueButton.onClick.Invoke();
                }
            }
        }

        public void SkipTutorial()
        {
            Debug.Log("[PrologueTutorial] Pemain memilih melewati tutorial (Skip).");
            FinishPrologueTutorial();
        }

        void FinishPrologueTutorial()
        {
            ChangeState(PrologueState.Completed);

            ClearCutoutSpotlight();

            if (_speechBubbleRoot != null) _speechBubbleRoot.SetActive(false);
            if (_instructionBoxRoot != null) _instructionBoxRoot.SetActive(false);
            if (_skipButtonRoot != null) _skipButtonRoot.SetActive(false);
            if (_tutorialCanvas != null) _tutorialCanvas.gameObject.SetActive(false);

            // Pastikan seluruh HUD aktif
            if (uiBridge != null)
            {
                if (uiBridge.bottomPanel != null) uiBridge.bottomPanel.SetActive(true);
                if (uiBridge.topPanel != null) uiBridge.topPanel.SetActive(true);
                if (uiBridge.hotbarRoot != null) uiBridge.hotbarRoot.SetActive(true);
                uiBridge.CloseCraftPanel();
                uiBridge.CloseRecipeDrawer();
            }

            if (harvestPipes != null)
            {
                harvestPipes.gameObject.SetActive(true);
            }

            // Berikan 1x ramuan Gel Sabun Beku jika slot 1 masih 0 agar pemain langsung punya amunisi
            if (alchemy != null && alchemy.potionStock != null && alchemy.potionStock[0] == 0)
            {
                alchemy.potionStock[0] = 1;
                alchemy.selectedSlotIndex = 0;
            }

            // Pastikan Wizard tampak penuh dan placeholder Marie tetap tersembunyi
            SetWizardAlpha(1f);
            HideMariePlaceholder();

            if (mainCamera != null)
            {
                float safeDefaultSize = ClampCameraZoom(defaultCameraSize);
                mainCamera.orthographicSize = safeDefaultSize;
                mainCamera.transform.position = ClampCameraPositionToBounds(new Vector3(0f, 0f, FixedCameraZ), safeDefaultSize);
            }

            if (cameraRig != null)
            {
                cameraRig.enabled = true;
            }

            // Aktifkan kembali lemparan ramuan
            if (throwTargeting != null)
            {
                throwTargeting.enabled = true;
            }

            // Hidupkan kembali zombie asam tutorial dan lanjutkan wave
            if (_spawnedScoutZombie != null)
            {
                _spawnedScoutZombie.gameObject.SetActive(true);
                if (_zombiePathFollower != null)
                {
                    _zombiePathFollower.moveSpeed = 0.15f; // gerak normal
                }
            }

            // Jalankan WaveManager
            if (waveManager != null)
            {
                waveManager.ResumeAfterTutorial();
            }

            // Lepaskan tahanan ZombieIntroSpotlight agar spotlight zombie baru kembali aktif normal
            ZombieIntroSpotlight.IsPausedByTutorial = false;

            EnsureArenaActive();

            if (uiBridge != null)
            {
                uiBridge.RefreshUI(true);
            }

            Debug.Log("[PrologueTutorial] Tutorial selesai. Wave 1 dimulai.");
        }

        // ------------------------------------------------------------------
        // Pembuatan UI Tutorial Dinamis (Cinzel SDF + Steampunk)
        // ------------------------------------------------------------------

        void EnsureTutorialUI()
        {
            if (_tutorialCanvas != null) return;

            // Buat Canvas UI Overlay Mandiri
            GameObject canvasGo = new GameObject("Prologue_Tutorial_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;

            _tutorialCanvas = canvasGo.GetComponent<Canvas>();
            _tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _tutorialCanvas.sortingOrder = 280; // Di atas game UI

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRt = canvasGo.GetComponent<RectTransform>();

            // --- 4 Panel Cutout Dimmer ---
            _dimmerTop = CreateDimmerPanel("Dimmer_Top", _canvasRt);
            _dimmerBottom = CreateDimmerPanel("Dimmer_Bottom", _canvasRt);
            _dimmerLeft = CreateDimmerPanel("Dimmer_Left", _canvasRt);
            _dimmerRight = CreateDimmerPanel("Dimmer_Right", _canvasRt);
            _dimmerFull = CreateDimmerPanel("Dimmer_Full", _canvasRt);
            _dimmerFull.anchorMin = Vector2.zero;
            _dimmerFull.anchorMax = Vector2.one;
            _dimmerFull.offsetMin = Vector2.zero;
            _dimmerFull.offsetMax = Vector2.zero;
            _dimmerFull.gameObject.SetActive(false);

            // --- Frame Spotlight Emas (Hollow: 4 Border Garis Tepi Tipis Berlubang) ---
            GameObject frameGo = new GameObject("Spotlight_Frame", typeof(RectTransform));
            frameGo.layer = 5;
            frameGo.transform.SetParent(_canvasRt, false);
            _spotlightFrameRt = frameGo.GetComponent<RectTransform>();
            _spotlightFrameRt.pivot = new Vector2(0.5f, 0.5f);

            _spotlightBorders = new Image[4];
            string[] borderNames = { "Border_Top", "Border_Bottom", "Border_Left", "Border_Right" };
            Color goldBorderCol = new Color(1f, 0.835f, 0.31f, 1f); // #FFD54F
            const float BorderThickness = 4.5f;

            for (int i = 0; i < 4; i++)
            {
                GameObject bGo = new GameObject(borderNames[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bGo.layer = 5;
                bGo.transform.SetParent(_spotlightFrameRt, false);

                Image bImg = bGo.GetComponent<Image>();
                bImg.color = goldBorderCol;
                bImg.raycastTarget = false; // 100% tembus klik/drag

                RectTransform bRt = bGo.GetComponent<RectTransform>();
                if (i == 0) // Top
                {
                    bRt.anchorMin = new Vector2(0f, 1f);
                    bRt.anchorMax = new Vector2(1f, 1f);
                    bRt.pivot = new Vector2(0.5f, 1f);
                    bRt.sizeDelta = new Vector2(0f, BorderThickness);
                    bRt.anchoredPosition = Vector2.zero;
                }
                else if (i == 1) // Bottom
                {
                    bRt.anchorMin = new Vector2(0f, 0f);
                    bRt.anchorMax = new Vector2(1f, 0f);
                    bRt.pivot = new Vector2(0.5f, 0f);
                    bRt.sizeDelta = new Vector2(0f, BorderThickness);
                    bRt.anchoredPosition = Vector2.zero;
                }
                else if (i == 2) // Left
                {
                    bRt.anchorMin = new Vector2(0f, 0f);
                    bRt.anchorMax = new Vector2(0f, 1f);
                    bRt.pivot = new Vector2(0f, 0.5f);
                    bRt.sizeDelta = new Vector2(BorderThickness, 0f);
                    bRt.anchoredPosition = Vector2.zero;
                }
                else if (i == 3) // Right
                {
                    bRt.anchorMin = new Vector2(1f, 0f);
                    bRt.anchorMax = new Vector2(1f, 1f);
                    bRt.pivot = new Vector2(1f, 0.5f);
                    bRt.sizeDelta = new Vector2(BorderThickness, 0f);
                    bRt.anchoredPosition = Vector2.zero;
                }

                _spotlightBorders[i] = bImg;
            }

            frameGo.SetActive(false);

            // --- Speech Bubble Marie Curie ---
            BuildSpeechBubbleUI();

            // --- Instruction Box ---
            BuildInstructionBoxUI();

            // --- Skip Button ---
            BuildSkipButtonUI();

            // Dengarkan event panen
            TowerPipeHarvester.OnIngredientHarvested += HandleIngredientHarvested;
        }

        void OnDestroy()
        {
            TowerPipeHarvester.OnIngredientHarvested -= HandleIngredientHarvested;
            if (Instance == this) Instance = null;
        }

        void HandleIngredientHarvested(IngredientType type)
        {
            if (type == IngredientType.BahanBasa_Biru || type == IngredientType.BahanPendingin_Cyan)
            {
                _harvestedBaseOrCoolant = true;
            }
        }

        RectTransform CreateDimmerPanel(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);

            Image img = go.GetComponent<Image>();
            Color c = dimmerColor;
            c.a = 0f; // Nonaktif / transparan penuh di awal
            img.color = c;
            img.raycastTarget = true; // Blokir klik di area gelap saat spotlight aktif

            go.SetActive(false);
            return go.GetComponent<RectTransform>();
        }

        void BuildSpeechBubbleUI()
        {
            GameObject bubbleGo = new GameObject("Marie_SpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            bubbleGo.layer = 5;
            bubbleGo.transform.SetParent(_canvasRt, false);

            _speechBubbleRoot = bubbleGo;
            _speechBubbleRt = bubbleGo.GetComponent<RectTransform>();
            _speechBubbleRt.sizeDelta = new Vector2(460f, 210f);
            _speechBubbleRt.pivot = new Vector2(0.5f, 0f);

            Image bubbleBg = bubbleGo.GetComponent<Image>();
            bubbleBg.color = darkPanelColor;

            Outline bubbleOutline = bubbleGo.GetComponent<Outline>();
            bubbleOutline.effectColor = goldAccentColor;
            bubbleOutline.effectDistance = new Vector2(2.5f, -2.5f);

            // Title
            _speechBubbleTitleText = UITextUtil.CreateText(bubbleGo.transform, "Title", 14f);
            RectTransform titleRt = _speechBubbleTitleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(16f, -12f);
            titleRt.sizeDelta = new Vector2(428f, 24f);
            _speechBubbleTitleText.text = "MARIE CURIE  -  PENELITI UTAMA";
            _speechBubbleTitleText.color = goldAccentColor;
            _speechBubbleTitleText.fontStyle = FontStyles.Bold;

            // Body
            _speechBubbleBodyText = UITextUtil.CreateText(bubbleGo.transform, "Body", 13.5f);
            RectTransform bodyRt = _speechBubbleBodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(16f, 48f);
            bodyRt.offsetMax = new Vector2(-16f, -38f);
            _speechBubbleBodyText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            _speechBubbleBodyText.lineSpacing = 12f;

            // Button Lanjut
            GameObject btnGo = new GameObject("Continue_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.layer = 5;
            btnGo.transform.SetParent(bubbleGo.transform, false);

            RectTransform btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-16f, 12f);
            btnRt.sizeDelta = new Vector2(170f, 34f);

            Image btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.18f, 0.45f, 0.22f, 0.95f);

            Outline btnOutline = btnGo.GetComponent<Outline>();
            btnOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

            _speechBubbleContinueButton = btnGo.GetComponent<Button>();
            _speechBubbleContinueButton.targetGraphic = btnImg;

            TextMeshProUGUI btnTxt = UITextUtil.CreateText(btnGo.transform, "Label", 13f);
            RectTransform txtRt = btnTxt.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.text = "LANJUT  [SPACE]";
            btnTxt.color = Color.white;
            btnTxt.fontStyle = FontStyles.Bold;

            bubbleGo.SetActive(false);
        }

        void BuildInstructionBoxUI()
        {
            GameObject boxGo = new GameObject("Instruction_Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            boxGo.layer = 5;
            boxGo.transform.SetParent(_canvasRt, false);

            _instructionBoxRoot = boxGo;
            _instructionBoxRt = boxGo.GetComponent<RectTransform>();
            _instructionBoxRt.anchorMin = new Vector2(0.5f, 1f);
            _instructionBoxRt.anchorMax = new Vector2(0.5f, 1f);
            _instructionBoxRt.pivot = new Vector2(0.5f, 1f);
            _instructionBoxRt.anchoredPosition = new Vector2(0f, -24f);
            _instructionBoxRt.sizeDelta = new Vector2(580f, 140f);

            Image boxBg = boxGo.GetComponent<Image>();
            boxBg.color = darkPanelColor;

            Outline boxOutline = boxGo.GetComponent<Outline>();
            boxOutline.effectColor = goldAccentColor;
            boxOutline.effectDistance = new Vector2(2.5f, -2.5f);

            // Step Badge
            _instructionStepBadge = UITextUtil.CreateText(boxGo.transform, "Badge", 12f);
            RectTransform badgeRt = _instructionStepBadge.rectTransform;
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(18f, -10f);
            badgeRt.sizeDelta = new Vector2(540f, 18f);
            _instructionStepBadge.text = "LANGKAH 1 DARI 6";
            _instructionStepBadge.color = goldAccentColor;
            _instructionStepBadge.fontStyle = FontStyles.Bold;

            // Title
            _instructionTitleText = UITextUtil.CreateText(boxGo.transform, "Title", 17f);
            RectTransform titleRt = _instructionTitleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(18f, -28f);
            titleRt.sizeDelta = new Vector2(540f, 26f);
            _instructionTitleText.color = Color.white;
            _instructionTitleText.fontStyle = FontStyles.Bold;

            // Body
            _instructionBodyText = UITextUtil.CreateText(boxGo.transform, "Body", 13.5f);
            RectTransform bodyRt = _instructionBodyText.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(18f, 38f);
            bodyRt.offsetMax = new Vector2(-18f, -54f);
            _instructionBodyText.color = new Color(0.90f, 0.92f, 0.96f, 1f);

            // Continue Button (Opsional untuk step tour)
            GameObject btnGo = new GameObject("Continue_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.layer = 5;
            btnGo.transform.SetParent(boxGo.transform, false);

            RectTransform btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(1f, 0f);
            btnRt.anchoredPosition = new Vector2(-16f, 10f);
            btnRt.sizeDelta = new Vector2(150f, 30f);

            Image btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.20f, 0.50f, 0.25f, 0.95f);

            _instructionContinueButton = btnGo.GetComponent<Button>();
            _instructionContinueButton.targetGraphic = btnImg;

            _instructionContinueLabel = UITextUtil.CreateText(btnGo.transform, "Label", 12.5f);
            RectTransform txtRt = _instructionContinueLabel.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            _instructionContinueLabel.alignment = TextAlignmentOptions.Center;
            _instructionContinueLabel.text = "LANJUT  [SPACE]";
            _instructionContinueLabel.color = Color.white;
            _instructionContinueLabel.fontStyle = FontStyles.Bold;

            boxGo.SetActive(false);
        }

        void BuildSkipButtonUI()
        {
            GameObject skipGo = new GameObject("Skip_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            skipGo.layer = 5;
            skipGo.transform.SetParent(_canvasRt, false);

            _skipButtonRoot = skipGo;
            RectTransform skipRt = skipGo.GetComponent<RectTransform>();
            skipRt.anchorMin = new Vector2(1f, 1f);
            skipRt.anchorMax = new Vector2(1f, 1f);
            skipRt.pivot = new Vector2(1f, 1f);
            skipRt.anchoredPosition = new Vector2(-20f, -20f);
            skipRt.sizeDelta = new Vector2(210f, 36f);

            Image skipImg = skipGo.GetComponent<Image>();
            skipImg.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);

            Outline skipOutline = skipGo.GetComponent<Outline>();
            skipOutline.effectColor = new Color(goldAccentColor.r, goldAccentColor.g, goldAccentColor.b, 0.65f);
            skipOutline.effectDistance = new Vector2(1.5f, -1.5f);

            _skipButton = skipGo.GetComponent<Button>();
            _skipButton.targetGraphic = skipImg;
            _skipButton.onClick.AddListener(SkipTutorial);

            TextMeshProUGUI skipTxt = UITextUtil.CreateText(skipGo.transform, "Label", 12f);
            RectTransform txtRt = skipTxt.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            skipTxt.alignment = TextAlignmentOptions.Center;
            skipTxt.text = "LEWATI TUTORIAL  [ESC]";
            skipTxt.color = new Color(1f, 0.92f, 0.70f, 0.95f);
            skipTxt.fontStyle = FontStyles.Bold;
        }
    }
}
