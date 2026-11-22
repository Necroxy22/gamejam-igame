using System;
using System.Collections.Generic;
using CuriesCrucible.Arena25D;
using CuriesCrucible.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// Sistem "New Zombie Spotlight & Quick Info":
    /// Mendeteksi kemunculan pertama kali dari setiap jenis zombie dalam satu sesi permainan,
    /// menunggu ~0.6 detik hingga zombie terlihat jelas di jalan, lalu melakukan zoom & pan sinematik
    /// dengan kamera Main Camera (Time.timeScale = 0f, zoom ~55% ukuran normal), memblokir lemparan ramuan
    /// dan peracikan di AlchemyManager, serta menyajikan kartu Quick Info bertema Victorian Parchment & Brass
    /// yang melayang tepat di atas zombie baru tersebut.
    /// Tekan [SPASI] atau klik tombol LANJUT [SPACE] untuk mengembalikan kamera secara halus dan melanjutkan game.
    /// Kompatibel 100% WebGL dan Unity 2022.3.62f3.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZombieIntroSpotlight : MonoBehaviour
    {
        public static ZombieIntroSpotlight Instance { get; private set; }

        /// <summary>Apakah spotlight kamera & kartu info sedang aktif.</summary>
        public static bool IsSpotlightActive { get; private set; }

        /// <summary>Ditahan saat PrologueTutorialManager sedang aktif agar tidak bentrok.</summary>
        public static bool IsPausedByTutorial { get; set; } = false;

        [Header("Referensi Kamera")]
        public Camera mainCamera;
        public Arena25DCameraRig cameraRig;

        [Header("Pengaturan Pan & Zoom")]
        [Tooltip("Faktor skala zoom terhadap ukuran awal kamera (0.55 = zoom in 55% dari orthographicSize awal).")]
        [Range(0.35f, 0.85f)]
        public float zoomMultiplier = 0.55f;

        [Tooltip("Kecepatan lerp kamera (unscaled time).")]
        public float panSpeed = 6.0f;

        [Tooltip("Jeda waktu (detik) setelah spawn sebelum kamera mulai bergerak, agar zombie masuk terlihat jelas di jalan.")]
        public float spawnDelay = 0.6f;

        [Header("Batas Gerak Kamera (Opsional)")]
        [Tooltip("SpriteRenderer BG_Arena untuk batas clamp kamera. Kosongkan untuk deteksi otomatis.")]
        public SpriteRenderer arenaBackground;

        [Header("UI Kartu Quick Info (Customizable)")]
        [Tooltip("Canvas overlay kartu info. Jika kosong, dibuat otomatis secara runtime.")]
        public GameObject cardCanvasRoot;
        public RectTransform cardFrameRect;
        public TMPro.TextMeshProUGUI headerBannerText;
        public TMPro.TextMeshProUGUI zombieTitleText;
        public TMPro.TextMeshProUGUI zombieSubtitleText;
        public TMPro.TextMeshProUGUI weaknessBodyText;
        public TMPro.TextMeshProUGUI tabooBodyText;
        public TMPro.TextMeshProUGUI continueButtonText;
        public Button continueButton;

        [Header("Kartu — Tampilan Bisa Dikustomisasi")]
        [Tooltip("Sprite latar bingkai kartu. Kosong = warna solid (cardBackgroundColor).")]
        public Sprite cardBackgroundSprite;
        public Color cardBackgroundColor = new Color(0.12f, 0.08f, 0.05f, 0.95f);
        [Tooltip("Warna garis tepi (Outline) kartu.")]
        public Color borderColor = new Color(0.80f, 0.62f, 0.28f);
        public Vector2 borderDistance = new Vector2(2f, -2f);

        [Tooltip("Sprite latar tombol Lanjut. Kosong = warna solid (continueButtonColor).")]
        public Sprite continueButtonSprite;
        public Color continueButtonColor = new Color(0.22f, 0.58f, 0.28f);

        [Tooltip("Label tombol default untuk semua zombie (boleh dikosongkan per-zombie).")]
        public string defaultContinueLabel = "LANJUT  [SPACE]";

        [Header("Kartu — Warna Teks (Bisa Dikustomisasi)")]
        [Tooltip("Warna teks banner header (• CATATAN LABORATORIUM ...).")]
        public Color headerTextColor = new Color(0.96f, 0.82f, 0.44f, 1f);
        [Tooltip("Warna judul. Dipakai bila titleColor pada data zombie masih transparan.")]
        public Color titleTextColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Warna subjudul / deskripsi kimia singkat.")]
        public Color subtitleTextColor = new Color(0.85f, 0.80f, 0.70f, 0.95f);
        [Tooltip("Warna teks isi kotak kelemahan & pantangan.")]
        public Color bodyTextColor = new Color(0.95f, 0.91f, 0.82f, 0.98f);
        [Tooltip("Warna label tombol LANJUT.")]
        public Color continueTextColor = new Color(1f, 1f, 1f, 1f);

        [Header("Kartu — Ukuran Teks (TextMeshPro)")]
        public float headerFontSize = 16f;
        public float titleFontSize = 24f;
        public float subtitleFontSize = 15f;
        public float bodyFontSize = 15f;
        public float buttonFontSize = 17f;

        [Header("Kartu — Data per Jenis Zombie")]
        [Tooltip("Urutan: 0=Asam, 1=Basa, 2=Api, 3=Besi, 4=Boss. Teks kosong = memakai data bawaan.")]
        public ZombieIntroCardData[] cardData = new ZombieIntroCardData[5];

        enum SpotlightState
        {
            Idle,
            WaitingDelay,
            PanningIn,
            ShowingCard,
            PanningOut
        }

        static readonly HashSet<ZombieType> _introducedTypes = new HashSet<ZombieType>();
        readonly Queue<ZombieChemicalEntity> _pendingQueue = new Queue<ZombieChemicalEntity>();

        SpotlightState _state = SpotlightState.Idle;
        ZombieChemicalEntity _targetZombie;
        float _delayTimer;

        Vector3 _originalCamPos;
        float _originalCamSize;
        Vector3 _targetCamPos;
        float _targetCamSize;

        Bounds _arenaBounds;
        bool _hasArenaBounds;

        static Font _cachedFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoIntegrateOnSceneLoad()
        {
            if (Instance == null)
            {
                ZombieIntroSpotlight existing = FindObjectOfType<ZombieIntroSpotlight>();
                if (existing == null)
                {
                    GameObject managerGo = GameObject.Find("Game_Manager");
                    if (managerGo != null)
                    {
                        existing = managerGo.AddComponent<ZombieIntroSpotlight>();
                    }
                    else
                    {
                        GameObject newGo = new GameObject("Zombie_Intro_Spotlight");
                        existing = newGo.AddComponent<ZombieIntroSpotlight>();
                    }
                }
                existing.ResolveCamera();
                existing.ResolveArenaBounds();
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Reset daftar zombie yang diperkenalkan pada awal sesi baru / restart scene
            ResetIntroduced();

            ResolveCamera();
            ResolveArenaBounds();
            EnsureCardUI();
        }

        void OnEnable()
        {
            WaveManager.OnZombieSpawned += HandleZombieSpawned;
        }

        void OnDisable()
        {
            WaveManager.OnZombieSpawned -= HandleZombieSpawned;
            if (IsSpotlightActive)
            {
                EndSpotlightInstant();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Reset riwayat tipe zombie yang sudah diperkenalkan (misal untuk restart scene).</summary>
        public static void ResetIntroduced()
        {
            _introducedTypes.Clear();
        }

        public void ResolveCamera()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null && cameraRig == null)
                cameraRig = mainCamera.GetComponent<Arena25DCameraRig>();
        }

        public void ResolveArenaBounds()
        {
            if (arenaBackground == null)
            {
                GameObject bgGo = GameObject.Find("BG_Arena");
                if (bgGo != null)
                    arenaBackground = bgGo.GetComponent<SpriteRenderer>();
            }

            if (arenaBackground != null)
            {
                _arenaBounds = arenaBackground.bounds;
                _hasArenaBounds = true;
            }
            else
            {
                // Fallback batas arena default
                _arenaBounds = new Bounds(Vector3.zero, new Vector3(17.2f, 9.6f, 10f));
                _hasArenaBounds = true;
            }
        }

        void HandleZombieSpawned(ZombieChemicalEntity zombie)
        {
            if (zombie == null)
                return;

            // Tahan bila tutorial prolog sedang berjalan
            if (IsPausedByTutorial || (PrologueTutorialManager.Instance != null && PrologueTutorialManager.Instance.CurrentState != PrologueState.Completed && PrologueTutorialManager.Instance.CurrentState != PrologueState.NotStarted))
                return;

            // Jika tipe zombie ini belum pernah diperkenalkan dalam sesi ini
            if (!_introducedTypes.Contains(zombie.zombieType))
            {
                _introducedTypes.Add(zombie.zombieType);
                _pendingQueue.Enqueue(zombie);
            }
        }

        void Update()
        {
            // Jangan memproses queue jika sedang ditahan oleh tutorial prolog
            if (IsPausedByTutorial || (PrologueTutorialManager.Instance != null && PrologueTutorialManager.Instance.CurrentState != PrologueState.Completed && PrologueTutorialManager.Instance.CurrentState != PrologueState.NotStarted))
                return;

            // Ambil kandidat dari antrean jika sedang Idle
            if (_state == SpotlightState.Idle && _pendingQueue.Count > 0)
            {
                ZombieChemicalEntity next = _pendingQueue.Dequeue();
                if (next != null && !next.IsDead)
                {
                    _targetZombie = next;
                    _delayTimer = spawnDelay;
                    _state = SpotlightState.WaitingDelay;
                }
            }

            switch (_state)
            {
                case SpotlightState.WaitingDelay:
                    UpdateWaitingDelay();
                    break;

                case SpotlightState.PanningIn:
                    UpdatePanningIn();
                    break;

                case SpotlightState.ShowingCard:
                    UpdateShowingCard();
                    break;

                case SpotlightState.PanningOut:
                    UpdatePanningOut();
                    break;
            }
        }

        void UpdateWaitingDelay()
        {
            if (_targetZombie == null || _targetZombie.IsDead)
            {
                _state = SpotlightState.Idle;
                return;
            }

            // Hitung mundur dengan deltaTime (simulasi game masih berjalan normal di sini)
            _delayTimer -= Time.deltaTime;
            if (_delayTimer <= 0f)
            {
                StartSpotlight();
            }
        }

        void StartSpotlight()
        {
            if (_targetZombie == null || _targetZombie.IsDead)
            {
                _state = SpotlightState.Idle;
                return;
            }

            ResolveCamera();
            if (mainCamera == null)
            {
                _state = SpotlightState.Idle;
                return;
            }

            IsSpotlightActive = true;

            // Simpan konfigurasi awal kamera
            _originalCamPos = mainCamera.transform.position;
            _originalCamSize = mainCamera.orthographicSize;

            // Nonaktifkan sementara CameraRig agar tidak menimpa zoom kamera
            if (cameraRig != null)
                cameraRig.enabled = false;

            // Set zoomSize dinamis berdasarkan initialOrthoSize * 0.55f (~55% ukuran normal)
            _targetCamSize = _originalCamSize * Mathf.Clamp(zoomMultiplier, 0.35f, 0.85f);

            // Fokuskan kamera ke posisi zombie dengan clamp lembut
            Vector3 zombiePos = _targetZombie.transform.position;
            _targetCamPos = ClampCameraPosition(new Vector3(zombiePos.x, zombiePos.y, _originalCamPos.z), _targetCamSize);

            // Bekukan simulasi game & hentikan pergerakan / tembakan sementara
            Time.timeScale = 0f;

            _state = SpotlightState.PanningIn;
        }

        void UpdatePanningIn()
        {
            if (mainCamera == null)
            {
                FinishSpotlight();
                return;
            }

            // Gerakkan kamera mendekati target secara halus (unscaledDeltaTime)
            float dt = Time.unscaledDeltaTime;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, _targetCamPos, dt * panSpeed);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, _targetCamSize, dt * panSpeed);

            // Jika sudah cukup dekat, tampilkan kartu info
            float dist = Vector2.Distance(new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.y),
                                          new Vector2(_targetCamPos.x, _targetCamPos.y));
            float sizeDiff = Mathf.Abs(mainCamera.orthographicSize - _targetCamSize);

            if (dist < 0.08f && sizeDiff < 0.08f)
            {
                mainCamera.transform.position = _targetCamPos;
                mainCamera.orthographicSize = _targetCamSize;
                ShowCardUI();
                _state = SpotlightState.ShowingCard;
            }
        }

        void UpdateShowingCard()
        {
            // Perbarui posisi kartu agar selalu presisi di atas kepala zombie
            UpdateCardPosition();

            // Input [SPASI] atau [ENTER] untuk melanjutkan
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                OnContinueClicked();
            }
        }

        void UpdatePanningOut()
        {
            if (mainCamera == null)
            {
                FinishSpotlight();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, _originalCamPos, dt * panSpeed);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, _originalCamSize, dt * panSpeed);

            float dist = Vector2.Distance(new Vector2(mainCamera.transform.position.x, mainCamera.transform.position.y),
                                          new Vector2(_originalCamPos.x, _originalCamPos.y));
            float sizeDiff = Mathf.Abs(mainCamera.orthographicSize - _originalCamSize);

            if (dist < 0.05f && sizeDiff < 0.05f)
            {
                FinishSpotlight();
            }
        }

        public void OnContinueClicked()
        {
            if (_state != SpotlightState.ShowingCard)
                return;

            HideCardUI();
            _state = SpotlightState.PanningOut;
        }

        void FinishSpotlight()
        {
            if (mainCamera != null)
            {
                mainCamera.transform.position = _originalCamPos;
                mainCamera.orthographicSize = _originalCamSize;
            }

            if (cameraRig != null)
                cameraRig.enabled = true;

            Time.timeScale = 1f;
            IsSpotlightActive = false;
            _targetZombie = null;
            _state = SpotlightState.Idle;
        }

        void EndSpotlightInstant()
        {
            HideCardUI();
            FinishSpotlight();
        }

        Vector3 ClampCameraPosition(Vector3 desired, float camSize)
        {
            if (!_hasArenaBounds || mainCamera == null)
                return desired;

            float aspect = mainCamera.aspect;
            float halfH = camSize;
            float halfW = halfH * aspect;

            // Berikan toleransi margin lembut agar kamera tetap berpusat dramatis pada zombie
            float minX = _arenaBounds.min.x + halfW - 0.75f;
            float maxX = _arenaBounds.max.x - halfW + 0.75f;
            float minY = _arenaBounds.min.y + halfH - 0.5f;
            float maxY = _arenaBounds.max.y - halfH + 0.5f;

            float clampedX = minX < maxX ? Mathf.Clamp(desired.x, minX, maxX) : desired.x;
            float clampedY = minY < maxY ? Mathf.Clamp(desired.y, minY, maxY) : desired.y;

            return new Vector3(clampedX, clampedY, desired.z);
        }

        // ==================================================================
        // UI Kartu Quick Info (Victorian Parchment & Brass)
        // ==================================================================

        void ShowCardUI()
        {
            if (_targetZombie == null)
                return;

            EnsureCardUI();

            ZombieType type = _targetZombie.zombieType;
            ZombieSpotlightInfo info = GetZombieInfo(type);
            ZombieIntroCardData data = ResolveCardData(type);

            if (zombieTitleText != null)
            {
                zombieTitleText.text = data.title;
                zombieTitleText.color = data.titleColor.a > 0f ? data.titleColor : info.AccentColor;
            }

            if (zombieSubtitleText != null)
            {
                bool hasSubtitle = !string.IsNullOrEmpty(data.subtitle);
                zombieSubtitleText.gameObject.SetActive(hasSubtitle);
                if (hasSubtitle)
                    zombieSubtitleText.text = data.subtitle;
            }

            if (weaknessBodyText != null)
                weaknessBodyText.text = data.weaknessText;

            if (tabooBodyText != null)
                tabooBodyText.text = data.mutationRiskText;

            if (continueButtonText != null)
                continueButtonText.text = data.buttonLabel;

            UpdateCardPosition();

            if (cardCanvasRoot != null)
                cardCanvasRoot.SetActive(true);
        }

        /// <summary>
        /// Ambil data kartu dari Inspector. Field teks kosong otomatis diisi dari
        /// ensiklopedia bawaan (hanya sebagai fallback, tidak menimpa isian user).
        /// </summary>
        ZombieIntroCardData ResolveCardData(ZombieType type)
        {
            ZombieSpotlightInfo info = GetZombieInfo(type);
            int index = (int)type;

            ZombieIntroCardData data = null;
            if (cardData != null && index >= 0 && index < cardData.Length)
                data = cardData[index];

            if (data == null)
                data = new ZombieIntroCardData();

            if (string.IsNullOrEmpty(data.title)) data.title = info.NameAndNature;
            if (string.IsNullOrEmpty(data.subtitle)) data.subtitle = info.ChemicalDescription;
            if (string.IsNullOrEmpty(data.chemicalDescription)) data.chemicalDescription = info.ChemicalDescription;
            if (string.IsNullOrEmpty(data.weaknessText)) data.weaknessText = info.EffectivePotion;
            if (string.IsNullOrEmpty(data.mutationRiskText)) data.mutationRiskText = info.TabooAndMutationRisk;
            if (string.IsNullOrEmpty(data.buttonLabel))
                data.buttonLabel = string.IsNullOrEmpty(defaultContinueLabel) ? "LANJUT  [SPACE]" : defaultContinueLabel;

            return data;
        }

        /// <summary>Susun array data bawaan (5 jenis zombie) untuk field Inspector.</summary>
        static ZombieIntroCardData[] CreateDefaultCardData()
        {
            var result = new ZombieIntroCardData[5];
            for (int i = 0; i < 5; i++)
            {
                ZombieType type = (ZombieType)i;
                ZombieSpotlightInfo info = GetZombieInfo(type);
                result[i] = new ZombieIntroCardData
                {
                    title = info.NameAndNature,
                    subtitle = info.ChemicalDescription,
                    chemicalDescription = info.ChemicalDescription,
                    weaknessText = info.EffectivePotion,
                    mutationRiskText = info.TabooAndMutationRisk,
                    buttonLabel = "LANJUT  [SPACE]"
                };
            }
            return result;
        }

        /// <summary>Pastikan array cardData selalu 5 elemen (satu per ZombieType).</summary>
        void EnsureCardDataArray()
        {
            if (cardData != null && cardData.Length == 5)
            {
                for (int i = 0; i < 5; i++)
                    if (cardData[i] == null) cardData[i] = new ZombieIntroCardData();
                return;
            }

            ZombieIntroCardData[] defaults = CreateDefaultCardData();
            if (cardData != null)
            {
                for (int i = 0; i < cardData.Length && i < 5; i++)
                    defaults[i] = cardData[i] ?? defaults[i];
            }
            cardData = defaults;
        }

        void HideCardUI()
        {
            if (cardCanvasRoot != null)
                cardCanvasRoot.SetActive(false);
        }

        /// <summary>
        /// Menempatkan kartu Quick Info tepat melayang di atas kepala zombie (WorldToScreenPoint)
        /// dan meng-clamp posisinya agar tetap utuh berada di dalam viewport layar.
        /// </summary>
        void UpdateCardPosition()
        {
            if (_targetZombie == null || cardFrameRect == null || cardCanvasRoot == null)
                return;

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera == null)
                return;

            // Titik dunia target: 1.1 unit di atas zombie
            Vector3 worldTarget = _targetZombie.transform.position + Vector3.up * 1.1f;
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldTarget);

            // Jika zombie berada di belakang bidang potong kamera (safety check)
            if (screenPos.z < 0f)
                return;

            RectTransform canvasRt = cardCanvasRoot.GetComponent<RectTransform>();
            Vector2 localPoint;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out localPoint))
            {
                float cardW = cardFrameRect.sizeDelta.x;
                float cardH = cardFrameRect.sizeDelta.y;
                float halfW = cardW * 0.5f;
                float margin = 20f;

                // Batas horizontal layar
                float minX = canvasRt.rect.xMin + halfW + margin;
                float maxX = canvasRt.rect.xMax - halfW - margin;
                localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);

                // Batas vertikal layar (pivot kartu ada di bawah-tengah: 0.5, 0)
                if (localPoint.y + cardH + margin > canvasRt.rect.yMax)
                {
                    // Jika menabrak atas layar, turunkan kartu agar tidak terpotong
                    localPoint.y = canvasRt.rect.yMax - cardH - margin;
                }

                if (localPoint.y < canvasRt.rect.yMin + margin)
                {
                    localPoint.y = canvasRt.rect.yMin + margin;
                }

                cardFrameRect.anchoredPosition = localPoint;
            }
        }

        void EnsureCardUI()
        {
            if (cardCanvasRoot != null)
            {
                // Instance sudah ada (mis. dipasang permanen di scene): cukup synkronkan
                // data dari Inspector, jangan bangun ulang / menimpa kustomisasi user.
                EnsureCardDataArray();
                SyncCardVisualsFromConfig();
                return;
            }

            EnsureCardDataArray();

            // Buat Canvas UI Overlay mandiri khusus Spotlight (sorting order 150)
            GameObject canvasGo = new GameObject("Zombie_Spotlight_Canvas");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Dimmer Background halus & transparan agar zombie tetap terlihat jelas
            GameObject dimmer = CreateUIObject("Dimmer", canvasGo.transform);
            SetStretchFull(dimmer.GetComponent<RectTransform>());
            var imgDimmer = dimmer.AddComponent<Image>();
            imgDimmer.color = new Color(0.02f, 0.01f, 0.00f, 0.35f);
            imgDimmer.raycastTarget = false; // Wajib false agar tidak memblokir aim lemparan di arena

            // Modal Board Outer Frame (bingkai melayang di atas zombie)
            GameObject boardOuter = CreateUIObject("Spotlight_Card_Frame", dimmer.transform);
            cardFrameRect = boardOuter.GetComponent<RectTransform>();
            cardFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardFrameRect.pivot = new Vector2(0.5f, 0f); // Pivot di bawah tengah agar berdiri di atas zombie
            cardFrameRect.sizeDelta = new Vector2(500, 340);

            var imgOuter = boardOuter.AddComponent<Image>();
            imgOuter.color = UITheme.BrassBase;

            Outline outerOutline = boardOuter.AddComponent<Outline>();
            outerOutline.effectColor = borderColor;
            outerOutline.effectDistance = borderDistance;

            // Modal Board Inner (latar kartu)
            GameObject board = CreateUIObject("Spotlight_Card_Inner", boardOuter.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            SetStretchFull(rtBoard);
            rtBoard.offsetMin = new Vector2(4, 4);
            rtBoard.offsetMax = new Vector2(-4, -4);

            var imgBoard = board.AddComponent<Image>();
            imgBoard.color = cardBackgroundColor;

            // Header Banner
            GameObject bannerGo = CreateUIObject("Header_Banner", board.transform);
            RectTransform rtBanner = bannerGo.GetComponent<RectTransform>();
            rtBanner.anchorMin = new Vector2(0, 1);
            rtBanner.anchorMax = new Vector2(1, 1);
            rtBanner.pivot = new Vector2(0.5f, 1);
            rtBanner.anchoredPosition = new Vector2(0, -10);
            rtBanner.sizeDelta = new Vector2(0, 28);
            headerBannerText = UITextUtil.CreateText(bannerGo.transform, "Txt", headerFontSize);
            SetStretchFull(headerBannerText.rectTransform);
            headerBannerText.fontStyle = TMPro.FontStyles.Bold;
            headerBannerText.alignment = TMPro.TextAlignmentOptions.Center;
            headerBannerText.text = "• CATATAN LABORATORIUM : MUTAN BARU •";
            headerBannerText.color = headerTextColor;
            headerBannerText.raycastTarget = false;

            // Zombie Name & Chemical Nature
            GameObject titleGo = CreateUIObject("Zombie_Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -40);
            rtTitle.sizeDelta = new Vector2(0, 32);
            zombieTitleText = UITextUtil.CreateText(titleGo.transform, "Txt", titleFontSize);
            SetStretchFull(zombieTitleText.rectTransform);
            zombieTitleText.fontStyle = TMPro.FontStyles.Bold;
            zombieTitleText.alignment = TMPro.TextAlignmentOptions.Center;
            zombieTitleText.text = "Zombie...";
            zombieTitleText.color = titleTextColor;
            zombieTitleText.raycastTarget = false;

            // Subjudul (opsional, disembunyikan bila kosong)
            GameObject subGo = CreateUIObject("Zombie_Subtitle", board.transform);
            RectTransform rtSub = subGo.GetComponent<RectTransform>();
            rtSub.anchorMin = new Vector2(0, 1);
            rtSub.anchorMax = new Vector2(1, 1);
            rtSub.pivot = new Vector2(0.5f, 1);
            rtSub.anchoredPosition = new Vector2(0, -74);
            rtSub.sizeDelta = new Vector2(0, 24);
            zombieSubtitleText = UITextUtil.CreateText(subGo.transform, "Txt", subtitleFontSize);
            SetStretchFull(zombieSubtitleText.rectTransform);
            zombieSubtitleText.alignment = TMPro.TextAlignmentOptions.Center;
            zombieSubtitleText.color = subtitleTextColor;
            zombieSubtitleText.raycastTarget = false;

            // Kotak 1: Kelemahan (Efektif)
            GameObject boxWeak = CreateUIObject("Box_Weakness", board.transform);
            RectTransform rtWeak = boxWeak.GetComponent<RectTransform>();
            rtWeak.anchorMin = new Vector2(0.5f, 1);
            rtWeak.anchorMax = new Vector2(0.5f, 1);
            rtWeak.pivot = new Vector2(0.5f, 1);
            rtWeak.anchoredPosition = new Vector2(0, -102);
            rtWeak.sizeDelta = new Vector2(464, 82);

            var imgWeakBorder = boxWeak.AddComponent<Image>();
            imgWeakBorder.color = UITheme.BrassDark;

            GameObject boxWeakInner = CreateUIObject("Inner", boxWeak.transform);
            SetStretchFull(boxWeakInner.GetComponent<RectTransform>());
            boxWeakInner.GetComponent<RectTransform>().offsetMin = new Vector2(2, 2);
            boxWeakInner.GetComponent<RectTransform>().offsetMax = new Vector2(-2, -2);
            var imgWeakInner = boxWeakInner.AddComponent<Image>();
            imgWeakInner.color = new Color(0.08f, 0.16f, 0.09f, 0.95f);

            weaknessBodyText = UITextUtil.CreateText(boxWeakInner.transform, "Txt", bodyFontSize);
            SetStretchFull(weaknessBodyText.rectTransform);
            weaknessBodyText.rectTransform.offsetMin = new Vector2(10, 6);
            weaknessBodyText.rectTransform.offsetMax = new Vector2(-10, -6);
            weaknessBodyText.fontStyle = TMPro.FontStyles.Normal;
            weaknessBodyText.color = bodyTextColor;
            weaknessBodyText.raycastTarget = false;

            // Kotak 2: Pantangan (Risiko Mutasi)
            GameObject boxTaboo = CreateUIObject("Box_Taboo", board.transform);
            RectTransform rtTaboo = boxTaboo.GetComponent<RectTransform>();
            rtTaboo.anchorMin = new Vector2(0.5f, 1);
            rtTaboo.anchorMax = new Vector2(0.5f, 1);
            rtTaboo.pivot = new Vector2(0.5f, 1);
            rtTaboo.anchoredPosition = new Vector2(0, -190);
            rtTaboo.sizeDelta = new Vector2(464, 82);

            var imgTabooBorder = boxTaboo.AddComponent<Image>();
            imgTabooBorder.color = UITheme.BrassDark;

            GameObject boxTabooInner = CreateUIObject("Inner", boxTaboo.transform);
            SetStretchFull(boxTabooInner.GetComponent<RectTransform>());
            boxTabooInner.GetComponent<RectTransform>().offsetMin = new Vector2(2, 2);
            boxTabooInner.GetComponent<RectTransform>().offsetMax = new Vector2(-2, -2);
            var imgTabooInner = boxTabooInner.AddComponent<Image>();
            imgTabooInner.color = new Color(0.20f, 0.08f, 0.07f, 0.95f);

            tabooBodyText = UITextUtil.CreateText(boxTabooInner.transform, "Txt", bodyFontSize);
            SetStretchFull(tabooBodyText.rectTransform);
            tabooBodyText.rectTransform.offsetMin = new Vector2(10, 6);
            tabooBodyText.rectTransform.offsetMax = new Vector2(-10, -6);
            tabooBodyText.fontStyle = TMPro.FontStyles.Normal;
            tabooBodyText.color = bodyTextColor;
            tabooBodyText.raycastTarget = false;

            // Tombol LANJUT
            GameObject btnGo = CreateUIObject("Btn_Continue", board.transform);
            RectTransform rtBtn = btnGo.GetComponent<RectTransform>();
            rtBtn.anchorMin = new Vector2(0.5f, 0);
            rtBtn.anchorMax = new Vector2(0.5f, 0);
            rtBtn.pivot = new Vector2(0.5f, 0);
            rtBtn.anchoredPosition = new Vector2(0, 12);
            rtBtn.sizeDelta = new Vector2(240, 40);

            var imgBtnFrame = btnGo.AddComponent<Image>();
            imgBtnFrame.color = UITheme.BrassBright;

            GameObject btnInner = CreateUIObject("Btn_Inner", btnGo.transform);
            SetStretchFull(btnInner.GetComponent<RectTransform>());
            btnInner.GetComponent<RectTransform>().offsetMin = new Vector2(2, 2);
            btnInner.GetComponent<RectTransform>().offsetMax = new Vector2(-2, -2);
            var imgBtnInner = btnInner.AddComponent<Image>();
            imgBtnInner.color = continueButtonColor;

            continueButton = btnGo.AddComponent<Button>();
            continueButton.targetGraphic = imgBtnInner;
            var colors = continueButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            continueButton.colors = colors;
            continueButton.onClick.AddListener(OnContinueClicked);

            continueButtonText = UITextUtil.CreateText(btnInner.transform, "Txt", buttonFontSize);
            SetStretchFull(continueButtonText.rectTransform);
            continueButtonText.fontStyle = TMPro.FontStyles.Bold;
            continueButtonText.alignment = TMPro.TextAlignmentOptions.Center;
            continueButtonText.text = string.IsNullOrEmpty(defaultContinueLabel) ? "LANJUT  [SPACE]" : defaultContinueLabel;
            continueButtonText.color = continueTextColor;
            continueButtonText.raycastTarget = false;

            cardCanvasRoot = canvasGo;
            cardCanvasRoot.SetActive(false);

            SyncCardVisualsFromConfig();
        }

        /// <summary>
        /// API Editor: isi <see cref="cardData"/> (5 jenis zombie) dengan teks bawaan bila
        /// masih kosong, agar seluruh teks langsung terlihat dan bisa diedit di Inspector.
        /// Tidak pernah menimpa teks yang sudah diisi user.
        /// </summary>
        public void EditorPopulateCardDataDefaults()
        {
            EnsureCardDataArray();

            ZombieIntroCardData[] defaults = CreateDefaultCardData();
            for (int i = 0; i < cardData.Length && i < 5; i++)
            {
                if (cardData[i] == null)
                {
                    cardData[i] = defaults[i];
                    continue;
                }
                FillEmptyCardFields(cardData[i], defaults[i]);
            }
        }

        static void FillEmptyCardFields(ZombieIntroCardData data, ZombieIntroCardData fallback)
        {
            if (string.IsNullOrEmpty(data.title)) data.title = fallback.title;
            if (string.IsNullOrEmpty(data.subtitle)) data.subtitle = fallback.subtitle;
            if (string.IsNullOrEmpty(data.chemicalDescription)) data.chemicalDescription = fallback.chemicalDescription;
            if (string.IsNullOrEmpty(data.weaknessText)) data.weaknessText = fallback.weaknessText;
            if (string.IsNullOrEmpty(data.mutationRiskText)) data.mutationRiskText = fallback.mutationRiskText;
            if (string.IsNullOrEmpty(data.buttonLabel)) data.buttonLabel = fallback.buttonLabel;
        }

        /// <summary>
        /// API Editor: bangun kartu Quick Info permanen di scene (Canvas overlay + seluruh
        /// teks TextMeshProUGUI Cinzel) supaya bisa dikustomisasi di Inspector dan
        /// tersimpan permanen di SampleScene, bukan hanya dibuat saat Play Mode.
        /// </summary>
        public void EditorBuildCardPermanently()
        {
            EnsureCardUI();

            if (cardCanvasRoot != null)
                cardCanvasRoot.SetActive(false);
        }

        /// <summary>
        /// Terapkan sprite/warna kartu dari Inspector ke komponen yang sudah ada.
        /// Tidak membangun ulang objek, jadi kustomisasi hierarki user tetap utuh.
        /// </summary>
        public void SyncCardVisualsFromConfig()
        {
            if (cardFrameRect == null)
                return;

            if (cardFrameRect.TryGetComponent(out Outline frameOutline))
            {
                frameOutline.effectColor = borderColor;
                frameOutline.effectDistance = borderDistance;
            }

            Transform frameImage = cardFrameRect.Find("Spotlight_Card_Inner");
            if (frameImage != null && frameImage.TryGetComponent(out Image boardImg))
            {
                boardImg.sprite = cardBackgroundSprite;
                boardImg.color = cardBackgroundSprite != null ? cardBackgroundColor : cardBackgroundColor;
                boardImg.type = (cardBackgroundSprite != null && cardBackgroundSprite.border.sqrMagnitude > 0f)
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            }

            if (continueButton != null)
            {
                Transform inner = continueButton.transform.Find("Btn_Inner");
                if (inner != null && inner.TryGetComponent(out Image btnImg))
                {
                    btnImg.sprite = continueButtonSprite;
                    btnImg.color = continueButtonColor;
                    btnImg.type = (continueButtonSprite != null && continueButtonSprite.border.sqrMagnitude > 0f)
                        ? Image.Type.Sliced
                        : Image.Type.Simple;
                    continueButton.targetGraphic = btnImg;
                }
            }

            ApplyTextSize(headerBannerText, headerFontSize);
            ApplyTextSize(zombieTitleText, titleFontSize);
            ApplyTextSize(zombieSubtitleText, subtitleFontSize);
            ApplyTextSize(weaknessBodyText, bodyFontSize);
            ApplyTextSize(tabooBodyText, bodyFontSize);
            ApplyTextSize(continueButtonText, buttonFontSize);

            // Warna teks dari Inspector (bisa diubah kapan saja tanpa rebuild objek).
            if (headerBannerText != null) headerBannerText.color = headerTextColor;
            if (zombieSubtitleText != null) zombieSubtitleText.color = subtitleTextColor;
            if (weaknessBodyText != null) weaknessBodyText.color = bodyTextColor;
            if (tabooBodyText != null) tabooBodyText.color = bodyTextColor;
            if (continueButtonText != null) continueButtonText.color = continueTextColor;
        }

        static void ApplyTextSize(TMPro.TextMeshProUGUI text, float size)
        {
            if (text == null) return;

            // Semua teks kartu memakai font Cinzel (SDF) yang sama.
            UITextUtil.ApplyProjectFont(text);

            if (size > 0f)
                text.fontSize = size;
        }

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static void SetStretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Pengambilan font yang kompatibel 100% dengan Unity 2022.3+:
        /// Menghindari 'Arial.ttf' yang deprecated/null, memakai 'LegacyRuntime.ttf',
        /// dengan fallback dinamis OS 'Arial' 16pt atau font apa pun yang ada di project.
        /// </summary>
        public static Font GetSafeFont()
        {
            if (_cachedFont != null)
                return _cachedFont;

            // Prioritas 1: Font kustom dari Assets/Resources/Fonts (misal Cinzel)
            Font[] customFonts = Resources.LoadAll<Font>("Fonts");
            if (customFonts != null && customFonts.Length > 0)
            {
                for (int i = 0; i < customFonts.Length; i++)
                {
                    if (customFonts[i] != null)
                    {
                        _cachedFont = customFonts[i];
                        return _cachedFont;
                    }
                }
            }

            _cachedFont = Resources.Load<Font>("Fonts/Cinzel-VariableFont_wght");
            if (_cachedFont != null)
                return _cachedFont;

            // Prioritas 2: Builtin LegacyRuntime.ttf (standar Unity 2022.3+)
            _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Prioritas 2: Dynamic font dari OS (Arial 16pt)
            if (_cachedFont == null)
            {
                try
                {
                    _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                catch {}
            }

            // Prioritas 3: Fallback ke font apa pun yang terdaftar di Resources
            if (_cachedFont == null)
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts != null && fonts.Length > 0)
                {
                    for (int i = 0; i < fonts.Length; i++)
                    {
                        if (fonts[i] != null)
                        {
                            _cachedFont = fonts[i];
                            break;
                        }
                    }
                }
            }

            return _cachedFont;
        }

        // ==================================================================
        // Ensiklopedia Kimia Zombie
        // ==================================================================

        public struct ZombieSpotlightInfo
        {
            public string NameAndNature;
            public string ChemicalDescription;
            public string EffectivePotion;
            public string TabooAndMutationRisk;
            public Color AccentColor;
        }

        public static ZombieSpotlightInfo GetZombieInfo(ZombieType type)
        {
            ZombieSpotlightInfo info = new ZombieSpotlightInfo();

            switch (type)
            {
                case ZombieType.ZombieAsam:
                    info.NameAndNature = "Zombie Asam  —  Sifat: Korosif & pH Rendah";
                    info.AccentColor = new Color(0.62f, 0.90f, 0.32f);
                    info.ChemicalDescription = "Sifat: Korosif & pH rendah (asam kuat)";
                    info.EffectivePotion =
                        "<b><color=#81C784>• KELEMAHAN (EFEKTIF MENETRALKAN):</color></b>\n" +
                        "• <b>Busa Sabun Mengembang</b> (Basa + Katalis) & <b>Bom Netralisasi</b>.\n" +
                        "• Reaksi netralisasi cepat merusak inti asam dan mementalkan zombie dengan knockback kuat!";
                    info.TabooAndMutationRisk =
                        "<b><color=#E57373>• PANTANGAN (RISIKO MUTASI):</color></b>\n" +
                        "• <b>Ramuan Asam</b> (Kabut Asam Dingin / Asam Pelarut Karat).\n" +
                        "• Kebal asam! Menyiram asam justru memperkuat ion tubuhnya dan memicu <b>Mutasi Membesar (Enlarge +50% HP)</b>.";
                    break;

                case ZombieType.ZombieBasa:
                    info.NameAndNature = "Zombie Basa  —  Sifat: Alkali Licin & pH Tinggi";
                    info.AccentColor = new Color(0.45f, 0.68f, 1.00f);
                    info.ChemicalDescription = "Sifat: Alkali licin & pH tinggi";
                    info.EffectivePotion =
                        "<b><color=#81C784>• KELEMAHAN (EFEKTIF MENETRALKAN):</color></b>\n" +
                        "• <b>Kabut Asam Dingin</b> (Asam + Pendingin) & <b>Asam Pelarut Karat</b>.\n" +
                        "• Semburan asam langsung merusak mantel lendir sabun alkali secara instan!";
                    info.TabooAndMutationRisk =
                        "<b><color=#E57373>• PANTANGAN (RISIKO MUTASI):</color></b>\n" +
                        "• <b>Ramuan Basa</b> (Gel Sabun Beku / Busa Sabun).\n" +
                        "• Menambah lapisan basa membuat tubuhnya semakin licin dan memicu <b>Mutasi Gerak Cepat (SpeedUp +40%)</b>.";
                    break;

                case ZombieType.ZombieApi:
                    info.NameAndNature = "Zombie Api  —  Sifat: Eksotermik & Sangat Cepat";
                    info.AccentColor = new Color(1.00f, 0.55f, 0.22f);
                    info.ChemicalDescription = "Sifat: Eksotermik & sangat cepat";
                    info.EffectivePotion =
                        "<b><color=#81C784>• KELEMAHAN (EFEKTIF MENETRALKAN):</color></b>\n" +
                        "• <b>Ledakan Es Instan</b> (Pendingin + Katalis) & <b>Gel Sabun Beku</b>.\n" +
                        "• Reaksi endotermik drastis menyerap panas tubuhnya dan membekukannya di tempat (Root)!";
                    info.TabooAndMutationRisk =
                        "<b><color=#E57373>• PANTANGAN (RISIKO MUTASI):</color></b>\n" +
                        "• <b>Katalis Murni Tanpa Es</b>.\n" +
                        "• Mempercepat laju pembakaran api dan memicu <b>Mutasi Aura Buff Kecepatan (AoE Speed Buff)</b> ke zombie di sekitarnya!";
                    break;

                case ZombieType.ZombieBesi:
                    info.NameAndNature = "Zombie Besi  —  Sifat: Logam Berzirah Tebal (Oksidasi)";
                    info.AccentColor = new Color(0.75f, 0.77f, 0.85f);
                    info.ChemicalDescription = "Sifat: Logam berzirah tebal (oksidasi)";
                    info.EffectivePotion =
                        "<b><color=#81C784>• KELEMAHAN (EFEKTIF MENETRALKAN):</color></b>\n" +
                        "• <b>Asam Pelarut Karat</b> (Asam + Katalis) — <b>Kerusakan 3x Lipat!</b>\n" +
                        "• Satu-satunya formula korosif pekat yang sanggup melarutkan zirah besi tebal mereka.";
                    info.TabooAndMutationRisk =
                        "<b><color=#E57373>• PANTANGAN (RISIKO MUTASI):</color></b>\n" +
                        "• <b>Serangan Non-Asam / Dingin Ringan</b>.\n" +
                        "• Logam zirah memantulkan sebagian besar kerusakan dan memicu <b>Mutasi Zirah Mengeras (Harden -40% DMG)</b>.";
                    break;

                case ZombieType.BossUnstableAmalgam:
                    info.NameAndNature = "Boss Unstable Amalgam  —  Sifat: Koloid Radioaktif Dua Tahap";
                    info.AccentColor = new Color(0.88f, 0.35f, 0.95f);
                    info.ChemicalDescription = "Sifat: Koloid radioaktif dua tahap";
                    info.EffectivePotion =
                        "<b><color=#81C784>• STRATEGI 2 TAHAP MUTLAK:</color></b>\n" +
                        "• <b>Tahap 1:</b> Dinginkan dengan 3x Ramuan Es/Pendingin hingga berstatus <b>Chilled</b> (8 detik).\n" +
                        "• <b>Tahap 2:</b> Saat Chilled, SEGERA lempar <b>Bom Netralisasi</b> (Damage 4x Lipat) untuk memusnahkannya!";
                    info.TabooAndMutationRisk =
                        "<b><color=#E57373>• PANTANGAN (BAHAYA FATAL):</color></b>\n" +
                        "• Melempar Bom Netralisasi sebelum status Chilled tercapai hanya memberi 12% damage dan memicu amukan mutasi!";
                    break;
            }

            return info;
        }
    }
}
