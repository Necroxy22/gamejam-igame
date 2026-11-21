using System;
using System.Collections.Generic;
using CuriesCrucible.Arena25D;
using UnityEngine;

namespace CuriesCrucible.Alchemy
{
    /// <summary>
    /// Inti sistem alkimia: 2 slot kuali, meteran pH (0-14, netral 7),
    /// stok bahan dasar, tas ramuan (maks 6), dan integrasi langsung ke ArenaThrowTargeting.
    ///
    /// Kontrol default (bisa diganti lewat UI):
    ///   1..4  -> masukkan bahan ke kuali
    ///   R     -> brew (racik)
    ///   Q / E -> pilih ramuan sebelumnya / berikutnya
    ///   F     -> kosongkan kuali (bahan dikembalikan)
    ///   F1    -> toggle overlay status
    /// </summary>
    [DisallowMultipleComponent]
    public class AlchemyManager : MonoBehaviour
    {
        public static AlchemyManager Instance { get; private set; }

        [Header("Kuali")]
        public bool slot1Filled;
        public IngredientType slot1 = IngredientType.BahanBasa_Biru;

        public bool slot2Filled;
        public IngredientType slot2 = IngredientType.BahanPendingin_Cyan;

        [Header("Stok Bahan Dasar")]
        [Tooltip("0=Basa, 1=Pendingin, 2=Asam, 3=Katalis")]
        public int[] ingredientStock = { 3, 3, 3, 3 };

        [Tooltip("Dipakai oleh RefillStock() untuk mengisi ulang stok awal.")]
        public int startingStockPerIngredient = 3;

        [Tooltip("Batas maksimal stok per bahan dasar.")]
        public int maxStockPerIngredient = 6;

        [Header("Tas Ramuan")]
        public List<MixedPotion> mixedPotions = new List<MixedPotion>();
        public int maxPotionSlots = 6;
        public int selectedIndex;

        [Header("Resep")]
        [Tooltip("Isi otomatis dari Assets/Data/Recipes saat menu Generate dijalankan. " +
                 "Kalau kosong, resep default dibuat saat runtime.")]
        public List<PotionRecipeSO> recipes = new List<PotionRecipeSO>();

        [Header("Integrasi")]
        [Tooltip("ArenaThrowTargeting yang akan menerima stok ramuan.")]
        public ArenaThrowTargeting targeting;

        [Header("Overlay")]
        public bool showDebugOverlay = true;

        /// <summary>Tampilkan buku resep (F2) alih-alih panel utama.</summary>
        public bool showRecipeBook = false;

        /// <summary>Terpanggil setiap ada pesan untuk pemain (bisa dihubungkan ke UI).</summary>
        public event Action<string> OnMessage;

        /// <summary>Terpanggil saat daftar ramuan berubah.</summary>
        public event Action OnInventoryChanged;

        string _lastMessage = "";
        float _messageUntil;

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        public float CurrentPH
        {
            get
            {
                float ph = AlchemyDatabase.NeutralPH;
                if (slot1Filled) ph += AlchemyDatabase.PHModifier(slot1);
                if (slot2Filled) ph += AlchemyDatabase.PHModifier(slot2);
                return Mathf.Clamp(ph, 0f, 14f);
            }
        }

        public int PotionCount
        {
            get { return mixedPotions != null ? mixedPotions.Count : 0; }
        }

        public bool HasPotion
        {
            get { return PotionCount > 0; }
        }

        public MixedPotion SelectedPotion
        {
            get
            {
                if (mixedPotions == null || mixedPotions.Count == 0)
                    return null;

                selectedIndex = Mathf.Clamp(selectedIndex, 0, mixedPotions.Count - 1);
                return mixedPotions[selectedIndex];
            }
        }

        public float SelectedRadius
        {
            get
            {
                MixedPotion potion = SelectedPotion;
                return potion != null ? potion.radius : 1.7f;
            }
        }

        public float SelectedDamage
        {
            get
            {
                MixedPotion potion = SelectedPotion;
                return potion != null ? potion.damage : 0f;
            }
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureStock();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            EnsureStock();
            EnsureRecipes();
        }

        void Update()
        {
            HandleInput();
        }

        // ------------------------------------------------------------------
        // Inisialisasi
        // ------------------------------------------------------------------

