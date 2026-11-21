#pragma warning disable CS0618

using System;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Controller HUD Gameplay Utama bertema Compact Dota-Style Victorian-Steampunk:
    /// - Atas: Health Menara, Indikator Wave & Timer/Status, Tombol Info "i" & Pause "||"
    /// - Pojok Kiri Bawah: Grid 2x2 Bahan Dasar (Basa, Pendingin, Asam, Katalis) + badge stok
    /// - Tengah Bawah: Tombol Bulat CRAFTING + 6 Slot Hotbar Ramuan Siap Lempar (dengan highlight frame terpilih)
    /// - Terhubung ke AlchemyCraftingPopup (Popup 2-Kolom Resep + Kuali/pH/BREW/CLEAR).
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("Referensi Sistem")]
        public AlchemyManager alchemy;
        public TowerHealthManager tower;
        public WaveManager waveManager;
        public ArenaThrowTargeting targeting;
        public UIManager uiManager;

        [Header("Popup 2-Kolom Crafting")]
        public AlchemyCraftingPopup craftingPopup;

        [Header("Kiri Atas: Menara & Controls")]
        public Image towerHealthFill;
        public Text towerHealthText;
        public Button infoButton;
        public Button pauseButton;

        [Header("Atas Tengah: Wave & Status Timer")]
        public Text waveTitleText;
        public Text waveStatusText;

        [Header("Pojok Kiri Bawah: Bahan Dasar 2x2 Grid")]
        public Button ingredientBaseButton;      // Basa (Biru)
        public Text ingredientBaseStockText;
        public Button ingredientCoolButton;      // Pendingin (Cyan)
        public Text ingredientCoolStockText;
        public Button ingredientAcidButton;      // Asam (Merah)
        public Text ingredientAcidStockText;
        public Button ingredientCatalystButton;  // Katalis (Kuning)
        public Text ingredientCatalystStockText;

        [Header("Tengah Bawah: Tombol Bulat CRAFTING")]
        public Button craftingButton;

        [Header("Tengah Bawah: 6 Slot Hotbar Ramuan (Dota-Style)")]
        public Button[] potionSlotButtons = new Button[6];
        public Image[] potionSlotFills = new Image[6];
        public Image[] potionSlotHighlights = new Image[6];
        public Text[] potionSlotTexts = new Text[6];

        [Header("Pesan Status / Notifikasi HUD")]
        public Text notificationText;
        public float notificationDuration = 2.5f;

        float _notificationTimer;

        void Awake()
        {
            HookButtons();
        }

        void Start()
        {
            ResolveReferences();
            if (alchemy != null)
            {
                alchemy.OnMessage += HandleAlchemyMessage;
                alchemy.OnInventoryChanged += RefreshInventory;
            }

            if (tower != null)
            {
                tower.OnHealthChanged += HandleTowerHealthChanged;
                UpdateTowerUI(tower.Health, tower.maxHealth);
            }

            RefreshAll();
        }

        void OnDestroy()
        {
            if (alchemy != null)
            {
                alchemy.OnMessage -= HandleAlchemyMessage;
                alchemy.OnInventoryChanged -= RefreshInventory;
            }

            if (tower != null)
            {
                tower.OnHealthChanged -= HandleTowerHealthChanged;
            }
        }

        void Update()
        {
            UpdateWaveDisplay();
            UpdateNotifications();
            HandleHotkeys();
        }

        void HandleHotkeys()
        {
            // Shortcut C untuk buka / tutup Crafting Popup
            if (Input.GetKeyDown(KeyCode.C))
            {
                ToggleCraftingPopup();
            }
        }

        public void ResolveReferences()
        {
            if (alchemy == null) alchemy = AlchemyManager.Instance ?? FindObjectOfType<AlchemyManager>();
            if (tower == null) tower = TowerHealthManager.Instance ?? FindObjectOfType<TowerHealthManager>();
            if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
            if (targeting == null) targeting = FindObjectOfType<ArenaThrowTargeting>();
            if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
            if (craftingPopup == null) craftingPopup = FindObjectOfType<AlchemyCraftingPopup>();
        }

        void HookButtons()
        {
            if (infoButton != null)
                infoButton.onClick.AddListener(() => { if (uiManager != null) uiManager.ToggleRecipeBook(); });

            if (pauseButton != null)
                pauseButton.onClick.AddListener(() => { if (uiManager != null) uiManager.TogglePause(); });

            if (craftingButton != null)
                craftingButton.onClick.AddListener(ToggleCraftingPopup);

            if (ingredientBaseButton != null)
                ingredientBaseButton.onClick.AddListener(() => AddIngredientAndRefresh(IngredientType.BahanBasa_Biru));

            if (ingredientCoolButton != null)
                ingredientCoolButton.onClick.AddListener(() => AddIngredientAndRefresh(IngredientType.BahanPendingin_Cyan));

            if (ingredientAcidButton != null)
                ingredientAcidButton.onClick.AddListener(() => AddIngredientAndRefresh(IngredientType.BahanAsam_Merah));

            if (ingredientCatalystButton != null)
                ingredientCatalystButton.onClick.AddListener(() => AddIngredientAndRefresh(IngredientType.BahanKatalis_Kuning));

            for (int i = 0; i < potionSlotButtons.Length; i++)
            {
                int slotIndex = i;
                if (potionSlotButtons[i] != null)
                {
                    potionSlotButtons[i].onClick.AddListener(() => SelectPotionSlot(slotIndex));
                }
            }
        }

        void AddIngredientAndRefresh(IngredientType type)
        {
            if (alchemy != null)
            {
                alchemy.AddIngredient(type);
                if (craftingPopup != null && craftingPopup.IsOpen)
                {
                    craftingPopup.RefreshCauldronUI();
                }
            }
        }

        public void ToggleCraftingPopup()
        {
            if (craftingPopup != null)
            {
                craftingPopup.Toggle();
            }
        }

        public void SelectPotionSlot(int index)
        {
            if (alchemy == null || alchemy.mixedPotions == null)
                return;

            if (index >= 0 && index < alchemy.mixedPotions.Count)
            {
                alchemy.selectedIndex = index;
                RefreshPotions();
            }
        }

        public void RefreshAll()
        {
            ResolveReferences();
            if (tower != null)
                UpdateTowerUI(tower.Health, tower.maxHealth);

            RefreshInventory();

            if (craftingPopup != null)
                craftingPopup.RefreshCauldronUI();
        }

        void HandleTowerHealthChanged(int current, int max)
        {
            UpdateTowerUI(current, max);
        }

        void UpdateTowerUI(int current, int max)
        {
            if (towerHealthFill != null)
            {
                float ratio = max > 0 ? (float)current / max : 0f;
                towerHealthFill.fillAmount = ratio;
                towerHealthFill.color = ratio > 0.45f ? UITheme.PHNeutral : UITheme.PHAcidRed;
            }

            if (towerHealthText != null)
            {
                towerHealthText.text = "HP: " + current + " / " + max;
            }
        }

        void UpdateWaveDisplay()
        {
            if (waveManager == null)
                return;

            int currentWave = Mathf.Max(0, waveManager.CurrentWaveIndex + 1);
            int totalWave = Mathf.Max(1, waveManager.TotalWaves);
            string waveName = waveManager.CurrentWave != null ? waveManager.CurrentWave.waveName : "Persiapan";

            if (waveTitleText != null)
            {
                waveTitleText.text = "Wave " + currentWave + "/" + totalWave + " - " + waveName;
            }

            if (waveStatusText != null)
            {
                if (waveManager.IsWaveActive)
                {
                    waveStatusText.text = "Musuh: " + waveManager.AliveZombies;
                    waveStatusText.color = UITheme.BrassBright;
                }
                else if (waveManager.IsWaitingForNextWave)
                {
                    waveStatusText.text = "Bersiap! [SPASI] untuk lanjut";
                    waveStatusText.color = UITheme.SelectionGlow;
                }
                else if (waveManager.IsGameComplete)
                {
                    waveStatusText.text = "Semua Gelombang Tuntas!";
                    waveStatusText.color = UITheme.PHNeutral;
                }
                else
                {
                    waveStatusText.text = "Menunggu gelombang...";
                    waveStatusText.color = UITheme.ParchmentBorder;
                }
            }
        }

        public void RefreshInventory()
        {
            if (alchemy == null)
                return;

            // Bahan Dasar Stok
            if (alchemy.ingredientStock != null && alchemy.ingredientStock.Length >= 4)
            {
                if (ingredientBaseStockText != null)
                    ingredientBaseStockText.text = "x" + alchemy.ingredientStock[(int)IngredientType.BahanBasa_Biru];

                if (ingredientCoolStockText != null)
                    ingredientCoolStockText.text = "x" + alchemy.ingredientStock[(int)IngredientType.BahanPendingin_Cyan];

                if (ingredientAcidStockText != null)
                    ingredientAcidStockText.text = "x" + alchemy.ingredientStock[(int)IngredientType.BahanAsam_Merah];

                if (ingredientCatalystStockText != null)
                    ingredientCatalystStockText.text = "x" + alchemy.ingredientStock[(int)IngredientType.BahanKatalis_Kuning];
            }

            RefreshPotions();

            if (craftingPopup != null)
                craftingPopup.RefreshCauldronUI();
        }

        void RefreshPotions()
        {
            if (alchemy == null)
                return;

            int count = alchemy.mixedPotions != null ? alchemy.mixedPotions.Count : 0;
            int selected = alchemy.selectedIndex;

            for (int i = 0; i < potionSlotButtons.Length; i++)
            {
                bool hasPotion = i < count;
                MixedPotion potion = hasPotion ? alchemy.mixedPotions[i] : null;

                if (potionSlotFills[i] != null)
                {
                    potionSlotFills[i].gameObject.SetActive(hasPotion);
                    if (hasPotion && potion != null)
                    {
                        potionSlotFills[i].color = potion.color;
                    }
                }

                if (potionSlotTexts[i] != null)
                {
                    if (hasPotion && potion != null)
                    {
                        potionSlotTexts[i].text = (i + 1) + "\n" + ShortenName(potion.displayName);
                        potionSlotTexts[i].color = Color.white;
                    }
                    else
                    {
                        potionSlotTexts[i].text = (i + 1) + "\n---";
                        potionSlotTexts[i].color = new Color(0.6f, 0.5f, 0.4f, 0.5f);
                    }
                }

                if (potionSlotHighlights[i] != null)
                {
                    bool isSelected = hasPotion && (i == selected);
                    potionSlotHighlights[i].gameObject.SetActive(isSelected);
                    if (isSelected)
                    {
                        potionSlotHighlights[i].color = UITheme.SelectionGlow;
                    }
                }

                if (potionSlotButtons[i] != null)
                {
                    potionSlotButtons[i].interactable = hasPotion;
                }
            }
        }

        string ShortenName(string full)
        {
            if (string.IsNullOrEmpty(full)) return "";
            if (full.Contains("Gel")) return "Gel";
            if (full.Contains("Kabut")) return "Kabut";
            if (full.Contains("Ledakan")) return "Ledakan";
            if (full.Contains("Karat")) return "Karat";
            if (full.Contains("Bom")) return "Bom";
            if (full.Contains("Busa")) return "Busa";
            return full.Length > 7 ? full.Substring(0, 7) : full;
        }

        void HandleAlchemyMessage(string msg)
        {
            if (notificationText != null)
            {
                notificationText.text = msg;
                notificationText.gameObject.SetActive(true);
                _notificationTimer = notificationDuration;
            }
        }

        void UpdateNotifications()
        {
            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                if (_notificationTimer <= 0f && notificationText != null)
                {
                    notificationText.gameObject.SetActive(false);
                }
            }
        }
    }
}
