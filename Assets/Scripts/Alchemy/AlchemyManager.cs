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

        [Header("Hotbar 6 Slot Tetap")]
        [Tooltip("Stok masing-masing dari 6 slot ramuan tetap.")]
        public int[] potionStock = new int[6];
        public int selectedSlotIndex = 0;

        public static readonly PotionType[] FixedSlotPotions = new PotionType[6]
        {
            PotionType.GelSabunBeku,        // Slot 0 (Key 1): Basa + Pendingin
            PotionType.KabutAsamDingin,      // Slot 1 (Key 2): Asam + Pendingin
            PotionType.LedakanEsInstan,      // Slot 2 (Key 3): Pendingin + Katalis
            PotionType.AsamPelarutKarat,     // Slot 3 (Key 4): Asam + Katalis
            PotionType.BomNetralisasi,       // Slot 4 (Key 5): Asam + Basa
            PotionType.BusaSabunMengembang   // Slot 5 (Key 6): Basa + Katalis
        };

        [Header("Tas Ramuan")]
        public List<MixedPotion> mixedPotions = new List<MixedPotion>();
        public int maxPotionSlots = 6;
        public int selectedIndex
        {
            get { return selectedSlotIndex; }
            set { selectedSlotIndex = Mathf.Clamp(value, 0, 5); }
        }

        [Header("Resep")]
        [Tooltip("Isi otomatis dari Assets/Data/Recipes saat menu Generate dijalankan. " +
                 "Kalau kosong, resep default dibuat saat runtime.")]
        public List<PotionRecipeSO> recipes = new List<PotionRecipeSO>();

        [Header("Integrasi")]
        [Tooltip("ArenaThrowTargeting yang akan menerima stok ramuan.")]
        public ArenaThrowTargeting targeting;

        [Tooltip("Jika false, GameplayUIBridge mengambil alih input keyboard (1-6 pilih ramuan, Q/E, R, F).")]
        public bool keyboardInputEnabled = true;

        /// <summary>Terpanggil setiap ada pesan untuk pemain (bisa dihubungkan ke UI).</summary>
        public event Action<string> OnMessage;

        /// <summary>Terpanggil saat daftar ramuan berubah.</summary>
        public event Action OnInventoryChanged;

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
            get
            {
                EnsurePotionStock();
                int total = 0;
                for (int i = 0; i < potionStock.Length; i++) total += potionStock[i];
                return total;
            }
        }

        public bool HasPotion
        {
            get
            {
                EnsurePotionStock();
                selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, 5);
                return potionStock[selectedSlotIndex] > 0;
            }
        }

        public MixedPotion SelectedPotion
        {
            get
            {
                selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, 5);
                return CreatePotionInstance(FixedSlotPotions[selectedSlotIndex]);
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

            EnsurePotionStock();
        }

        public void EnsurePotionStock()
        {
            if (potionStock == null || potionStock.Length < 6)
            {
                int[] old = potionStock;
                potionStock = new int[6];
                if (old != null)
                {
                    for (int i = 0; i < old.Length && i < 6; i++)
                        potionStock[i] = old[i];
                }
            }
        }

        /// <summary>Isi ulang stok bahan dasar & ramuan ke nilai awal (mis. untuk restart).</summary>
        public void RefillStock()
        {
            if (ingredientStock == null || ingredientStock.Length < 4)
                ingredientStock = new int[4];

            for (int i = 0; i < ingredientStock.Length; i++)
                ingredientStock[i] = Mathf.Max(0, startingStockPerIngredient);

            EnsurePotionStock();
            for (int i = 0; i < potionStock.Length; i++)
                potionStock[i] = 0;

            SyncMixedPotionsList();
            FireInventoryChanged();
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
            // Jika UI Bridge aktif, ia mengambil alih seluruh input keyboard.
            if (!keyboardInputEnabled)
                return;

            // Abaikan input jika waktu dibekukan atau spotlight zombie baru sedang aktif
            if (Time.timeScale <= 0f || CuriesCrucible.Gameplay.ZombieIntroSpotlight.IsSpotlightActive)
                return;

            // 1-6: Pilih slot ramuan aktif
            for (int i = 0; i < 6; i++)
            {
                KeyCode num = (KeyCode)((int)KeyCode.Alpha1 + i);
                KeyCode pad = (KeyCode)((int)KeyCode.Keypad1 + i);
                if (Input.GetKeyDown(num) || Input.GetKeyDown(pad))
                    SelectSlot(i);
            }

            // R: Racik 1x resep dari slot terpilih
            if (Input.GetKeyDown(KeyCode.R))
                BrewSelectedSlot();

            if (Input.GetKeyDown(KeyCode.Q))
                SelectPrevious();

            if (Input.GetKeyDown(KeyCode.E))
                SelectNext();

            if (Input.GetKeyDown(KeyCode.F))
                ClearCauldron(true);
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

            // Masukkan juga ke slot Hotbar tetap yang sesuai
            EnsurePotionStock();
            for (int i = 0; i < FixedSlotPotions.Length; i++)
            {
                if (FixedSlotPotions[i] == recipe.potionType)
                {
                    potionStock[i]++;
                    selectedSlotIndex = i;
                    break;
                }
            }

            SyncMixedPotionsList();
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

        /// <summary>Keluarkan bahan dari slot kuali (0 = slot1, 1 = slot2) dan kembalikan ke stok.</summary>
        public bool RemoveIngredientFromSlot(int slot)
        {
            EnsureStock();
            if (slot == 0 && slot1Filled)
            {
                ingredientStock[(int)slot1]++;
                slot1Filled = false;
                FireInventoryChanged();
                return true;
            }
            if (slot == 1 && slot2Filled)
            {
                ingredientStock[(int)slot2]++;
                slot2Filled = false;
                FireInventoryChanged();
                return true;
            }
            return false;
        }

        /// <summary>Memasang bahan ke slot kuali tertentu (0 = slot1, 1 = slot2). Jika slot sudah terisi, bahan lama di-refund.</summary>
        public bool SetCauldronSlot(int slot, IngredientType type)
        {
            EnsureStock();
            int idx = (int)type;
            if (idx < 0 || idx >= ingredientStock.Length || ingredientStock[idx] <= 0)
            {
                Notify("Stok " + AlchemyDatabase.IngredientName(type) + " habis");
                return false;
            }

            if (slot == 0)
            {
                if (slot1Filled)
                    ingredientStock[(int)slot1]++;
                slot1 = type;
                slot1Filled = true;
                ingredientStock[idx]--;
                Notify("Memasang " + AlchemyDatabase.IngredientName(type) + " di Slot 1");
                FireInventoryChanged();
                return true;
            }
            else if (slot == 1)
            {
                if (slot2Filled)
                    ingredientStock[(int)slot2]++;
                slot2 = type;
                slot2Filled = true;
                ingredientStock[idx]--;
                Notify("Memasang " + AlchemyDatabase.IngredientName(type) + " di Slot 2");
                FireInventoryChanged();
                return true;
            }
            return false;
        }

        /// <summary>Membuang 1x bahan langsung dari stok pemain.</summary>
        public bool DiscardFromStock(IngredientType type)
        {
            EnsureStock();
            int idx = (int)type;
            if (idx >= 0 && idx < ingredientStock.Length && ingredientStock[idx] > 0)
            {
                ingredientStock[idx]--;
                Notify("Membuang 1x " + AlchemyDatabase.IngredientName(type));
                FireInventoryChanged();
                return true;
            }
            return false;
        }

        /// <summary>Membuang bahan dari slot kuali tanpa refund ke stok.</summary>
        public bool DiscardFromCauldron(int slot)
        {
            EnsureStock();
            if (slot == 0 && slot1Filled)
            {
                IngredientType t = slot1;
                slot1Filled = false;
                Notify("Membuang 1x " + AlchemyDatabase.IngredientName(t));
                FireInventoryChanged();
                return true;
            }
            else if (slot == 1 && slot2Filled)
            {
                IngredientType t = slot2;
                slot2Filled = false;
                Notify("Membuang 1x " + AlchemyDatabase.IngredientName(t));
                FireInventoryChanged();
                return true;
            }
            return false;
        }

        /// <summary>Pindah atau tukar isi slot kuali antar slot1 dan slot2.</summary>
        public void SwapCauldronSlots()
        {
            if (!slot1Filled && !slot2Filled) return;
            IngredientType tempType = slot1;
            bool tempFilled = slot1Filled;
            slot1 = slot2;
            slot1Filled = slot2Filled;
            slot2 = tempType;
            slot2Filled = tempFilled;
            FireInventoryChanged();
        }

        // ------------------------------------------------------------------
        // Hotbar 6 Slot Tetap & Racik Langsung (HUD Minimalis)
        // ------------------------------------------------------------------

        public static void GetSlotIngredients(int slot, out IngredientType a, out IngredientType b)
        {
            switch (slot)
            {
                case 0: a = IngredientType.BahanBasa_Biru; b = IngredientType.BahanPendingin_Cyan; break;     // Gel Sabun Beku
                case 1: a = IngredientType.BahanAsam_Merah; b = IngredientType.BahanPendingin_Cyan; break;    // Kabut Asam Dingin
                case 2: a = IngredientType.BahanPendingin_Cyan; b = IngredientType.BahanKatalis_Kuning; break; // Ledakan Es Instan
                case 3: a = IngredientType.BahanAsam_Merah; b = IngredientType.BahanKatalis_Kuning; break;    // Asam Pelarut Karat
                case 4: a = IngredientType.BahanAsam_Merah; b = IngredientType.BahanBasa_Biru; break;         // Bom Netralisasi
                case 5: a = IngredientType.BahanBasa_Biru; b = IngredientType.BahanKatalis_Kuning; break;     // Busa Sabun Mengembang
                default: a = IngredientType.BahanBasa_Biru; b = IngredientType.BahanPendingin_Cyan; break;
            }
        }

        /// <summary>Racik 1x resep dari slot 1-6 yang sedang dipilih.</summary>
        public bool BrewSelectedSlot()
        {
            return BrewSlot(selectedSlotIndex);
        }

        /// <summary>Racik 1x resep slot tertentu (0-5) jika stok kedua bahan dasar mencukupi.</summary>
        public bool BrewSlot(int slot)
        {
            if (slot < 0 || slot >= 6) return false;

            EnsureStock();
            EnsurePotionStock();

            IngredientType a, b;
            GetSlotIngredients(slot, out a, out b);

            int idxA = (int)a;
            int idxB = (int)b;

            if (ingredientStock[idxA] < 1 || ingredientStock[idxB] < 1)
            {
                string pName = AlchemyDatabase.PotionName(FixedSlotPotions[slot]);
                Notify("Bahan tidak cukup untuk meracik " + pName + "! Butuh " +
                    AlchemyDatabase.IngredientName(a) + " & " + AlchemyDatabase.IngredientName(b));
                return false;
            }

            ingredientStock[idxA]--;
            ingredientStock[idxB]--;
            potionStock[slot]++;

            SyncMixedPotionsList();

            string brewedName = AlchemyDatabase.PotionName(FixedSlotPotions[slot]);
            Notify("Berhasil meracik " + brewedName + "! (Stok: " + potionStock[slot] + "x)");
            FireInventoryChanged();
            return true;
        }

        /// <summary>Ambil & konsumsi 1x ramuan dari slot terpilih (dipakai ArenaThrowTargeting saat melempar).</summary>
        public bool TryConsumeSelected(out MixedPotion potion)
        {
            potion = null;
            EnsurePotionStock();

            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, 5);

            if (potionStock[selectedSlotIndex] <= 0)
            {
                string pName = AlchemyDatabase.PotionName(FixedSlotPotions[selectedSlotIndex]);
                Notify("Stok " + pName + " habis (0x)! Tekan R untuk meracik.");
                return false;
            }

            potionStock[selectedSlotIndex]--;
            potion = CreatePotionInstance(FixedSlotPotions[selectedSlotIndex]);

            SyncMixedPotionsList();
            FireInventoryChanged();
            return true;
        }

        public MixedPotion CreatePotionInstance(PotionType type)
        {
            EnsureRecipes();
            if (recipes != null)
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    if (recipes[i] != null && recipes[i].potionType == type)
                        return recipes[i].CreatePotion();
                }
            }

            return new MixedPotion
            {
                type = type,
                displayName = AlchemyDatabase.PotionName(type),
                color = AlchemyDatabase.PotionColor(type),
                damage = 25f,
                radius = 1.8f
            };
        }

        public void SyncMixedPotionsList()
        {
            if (mixedPotions == null)
                mixedPotions = new List<MixedPotion>();
            else
                mixedPotions.Clear();

            EnsurePotionStock();
            for (int i = 0; i < 6; i++)
            {
                for (int count = 0; count < potionStock[i]; count++)
                {
                    mixedPotions.Add(CreatePotionInstance(FixedSlotPotions[i]));
                }
            }
        }

        public bool AddPotion(MixedPotion potion)
        {
            if (potion == null)
                return false;

            EnsurePotionStock();
            int slot = (int)potion.type;
            if (slot >= 0 && slot < 6)
            {
                potionStock[slot]++;
                selectedSlotIndex = slot;
                SyncMixedPotionsList();
                FireInventoryChanged();
                return true;
            }

            return false;
        }

        /// <summary>Hadiah wave: tambah N ramuan acak dari 6 slot ramuan tetap.</summary>
        public int AddRandomPotions(int amount)
        {
            EnsurePotionStock();
            for (int i = 0; i < amount; i++)
            {
                int r = UnityEngine.Random.Range(0, 6);
                potionStock[r]++;
            }

            SyncMixedPotionsList();
            FireInventoryChanged();
            Notify("Menerima " + amount + " ramuan hadiah wave");
            return amount;
        }

        public void SelectSlot(int index)
        {
            selectedSlotIndex = Mathf.Clamp(index, 0, 5);
            FireInventoryChanged();
        }

        public void SelectNext()
        {
            selectedSlotIndex = (selectedSlotIndex + 1) % 6;
            FireInventoryChanged();
        }

        public void SelectPrevious()
        {
            selectedSlotIndex = (selectedSlotIndex - 1 + 6) % 6;
            FireInventoryChanged();
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
            if (OnMessage != null)
                OnMessage(message);

            Debug.Log("[Alchemy] " + message);
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