        void EnsureStock()
        {
            if (ingredientStock == null || ingredientStock.Length < 4)
            {
                ingredientStock = new int[4];

                for (int i = 0; i < ingredientStock.Length; i++)
                    ingredientStock[i] = Mathf.Max(0, startingStockPerIngredient);
            }
        }

        /// <summary>Isi ulang stok bahan dasar ke nilai awal (mis. untuk restart).</summary>
        public void RefillStock()
        {
            if (ingredientStock == null || ingredientStock.Length < 4)
                ingredientStock = new int[4];

            for (int i = 0; i < ingredientStock.Length; i++)
                ingredientStock[i] = Mathf.Max(0, startingStockPerIngredient);
        }

        void EnsureRecipes()
        {
            if (recipes != null && recipes.Count > 0)
                return;

            recipes = RecipeLibrary.CreateDefaultRecipes();
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        void HandleInput()
        {
            // Abaikan input jika waktu dibekukan atau spotlight zombie baru sedang aktif
            if (Time.timeScale <= 0f || CuriesCrucible.Gameplay.ZombieIntroSpotlight.IsSpotlightActive)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                AddIngredient(IngredientType.BahanBasa_Biru);

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                AddIngredient(IngredientType.BahanPendingin_Cyan);

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                AddIngredient(IngredientType.BahanAsam_Merah);

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                AddIngredient(IngredientType.BahanKatalis_Kuning);

            if (Input.GetKeyDown(KeyCode.R))
                Brew();

            if (Input.GetKeyDown(KeyCode.Q))
                SelectPrevious();

            if (Input.GetKeyDown(KeyCode.E))
                SelectNext();

            if (Input.GetKeyDown(KeyCode.F))
                ClearCauldron(true);

            if (Input.GetKeyDown(KeyCode.F1))
                showDebugOverlay = !showDebugOverlay;

            if (Input.GetKeyDown(KeyCode.F2))
                showRecipeBook = !showRecipeBook;
        }

        // ------------------------------------------------------------------
        // Kuali
        // ------------------------------------------------------------------

        /// <summary>Masukkan bahan ke slot kosong berikutnya. Mengurangi stok bahan.</summary>
        public bool AddIngredient(IngredientType type)
        {
            EnsureStock();

            int index = (int)type;
            if (index < 0 || index >= ingredientStock.Length)
                return false;

            if (ingredientStock[index] <= 0)
            {
                Notify("Stok " + AlchemyDatabase.IngredientName(type) + " habis");
                return false;
            }

            if (!slot1Filled)
            {
                slot1 = type;
                slot1Filled = true;
            }
            else if (!slot2Filled)
            {
                slot2 = type;
                slot2Filled = true;
            }
            else
            {
                Notify("Kuali penuh - brew atau kosongkan dulu (F)");
                return false;
            }

            ingredientStock[index]--;
            Notify("Menambahkan " + AlchemyDatabase.IngredientName(type) + " ke kuali");
            return true;
        }

        /// <summary>Racik isi kuali menjadi ramuan.</summary>
        public bool Brew()
        {
            EnsureRecipes();

            if (!slot1Filled || !slot2Filled)
            {
                Notify("Isi kedua slot kuali terlebih dahulu");
                return false;
            }

            float ph = CurrentPH;
            PotionRecipeSO recipe = FindRecipe(slot1, slot2, ph);

            if (recipe == null)
            {
                Notify("Tidak ada resep untuk kombinasi ini (pH " + ph.ToString("0.0") + ")");
                return false;
            }

            if (mixedPotions.Count >= maxPotionSlots)
            {
                Notify("Tas ramuan penuh (" + maxPotionSlots + " slot)");
                return false;
            }

            MixedPotion potion = recipe.CreatePotion();
            mixedPotions.Add(potion);
            selectedIndex = mixedPotions.Count - 1;

            ClearCauldron(false);
            Notify("Berhasil meracik " + potion.displayName + "!");
            FireInventoryChanged();
            return true;
        }

        public PotionRecipeSO FindRecipe(IngredientType a, IngredientType b, float ph)
        {
            if (recipes == null)
                return null;

            for (int i = 0; i < recipes.Count; i++)
            {
                PotionRecipeSO recipe = recipes[i];
                if (recipe == null)
                    continue;

                if (recipe.Matches(a, b, ph))
                    return recipe;
            }

            return null;
        }

        /// <summary>Kosongkan kuali. Refund = true berarti bahan dikembalikan ke stok.</summary>
        public void ClearCauldron(bool refund)
        {
            EnsureStock();

            if (refund)
            {
                if (slot1Filled)
                    ingredientStock[(int)slot1]++;
                if (slot2Filled)
                    ingredientStock[(int)slot2]++;
            }

            slot1Filled = false;
            slot2Filled = false;
        }

        // ------------------------------------------------------------------
        // Tas ramuan
        // ------------------------------------------------------------------

        /// <summary>Ambil & hapus ramuan terpilih (dipakai ArenaThrowTargeting saat melempar).</summary>
        public bool TryConsumeSelected(out MixedPotion potion)
        {
            potion = null;

            if (mixedPotions == null || mixedPotions.Count == 0)
            {
                Notify("Tidak ada ramuan - racik dulu di kuali (1-4 lalu R)");
                return false;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, mixedPotions.Count - 1);
            potion = mixedPotions[selectedIndex];

            mixedPotions.RemoveAt(selectedIndex);

            if (mixedPotions.Count == 0)
                selectedIndex = 0;
            else
                selectedIndex = Mathf.Clamp(selectedIndex, 0, mixedPotions.Count - 1);

            FireInventoryChanged();
            return true;
        }

        public bool AddPotion(MixedPotion potion)
        {
            if (potion == null)
                return false;

            if (mixedPotions == null)
                mixedPotions = new List<MixedPotion>();

            if (mixedPotions.Count >= maxPotionSlots)
                return false;

            mixedPotions.Add(potion);
            selectedIndex = mixedPotions.Count - 1;
            FireInventoryChanged();
            return true;
        }

        /// <summary>Hadiah wave: tambah N ramuan acak dari resep yang tersedia.</summary>
        public int AddRandomPotions(int amount)
        {
            EnsureRecipes();

            if (mixedPotions == null)
                mixedPotions = new List<MixedPotion>();

            int added = 0;

            for (int i = 0; i < amount; i++)
            {
                if (mixedPotions.Count >= maxPotionSlots)
                    break;

                PotionRecipeSO recipe = PickRandomRecipe();
                if (recipe == null)
                    break;

                mixedPotions.Add(recipe.CreatePotion());
                added++;
            }

            if (added > 0)
            {
                selectedIndex = mixedPotions.Count - 1;
                FireInventoryChanged();
                Notify("Menerima " + added + " ramuan hadiah wave");
            }

            return added;
        }

        PotionRecipeSO PickRandomRecipe()
        {
            if (recipes == null || recipes.Count == 0)
                return null;

            for (int attempt = 0; attempt < recipes.Count; attempt++)
            {
                PotionRecipeSO recipe = recipes[UnityEngine.Random.Range(0, recipes.Count)];
                if (recipe != null)
                    return recipe;
            }

            return null;
        }

        public void SelectNext()
        {
            if (mixedPotions == null || mixedPotions.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % mixedPotions.Count;
        }

        public void SelectPrevious()
        {
            if (mixedPotions == null || mixedPotions.Count == 0)
                return;

            selectedIndex--;
            if (selectedIndex < 0)
                selectedIndex = mixedPotions.Count - 1;
        }

        // ------------------------------------------------------------------
        // Stok bahan (drop zombie)
        // ------------------------------------------------------------------

        /// <summary>Tambah bahan dasar acak (hasil drop zombie).</summary>
        public IngredientType AddRandomIngredient(int amount)
        {
            EnsureStock();

            IngredientType last = IngredientType.BahanBasa_Biru;

            for (int i = 0; i < amount; i++)
            {
                int index = UnityEngine.Random.Range(0, ingredientStock.Length);
                ingredientStock[index]++;
                last = (IngredientType)index;
            }

            if (amount > 0)
                Notify("Drop: +" + amount + " " + AlchemyDatabase.IngredientName(last));

            return last;
        }

        /// <summary>Cek apakah stok bahan dasar tertentu belum mencapai batas maksimal.</summary>
        public bool CanAddIngredient(IngredientType type)
        {
            EnsureStock();
            int index = (int)type;
            if (index < 0 || index >= ingredientStock.Length) return false;
            return ingredientStock[index] < maxStockPerIngredient;
        }

        /// <summary>Tambah bahan langsung dari hasil panen pipa menara.</summary>
        public bool HarvestIngredient(IngredientType type, int amount = 1)
        {
            EnsureStock();
            int index = (int)type;
            if (index < 0 || index >= ingredientStock.Length) return false;

            if (ingredientStock[index] >= maxStockPerIngredient)
            {
                Notify("Stok " + AlchemyDatabase.IngredientName(type) + " sudah penuh (" + maxStockPerIngredient + ")");
                return false;
            }

            ingredientStock[index] = Mathf.Min(maxStockPerIngredient, ingredientStock[index] + amount);
            Notify("Panen: +" + amount + " " + AlchemyDatabase.IngredientName(type));
            FireInventoryChanged();
            return true;
        }

        public void AddIngredientDirect(IngredientType type)
        {
            EnsureStock();
            ingredientStock[(int)type]++;
            FireInventoryChanged();
        }

        // ------------------------------------------------------------------
        // Helper
        // ------------------------------------------------------------------

        public void FireInventoryChanged()
        {
            if (OnInventoryChanged != null)
                OnInventoryChanged();
        }

        void Notify(string message)
        {
            _lastMessage = message;
            _messageUntil = Time.time + 3f;

            if (OnMessage != null)
                OnMessage(message);

            Debug.Log("[Alchemy] " + message);
        }

        void DrawMessage()
        {
            if (string.IsNullOrEmpty(_lastMessage) || Time.time >= _messageUntil)
                return;

            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            style.fontSize = 15;
            style.alignment = TextAnchor.MiddleCenter;

            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.Label(
                new Rect(160f, Screen.height - 54f, Screen.width - 320f, 30f),
                _lastMessage,
                style);
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // Overlay debug (bisa dimatikan di Inspector)
        // ------------------------------------------------------------------

        void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            DrawMessage();

            if (showRecipeBook)
            {
                DrawRecipeBook();
                return;
            }

            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            style.fontSize = 13;

            GUILayout.BeginArea(new Rect(10f, 10f, 620f, 250f), GUI.skin.box);

            GUILayout.Label("<b>ALCHEMY</b>", style);
            GUILayout.Label("pH: <b>" + CurrentPH.ToString("0.0") + "</b> / 14.0   (netral 7.0)", style);

            string slotText = "Kuali: <color=#9fe>[ " +
                              (slot1Filled ? AlchemyDatabase.IngredientName(slot1) : "kosong") +
                              " ]</color>  +  <color=#9fe>[ " +
                              (slot2Filled ? AlchemyDatabase.IngredientName(slot2) : "kosong") +
                              " ]</color>";
            GUILayout.Label(slotText, style);

            string stockText = "Stok: ";
            for (int i = 0; i < ingredientStock.Length && i < 4; i++)
            {
                var type = (IngredientType)i;
                stockText += "<color=#" + ColorUtility.ToHtmlStringRGB(AlchemyDatabase.IngredientColor(type)) + ">" +
                             (i + 1) + ":" + AlchemyDatabase.IngredientName(type) + "=" + ingredientStock[i] +
                             "</color>   ";
            }
            GUILayout.Label(stockText, style);

            GUILayout.Label("Ramuan (" + PotionCount + "/" + maxPotionSlots + "):", style);

            if (PotionCount == 0)
            {
                GUILayout.Label("  <i>(kosong)</i>", style);
            }
            else
            {
                for (int i = 0; i < mixedPotions.Count; i++)
                {
                    string marker = i == selectedIndex ? "â–¶ " : "   ";
                    GUILayout.Label(marker + (i + 1) + ". " + mixedPotions[i].displayName, style);
                }
            }

            GUILayout.Label("<i>1-4 bahan  |  R brew  |  Q/E pilih  |  F kosongkan  |  F1 overlay  |  F2 resep</i>", style);
            GUILayout.Label("<i>A/D geser Marie  |  Klik kiri di jalan = lempar ramuan terpilih</i>", style);

            GUILayout.EndArea();
        }

        void DrawRecipeBook()
        {
            EnsureRecipes();

            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            style.fontSize = 13;

            GUILayout.BeginArea(new Rect(10f, 10f, 620f, 320f), GUI.skin.box);

            GUILayout.Label("<b>BUKU RESEP</b>  <i>(F2 untuk kembali)</i>", style);
            GUILayout.Label("pH = 7.0 + modifier bahan. Asam -4.5, Basa +4.5, Pendingin/Katalis 0.", style);

            if (recipes != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    PotionRecipeSO recipe = recipes[i];
                    if (recipe == null)
                        continue;

                    string color = ColorUtility.ToHtmlStringRGB(AlchemyDatabase.PotionColor(recipe.potionType));

                    GUILayout.Label(
                        "<color=#" + color + ">â– </color> <b>" + recipe.ResolvedDisplayName + "</b>" +
                        " = " + AlchemyDatabase.IngredientName(recipe.ingredientA) +
                        " + " + AlchemyDatabase.IngredientName(recipe.ingredientB) +
                        "   <i>(pH " + recipe.minPH.ToString("0.0") + "-" + recipe.maxPH.ToString("0.0") +
                        ", DMG " + recipe.damage.ToString("0") + ")</i>",
                        style);
                }
            }

            GUILayout.EndArea();
        }
    }

