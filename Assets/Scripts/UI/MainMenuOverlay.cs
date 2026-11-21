using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Layar Menu Utama yang muncul saat game dimulai:
    /// Menampilkan judul Victorian-Steampunk, tombol PLAY, TUTORIAL/INFO, PENGATURAN, dan KREDIT.
    /// </summary>
    public class MainMenuOverlay : MonoBehaviour
    {
        [Header("Root")]
        public GameObject menuRoot;

        [Header("Tombol")]
        public Button playButton;
        public Button tutorialButton;
        public Button settingsButton;
        public Button creditsButton;

        [Header("Manajer UI")]
        public UIManager uiManager;

        void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (tutorialButton != null)
                tutorialButton.onClick.AddListener(OnTutorialClicked);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettingsClicked);

            if (creditsButton != null)
                creditsButton.onClick.AddListener(OnCreditsClicked);
        }

        public void Show()
        {
            if (menuRoot != null)
                menuRoot.SetActive(true);
            else
                gameObject.SetActive(true);

            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);
            else
                gameObject.SetActive(false);

            Time.timeScale = 1f;
        }

        void OnPlayClicked()
        {
            Hide();
            if (uiManager != null)
                uiManager.OnGameStarted();
        }

        void OnTutorialClicked()
        {
            if (uiManager != null)
                uiManager.ShowRecipeBook();
        }

        void OnSettingsClicked()
        {
            if (uiManager != null)
                uiManager.ShowSettings();
        }

        void OnCreditsClicked()
        {
            if (uiManager != null)
                uiManager.ShowCredits();
        }
    }
}
