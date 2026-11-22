#pragma warning disable CS0618

using CuriesCrucible.Gameplay;
using UnityEngine;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Koordinator Pusat UI (Master Manager):
    /// Menghubungkan seluruh sistem HUD, Menu Utama, Popup Resep, Crafting Popup,
    /// Pause, Pengaturan, Kredit, serta mendeteksi kondisi Victory & Game Over.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Komponen Panel UI")]
        public GameplayHUD gameplayHUD;
        public AlchemyCraftingPopup craftingPopup;
        public MainMenuOverlay mainMenuOverlay;
        public RecipeBookPopup recipeBookPopup;
        public PausePanel pausePanel;
        public GameEndPanel gameEndPanel;
        public SettingsPopup settingsPopup;
        public CreditsPopup creditsPopup;

        [Header("Referensi Sistem")]
        public TowerHealthManager towerHealth;
        public WaveManager waveManager;

        [Header("Pengaturan Flow Awal")]
        public bool showMainMenuOnStart = true;

        bool _isGameEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            ResolveReferences();

            if (towerHealth != null)
                towerHealth.OnGameOver += HandleTowerDestroyed;

            if (showMainMenuOnStart && mainMenuOverlay != null)
            {
                mainMenuOverlay.Show();
            }
            else
            {
                if (mainMenuOverlay != null)
                    mainMenuOverlay.Hide();
            }
        }

        void OnDestroy()
        {
            if (towerHealth != null)
                towerHealth.OnGameOver -= HandleTowerDestroyed;

            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            CheckEndConditions();
            HandleHotkeys();
        }

        void ResolveReferences()
        {
            if (towerHealth == null) towerHealth = TowerHealthManager.Instance.Ensure(() => FindObjectOfType<TowerHealthManager>());
            if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
            if (craftingPopup == null) craftingPopup = FindObjectOfType<AlchemyCraftingPopup>();
            if (gameplayHUD == null) gameplayHUD = FindObjectOfType<GameplayHUD>();
        }

        void HandleHotkeys()
        {
            if (_isGameEnded)
                return;

            // Tombol Escape atau P untuk Pause
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                // Tutup popup terlebih dahulu jika ada yang terbuka
                if (craftingPopup != null && craftingPopup.IsOpen)
                {
                    craftingPopup.Close();
                    return;
                }
                if (recipeBookPopup != null && recipeBookPopup.gameObject.activeSelf)
                {
                    recipeBookPopup.Close();
                    return;
                }
                if (settingsPopup != null && settingsPopup.gameObject.activeSelf)
                {
                    settingsPopup.Close();
                    return;
                }
                if (creditsPopup != null && creditsPopup.gameObject.activeSelf)
                {
                    creditsPopup.Close();
                    return;
                }

                TogglePause();
            }

            // Tombol I atau F2 untuk Buku Resep
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.F2))
            {
                ToggleRecipeBook();
            }
        }

        void CheckEndConditions()
        {
            if (_isGameEnded)
                return;

            if (waveManager != null && waveManager.IsGameComplete)
            {
                _isGameEnded = true;
                if (gameEndPanel != null)
                    gameEndPanel.ShowVictory();
                CuriesCrucible.Audio.GameAudioManager.Instance?.PlayEndingBGM();
            }
        }

        void HandleTowerDestroyed()
        {
            if (_isGameEnded)
                return;

            _isGameEnded = true;
            if (gameEndPanel != null)
                gameEndPanel.ShowGameOver();
            CuriesCrucible.Audio.GameAudioManager.Instance?.PlayContinueBGM();
        }

        public void OnGameStarted()
        {
            if (gameplayHUD != null)
                gameplayHUD.RefreshAll();
        }

        public void TogglePause()
        {
            if (pausePanel == null)
                return;

            bool isCurrentlyPaused = pausePanel.gameObject.activeSelf;
            if (isCurrentlyPaused)
                pausePanel.Resume();
            else
                pausePanel.Show();
        }

        public void ToggleRecipeBook()
        {
            if (recipeBookPopup == null)
                return;

            bool isOpen = recipeBookPopup.gameObject.activeSelf;
            if (isOpen)
                recipeBookPopup.Close();
            else
                recipeBookPopup.Open();
        }

        public void ToggleCraftingPopup()
        {
            if (craftingPopup != null)
                craftingPopup.Toggle();
        }

        public void ShowRecipeBook()
        {
            if (recipeBookPopup != null)
                recipeBookPopup.Open();
        }

        public void ShowSettings()
        {
            if (settingsPopup != null)
                settingsPopup.Open();
        }

        public void ShowCredits()
        {
            if (creditsPopup != null)
                creditsPopup.Open();
        }
    }
}
