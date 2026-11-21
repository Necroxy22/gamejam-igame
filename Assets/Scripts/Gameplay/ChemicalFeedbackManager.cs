using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// Floating Chemical Feedback: teks melayang bersih (TextMesh) dengan tipografi klasik
    /// Victorian/Steampunk (Bold + Rich Text) + outline hitam tebal, di atas kepala zombie.
    ///
    ///   - Font: serif klasik (classicFont di Inspector / Resources/Fonts / OS font) -> fallback LegacyRuntime.ttf (aman WebGL).
    ///   - Rich Text: <b>...</b> + <size=58>...</size> untuk angka damage lebih besar.
    ///   - Outline hitam: shadow berlapis (salinan hitam lebih besar di belakang).
    ///   - Animasi pop halus 0.85 -> 1.05 -> 1.0, melayang naik, fade-out. TANPA coroutine (aman WebGL).
    /// </summary>
    [DisallowMultipleComponent]
    public class ChemicalFeedbackManager : MonoBehaviour
    {
        // Fase animasi (detik).
        const float PopInDuration = 0.15f;
        const float SettleDuration = 0.30f;
        const float FadeDuration = 0.40f;

        const int MainSortingOrder = 700;
        const int OutlineSortingOrder = 699;
        const float OutlineScale = 1.18f;   // shadow hitam berlapis (outline tebal)

        [Header("Font Klasik (opsional)")]
        [Tooltip("Font serif klasik (mis. Cinzel / MedievalSharp / Georgia). Kosong = auto.")]
        public Font classicFont;

        [Header("Tampilan")]
        [Tooltip("Durasi melayang + memudar (detik).")]
        public float popupDuration = 1.0f;

        [Tooltip("Kecepatan melayang naik (world unit/detik).")]
        public float riseSpeed = 0.8f;

        [Tooltip("Variasi posisi X acak (world unit) agar angka tidak menumpuk.")]
        public float jitterX = 0.22f;

        // Ukuran tipografi (sesuai spesifikasi Victorian).
        const int FontSize = 52;
        const float CharacterSize = 0.042f;

        // ------------------------------------------------------------------

        class Popup
        {
            public GameObject root;
            public Transform visual;   // di-scale untuk animasi pop
            public TextMesh main;
            public TextMesh outline;
            public Vector3 startPos;
            public float age;
            public float duration;
            public float rise;
            public Color baseColor;
        }

        static ChemicalFeedbackManager _instance;

        readonly List<Popup> _popups = new List<Popup>(16);
        Transform _container;
        Font _font;

        /// <summary>Instance singleton (mencari yang ada di scene, atau membuat baru).</summary>
        public static ChemicalFeedbackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ChemicalFeedbackManager>();

                    if (_instance == null)
                    {
                        GameObject go = new GameObject("ChemicalFeedbackManager");
                        _instance = go.AddComponent<ChemicalFeedbackManager>();
                    }
                }

                return _instance;
            }
        }

        void Awake()
        {
            _instance = this;
            EnsureInit();
        }

        /// <summary>Inisialisasi lazy (container + font) yang aman dipanggil dari mana saja.</summary>
        void EnsureInit()
        {
            if (_instance == null)
                _instance = this;

            if (_container == null)
            {
                _container = new GameObject("ChemicalFeedbackTexts").transform;
                _container.SetParent(transform, false);
            }

            if (_font == null)
                _font = ResolveFont();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Tampilkan teks feedback (Rich Text) di atas zombie. Melayang naik + memudar 1.0 detik.
        /// Posisi X diberi jitter acak agar tidak menumpuk.
        /// </summary>
        public static void Spawn(Vector3 worldPosition, string richText, Color color)
        {
            Instance.SpawnInternal(worldPosition, richText, color);
        }

        void SpawnInternal(Vector3 worldPosition, string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            EnsureInit();

            Vector3 pos = worldPosition;
            pos.x += Random.Range(-Mathf.Abs(jitterX), Mathf.Abs(jitterX));

            var root = new GameObject("ChemFeedback");
            root.transform.SetParent(_container, false);
            root.transform.position = pos;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.85f;

            // Outline hitam tebal (shadow berlapis) di belakang, lalu teks utama berwarna.
            TextMesh outline = CreateText(visual.transform, "Outline", text, Color.black, OutlineSortingOrder, OutlineScale);
            TextMesh main = CreateText(visual.transform, "Main", text, color, MainSortingOrder, 1f);

            _popups.Add(new Popup
            {
                root = root,
                visual = visual.transform,
                main = main,
                outline = outline,
                startPos = pos,
                age = 0f,
                duration = Mathf.Max(0.4f, popupDuration),
                rise = Mathf.Max(0.01f, riseSpeed),
                baseColor = color
            });
        }

        TextMesh CreateText(Transform parent, string objectName, string text, Color color, int sortingOrder, float localScale)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * localScale;

            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontStyle = FontStyle.Bold;
            tm.richText = true;
            tm.fontSize = FontSize;
            tm.characterSize = CharacterSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;

            Font font = _font != null ? _font : ResolveFont();
            if (font != null)
            {
                tm.font = font;

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = font.material;
                    mr.sortingOrder = sortingOrder;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }

            return tm;
        }

        /// <summary>
        /// Resolusi font klasik ber-serif:
        ///   1) classicFont (assign di Inspector) -> 2) Resources/Fonts -> 3) OS font (non-WebGL)
        ///   -> 4) LegacyRuntime.ttf (fallback aman WebGL).
        /// </summary>
        Font ResolveFont()
        {
            if (classicFont != null)
                return classicFont;

            string[] resourcePaths =
            {
                "Fonts/Cinzel", "Fonts/MedievalSharp", "Fonts/Georgia",
                "Cinzel", "MedievalSharp", "Georgia"
            };

            for (int i = 0; i < resourcePaths.Length; i++)
            {
                Font f = Resources.Load<Font>(resourcePaths[i]);
                if (f != null)
                    return f;
            }

            // OS font hanya bermakna di standalone/editor (di WebGL tidak tersedia).
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Font os = null;
                try
                {
                    os = Font.CreateDynamicFontFromOSFont(
                        new[] { "Georgia", "Palatino Linotype", "Times New Roman", "Liberation Serif" },
                        FontSize);
                }
                catch { }

                if (os != null)
                    return os;
            }

            // Fallback terakhir: builtin (aman 100% WebGL).
            return ZombieIntroSpotlight.GetSafeFont();
        }

        // ------------------------------------------------------------------
        // Update (tanpa coroutine — aman WebGL)
        // ------------------------------------------------------------------

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                Popup popup = _popups[i];

                if (popup.root == null)
                {
                    _popups.RemoveAt(i);
                    continue;
                }

                popup.age += dt;
                float age = popup.age;
                float duration = popup.duration;

                // Pop halus 0.85 -> 1.05 -> 1.0.
                float anim = ComputePopScale(age);
                if (popup.visual != null)
                    popup.visual.localScale = new Vector3(anim, anim, 1f);

                // Melayang naik lembut (mulai setelah fase settle).
                float floatT = Mathf.Clamp01((age - SettleDuration) / Mathf.Max(0.0001f, duration - SettleDuration));
                popup.root.transform.position = popup.startPos + new Vector3(0f, floatT * popup.rise, 0f);

                // Fade-out.
                float alpha = 1f;
                float fadeStart = duration - FadeDuration;
                if (age > fadeStart)
                    alpha = 1f - Mathf.Clamp01((age - fadeStart) / FadeDuration);

                if (popup.main != null)
                {
                    Color c = popup.baseColor;
                    c.a = alpha;
                    popup.main.color = c;
                }

                if (popup.outline != null)
                {
                    Color o = Color.black;
                    o.a = alpha;
                    popup.outline.color = o;
                }

                if (age >= duration)
                {
                    Destroy(popup.root);
                    _popups.RemoveAt(i);
                }
            }
        }

        static float ComputePopScale(float age)
        {
            if (age < PopInDuration)
            {
                float t = age / PopInDuration;
                float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                return Mathf.Lerp(0.85f, 1.05f, e);
            }

            if (age < SettleDuration)
            {
                float t = (age - PopInDuration) / (SettleDuration - PopInDuration);
                float e = t * t * (3f - 2f * t);      // smoothstep
                return Mathf.Lerp(1.05f, 1.0f, e);
            }

            return 1.0f;
        }
    }
}