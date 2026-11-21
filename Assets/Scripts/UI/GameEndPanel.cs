using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Panel Akhir Permainan (Victory & Game Over):
    /// Muncul saat pemain memenangkan permainan (seluruh wave tuntas)
    /// atau saat HP Menara habis (Game Over).
    /// </summary>
    public class GameEndPanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;

        [Header("Teks")]
        public Text headerTitleText;
        public Text subtitleText;
        public Text resultDetailText;

        [Header("Tombol")]
        public Button restartButton;

        void Awake()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(RestartGame);
        }

        public void ShowVictory()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            if (headerTitleText != null)
            {
                headerTitleText.text = "KEMENANGAN ALKIMIA!";
                headerTitleText.color = UITheme.SelectionGlow;
            }

            if (subtitleText != null)
            {
                subtitleText.text = "Menara Marie Curie Berhasil Dipertahankan!";
                subtitleText.color = UITheme.PHNeutral;
            }

            if (resultDetailText != null)
            {
                resultDetailText.text =
                    "Eksperimen Unstable Amalgam berhasil dinetralisir sempurna!\n" +
                    "Formula kesetimbangan kimia Marie Curie telah menyelamatkan kota dari malapetaka.";
            }

            Time.timeScale = 0f;
        }

        public void ShowGameOver()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            if (headerTitleText != null)
            {
                headerTitleText.text = "MENARA RUNTUH!";
                headerTitleText.color = UITheme.PHAcidRed;
            }

            if (subtitleText != null)
            {
                subtitleText.text = "Reaksi Kimia Telah Menembus Pertahanan...";
                subtitleText.color = UITheme.BrassBright;
            }

            if (resultDetailText != null)
            {
                resultDetailText.text =
                    "Mutan berhasil mencapai kuali laboratorium utama!\n" +
                    "Evaluasi kembali taktik pH dan racik ramuan penahan sebelum gelombang mendekat.";
            }

            Time.timeScale = 0f;
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
