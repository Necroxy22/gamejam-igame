using System;
using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using UnityEngine;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// Sistem interaktif "Tower Pipe Harvest Bubbles" (seperti gelembung panen di Clash of Clans).
    /// Mengelola 4 titik pipa distilasi di menara laboratorium:
    ///   1. Kiri Atas  : Bahan Basa (Biru)
    ///   2. Kiri Bawah : Bahan Pendingin (Cyan)
    ///   3. Kanan Atas : Bahan Asam (Merah)
    ///   4. Kanan Bawah: Bahan Katalis (Kuning)
    ///
    /// Menampilkan speech bubble floating (sorting order 600) ber-ekor penunjuk,
    /// animasi bobbing + pulse, collider interaktif, dan proteksi pelemparan ramuan
    /// ke jalanan melalui TowerPipeHarvester.IsPointerOverBubble().
    /// </summary>
    [DisallowMultipleComponent]
    public class TowerPipeHarvester : MonoBehaviour
    {
        public static TowerPipeHarvester Instance { get; private set; }

        [System.Serializable]
        public class HarvestPipe
        {
            [Tooltip("Nama label pipa untuk identifikasi di Inspector")]
            public string pipeName = "Pipe";

            [Tooltip("Transform titik pipa di menara (anchor)")]
            public Transform pipeAnchor;

            [Tooltip("Jenis bahan dasar yang disuling oleh pipa ini")]
            public IngredientType ingredientType = IngredientType.BahanBasa_Biru;

            [Header("Bubble References")]
            [Tooltip("Root GameObject gelembung notifikasi")]
            public GameObject bubbleRoot;

            [Tooltip("Transform gelembung untuk animasi bobbing/pulse")]
            public Transform bubbleTransform;

            [Tooltip("SpriteRenderer background gelembung (speech bubble dengan tail)")]
            public SpriteRenderer backgroundRenderer;

            [Tooltip("SpriteRenderer ikon bahan kimia di tengah gelembung")]
            public SpriteRenderer iconRenderer;

            [Tooltip("Collider2D untuk deteksi klik kursor")]
            public Collider2D bubbleCollider;

            [Header("Runtime State")]
            [Tooltip("Progress waktu penyulingan (detik)")]
            public float currentTimer = 0f;

            [Tooltip("Status apakah gelembung sedang aktif menunggu dipanen")]
            public bool isBubbleActive = false;

            [HideInInspector]
            public Vector3 initialLocalPos = Vector3.up * 0.65f;

            [HideInInspector]
            public float animPhaseOffset = 0f;
        }

        [Header("Referensi")]
        public AlchemyManager alchemy;
        public Camera arenaCamera;

        [Header("Pengaturan Produksi")]
        [Tooltip("Interval waktu penyulingan bahan per pipa (detik)")]
        public float productionInterval = 7.0f;

        [Tooltip("Batas maksimal stok per bahan dasar di AlchemyManager")]
        public int maxStockPerIngredient = 6;

        [Header("Animasi Gelembung")]
        [Tooltip("Kecepatan melayang naik-turun (bobbing)")]
        public float bobbingSpeed = 3.2f;

        [Tooltip("Jarak melayang naik-turun")]
        public float bobbingAmplitude = 0.08f;

        [Tooltip("Kecepatan denyut skala (pulsing)")]
        public float pulseSpeed = 4.0f;

        [Tooltip("Amplitudo denyut skala")]
        public float pulseAmplitude = 0.06f;

        [Tooltip("Sorting Order gelembung agar di atas balkon (500) dan Marie (450)")]
        public int bubbleSortingOrder = 600;

        [Header("Daftar Titik Pipa (4 Pipa Laboratorium)")]
        public List<HarvestPipe> pipes = new List<HarvestPipe>();

        [Header("Visual Feedback (+1 Popup)")]
        [Tooltip("Durasi teks popup +1 melayang dan memudar (detik)")]
        public float popupDuration = 0.85f;

        [Tooltip("Kecepatan teks popup meluncur ke atas")]
        public float popupRiseSpeed = 1.6f;

        // Internal tracking untuk feedback popup
        class FloatingPopup
        {
            public GameObject root;
            public TextMesh textMesh;
            public Vector3 startPos;
            public float age;
            public float duration;
            public Color baseColor;
        }

        readonly List<FloatingPopup> _activePopups = new List<FloatingPopup>();
        Transform _popupContainer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ResolveReferences();
            InitializePipes();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            ResolveReferences();
        }

        void Update()
        {
            if (Time.timeScale <= 0f)
                return;

            UpdateProduction();
            UpdateBubbleAnimations();
            HandleHarvestInput();
            UpdatePopups();
        }

        public void ResolveReferences()
        {
            if (alchemy == null)
                alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();

            if (arenaCamera == null)
                arenaCamera = Camera.main;

            if (_popupContainer == null)
            {
                Transform existing = transform.Find("Harvest_Popups_Container");
                if (existing != null)
                {
                    _popupContainer = existing;
                }
                else
                {
                    GameObject go = new GameObject("Harvest_Popups_Container");
                    go.transform.SetParent(transform, false);
                    _popupContainer = go.transform;
                }
            }
        }

        void InitializePipes()
        {
            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null)
                    continue;

                pipe.animPhaseOffset = i * 1.57f; // phase offset berbeda agar tidak bergerak sinkron kaku

                if (pipe.bubbleTransform != null)
                    pipe.initialLocalPos = pipe.bubbleTransform.localPosition;

                // Terapkan warna bahan ke icon
                if (pipe.iconRenderer != null)
                    pipe.iconRenderer.color = AlchemyDatabase.IngredientColor(pipe.ingredientType);

                // Pastikan sorting order benar
                if (pipe.backgroundRenderer != null)
                    pipe.backgroundRenderer.sortingOrder = bubbleSortingOrder;

                if (pipe.iconRenderer != null)
                    pipe.iconRenderer.sortingOrder = bubbleSortingOrder + 1;

                // Nonaktifkan gelembung di awal jika belum siap
                if (pipe.bubbleRoot != null)
                    pipe.bubbleRoot.SetActive(pipe.isBubbleActive);
            }
        }

        // ------------------------------------------------------------------
        // Produksi Otomatis
        // ------------------------------------------------------------------

        void UpdateProduction()
        {
            if (alchemy == null)
                return;

            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null)
                    continue;

                // Jika gelembung sudah muncul, jangan menambah timer (tunggu dipanen)
                if (pipe.isBubbleActive)
                    continue;

                // Cek stok bahan di AlchemyManager: jika sudah mencapai batas maksimal (6), tahan produksi
                int currentStock = GetStock(pipe.ingredientType);
                if (currentStock >= maxStockPerIngredient)
                    continue;

                pipe.currentTimer += Time.deltaTime;

                if (pipe.currentTimer >= productionInterval)
                {
                    ActivateBubble(pipe);
                }
            }
        }

        int GetStock(IngredientType type)
        {
            if (alchemy == null || alchemy.ingredientStock == null)
                return 0;

            int idx = (int)type;
            if (idx >= 0 && idx < alchemy.ingredientStock.Length)
                return alchemy.ingredientStock[idx];

            return 0;
        }

        void ActivateBubble(HarvestPipe pipe)
        {
            pipe.isBubbleActive = true;
            pipe.currentTimer = 0f;

            if (pipe.bubbleRoot != null)
            {
                pipe.bubbleRoot.SetActive(true);

                // Efek spawn pop (skala kecil lalu membesar)
                if (pipe.bubbleTransform != null)
                    pipe.bubbleTransform.localScale = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------
        // Animasi Bobbing & Pulse
        // ------------------------------------------------------------------

        void UpdateBubbleAnimations()
        {
            float time = Time.time;

            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null || !pipe.isBubbleActive || pipe.bubbleTransform == null)
                    continue;

                // Bobbing vertikal halus
                float bobOffset = Mathf.Sin((time + pipe.animPhaseOffset) * bobbingSpeed) * bobbingAmplitude;
                pipe.bubbleTransform.localPosition = pipe.initialLocalPos + new Vector3(0f, bobOffset, 0f);

                // Denyut skala halus
                float pulse = 1f + Mathf.Sin((time + pipe.animPhaseOffset) * pulseSpeed) * pulseAmplitude;
                pipe.bubbleTransform.localScale = Vector3.one * pulse;
            }
        }

        // ------------------------------------------------------------------
        // Deteksi Kursor & Interaksi Klik
        // ------------------------------------------------------------------

        /// <summary>
        /// Mengecek apakah kursor mouse saat ini sedang mengarah ke salah satu gelembung panen aktif.
        /// Dipanggil oleh ArenaThrowTargeting untuk mencegah lemparan ramuan saat memanen.
        /// </summary>
        public static bool IsPointerOverBubble()
        {
            if (Instance == null)
                return false;

            return Instance.CheckPointerOverAnyBubble();
        }

        public bool CheckPointerOverAnyBubble()
        {
            Vector2 mouseWorld = GetMouseWorldPosition();

            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null || !pipe.isBubbleActive)
                    continue;

                if (IsPointInsidePipeBubble(pipe, mouseWorld))
                    return true;
            }

            return false;
        }

        bool IsPointInsidePipeBubble(HarvestPipe pipe, Vector2 worldPos)
        {
            if (pipe.bubbleCollider != null && pipe.bubbleCollider.enabled && pipe.bubbleRoot != null && pipe.bubbleRoot.activeInHierarchy)
            {
                return pipe.bubbleCollider.OverlapPoint(worldPos);
            }

            // Fallback jarak radius jika collider belum terpasang
            if (pipe.bubbleTransform != null && pipe.bubbleRoot != null && pipe.bubbleRoot.activeInHierarchy)
            {
                float dist = Vector2.Distance(worldPos, pipe.bubbleTransform.position);
                return dist <= 0.55f;
            }

            return false;
        }

        Vector2 GetMouseWorldPosition()
        {
            Camera cam = arenaCamera != null ? arenaCamera : Camera.main;
            if (cam == null)
                return Vector2.zero;

            Vector3 screenPos = Input.mousePosition;
            screenPos.z = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(screenPos);
            return new Vector2(world.x, world.y);
        }

        void HandleHarvestInput()
        {
            if (!Input.GetMouseButtonDown(0))
                return;

            Vector2 mouseWorld = GetMouseWorldPosition();

            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null || !pipe.isBubbleActive)
                    continue;

                if (IsPointInsidePipeBubble(pipe, mouseWorld))
                {
                    Harvest(pipe);
                    break;
                }
            }
        }

        public void Harvest(HarvestPipe pipe)
        {
            if (pipe == null || !pipe.isBubbleActive)
                return;

            // Tambahkan stok bahan ke AlchemyManager
            if (alchemy != null)
            {
                bool success = alchemy.HarvestIngredient(pipe.ingredientType, 1);
                if (!success)
                {
                    // Stok sudah penuh (mencapai batas 6)
                    SpawnFloatingFeedback(pipe, "Stok Penuh!", Color.yellow);
                    return;
                }
            }

            // Sembunyikan gelembung & reset timer
            pipe.isBubbleActive = false;
            pipe.currentTimer = 0f;

            if (pipe.bubbleRoot != null)
                pipe.bubbleRoot.SetActive(false);

            // Munculkan feedback visual +1
            string shortName = GetShortIngredientName(pipe.ingredientType);
            Color textCol = AlchemyDatabase.IngredientColor(pipe.ingredientType);

            SpawnFloatingFeedback(pipe, "+1 " + shortName, textCol);
        }

        string GetShortIngredientName(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.BahanBasa_Biru: return "Basa";
                case IngredientType.BahanPendingin_Cyan: return "Pendingin";
                case IngredientType.BahanAsam_Merah: return "Asam";
                case IngredientType.BahanKatalis_Kuning: return "Katalis";
                default: return "Bahan";
            }
        }

        // ------------------------------------------------------------------
        // Visual Feedback (+1 Popup)
        // ------------------------------------------------------------------

        void SpawnFloatingFeedback(HarvestPipe pipe, string text, Color color)
        {
            Vector3 spawnPos = pipe.bubbleTransform != null 
                ? pipe.bubbleTransform.position 
                : (pipe.pipeAnchor != null ? pipe.pipeAnchor.position + Vector3.up * 0.7f : transform.position);

            GameObject go = new GameObject("Harvest_Popup_" + text);
            go.transform.SetParent(_popupContainer, false);
            go.transform.position = spawnPos;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 28;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;

            Font safeFont = ZombieIntroSpotlight.GetSafeFont();
            if (safeFont != null)
            {
                tm.font = safeFont;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.material = safeFont.material;
                    mr.sortingOrder = bubbleSortingOrder + 50; // di atas gelembung
                }
            }

            _activePopups.Add(new FloatingPopup
            {
                root = go,
                textMesh = tm,
                startPos = spawnPos,
                age = 0f,
                duration = Mathf.Max(0.2f, popupDuration),
                baseColor = color
            });
        }

        void UpdatePopups()
        {
            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                FloatingPopup pop = _activePopups[i];
                if (pop.root == null)
                {
                    _activePopups.RemoveAt(i);
                    continue;
                }

                pop.age += Time.deltaTime;
                float t = Mathf.Clamp01(pop.age / pop.duration);

                // Bergerak meluncur ke atas
                pop.root.transform.position = pop.startPos + new Vector3(0f, t * popupRiseSpeed, 0f);

                // Fade-out alpha
                if (pop.textMesh != null)
                {
                    Color c = pop.baseColor;
                    c.a = Mathf.Lerp(1f, 0f, t * t);
                    pop.textMesh.color = c;
                }

                if (t >= 1f)
                {
                    Destroy(pop.root);
                    _activePopups.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        // Scene View Gizmos (Mempermudah developer menggeser titik pipa)
        // ------------------------------------------------------------------

        void OnDrawGizmos()
        {
            if (pipes == null)
                return;

            for (int i = 0; i < pipes.Count; i++)
            {
                HarvestPipe pipe = pipes[i];
                if (pipe == null || pipe.pipeAnchor == null)
                    continue;

                Color col = AlchemyDatabase.IngredientColor(pipe.ingredientType);
                Gizmos.color = col;

                Vector3 anchorPos = pipe.pipeAnchor.position;

                // Titik Pipa
                Gizmos.DrawWireSphere(anchorPos, 0.22f);

                // Titik Gelembung
                Vector3 bubblePos = pipe.bubbleTransform != null 
                    ? pipe.bubbleTransform.position 
                    : anchorPos + Vector3.up * 0.65f;

                Gizmos.DrawLine(anchorPos, bubblePos);
                Gizmos.DrawWireSphere(bubblePos, 0.40f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    anchorPos + Vector3.down * 0.25f,
                    pipe.pipeName + " (" + GetShortIngredientName(pipe.ingredientType) + ")"
                );
#endif
            }
        }
    }
}
