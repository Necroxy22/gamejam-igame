#pragma warning disable CS0618

using CuriesCrucible.Alchemy;
using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Popup Laboratorium Alkimia 2-Kolom:
    /// - Kolom Kiri: Buku Resep & Penjelasan Sains Kimia (pH, Netralisasi, Termokimia, Katalis, 6 Ramuan & Mutasi)
    /// - Kolom Kanan: Kuali Alkimia (2 Slot bahan aktif), Meteran pH Balance horizontal (Jarum Lerp),
    ///                Tombol BREW (R) & CLEAR (F).
    /// </summary>
    public class AlchemyCraftingPopup : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;
        public Button closeButton;

        [Header("Referensi Sistem")]
        public AlchemyManager alchemy;

        [Header("Kolom Kanan: Meteran pH Balance")]
        public RectTransform phBarRect;
        public RectTransform phNeedleRect;
        public Text phValueText;
        public float needleLerpSpeed = 8f;
        public float phMinX = -135f;
        public float phMaxX = 135f;

        [Header("Kolom Kanan: Kuali (Cauldron)")]
        public Image cauldronSlot1Image;
        public Text cauldronSlot1Text;
        public Image cauldronSlot2Image;
        public Text cauldronSlot2Text;
        public Button brewButton;
        public Image brewButtonImage;
        public Text brewButtonText;
        public Button clearButton;

        [Header("Kolom Kiri: Panduan & Resep")]
        public Text recipeGuideText;

        float _displayedPH = 7f;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (brewButton != null)
                brewButton.onClick.AddListener(OnBrewClicked);

            if (clearButton != null)
                clearButton.onClick.AddListener(OnClearClicked);

            PopulateRecipeGuide();
        }

        void Start()
        {
            if (alchemy == null)
                alchemy = AlchemyManager.Instance.Ensure(() => FindObjectOfType<AlchemyManager>());

            RefreshCauldronUI();
        }

        void Update()
        {
            if (panelRoot != null && !panelRoot.activeSelf)
                return;

            UpdatePHMeter();

            // Shortcut keyboard saat popup terbuka
            if (Input.GetKeyDown(KeyCode.R))
                OnBrewClicked();

            if (Input.GetKeyDown(KeyCode.F))
                OnClearClicked();

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.C))
                Close();
        }

        public void Open()
        {
            if (alchemy == null)
                alchemy = AlchemyManager.Instance.Ensure(() => FindObjectOfType<AlchemyManager>());

            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            RefreshCauldronUI();
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        public void Toggle()
        {
            bool isOpen = panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;
            if (isOpen)
                Close();
            else
                Open();
        }

        public bool IsOpen
        {
            get { return panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf; }
        }

        void OnBrewClicked()
        {
            if (alchemy != null)
            {
                alchemy.Brew();
                RefreshCauldronUI();
            }
        }

        void OnClearClicked()
        {
            if (alchemy != null)
            {
                alchemy.ClearCauldron(true);
                RefreshCauldronUI();
            }
        }

        public void RefreshCauldronUI()
        {
            if (alchemy == null)
                return;

            UpdateCauldronSlot(cauldronSlot1Image, cauldronSlot1Text, alchemy.slot1Filled, alchemy.slot1, 1);
            UpdateCauldronSlot(cauldronSlot2Image, cauldronSlot2Text, alchemy.slot2Filled, alchemy.slot2, 2);

            bool canBrew = alchemy.slot1Filled && alchemy.slot2Filled;
            if (brewButton != null)
                brewButton.interactable = canBrew;

            if (brewButtonImage != null)
                brewButtonImage.color = canBrew ? UITheme.BrewButtonNormal : UITheme.BrewButtonDisabled;

            if (brewButtonText != null)
                brewButtonText.color = canBrew ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);

            if (clearButton != null)
                clearButton.interactable = alchemy.slot1Filled || alchemy.slot2Filled;
        }

        void UpdateCauldronSlot(Image img, Text txt, bool filled, IngredientType type, int slotNum)
        {
            if (img != null)
            {
                img.color = filled ? AlchemyDatabase.IngredientColor(type) : new Color(0.16f, 0.12f, 0.08f, 0.85f);
            }

            if (txt != null)
            {
                if (filled)
                {
                    txt.text = AlchemyDatabase.IngredientName(type);
                    txt.color = Color.white;
                }
                else
                {
                    txt.text = "Slot " + slotNum + "\n[Kosong]";
                    txt.color = new Color(0.7f, 0.6f, 0.45f, 0.7f);
                }
            }
        }

        void UpdatePHMeter()
        {
            float targetPH = alchemy != null ? alchemy.CurrentPH : 7f;
            _displayedPH = Mathf.Lerp(_displayedPH, targetPH, Time.deltaTime * needleLerpSpeed);

            if (phNeedleRect != null)
            {
                float t = Mathf.Clamp01(_displayedPH / 14f);
                float posX = Mathf.Lerp(phMinX, phMaxX, t);
                Vector2 pos = phNeedleRect.anchoredPosition;
                pos.x = posX;
                phNeedleRect.anchoredPosition = pos;
            }

            if (phValueText != null)
            {
                string state;
                Color col;
                if (_displayedPH < 6.4f)
                {
                    state = "ASAM";
                    col = UITheme.PHAcidRed;
                }
                else if (_displayedPH > 7.6f)
                {
                    state = "BASA";
                    col = UITheme.PHBaseBlue;
                }
                else
                {
                    state = "NETRAL";
                    col = UITheme.PHNeutral;
                }

                phValueText.text = "pH: " + _displayedPH.ToString("0.0") + "  [" + state + "]";
                phValueText.color = col;
            }
        }

        void PopulateRecipeGuide()
        {
            if (recipeGuideText != null)
            {
                recipeGuideText.text =
                    "<b>RESEP ALKIMIA UTAMA:</b>\n" +
                    "1. <b>Gel Sabun Beku:</b> <color=#1E88E5>Basa</color> + <color=#00ACC1>Pendingin</color> (pH 11-12)\n" +
                    "   • Efek: <i>Root 3 detik + Slow 45%</i>\n\n" +
                    "2. <b>Kabut Asam Dingin:</b> <color=#E53935>Asam</color> + <color=#00ACC1>Pendingin</color> (pH 2-3)\n" +
                    "   • Efek: <i>AoE Damage + Slow, Kuat vs Basa</i>\n\n" +
                    "3. <b>Ledakan Es Instan:</b> <color=#00ACC1>Pendingin</color> + <color=#FBC02D>Katalis</color> (pH 6.5-7.5)\n" +
                    "   • Efek: <i>Kerusakan Besar (30 DMG) + Root 2s</i>\n\n" +
                    "4. <b>Asam Pelarut Karat:</b> <color=#E53935>Asam</color> + <color=#FBC02D>Katalis</color> (pH 2-3)\n" +
                    "   • Efek: <i>Korosif Kuat! Wajib vs Zombie Besi</i>\n\n" +
                    "5. <b>Bom Netralisasi:</b> <color=#E53935>Asam</color> + <color=#1E88E5>Basa</color> (pH 6.5-7.5)\n" +
                    "   • Efek: <i>DMG 26 (x4 vs Boss Unstable Amalgam!)</i>\n\n" +
                    "6. <b>Busa Sabun Mengembang:</b> <color=#1E88E5>Basa</color> + <color=#FBC02D>Katalis</color> (pH 11-12)\n" +
                    "   • Efek: <i>Knockback Kuat + Slow vs Zombie Asam</i>\n\n" +
                    "<b>PRINSIP REAKSI & MUTASI:</b>\n" +
                    "• Asam (-4.5), Basa (+4.5), Pendingin/Katalis (0).\n" +
                    "• Netralisasi menghasilkan hentakan eksotermik besar.\n" +
                    "• Waspadai mutasi zombie: Enlarge, SpeedUp, Harden!";
            }
        }
    }
}