    /// <summary>Resep default (dipakai kalau tidak ada PotionRecipeSO yang di-assign).</summary>
    public static class RecipeLibrary
    {
        public static List<PotionRecipeSO> CreateDefaultRecipes()
        {
            var list = new List<PotionRecipeSO>
            {
                CreateRecipe(
                    PotionType.GelSabunBeku,
                    IngredientType.BahanBasa_Biru,
                    IngredientType.BahanPendingin_Cyan,
                    11.0f, 12.0f,
                    12f, 1.8f,
                    3f, 0.55f, 3f, 0f,
                    1.0f,
                    "Melapisi zombie dengan gel beku: root 3 detik + lambat."),

                CreateRecipe(
                    PotionType.KabutAsamDingin,
                    IngredientType.BahanAsam_Merah,
                    IngredientType.BahanPendingin_Cyan,
                    2.0f, 3.0f,
                    18f, 2.2f,
                    0f, 0.60f, 3f, 0f,
                    0.5f,
                    "Kabut asam dingin: damage area + lambat, bagus vs zombie basa."),

                CreateRecipe(
                    PotionType.LedakanEsInstan,
                    IngredientType.BahanPendingin_Cyan,
                    IngredientType.BahanKatalis_Kuning,
                    6.5f, 7.5f,
                    30f, 2.0f,
                    2f, 0.8f, 1.5f, 0f,
                    0.5f,
                    "Ledakan kristal es: damage besar + root singkat."),

                CreateRecipe(
                    PotionType.AsamPelarutKarat,
                    IngredientType.BahanAsam_Merah,
                    IngredientType.BahanKatalis_Kuning,
                    2.0f, 3.0f,
                    24f, 1.7f,
                    0f, 1f, 0f, 0f,
                    0.5f,
                    "Asam pekat pelarut karat: sangat efektif vs ZombieBesi."),

                CreateRecipe(
                    PotionType.BomNetralisasi,
                    IngredientType.BahanAsam_Merah,
                    IngredientType.BahanBasa_Biru,
                    6.5f, 7.5f,
                    26f, 2.4f,
                    0f, 1f, 0f, 0.6f,
                    4.0f,
                    "Netralisasi asam-basa: damage besar, kunci tahap kedua Boss."),

                CreateRecipe(
                    PotionType.BusaSabunMengembang,
                    IngredientType.BahanBasa_Biru,
                    IngredientType.BahanKatalis_Kuning,
                    11.0f, 12.0f,
                    10f, 2.2f,
                    0f, 0.7f, 2.5f, 1.2f,
                    1.0f,
                    "Busa sabun mengembang: knockback + lambat, bagus vs ZombieAsam.")
            };

            return list;
        }

        static PotionRecipeSO CreateRecipe(
            PotionType type,
            IngredientType a,
            IngredientType b,
            float minPH,
            float maxPH,
            float damage,
            float radius,
            float rootDuration,
            float slowFactor,
            float slowDuration,
            float knockback,
            float bossMultiplier,
            string description)
        {
            var recipe = ScriptableObject.CreateInstance<PotionRecipeSO>();
            recipe.potionType = type;
            recipe.ingredientA = a;
            recipe.ingredientB = b;
            recipe.minPH = minPH;
            recipe.maxPH = maxPH;
            recipe.damage = damage;
            recipe.radius = radius;
            recipe.rootDuration = rootDuration;
            recipe.slowFactor = slowFactor;
            recipe.slowDuration = slowDuration;
            recipe.knockback = knockback;
            recipe.bossMultiplier = bossMultiplier;
            recipe.color = AlchemyDatabase.PotionColor(type);
            recipe.description = description;
            return recipe;
        }
    }
}


