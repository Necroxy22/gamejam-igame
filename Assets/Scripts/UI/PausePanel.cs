using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Panel Pause Permainan:
    /// Menampilkan status jeda, tombol Lanjutkan (Resume), Pengaturan, dan Mulai Ulang (Restart).
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        [Header("Root")]
        public GameObject panelRoot;

        [Header("Tombol")]
        public Button resumeButton;
        public Button settingsButton;
        public Button restartButton;

        public UIManager uiManager;

        void Awake()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettings);

            if (restartButton != null)
                restartButton.onClick.AddListener(RestartGame);
        }

        public void Show()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        public void Resume()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
            else
                gameObject.SetActive(false);

            Time.timeScale = 1f;
        }

        void OpenSettings()
        {
            if (uiManager != null)
                uiManager.ShowSettings();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
