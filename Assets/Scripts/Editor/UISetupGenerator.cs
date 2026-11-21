#pragma warning disable CS0618

using System;
using System.IO;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Gameplay;
using CuriesCrucible.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Menu: Tools -> Curie's Crucible -> 4. Generate Compact Dota-Style UI
    ///
    /// Menghasilkan Canvas uGUI lengkap bertema Compact Dota-Style Victorian-Steampunk:
    /// - EventSystem & GraphicRaycaster
    /// - HUD Atas: Health Menara, Wave Tracker & Status, Tombol Pause & Info
    /// - HUD Kiri Bawah: Grid 2x2 Bahan Dasar (Basa, Pendingin, Asam, Katalis) + badge stok
    /// - HUD Tengah Bawah: Tombol Bulat CRAFTING + 6 Slot Hotbar Ramuan Siap Lempar (dengan highlight slot terpilih)
    /// - Popup 2-Kolom: Kolom Kiri (Panduan Resep & Mutasi) + Kolom Kanan (Kuali 2-Slot, pH Balance Meter Lerp, BREW, CLEAR)
    /// - Menus & Overlays: Main Menu Overlay, Pause Panel, Victory / Game Over, Settings & Credits.
    /// </summary>
    public static class UISetupGenerator
    {
        const string CanvasName = "UI_Canvas";
        const string EventSystemName = "EventSystem";

        [MenuItem("Tools/Curie's Crucible/4. Generate Compact Dota-Style UI", false, 40)]
        public static void GenerateUI()
        {
            try
            {
                // 1. Pastikan aset sprite sudah ada di Assets/Art/UI/
                UISpriteAssetGenerator.EnsureAllSprites(false);

                // 2. Pastikan EventSystem di scene
                EnsureEventSystem();

                // 3. Load sprite dengan loader anti-crash
                Sprite spWood = LoadSprite("PanelWoodBrass.png");
                Sprite spParchment = LoadSprite("PanelParchment.png");
                Sprite spButton = LoadSprite("ButtonBrass.png");
                Sprite spBtnRound = LoadSprite("ButtonRoundBrass.png") ?? spButton;
                Sprite spSlot = LoadSprite("SlotFrame.png");
                Sprite spHighlight = LoadSprite("SlotHighlight.png");
                Sprite spPHBar = LoadSprite("PHBarGradient.png");
                Sprite spNeedle = LoadSprite("NeedleBrass.png");
                Sprite spPotion = LoadSprite("IconPotion.png");
                Sprite spIngredient = LoadSprite("IconIngredient.png");
                Sprite spInfo = LoadSprite("IconInfo.png");
                Sprite spPause = LoadSprite("IconPause.png");
                Sprite spTower = LoadSprite("IconTower.png");
                Sprite spCrafting = LoadSprite("IconCrafting.png");

                Font defaultFont = GetBuiltinFont();

                // 4. Buat Canvas di scene
                GameObject canvasGo = GameObject.Find(CanvasName);
                if (canvasGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(canvasGo);
                }

                canvasGo = new GameObject(CanvasName);
                canvasGo.SetActive(true);
                Undo.RegisterCreatedObjectUndo(canvasGo, "Generate Compact Dota-Style UI");

                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();

                // Master UIManager
                UIManager uiManager = canvasGo.AddComponent<UIManager>();

                // Ambil referensi Game Manager & sistem scene
                GameObject gm = GameObject.Find("Game_Manager");
                AlchemyManager alchemy = UnityEngine.Object.FindObjectOfType<AlchemyManager>();
                if (alchemy == null && gm != null) alchemy = gm.GetComponent<AlchemyManager>();

                TowerHealthManager tower = UnityEngine.Object.FindObjectOfType<TowerHealthManager>();
                if (tower == null && gm != null) tower = gm.GetComponent<TowerHealthManager>();

                WaveManager waveManager = UnityEngine.Object.FindObjectOfType<WaveManager>();
                if (waveManager == null && gm != null) waveManager = gm.GetComponent<WaveManager>();

                ArenaThrowTargeting targeting = UnityEngine.Object.FindObjectOfType<ArenaThrowTargeting>();
                if (targeting == null) targeting = FindTargets.FindTargeting();

                if (alchemy != null)
                {
                    alchemy.showDebugOverlay = false;
                    alchemy.showRecipeBook = false;
                    EditorUtility.SetDirty(alchemy);
                }

                if (targeting != null)
                {
                    targeting.blockThrowOverUI = true;
                    EditorUtility.SetDirty(targeting);
                }

                // 5. Bangun Gameplay HUD
                GameObject hudGo = CreateUIObject("Gameplay_HUD", canvasGo.transform);
                RectTransform hudRect = hudGo.GetComponent<RectTransform>();
                SetStretchFull(hudRect);
                GameplayHUD hud = hudGo.AddComponent<GameplayHUD>();
                hud.alchemy = alchemy;
                hud.tower = tower;
                hud.waveManager = waveManager;
                hud.targeting = targeting;
                hud.uiManager = uiManager;
                uiManager.gameplayHUD = hud;

                // Kiri Atas: Health Menara & Kontrol
                BuildTopLeftHUD(hudGo.transform, hud, spWood, spButton, spInfo, spPause, spTower, defaultFont);

                // Atas Tengah: Wave & Status Timer
                BuildTopCenterHUD(hudGo.transform, hud, spWood, defaultFont);

                // Kiri Bawah: Grid 2x2 Bahan Dasar
                BuildBottomLeftIngredientsGrid(hudGo.transform, hud, spWood, spButton, spIngredient, defaultFont);

                // Tengah Bawah: Dota-Style Hotbar (Tombol Bulat Crafting + 6 Slot Ramuan)
                BuildBottomCenterDotaHotbar(hudGo.transform, hud, spWood, spBtnRound, spSlot, spHighlight, spPotion, spCrafting, defaultFont);

                // Notifikasi Tengah
                BuildNotificationText(hudGo.transform, hud, defaultFont);

                // 6. Bangun Popup 2-Kolom Crafting (Kuali Alkimia + Resep)
                AlchemyCraftingPopup craftingPopup = BuildAlchemyCraftingPopup(canvasGo.transform, alchemy, spWood, spParchment, spButton, spSlot, spPHBar, spNeedle, defaultFont);
                hud.craftingPopup = craftingPopup;
                uiManager.craftingPopup = craftingPopup;

                // 7. Bangun Popup Buku Resep / Info Kimia Tambahan
                RecipeBookPopup recipePopup = BuildRecipeBookPopup(canvasGo.transform, spWood, spParchment, spButton, defaultFont);
                uiManager.recipeBookPopup = recipePopup;

                // 8. Bangun Main Menu Overlay
                MainMenuOverlay mainMenu = BuildMainMenuOverlay(canvasGo.transform, uiManager, spWood, spButton, defaultFont);
                uiManager.mainMenuOverlay = mainMenu;

                // 9. Bangun Pause Panel
                PausePanel pausePanel = BuildPausePanel(canvasGo.transform, uiManager, spWood, spButton, defaultFont);
                uiManager.pausePanel = pausePanel;

                // 10. Bangun Game End Panel (Victory & Game Over)
                GameEndPanel gameEnd = BuildGameEndPanel(canvasGo.transform, spWood, spButton, defaultFont);
                uiManager.gameEndPanel = gameEnd;

                // 11. Bangun Settings Popup
                SettingsPopup settingsPopup = BuildSettingsPopup(canvasGo.transform, spWood, spButton, defaultFont);
                uiManager.settingsPopup = settingsPopup;

                // 12. Bangun Credits Popup
                CreditsPopup creditsPopup = BuildCreditsPopup(canvasGo.transform, spWood, spButton, defaultFont);
                uiManager.creditsPopup = creditsPopup;

                // Sambungkan UIManager
                uiManager.towerHealth = tower;
                uiManager.waveManager = waveManager;
                uiManager.showMainMenuOnStart = true;

                EditorUtility.SetDirty(canvasGo);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Compact Dota-Style UI Berhasil Dibuat!",
                        "Seluruh sistem UI Canvas bertema Compact Dota-Style Victorian-Steampunk telah berhasil dipasang:\n\n" +
                        "• Atas: Health Bar Menara (0-10) + Wave & Status Timer + Pause & Info\n" +
                        "• Pojok Kiri Bawah: Grid 2x2 Bahan Dasar [1-4] + badge stok real-time\n" +
                        "• Tengah Bawah: Tombol Bulat CRAFTING (C) + 6 Slot Hotbar Ramuan Siap Lempar (dengan highlight slot terpilih)\n" +
                        "• Popup 2-Kolom: Laboratorium Alkimia (Kuali 2-Slot, pH Balance Meter Lerp, BREW/CLEAR + Panduan Resep)\n" +
                        "• Overlays: Main Menu, Pause, Victory, Game Over, Settings & Credits\n" +
                        "• Debug OnGUI otomatis dinonaktifkan & blockThrowOverUI diaktifkan.",
                        "OK");
                }

                Debug.Log("<color=#F5D170>[Curie UI] Sukses membangun Compact Dota-Style UI Canvas!</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Curie UI Generator] Gagal membuat UI: " + ex);
            }
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject(EventSystemName);
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }
        }

        // ==================================================================
        // Sub-Builders HUD (Compact Dota-Style)
        // ==================================================================

        static void BuildTopLeftHUD(
            Transform parent,
            GameplayHUD hud,
            Sprite spWood,
            Sprite spButton,
            Sprite spInfo,
            Sprite spPause,
            Sprite spTower,
            Font font)
        {
            GameObject container = CreateUIObject("Top_Left_HUD", parent);
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(24, -18);
            rt.sizeDelta = new Vector2(340, 56);

            var imgBg = container.AddComponent<Image>();
            imgBg.sprite = spWood;
            imgBg.type = Image.Type.Sliced;
            imgBg.color = Color.white;

            // Ikon Menara
            GameObject towerIconGo = CreateUIObject("Tower_Icon", container.transform);
            RectTransform rtTower = towerIconGo.GetComponent<RectTransform>();
            rtTower.anchorMin = new Vector2(0, 0.5f);
            rtTower.anchorMax = new Vector2(0, 0.5f);
            rtTower.pivot = new Vector2(0, 0.5f);
            rtTower.anchoredPosition = new Vector2(10, 0);
            rtTower.sizeDelta = new Vector2(32, 32);
            var imgTower = towerIconGo.AddComponent<Image>();
            imgTower.sprite = spTower;
            imgTower.color = UITheme.BrassBright;

            // Health Bar Background Track
            GameObject hpTrack = CreateUIObject("Health_Track", container.transform);
            RectTransform rtTrack = hpTrack.GetComponent<RectTransform>();
            rtTrack.anchorMin = new Vector2(0, 0.5f);
            rtTrack.anchorMax = new Vector2(0, 0.5f);
            rtTrack.pivot = new Vector2(0, 0.5f);
            rtTrack.anchoredPosition = new Vector2(48, 0);
            rtTrack.sizeDelta = new Vector2(190, 22);
            var imgTrack = hpTrack.AddComponent<Image>();
            imgTrack.sprite = spWood;
            imgTrack.type = Image.Type.Sliced;
            imgTrack.color = new Color(0.12f, 0.08f, 0.05f, 0.95f);

            // Health Bar Fill
            GameObject hpFill = CreateUIObject("Health_Fill", hpTrack.transform);
            RectTransform rtFill = hpFill.GetComponent<RectTransform>();
            SetStretchFull(rtFill);
            rtFill.offsetMin = new Vector2(3, 3);
            rtFill.offsetMax = new Vector2(-3, -3);
            var imgFill = hpFill.AddComponent<Image>();
            imgFill.type = Image.Type.Filled;
            imgFill.fillMethod = Image.FillMethod.Horizontal;
            imgFill.fillOrigin = 0;
            imgFill.fillAmount = 1f;
            imgFill.color = UITheme.PHNeutral;
            hud.towerHealthFill = imgFill;

            // Health Bar Text
            GameObject hpTextGo = CreateUIObject("Health_Text", hpTrack.transform);
            RectTransform rtHpTxt = hpTextGo.GetComponent<RectTransform>();
            SetStretchFull(rtHpTxt);
            var txtHp = hpTextGo.AddComponent<Text>();
            txtHp.font = font;
            txtHp.fontSize = 13;
            txtHp.fontStyle = FontStyle.Bold;
            txtHp.alignment = TextAnchor.MiddleCenter;
            txtHp.text = "HP: 10 / 10";
            txtHp.color = Color.white;
            AddOutline(hpTextGo, Color.black, 1.2f);
            hud.towerHealthText = txtHp;

            // Tombol Info ("i")
            GameObject btnInfoGo = CreateUIObject("Btn_Info", container.transform);
            RectTransform rtInfo = btnInfoGo.GetComponent<RectTransform>();
            rtInfo.anchorMin = new Vector2(1, 0.5f);
            rtInfo.anchorMax = new Vector2(1, 0.5f);
            rtInfo.pivot = new Vector2(1, 0.5f);
            rtInfo.anchoredPosition = new Vector2(-50, 0);
            rtInfo.sizeDelta = new Vector2(36, 36);
            var btnInfo = btnInfoGo.AddComponent<Button>();
            var imgBtnInfo = btnInfoGo.AddComponent<Image>();
            imgBtnInfo.sprite = spInfo;
            hud.infoButton = btnInfo;

            // Tombol Pause ("||")
            GameObject btnPauseGo = CreateUIObject("Btn_Pause", container.transform);
            RectTransform rtPause = btnPauseGo.GetComponent<RectTransform>();
            rtPause.anchorMin = new Vector2(1, 0.5f);
            rtPause.anchorMax = new Vector2(1, 0.5f);
            rtPause.pivot = new Vector2(1, 0.5f);
            rtPause.anchoredPosition = new Vector2(-8, 0);
            rtPause.sizeDelta = new Vector2(36, 36);
            var btnPause = btnPauseGo.AddComponent<Button>();
            var imgBtnPause = btnPauseGo.AddComponent<Image>();
            imgBtnPause.sprite = spPause;
            hud.pauseButton = btnPause;
        }

        static void BuildTopCenterHUD(
            Transform parent,
            GameplayHUD hud,
            Sprite spWood,
            Font font)
        {
            GameObject container = CreateUIObject("Top_Center_HUD", parent);
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -18);
            rt.sizeDelta = new Vector2(380, 56);

            var imgBg = container.AddComponent<Image>();
            imgBg.sprite = spWood;
            imgBg.type = Image.Type.Sliced;
            imgBg.color = Color.white;

            // Wave Title Text
            GameObject waveTitleGo = CreateUIObject("Wave_Title", container.transform);
            RectTransform rtWave = waveTitleGo.GetComponent<RectTransform>();
            rtWave.anchorMin = new Vector2(0, 1);
            rtWave.anchorMax = new Vector2(1, 1);
            rtWave.pivot = new Vector2(0.5f, 1);
            rtWave.anchoredPosition = new Vector2(0, -6);
            rtWave.sizeDelta = new Vector2(0, 24);
            var txtWave = waveTitleGo.AddComponent<Text>();
            txtWave.font = font;
            txtWave.fontSize = 16;
            txtWave.fontStyle = FontStyle.Bold;
            txtWave.alignment = TextAnchor.MiddleCenter;
            txtWave.text = "Wave 1/5 - Tutorial";
            txtWave.color = UITheme.BrassBright;
            AddOutline(waveTitleGo, Color.black, 1f);
            hud.waveTitleText = txtWave;

            // Wave Status / Timer Text
            GameObject waveStatusGo = CreateUIObject("Wave_Status", container.transform);
            RectTransform rtStatus = waveStatusGo.GetComponent<RectTransform>();
            rtStatus.anchorMin = new Vector2(0, 0);
            rtStatus.anchorMax = new Vector2(1, 0);
            rtStatus.pivot = new Vector2(0.5f, 0);
            rtStatus.anchoredPosition = new Vector2(0, 6);
            rtStatus.sizeDelta = new Vector2(0, 20);
            var txtStatus = waveStatusGo.AddComponent<Text>();
            txtStatus.font = font;
            txtStatus.fontSize = 12;
            txtStatus.fontStyle = FontStyle.Italic;
            txtStatus.alignment = TextAnchor.MiddleCenter;
            txtStatus.text = "Musuh: 0";
            txtStatus.color = UITheme.ParchmentBorder;
            hud.waveStatusText = txtStatus;
        }

        static void BuildBottomLeftIngredientsGrid(
            Transform parent,
            GameplayHUD hud,
            Sprite spWood,
            Sprite spButton,
            Sprite spIcon,
            Font font)
        {
            GameObject container = CreateUIObject("Bottom_Left_Ingredients_2x2", parent);
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(24, 20);
            rt.sizeDelta = new Vector2(240, 134);

            var imgBg = container.AddComponent<Image>();
            imgBg.sprite = spWood;
            imgBg.type = Image.Type.Sliced;
            imgBg.color = Color.white;

            // Header Plat
            GameObject hdrGo = CreateUIObject("Header", container.transform);
            RectTransform rtHdr = hdrGo.GetComponent<RectTransform>();
            rtHdr.anchorMin = new Vector2(0, 1);
            rtHdr.anchorMax = new Vector2(1, 1);
            rtHdr.pivot = new Vector2(0.5f, 1);
            rtHdr.anchoredPosition = new Vector2(0, -6);
            rtHdr.sizeDelta = new Vector2(0, 20);
            var txtHdr = hdrGo.AddComponent<Text>();
            txtHdr.font = font;
            txtHdr.fontSize = 12;
            txtHdr.fontStyle = FontStyle.Bold;
            txtHdr.alignment = TextAnchor.MiddleCenter;
            txtHdr.text = "BAHAN DASAR [1-4]";
            txtHdr.color = UITheme.BrassBright;

            // Grid Container 2x2
            GameObject gridGo = CreateUIObject("Grid", container.transform);
            RectTransform rtGrid = gridGo.GetComponent<RectTransform>();
            rtGrid.anchorMin = new Vector2(0, 0);
            rtGrid.anchorMax = new Vector2(1, 1);
            rtGrid.offsetMin = new Vector2(10, 10);
            rtGrid.offsetMax = new Vector2(-10, -28);

            var glg = gridGo.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(106, 42);
            glg.spacing = new Vector2(8, 6);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            // 1: Basa (Biru)
            hud.ingredientBaseButton = CreateGridIngredientButton(gridGo.transform, "[1] Basa", AlchemyDatabase.IngredientColor(IngredientType.BahanBasa_Biru), spButton, spIcon, font, out hud.ingredientBaseStockText);
            // 2: Pendingin (Cyan)
            hud.ingredientCoolButton = CreateGridIngredientButton(gridGo.transform, "[2] Pendingin", AlchemyDatabase.IngredientColor(IngredientType.BahanPendingin_Cyan), spButton, spIcon, font, out hud.ingredientCoolStockText);
            // 3: Asam (Merah)
            hud.ingredientAcidButton = CreateGridIngredientButton(gridGo.transform, "[3] Asam", AlchemyDatabase.IngredientColor(IngredientType.BahanAsam_Merah), spButton, spIcon, font, out hud.ingredientAcidStockText);
            // 4: Katalis (Kuning)
            hud.ingredientCatalystButton = CreateGridIngredientButton(gridGo.transform, "[4] Katalis", AlchemyDatabase.IngredientColor(IngredientType.BahanKatalis_Kuning), spButton, spIcon, font, out hud.ingredientCatalystStockText);
        }

        static Button CreateGridIngredientButton(
            Transform parent,
            string label,
            Color accentColor,
            Sprite spButton,
            Sprite spIcon,
            Font font,
            out Text stockTextOut)
        {
            GameObject btnGo = CreateUIObject("Btn_" + label, parent);
            var btn = btnGo.AddComponent<Button>();
            var img = btnGo.AddComponent<Image>();
            img.sprite = spButton;
            img.type = Image.Type.Sliced;
            img.color = Color.Lerp(Color.white, accentColor, 0.4f);

            // Icon
            GameObject iconGo = CreateUIObject("Icon", btnGo.transform);
            RectTransform rtIcon = iconGo.GetComponent<RectTransform>();
            rtIcon.anchorMin = new Vector2(0, 0.5f);
            rtIcon.anchorMax = new Vector2(0, 0.5f);
            rtIcon.pivot = new Vector2(0, 0.5f);
            rtIcon.anchoredPosition = new Vector2(6, 0);
            rtIcon.sizeDelta = new Vector2(22, 22);
            var imgIcon = iconGo.AddComponent<Image>();
            imgIcon.sprite = spIcon;
            imgIcon.color = accentColor;

            // Name
            GameObject lblGo = CreateUIObject("Label", btnGo.transform);
            RectTransform rtLbl = lblGo.GetComponent<RectTransform>();
            rtLbl.anchorMin = new Vector2(0, 0);
            rtLbl.anchorMax = new Vector2(1, 1);
            rtLbl.offsetMin = new Vector2(30, 0);
            rtLbl.offsetMax = new Vector2(-28, 0);
            var txtLbl = lblGo.AddComponent<Text>();
            txtLbl.font = font;
            txtLbl.fontSize = 11;
            txtLbl.fontStyle = FontStyle.Bold;
            txtLbl.alignment = TextAnchor.MiddleLeft;
            txtLbl.text = label;
            txtLbl.color = Color.white;

            // Stock Badge
            GameObject stGo = CreateUIObject("Stock", btnGo.transform);
            RectTransform rtSt = stGo.GetComponent<RectTransform>();
            rtSt.anchorMin = new Vector2(1, 0.5f);
            rtSt.anchorMax = new Vector2(1, 0.5f);
            rtSt.pivot = new Vector2(1, 0.5f);
            rtSt.anchoredPosition = new Vector2(-4, 0);
            rtSt.sizeDelta = new Vector2(26, 20);
            var txtSt = stGo.AddComponent<Text>();
            txtSt.font = font;
            txtSt.fontSize = 12;
            txtSt.fontStyle = FontStyle.Bold;
            txtSt.alignment = TextAnchor.MiddleRight;
            txtSt.text = "x3";
            txtSt.color = UITheme.BrassBright;

            stockTextOut = txtSt;
            return btn;
        }

        static void BuildBottomCenterDotaHotbar(
            Transform parent,
            GameplayHUD hud,
            Sprite spWood,
            Sprite spBtnRound,
            Sprite spSlot,
            Sprite spHighlight,
            Sprite spPotion,
            Sprite spCrafting,
            Font font)
        {
            GameObject container = CreateUIObject("Bottom_Center_DotaHotbar", parent);
            RectTransform rt = container.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0);
            rt.anchorMax = new Vector2(0.5f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 20);
            rt.sizeDelta = new Vector2(590, 84);

            var imgBg = container.AddComponent<Image>();
            imgBg.sprite = spWood;
            imgBg.type = Image.Type.Sliced;
            imgBg.color = Color.white;

            // Tombol Bulat CRAFTING
            GameObject craftGo = CreateUIObject("Btn_Round_Crafting", container.transform);
            RectTransform rtCraft = craftGo.GetComponent<RectTransform>();
            rtCraft.anchorMin = new Vector2(0, 0.5f);
            rtCraft.anchorMax = new Vector2(0, 0.5f);
            rtCraft.pivot = new Vector2(0, 0.5f);
            rtCraft.anchoredPosition = new Vector2(14, 0);
            rtCraft.sizeDelta = new Vector2(74, 74);

            var btnCraft = craftGo.AddComponent<Button>();
            var imgCraft = craftGo.AddComponent<Image>();
            imgCraft.sprite = spBtnRound;
            imgCraft.color = Color.white;
            hud.craftingButton = btnCraft;

            // Icon Crafting
            GameObject craftIconGo = CreateUIObject("Icon", craftGo.transform);
            RectTransform rtCraftIcon = craftIconGo.GetComponent<RectTransform>();
            SetStretchFull(rtCraftIcon);
            rtCraftIcon.offsetMin = new Vector2(12, 16);
            rtCraftIcon.offsetMax = new Vector2(-12, -8);
            var imgIcon = craftIconGo.AddComponent<Image>();
            imgIcon.sprite = spCrafting;

            // Text Label "CRAFT (C)"
            GameObject craftTxtGo = CreateUIObject("Text", craftGo.transform);
            RectTransform rtCraftTxt = craftTxtGo.GetComponent<RectTransform>();
            rtCraftTxt.anchorMin = new Vector2(0, 0);
            rtCraftTxt.anchorMax = new Vector2(1, 0);
            rtCraftTxt.pivot = new Vector2(0.5f, 0);
            rtCraftTxt.anchoredPosition = new Vector2(0, 6);
            rtCraftTxt.sizeDelta = new Vector2(0, 16);
            var txtCraft = craftTxtGo.AddComponent<Text>();
            txtCraft.font = font;
            txtCraft.fontSize = 10;
            txtCraft.fontStyle = FontStyle.Bold;
            txtCraft.alignment = TextAnchor.MiddleCenter;
            txtCraft.text = "CRAFT [C]";
            txtCraft.color = UITheme.BrassBright;
            AddOutline(craftTxtGo, Color.black, 1f);

            // 6 Slot Hotbar Ramuan Horizontal
            for (int i = 0; i < 6; i++)
            {
                CreateDotaPotionSlot(
                    container.transform, i, spSlot, spHighlight, spPotion, font,
                    out hud.potionSlotButtons[i],
                    out hud.potionSlotFills[i],
                    out hud.potionSlotHighlights[i],
                    out hud.potionSlotTexts[i]);
            }
        }

        static void CreateDotaPotionSlot(
            Transform parent,
            int index,
            Sprite spSlot,
            Sprite spHighlight,
            Sprite spPotionIcon,
            Font font,
            out Button btnOut,
            out Image fillOut,
            out Image highlightOut,
            out Text textOut)
        {
            GameObject slotGo = CreateUIObject("Slot_" + (index + 1), parent);
            RectTransform rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            // Mulai setelah tombol bulat crafting di X = 102
            rt.anchoredPosition = new Vector2(102 + (index * 78), 0);
            rt.sizeDelta = new Vector2(70, 70);

            var btn = slotGo.AddComponent<Button>();
            var img = slotGo.AddComponent<Image>();
            img.sprite = spSlot;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            // Highlight Glow Frame Emas
            GameObject hlGo = CreateUIObject("Highlight", slotGo.transform);
            RectTransform rtHl = hlGo.GetComponent<RectTransform>();
            SetStretchFull(rtHl);
            rtHl.offsetMin = new Vector2(-4, -4);
            rtHl.offsetMax = new Vector2(4, 4);
            var imgHl = hlGo.AddComponent<Image>();
            imgHl.sprite = spHighlight;
            imgHl.type = Image.Type.Sliced;
            imgHl.color = UITheme.SelectionGlow;
            hlGo.SetActive(false);

            // Potion Icon Fill
            GameObject iconGo = CreateUIObject("Icon", slotGo.transform);
            RectTransform rtIcon = iconGo.GetComponent<RectTransform>();
            rtIcon.anchorMin = new Vector2(0.5f, 0.5f);
            rtIcon.anchorMax = new Vector2(0.5f, 0.5f);
            rtIcon.pivot = new Vector2(0.5f, 0.5f);
            rtIcon.anchoredPosition = new Vector2(0, 6);
            rtIcon.sizeDelta = new Vector2(36, 36);
            var imgIcon = iconGo.AddComponent<Image>();
            imgIcon.sprite = spPotionIcon;
            imgIcon.color = Color.gray;

            // Slot Label
            GameObject txtGo = CreateUIObject("Label", slotGo.transform);
            RectTransform rtTxt = txtGo.GetComponent<RectTransform>();
            rtTxt.anchorMin = new Vector2(0, 0);
            rtTxt.anchorMax = new Vector2(1, 0);
            rtTxt.pivot = new Vector2(0.5f, 0);
            rtTxt.anchoredPosition = new Vector2(0, 4);
            rtTxt.sizeDelta = new Vector2(0, 22);
            var txt = txtGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 10;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = (index + 1) + "\n---";
            txt.color = new Color(0.7f, 0.6f, 0.5f, 0.8f);

            btnOut = btn;
            fillOut = imgIcon;
            highlightOut = imgHl;
            textOut = txt;
        }

        static void BuildNotificationText(Transform parent, GameplayHUD hud, Font font)
        {
            GameObject notifGo = CreateUIObject("HUD_Notification", parent);
            RectTransform rt = notifGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 110);
            rt.sizeDelta = new Vector2(600, 44);

            var txt = notifGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 18;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = UITheme.SelectionGlow;
            AddOutline(notifGo, Color.black, 1.5f);

            notifGo.SetActive(false);
            hud.notificationText = txt;
        }

        // ==================================================================
        // Popup 2-Kolom Crafting (Kuali & pH + Resep)
        // ==================================================================

        static AlchemyCraftingPopup BuildAlchemyCraftingPopup(
            Transform parent,
            AlchemyManager alchemy,
            Sprite spWood,
            Sprite spParchment,
            Sprite spButton,
            Sprite spSlot,
            Sprite spPHBar,
            Sprite spNeedle,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Popup_Alchemy_Crafting", parent);
            var popup = overlay.AddComponent<AlchemyCraftingPopup>();
            popup.panelRoot = overlay;
            popup.alchemy = alchemy;

            // Frame Utama
            GameObject board = CreateUIObject("Crafting_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(920, 580);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            // Title Header
            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -16);
            rtTitle.sizeDelta = new Vector2(0, 34);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 22;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "LABORATORIUM ALKIMIA & BUKU RESEP";
            txtTitle.color = UITheme.BrassBright;

            // Close Button
            GameObject closeGo = CreateUIObject("Btn_Close", board.transform);
            RectTransform rtClose = closeGo.GetComponent<RectTransform>();
            rtClose.anchorMin = new Vector2(1, 1);
            rtClose.anchorMax = new Vector2(1, 1);
            rtClose.pivot = new Vector2(1, 1);
            rtClose.anchoredPosition = new Vector2(-16, -14);
            rtClose.sizeDelta = new Vector2(36, 36);
            var btnClose = closeGo.AddComponent<Button>();
            var imgClose = closeGo.AddComponent<Image>();
            imgClose.sprite = spButton;
            imgClose.type = Image.Type.Sliced;
            imgClose.color = UITheme.ClearButtonNormal;
            popup.closeButton = btnClose;

            GameObject closeTxtGo = CreateUIObject("Text", closeGo.transform);
            SetStretchFull(closeTxtGo.GetComponent<RectTransform>());
            var txtClose = closeTxtGo.AddComponent<Text>();
            txtClose.font = font;
            txtClose.fontSize = 18;
            txtClose.fontStyle = FontStyle.Bold;
            txtClose.alignment = TextAnchor.MiddleCenter;
            txtClose.text = "X";
            txtClose.color = Color.white;

            // ==================== KOLOM KIRI: RESEP & PANDUAN ====================
            GameObject colLeft = CreateUIObject("Column_Left_Recipes", board.transform);
            RectTransform rtLeft = colLeft.GetComponent<RectTransform>();
            rtLeft.anchorMin = new Vector2(0, 0);
            rtLeft.anchorMax = new Vector2(0.5f, 1);
            rtLeft.offsetMin = new Vector2(24, 24);
            rtLeft.offsetMax = new Vector2(-12, -60);
            var imgLeft = colLeft.AddComponent<Image>();
            imgLeft.sprite = spParchment;
            imgLeft.type = Image.Type.Sliced;
            imgLeft.color = Color.white;

            GameObject guideGo = CreateUIObject("Guide_Text", colLeft.transform);
            RectTransform rtGuide = guideGo.GetComponent<RectTransform>();
            SetStretchFull(rtGuide);
            rtGuide.offsetMin = new Vector2(16, 16);
            rtGuide.offsetMax = new Vector2(-16, -16);
            var txtGuide = guideGo.AddComponent<Text>();
            txtGuide.font = font;
            txtGuide.fontSize = 12;
            txtGuide.lineSpacing = 1.15f;
            txtGuide.color = UITheme.ParchmentText;
            txtGuide.supportRichText = true;
            popup.recipeGuideText = txtGuide;

            // ==================== KOLOM KANAN: KUALI & PH ====================
            GameObject colRight = CreateUIObject("Column_Right_Cauldron", board.transform);
            RectTransform rtRight = colRight.GetComponent<RectTransform>();
            rtRight.anchorMin = new Vector2(0.5f, 0);
            rtRight.anchorMax = new Vector2(1, 1);
            rtRight.offsetMin = new Vector2(12, 24);
            rtRight.offsetMax = new Vector2(-24, -60);
            var imgRight = colRight.AddComponent<Image>();
            imgRight.sprite = spWood;
            imgRight.type = Image.Type.Sliced;
            imgRight.color = new Color(0.20f, 0.14f, 0.09f, 0.95f);

            // Sub-Header Kolom Kanan
            GameObject subHdrGo = CreateUIObject("SubHeader", colRight.transform);
            RectTransform rtSubHdr = subHdrGo.GetComponent<RectTransform>();
            rtSubHdr.anchorMin = new Vector2(0, 1);
            rtSubHdr.anchorMax = new Vector2(1, 1);
            rtSubHdr.pivot = new Vector2(0.5f, 1);
            rtSubHdr.anchoredPosition = new Vector2(0, -14);
            rtSubHdr.sizeDelta = new Vector2(0, 24);
            var txtSubHdr = subHdrGo.AddComponent<Text>();
            txtSubHdr.font = font;
            txtSubHdr.fontSize = 16;
            txtSubHdr.fontStyle = FontStyle.Bold;
            txtSubHdr.alignment = TextAnchor.MiddleCenter;
            txtSubHdr.text = "KUALI PERACIKAN ALKIMIA";
            txtSubHdr.color = UITheme.BrassBright;

            // 1. Meteran pH Balance
            GameObject phBox = CreateUIObject("PH_Meter_Box", colRight.transform);
            RectTransform rtPhBox = phBox.GetComponent<RectTransform>();
            rtPhBox.anchorMin = new Vector2(0.5f, 1);
            rtPhBox.anchorMax = new Vector2(0.5f, 1);
            rtPhBox.pivot = new Vector2(0.5f, 1);
            rtPhBox.anchoredPosition = new Vector2(0, -48);
            rtPhBox.sizeDelta = new Vector2(360, 100);

            var phTitle = CreateUIObject("Title", phBox.transform).AddComponent<Text>();
            RectTransform rtPhTitle = phTitle.GetComponent<RectTransform>();
            rtPhTitle.anchoredPosition = new Vector2(0, 36);
            rtPhTitle.sizeDelta = new Vector2(340, 22);
            phTitle.font = font;
            phTitle.fontSize = 13;
            phTitle.fontStyle = FontStyle.Bold;
            phTitle.alignment = TextAnchor.MiddleCenter;
            phTitle.text = "METERAN PH BALANCE REAL-TIME";
            phTitle.color = UITheme.BrassBright;

            var phLegend = CreateUIObject("Legend", phBox.transform).AddComponent<Text>();
            RectTransform rtPhLegend = phLegend.GetComponent<RectTransform>();
            rtPhLegend.anchoredPosition = new Vector2(0, 16);
            rtPhLegend.sizeDelta = new Vector2(340, 20);
            phLegend.font = font;
            phLegend.fontSize = 11;
            phLegend.fontStyle = FontStyle.Bold;
            phLegend.alignment = TextAnchor.MiddleCenter;
            phLegend.supportRichText = true;
            phLegend.text = "<color=#E53935>0 Asam</color>          <color=#43A047>7 Netral</color>          <color=#1E88E5>14 Basa</color>";

            GameObject barGo = CreateUIObject("Bar_Track", phBox.transform);
            RectTransform rtBar = barGo.GetComponent<RectTransform>();
            rtBar.sizeDelta = new Vector2(300, 26);
            rtBar.anchoredPosition = new Vector2(0, -8);
            var imgBar = barGo.AddComponent<Image>();
            imgBar.sprite = spPHBar;
            popup.phBarRect = rtBar;

            GameObject needleGo = CreateUIObject("Needle", barGo.transform);
            RectTransform rtNeedle = needleGo.GetComponent<RectTransform>();
            rtNeedle.sizeDelta = new Vector2(24, 46);
            rtNeedle.anchoredPosition = new Vector2(0, 16);
            var imgNeedle = needleGo.AddComponent<Image>();
            imgNeedle.sprite = spNeedle;
            popup.phNeedleRect = rtNeedle;
            popup.phMinX = -135f;
            popup.phMaxX = 135f;

            var phVal = CreateUIObject("Val", phBox.transform).AddComponent<Text>();
            RectTransform rtPhVal = phVal.GetComponent<RectTransform>();
            rtPhVal.anchoredPosition = new Vector2(0, -34);
            rtPhVal.sizeDelta = new Vector2(340, 24);
            phVal.font = font;
            phVal.fontSize = 14;
            phVal.fontStyle = FontStyle.Bold;
            phVal.alignment = TextAnchor.MiddleCenter;
            phVal.text = "pH: 7.0  [NETRAL]";
            phVal.color = UITheme.PHNeutral;
            popup.phValueText = phVal;

            // 2. Kuali 2 Slot
            GameObject slotsBox = CreateUIObject("Slots_Box", colRight.transform);
            RectTransform rtSlotsBox = slotsBox.GetComponent<RectTransform>();
            rtSlotsBox.anchorMin = new Vector2(0.5f, 1);
            rtSlotsBox.anchorMax = new Vector2(0.5f, 1);
            rtSlotsBox.pivot = new Vector2(0.5f, 1);
            rtSlotsBox.anchoredPosition = new Vector2(0, -170);
            rtSlotsBox.sizeDelta = new Vector2(360, 90);

            // Slot 1
            GameObject s1Go = CreateUIObject("Slot1", slotsBox.transform);
            RectTransform rtS1 = s1Go.GetComponent<RectTransform>();
            rtS1.anchorMin = new Vector2(0, 0.5f);
            rtS1.anchorMax = new Vector2(0, 0.5f);
            rtS1.pivot = new Vector2(0, 0.5f);
            rtS1.anchoredPosition = new Vector2(10, 0);
            rtS1.sizeDelta = new Vector2(140, 70);
            var imgS1 = s1Go.AddComponent<Image>();
            imgS1.sprite = spSlot;
            imgS1.type = Image.Type.Sliced;
            popup.cauldronSlot1Image = imgS1;

            var txtS1 = CreateUIObject("Txt", s1Go.transform).AddComponent<Text>();
            SetStretchFull(txtS1.GetComponent<RectTransform>());
            txtS1.font = font;
            txtS1.fontSize = 12;
            txtS1.fontStyle = FontStyle.Bold;
            txtS1.alignment = TextAnchor.MiddleCenter;
            txtS1.text = "Slot 1\n[Kosong]";
            popup.cauldronSlot1Text = txtS1;

            // Plus
            var plusTxt = CreateUIObject("Plus", slotsBox.transform).AddComponent<Text>();
            RectTransform rtPlus = plusTxt.GetComponent<RectTransform>();
            rtPlus.anchoredPosition = new Vector2(0, 0);
            rtPlus.sizeDelta = new Vector2(40, 40);
            plusTxt.font = font;
            plusTxt.fontSize = 26;
            plusTxt.fontStyle = FontStyle.Bold;
            plusTxt.alignment = TextAnchor.MiddleCenter;
            plusTxt.text = "+";
            plusTxt.color = UITheme.BrassBright;

            // Slot 2
            GameObject s2Go = CreateUIObject("Slot2", slotsBox.transform);
            RectTransform rtS2 = s2Go.GetComponent<RectTransform>();
            rtS2.anchorMin = new Vector2(1, 0.5f);
            rtS2.anchorMax = new Vector2(1, 0.5f);
            rtS2.pivot = new Vector2(1, 0.5f);
            rtS2.anchoredPosition = new Vector2(-10, 0);
            rtS2.sizeDelta = new Vector2(140, 70);
            var imgS2 = s2Go.AddComponent<Image>();
            imgS2.sprite = spSlot;
            imgS2.type = Image.Type.Sliced;
            popup.cauldronSlot2Image = imgS2;

            var txtS2 = CreateUIObject("Txt", s2Go.transform).AddComponent<Text>();
            SetStretchFull(txtS2.GetComponent<RectTransform>());
            txtS2.font = font;
            txtS2.fontSize = 12;
            txtS2.fontStyle = FontStyle.Bold;
            txtS2.alignment = TextAnchor.MiddleCenter;
            txtS2.text = "Slot 2\n[Kosong]";
            popup.cauldronSlot2Text = txtS2;

            // 3. Tombol BREW & CLEAR
            GameObject btnBox = CreateUIObject("Buttons_Box", colRight.transform);
            RectTransform rtBtnBox = btnBox.GetComponent<RectTransform>();
            rtBtnBox.anchorMin = new Vector2(0.5f, 0);
            rtBtnBox.anchorMax = new Vector2(0.5f, 0);
            rtBtnBox.pivot = new Vector2(0.5f, 0);
            rtBtnBox.anchoredPosition = new Vector2(0, 20);
            rtBtnBox.sizeDelta = new Vector2(360, 60);

            // BREW Button
            GameObject brewGo = CreateUIObject("Btn_BREW", btnBox.transform);
            RectTransform rtBrew = brewGo.GetComponent<RectTransform>();
            rtBrew.anchorMin = new Vector2(0, 0.5f);
            rtBrew.anchorMax = new Vector2(0, 0.5f);
            rtBrew.pivot = new Vector2(0, 0.5f);
            rtBrew.anchoredPosition = new Vector2(10, 0);
            rtBrew.sizeDelta = new Vector2(200, 52);
            var btnBrew = brewGo.AddComponent<Button>();
            var imgBrew = brewGo.AddComponent<Image>();
            imgBrew.sprite = spButton;
            imgBrew.type = Image.Type.Sliced;
            imgBrew.color = UITheme.BrewButtonNormal;
            popup.brewButton = btnBrew;
            popup.brewButtonImage = imgBrew;

            var txtBrew = CreateUIObject("Txt", brewGo.transform).AddComponent<Text>();
            SetStretchFull(txtBrew.GetComponent<RectTransform>());
            txtBrew.font = font;
            txtBrew.fontSize = 16;
            txtBrew.fontStyle = FontStyle.Bold;
            txtBrew.alignment = TextAnchor.MiddleCenter;
            txtBrew.text = "RACIK / BREW [R]";
            txtBrew.color = Color.white;
            popup.brewButtonText = txtBrew;

            // CLEAR Button
            GameObject clearGo = CreateUIObject("Btn_CLEAR", btnBox.transform);
            RectTransform rtClear = clearGo.GetComponent<RectTransform>();
            rtClear.anchorMin = new Vector2(1, 0.5f);
            rtClear.anchorMax = new Vector2(1, 0.5f);
            rtClear.pivot = new Vector2(1, 0.5f);
            rtClear.anchoredPosition = new Vector2(-10, 0);
            rtClear.sizeDelta = new Vector2(120, 52);
            var btnClear = clearGo.AddComponent<Button>();
            var imgClear = clearGo.AddComponent<Image>();
            imgClear.sprite = spButton;
            imgClear.type = Image.Type.Sliced;
            imgClear.color = UITheme.ClearButtonNormal;
            popup.clearButton = btnClear;

            var txtClear = CreateUIObject("Txt", clearGo.transform).AddComponent<Text>();
            SetStretchFull(txtClear.GetComponent<RectTransform>());
            txtClear.font = font;
            txtClear.fontSize = 14;
            txtClear.fontStyle = FontStyle.Bold;
            txtClear.alignment = TextAnchor.MiddleCenter;
            txtClear.text = "RESET [F]";
            txtClear.color = Color.white;

            overlay.SetActive(false);
            return popup;
        }

        static RecipeBookPopup BuildRecipeBookPopup(
            Transform parent,
            Sprite spWood,
            Sprite spParchment,
            Sprite spButton,
            Font font)
        {
            GameObject popupRoot = CreateModalOverlay("Popup_Recipe_Book", parent);
            var popup = popupRoot.AddComponent<RecipeBookPopup>();
            popup.panelRoot = popupRoot;

            GameObject board = CreateUIObject("Parchment_Board", popupRoot.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(860, 620);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spParchment;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject headerGo = CreateUIObject("Title", board.transform);
            RectTransform rtHeader = headerGo.GetComponent<RectTransform>();
            rtHeader.anchorMin = new Vector2(0, 1);
            rtHeader.anchorMax = new Vector2(1, 1);
            rtHeader.pivot = new Vector2(0.5f, 1);
            rtHeader.anchoredPosition = new Vector2(0, -18);
            rtHeader.sizeDelta = new Vector2(0, 36);
            var txtHeader = headerGo.AddComponent<Text>();
            txtHeader.font = font;
            txtHeader.fontSize = 22;
            txtHeader.fontStyle = FontStyle.Bold;
            txtHeader.alignment = TextAnchor.MiddleCenter;
            txtHeader.text = "BUKU RESEP & PANDUAN KIMIA CURIE";
            txtHeader.color = UITheme.ParchmentText;

            GameObject closeGo = CreateUIObject("Btn_Close", board.transform);
            RectTransform rtClose = closeGo.GetComponent<RectTransform>();
            rtClose.anchorMin = new Vector2(1, 1);
            rtClose.anchorMax = new Vector2(1, 1);
            rtClose.pivot = new Vector2(1, 1);
            rtClose.anchoredPosition = new Vector2(-16, -16);
            rtClose.sizeDelta = new Vector2(36, 36);
            var btnClose = closeGo.AddComponent<Button>();
            var imgClose = closeGo.AddComponent<Image>();
            imgClose.sprite = spButton;
            imgClose.type = Image.Type.Sliced;
            imgClose.color = UITheme.ClearButtonNormal;
            popup.closeButton = btnClose;

            var txtClose = CreateUIObject("Text", closeGo.transform).AddComponent<Text>();
            SetStretchFull(txtClose.GetComponent<RectTransform>());
            txtClose.font = font;
            txtClose.fontSize = 18;
            txtClose.fontStyle = FontStyle.Bold;
            txtClose.alignment = TextAnchor.MiddleCenter;
            txtClose.text = "X";
            txtClose.color = Color.white;

            GameObject tabChemGo = CreateUIObject("Tab_Chemistry", board.transform);
            RectTransform rtTabChem = tabChemGo.GetComponent<RectTransform>();
            rtTabChem.anchorMin = new Vector2(0, 1);
            rtTabChem.anchorMax = new Vector2(0, 1);
            rtTabChem.pivot = new Vector2(0, 1);
            rtTabChem.anchoredPosition = new Vector2(30, -64);
            rtTabChem.sizeDelta = new Vector2(210, 38);
            var btnTabChem = tabChemGo.AddComponent<Button>();
            var imgTabChem = tabChemGo.AddComponent<Image>();
            imgTabChem.sprite = spButton;
            imgTabChem.type = Image.Type.Sliced;
            imgTabChem.color = UITheme.BrassBright;
            popup.tabChemistryButton = btnTabChem;

            var txtChemTab = CreateUIObject("Text", tabChemGo.transform).AddComponent<Text>();
            SetStretchFull(txtChemTab.GetComponent<RectTransform>());
            txtChemTab.font = font;
            txtChemTab.fontSize = 14;
            txtChemTab.fontStyle = FontStyle.Bold;
            txtChemTab.alignment = TextAnchor.MiddleCenter;
            txtChemTab.text = "1. PRINSIP KIMIA";
            txtChemTab.color = UITheme.WoodDark;

            GameObject tabRecGo = CreateUIObject("Tab_Recipes", board.transform);
            RectTransform rtTabRec = tabRecGo.GetComponent<RectTransform>();
            rtTabRec.anchorMin = new Vector2(0, 1);
            rtTabRec.anchorMax = new Vector2(0, 1);
            rtTabRec.pivot = new Vector2(0, 1);
            rtTabRec.anchoredPosition = new Vector2(250, -64);
            rtTabRec.sizeDelta = new Vector2(230, 38);
            var btnTabRec = tabRecGo.AddComponent<Button>();
            var imgTabRec = tabRecGo.AddComponent<Image>();
            imgTabRec.sprite = spButton;
            imgTabRec.type = Image.Type.Sliced;
            imgTabRec.color = UITheme.BrassDark;
            popup.tabRecipesButton = btnTabRec;

            var txtRecTab = CreateUIObject("Text", tabRecGo.transform).AddComponent<Text>();
            SetStretchFull(txtRecTab.GetComponent<RectTransform>());
            txtRecTab.font = font;
            txtRecTab.fontSize = 14;
            txtRecTab.fontStyle = FontStyle.Bold;
            txtRecTab.alignment = TextAnchor.MiddleCenter;
            txtRecTab.text = "2. RESEP & MUTASI";
            txtRecTab.color = UITheme.WoodDark;

            GameObject chemPage = CreateUIObject("Page_Chemistry", board.transform);
            RectTransform rtChemPage = chemPage.GetComponent<RectTransform>();
            rtChemPage.anchorMin = new Vector2(0, 0);
            rtChemPage.anchorMax = new Vector2(1, 1);
            rtChemPage.offsetMin = new Vector2(32, 24);
            rtChemPage.offsetMax = new Vector2(-32, -114);
            var txtChemBody = chemPage.AddComponent<Text>();
            txtChemBody.font = font;
            txtChemBody.fontSize = 14;
            txtChemBody.lineSpacing = 1.25f;
            txtChemBody.color = UITheme.ParchmentText;
            txtChemBody.supportRichText = true;
            popup.chemistryPage = chemPage;
            popup.chemistryBodyText = txtChemBody;

            GameObject recPage = CreateUIObject("Page_Recipes", board.transform);
            RectTransform rtRecPage = recPage.GetComponent<RectTransform>();
            rtRecPage.anchorMin = new Vector2(0, 0);
            rtRecPage.anchorMax = new Vector2(1, 1);
            rtRecPage.offsetMin = new Vector2(32, 24);
            rtRecPage.offsetMax = new Vector2(-32, -114);
            var txtRecBody = recPage.AddComponent<Text>();
            txtRecBody.font = font;
            txtRecBody.fontSize = 14;
            txtRecBody.lineSpacing = 1.2f;
            txtRecBody.color = UITheme.ParchmentText;
            txtRecBody.supportRichText = true;
            recPage.SetActive(false);
            popup.recipesPage = recPage;
            popup.recipesBodyText = txtRecBody;

            popupRoot.SetActive(false);
            return popup;
        }

        static MainMenuOverlay BuildMainMenuOverlay(
            Transform parent,
            UIManager uiManager,
            Sprite spWood,
            Sprite spButton,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Overlay_Main_Menu", parent);
            var menu = overlay.AddComponent<MainMenuOverlay>();
            menu.menuRoot = overlay;
            menu.uiManager = uiManager;

            GameObject board = CreateUIObject("Menu_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(620, 520);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -32);
            rtTitle.sizeDelta = new Vector2(0, 48);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 32;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "CURIE'S CRUCIBLE";
            txtTitle.color = UITheme.SelectionGlow;
            AddOutline(titleGo, Color.black, 1.5f);

            GameObject subGo = CreateUIObject("Subtitle", board.transform);
            RectTransform rtSub = subGo.GetComponent<RectTransform>();
            rtSub.anchorMin = new Vector2(0, 1);
            rtSub.anchorMax = new Vector2(1, 1);
            rtSub.pivot = new Vector2(0.5f, 1);
            rtSub.anchoredPosition = new Vector2(0, -78);
            rtSub.sizeDelta = new Vector2(0, 30);
            var txtSub = subGo.AddComponent<Text>();
            txtSub.font = font;
            txtSub.fontSize = 20;
            txtSub.fontStyle = FontStyle.Bold;
            txtSub.alignment = TextAnchor.MiddleCenter;
            txtSub.text = "EQUILIBRIUM";
            txtSub.color = UITheme.BrassBright;

            GameObject tagGo = CreateUIObject("Tagline", board.transform);
            RectTransform rtTag = tagGo.GetComponent<RectTransform>();
            rtTag.anchorMin = new Vector2(0, 1);
            rtTag.anchorMax = new Vector2(1, 1);
            rtTag.pivot = new Vector2(0.5f, 1);
            rtTag.anchoredPosition = new Vector2(0, -112);
            rtTag.sizeDelta = new Vector2(0, 24);
            var txtTag = tagGo.AddComponent<Text>();
            txtTag.font = font;
            txtTag.fontSize = 13;
            txtTag.fontStyle = FontStyle.Italic;
            txtTag.alignment = TextAnchor.MiddleCenter;
            txtTag.text = "A 2.5D Victorian-Steampunk Alchemical Tower Defense";
            txtTag.color = UITheme.ParchmentBorder;

            menu.playButton = CreateMenuButton(board.transform, "Btn_Play", "MULAI EKSPERIMEN (PLAY)", -170, 52, UITheme.BrewButtonNormal, spButton, font);
            menu.tutorialButton = CreateMenuButton(board.transform, "Btn_Tutorial", "PANDUAN / BUKU RESEP", -234, 46, UITheme.BrassBase, spButton, font);
            menu.settingsButton = CreateMenuButton(board.transform, "Btn_Settings", "PENGATURAN SUARA", -292, 46, UITheme.BrassBase, spButton, font);
            menu.creditsButton = CreateMenuButton(board.transform, "Btn_Credits", "KREDIT PERMAINAN", -350, 46, UITheme.BrassBase, spButton, font);

            return menu;
        }

        static PausePanel BuildPausePanel(
            Transform parent,
            UIManager uiManager,
            Sprite spWood,
            Sprite spButton,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Overlay_Pause", parent);
            var pause = overlay.AddComponent<PausePanel>();
            pause.panelRoot = overlay;
            pause.uiManager = uiManager;

            GameObject board = CreateUIObject("Pause_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(460, 340);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -28);
            rtTitle.sizeDelta = new Vector2(0, 36);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 24;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "EKSPERIMEN DIPAUSE";
            txtTitle.color = UITheme.BrassBright;

            pause.resumeButton = CreateMenuButton(board.transform, "Btn_Resume", "LANJUTKAN (RESUME)", -90, 48, UITheme.BrewButtonNormal, spButton, font);
            pause.settingsButton = CreateMenuButton(board.transform, "Btn_Settings", "PENGATURAN", -154, 46, UITheme.BrassBase, spButton, font);
            pause.restartButton = CreateMenuButton(board.transform, "Btn_Restart", "MULAI ULANG (RESTART)", -218, 46, UITheme.ClearButtonNormal, spButton, font);

            overlay.SetActive(false);
            return pause;
        }

        static GameEndPanel BuildGameEndPanel(
            Transform parent,
            Sprite spWood,
            Sprite spButton,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Overlay_Game_End", parent);
            var endPanel = overlay.AddComponent<GameEndPanel>();
            endPanel.panelRoot = overlay;

            GameObject board = CreateUIObject("End_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(580, 380);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -32);
            rtTitle.sizeDelta = new Vector2(0, 40);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 28;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "HASIL PERMAINAN";
            endPanel.headerTitleText = txtTitle;

            GameObject subGo = CreateUIObject("Subtitle", board.transform);
            RectTransform rtSub = subGo.GetComponent<RectTransform>();
            rtSub.anchorMin = new Vector2(0, 1);
            rtSub.anchorMax = new Vector2(1, 1);
            rtSub.pivot = new Vector2(0.5f, 1);
            rtSub.anchoredPosition = new Vector2(0, -80);
            rtSub.sizeDelta = new Vector2(0, 26);
            var txtSub = subGo.AddComponent<Text>();
            txtSub.font = font;
            txtSub.fontSize = 17;
            txtSub.fontStyle = FontStyle.Bold;
            txtSub.alignment = TextAnchor.MiddleCenter;
            txtSub.text = "...";
            endPanel.subtitleText = txtSub;

            GameObject detailGo = CreateUIObject("Detail", board.transform);
            RectTransform rtDetail = detailGo.GetComponent<RectTransform>();
            rtDetail.anchorMin = new Vector2(0, 1);
            rtDetail.anchorMax = new Vector2(1, 1);
            rtDetail.pivot = new Vector2(0.5f, 1);
            rtDetail.anchoredPosition = new Vector2(0, -120);
            rtDetail.sizeDelta = new Vector2(500, 90);
            var txtDetail = detailGo.AddComponent<Text>();
            txtDetail.font = font;
            txtDetail.fontSize = 14;
            txtDetail.lineSpacing = 1.25f;
            txtDetail.alignment = TextAnchor.MiddleCenter;
            txtDetail.text = "...";
            txtDetail.color = UITheme.ParchmentBorder;
            endPanel.resultDetailText = txtDetail;

            endPanel.restartButton = CreateMenuButton(board.transform, "Btn_Restart", "MAIN LAGI (RESTART)", -250, 52, UITheme.BrewButtonNormal, spButton, font);

            overlay.SetActive(false);
            return endPanel;
        }

        static SettingsPopup BuildSettingsPopup(
            Transform parent,
            Sprite spWood,
            Sprite spButton,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Popup_Settings", parent);
            var settings = overlay.AddComponent<SettingsPopup>();
            settings.panelRoot = overlay;

            GameObject board = CreateUIObject("Settings_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(480, 320);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -24);
            rtTitle.sizeDelta = new Vector2(0, 32);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 22;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "PENGATURAN SUARA";
            txtTitle.color = UITheme.BrassBright;

            settings.bgmSlider = CreateSliderRow(board.transform, "BGM Volume", -80, font, out settings.bgmValueText);
            settings.sfxSlider = CreateSliderRow(board.transform, "SFX Volume", -150, font, out settings.sfxValueText);
            settings.closeButton = CreateMenuButton(board.transform, "Btn_Close", "TUTUP", -230, 44, UITheme.BrassBase, spButton, font);

            overlay.SetActive(false);
            return settings;
        }

        static CreditsPopup BuildCreditsPopup(
            Transform parent,
            Sprite spWood,
            Sprite spButton,
            Font font)
        {
            GameObject overlay = CreateModalOverlay("Popup_Credits", parent);
            var credits = overlay.AddComponent<CreditsPopup>();
            credits.panelRoot = overlay;

            GameObject board = CreateUIObject("Credits_Board", overlay.transform);
            RectTransform rtBoard = board.GetComponent<RectTransform>();
            rtBoard.anchorMin = new Vector2(0.5f, 0.5f);
            rtBoard.anchorMax = new Vector2(0.5f, 0.5f);
            rtBoard.pivot = new Vector2(0.5f, 0.5f);
            rtBoard.sizeDelta = new Vector2(620, 440);
            var imgBoard = board.AddComponent<Image>();
            imgBoard.sprite = spWood;
            imgBoard.type = Image.Type.Sliced;
            imgBoard.color = Color.white;

            GameObject titleGo = CreateUIObject("Title", board.transform);
            RectTransform rtTitle = titleGo.GetComponent<RectTransform>();
            rtTitle.anchorMin = new Vector2(0, 1);
            rtTitle.anchorMax = new Vector2(1, 1);
            rtTitle.pivot = new Vector2(0.5f, 1);
            rtTitle.anchoredPosition = new Vector2(0, -24);
            rtTitle.sizeDelta = new Vector2(0, 32);
            var txtTitle = titleGo.AddComponent<Text>();
            txtTitle.font = font;
            txtTitle.fontSize = 22;
            txtTitle.fontStyle = FontStyle.Bold;
            txtTitle.alignment = TextAnchor.MiddleCenter;
            txtTitle.text = "KREDIT & DEDIKASI";
            txtTitle.color = UITheme.BrassBright;

            GameObject bodyGo = CreateUIObject("Body", board.transform);
            RectTransform rtBody = bodyGo.GetComponent<RectTransform>();
            rtBody.anchorMin = new Vector2(0, 0);
            rtBody.anchorMax = new Vector2(1, 1);
            rtBody.offsetMin = new Vector2(36, 80);
            rtBody.offsetMax = new Vector2(-36, -65);
            var txtBody = bodyGo.AddComponent<Text>();
            txtBody.font = font;
            txtBody.fontSize = 13;
            txtBody.lineSpacing = 1.25f;
            txtBody.color = UITheme.ParchmentBg;
            txtBody.supportRichText = true;
            credits.bodyText = txtBody;

            credits.closeButton = CreateMenuButton(board.transform, "Btn_Close", "TUTUP", -360, 44, UITheme.BrassBase, spButton, font);

            overlay.SetActive(false);
            return credits;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        static GameObject CreateModalOverlay(string name, Transform parent)
        {
            GameObject overlay = CreateUIObject(name, parent);
            RectTransform rt = overlay.GetComponent<RectTransform>();
            SetStretchFull(rt);
            var img = overlay.AddComponent<Image>();
            img.color = new Color(0.05f, 0.03f, 0.02f, 0.85f);
            return overlay;
        }

        static Button CreateMenuButton(
            Transform parent,
            string name,
            string text,
            float posY,
            float height,
            Color color,
            Sprite spButton,
            Font font)
        {
            GameObject btnGo = CreateUIObject(name, parent);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, posY);
            rt.sizeDelta = new Vector2(340, height);

            var btn = btnGo.AddComponent<Button>();
            var img = btnGo.AddComponent<Image>();
            img.sprite = spButton;
            img.type = Image.Type.Sliced;
            img.color = color;

            var txt = CreateUIObject("Text", btnGo.transform).AddComponent<Text>();
            SetStretchFull(txt.GetComponent<RectTransform>());
            txt.font = font;
            txt.fontSize = 15;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;
            txt.color = Color.white;

            return btn;
        }

        static Slider CreateSliderRow(
            Transform parent,
            string label,
            float posY,
            Font font,
            out Text valTextOut)
        {
            GameObject row = CreateUIObject("Row_" + label.Replace(" ", ""), parent);
            RectTransform rtRow = row.GetComponent<RectTransform>();
            rtRow.anchorMin = new Vector2(0.5f, 1);
            rtRow.anchorMax = new Vector2(0.5f, 1);
            rtRow.pivot = new Vector2(0.5f, 1);
            rtRow.anchoredPosition = new Vector2(0, posY);
            rtRow.sizeDelta = new Vector2(380, 48);

            var txtLbl = CreateUIObject("Label", row.transform).AddComponent<Text>();
            RectTransform rtLbl = txtLbl.GetComponent<RectTransform>();
            rtLbl.anchorMin = new Vector2(0, 1);
            rtLbl.anchorMax = new Vector2(1, 1);
            rtLbl.pivot = new Vector2(0.5f, 1);
            rtLbl.anchoredPosition = new Vector2(0, 0);
            rtLbl.sizeDelta = new Vector2(0, 20);
            txtLbl.font = font;
            txtLbl.fontSize = 13;
            txtLbl.fontStyle = FontStyle.Bold;
            txtLbl.text = label;
            txtLbl.color = UITheme.BrassBright;

            GameObject sliderGo = CreateUIObject("Slider", row.transform);
            RectTransform rtSlider = sliderGo.GetComponent<RectTransform>();
            rtSlider.anchorMin = new Vector2(0, 0);
            rtSlider.anchorMax = new Vector2(1, 0);
            rtSlider.pivot = new Vector2(0.5f, 0);
            rtSlider.anchoredPosition = new Vector2(-30, 4);
            rtSlider.sizeDelta = new Vector2(-60, 20);

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            GameObject bgGo = CreateUIObject("Background", sliderGo.transform);
            SetStretchFull(bgGo.GetComponent<RectTransform>());
            var imgBg = bgGo.AddComponent<Image>();
            imgBg.color = new Color(0.15f, 0.10f, 0.05f, 0.9f);

            GameObject fillArea = CreateUIObject("Fill Area", sliderGo.transform);
            RectTransform rtFillArea = fillArea.GetComponent<RectTransform>();
            SetStretchFull(rtFillArea);
            rtFillArea.offsetMin = new Vector2(5, 0);
            rtFillArea.offsetMax = new Vector2(-5, 0);

            GameObject fill = CreateUIObject("Fill", fillArea.transform);
            SetStretchFull(fill.GetComponent<RectTransform>());
            var imgFill = fill.AddComponent<Image>();
            imgFill.color = UITheme.BrassBase;
            slider.fillRect = fill.GetComponent<RectTransform>();

            var txtVal = CreateUIObject("ValText", row.transform).AddComponent<Text>();
            RectTransform rtVal = txtVal.GetComponent<RectTransform>();
            rtVal.anchorMin = new Vector2(1, 0);
            rtVal.anchorMax = new Vector2(1, 0);
            rtVal.pivot = new Vector2(1, 0);
            rtVal.anchoredPosition = new Vector2(0, 4);
            rtVal.sizeDelta = new Vector2(50, 20);
            txtVal.font = font;
            txtVal.fontSize = 13;
            txtVal.fontStyle = FontStyle.Bold;
            txtVal.alignment = TextAnchor.MiddleRight;
            txtVal.text = "80%";
            txtVal.color = Color.white;
            valTextOut = txtVal;

            return slider;
        }

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static void SetStretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AddOutline(GameObject go, Color color, float dist)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(dist, -dist);
        }

        static Sprite LoadSprite(string fileName)
        {
            try
            {
                string path = Path.Combine(UISpriteAssetGenerator.ArtFolder, fileName).Replace("\\", "/");
                Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null) return sp;

                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] is Sprite s) return s;
                    }
                }

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UI Generator] Gagal memuat sprite " + fileName + ": " + ex.Message);
            }
            return null;
        }

        static Font GetBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Font[] fonts = Resources.FindObjectsOfTypeAll<Font>();
                if (fonts != null && fonts.Length > 0)
                    font = fonts[0];
            }
            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont("Arial", 14); } catch {}
            }
            return font;
        }
    }
}
