using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Popup Buku Resep & Quick Info ("i"):
    /// Menyajikan konsep kimia (pH, netralisasi, termokimia, katalis)
    /// serta 6 resep ramuan, fungsi ke zombie, dan risiko mutasinya.
    /// </summary>
    public class RecipeBookPopup : MonoBehaviour
    {
        [Header("Komponen")]
        public GameObject panelRoot;
        public Button closeButton;
        public Button tabChemistryButton;
        public Button tabRecipesButton;

        public GameObject chemistryPage;
        public GameObject recipesPage;

        [Header("Teks Halaman")]
        public Text chemistryTitleText;
        public Text chemistryBodyText;
        public Text recipesTitleText;
        public Text recipesBodyText;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (tabChemistryButton != null)
                tabChemistryButton.onClick.AddListener(ShowChemistryTab);

            if (tabRecipesButton != null)
                tabRecipesButton.onClick.AddListener(ShowRecipesTab);

            PopulateDefaultContent();
        }

        public void Open()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            ShowChemistryTab();
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        public void ShowChemistryTab()
        {
            if (chemistryPage != null) chemistryPage.SetActive(true);
            if (recipesPage != null) recipesPage.SetActive(false);

            HighlightTab(tabChemistryButton, true);
            HighlightTab(tabRecipesButton, false);
        }

        public void ShowRecipesTab()
        {
            if (chemistryPage != null) chemistryPage.SetActive(false);
            if (recipesPage != null) recipesPage.SetActive(true);

            HighlightTab(tabChemistryButton, false);
            HighlightTab(tabRecipesButton, true);
        }

        void HighlightTab(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? UITheme.BrassBright : UITheme.BrassDark;
        }

        void PopulateDefaultContent()
        {
            if (chemistryBodyText != null)
            {
                chemistryBodyText.text =
                    "<b>1. SKALA PH BALANCE (0 - 14):</b>\n" +
                    "• <color=#E53935>Asam (pH < 7):</color> Bahan Asam Merah menurunkan pH sebesar -4.5.\n" +
                    "• <color=#43A047>Netral (pH = 7):</color> Keseimbangan sempurna untuk reaksi netralisasi & kristal es.\n" +
                    "• <color=#1E88E5>Basa (pH > 7):</color> Bahan Basa Biru menaikkan pH sebesar +4.5.\n" +
                    "• Bahan Pendingin (Cyan) & Katalis (Kuning) bersifat netral (pH modifier = 0).\n\n" +
                    "<b>2. REAKSI NETRALISASI:</b>\n" +
                    "Penyatuan Asam + Basa menghasilkan reaksi eksotermik hebat yang menghancurkan struktur " +
                    "Unstable Amalgam (Boss) hingga pengali damage 4x lipat!\n\n" +
                    "<b>3. HUKUM TERMOKIMIA:</b>\n" +
                    "Bahan Pendingin menyerap panas kinetik zombie, memperlambat kecepatan langkah dan membekukan " +
                    "musuh di tempat (Root).\n\n" +
                    "<b>4. KATALISATOR REAKSI:</b>\n" +
                    "Katalis mempercepat laju reaksi, memperluas radius ledakan (AoE), dan memicu hentakan kinetik mundur (Knockback).";
            }

            if (recipesBodyText != null)
            {
                recipesBodyText.text =
                    "<b>1. Gel Sabun Beku</b> [<color=#1E88E5>Basa</color> + <color=#00ACC1>Pendingin</color> | pH 11-12]\n" +
                    "• Efek: Root 3s + Slow 45%. Sangat ampuh menghentikan laju musuh cepat.\n\n" +
                    "<b>2. Kabut Asam Dingin</b> [<color=#E53935>Asam</color> + <color=#00ACC1>Pendingin</color> | pH 2-3]\n" +
                    "• Efek: Kerusakan Area (AoE) + Slow. Efektif melibas kawanan Zombie Basa.\n\n" +
                    "<b>3. Ledakan Es Instan</b> [<color=#00ACC1>Pendingin</color> + <color=#FBC02D>Katalis</color> | pH 6.5-7.5]\n" +
                    "• Efek: Kerusakan besar (30 DMG) + Root singkat 2 detik.\n\n" +
                    "<b>4. Asam Pelarut Karat</b> [<color=#E53935>Asam</color> + <color=#FBC02D>Katalis</color> | pH 2-3]\n" +
                    "• Efek: Asam pekat korosif! Wajib dipakai untuk menembus zirah <b>Zombie Besi</b>.\n\n" +
                    "<b>5. Bom Netralisasi</b> [<color=#E53935>Asam</color> + <color=#1E88E5>Basa</color> | pH 6.5-7.5]\n" +
                    "• Efek: DMG 26 + Knockback. Multiplier 4x lipat untuk menghabisi Boss Wave 5!\n\n" +
                    "<b>6. Busa Sabun Mengembang</b> [<color=#1E88E5>Basa</color> + <color=#FBC02D>Katalis</color> | pH 11-12]\n" +
                    "• Efek: Knockback kuat + Slow 30%. Menetralkan Zombie Asam yang agresif.\n\n" +
                    "<i><color=#C8B688>Peringatan Mutasi: Pada gelombang tinggi, musuh yang tidak segera ditangani dapat membesar (Enlarge), mempercepat diri (SpeedUp), atau mengeras (Harden)!</color></i>";
            }
        }
    }
}
