using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Popup Kredit Permainan:
    /// Menampilkan apresiasi kepada Marie Skłodowska-Curie dan sains kimia.
    /// </summary>
    public class CreditsPopup : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;

        [Header("Teks")]
        public Text titleText;
        public Text bodyText;

        [Header("Tombol")]
        public Button closeButton;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (bodyText != null)
            {
                bodyText.text =
                    "<b>CURIE'S CRUCIBLE: EQUILIBRIUM</b>\n" +
                    "Sebuah Permainan Tower Defense Alkimia 2.5D Victorian-Steampunk.\n\n" +
                    "<b>Dedikasi:</b>\n" +
                    "Terinspirasi oleh kehidupan dan dedikasi ilmiah tanpa henti dari " +
                    "<b>Marie Skłodowska-Curie (1867–1934)</b>, perintis riset radioaktivitas " +
                    "dan pemenang Penghargaan Nobel Fisika (1903) serta Kimia (1911).\n\n" +
                    "<b>Konsep Ilmiah:</b>\n" +
                    "Kesetimbangan Asam-Basa (pH), Reaksi Netralisasi Eksotermik, " +
                    "Kinetika Kimia & Katalis.\n\n" +
                    "<i>\"Nothing in life is to be feared, it is only to be understood. " +
                    "Now is the time to understand more, so that we may fear less.\"</i>";
            }
        }

        public void Open()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
    }
}
