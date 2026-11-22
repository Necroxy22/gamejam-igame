using UnityEngine;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Konfigurasi tampilan Laci Resep (Recipe_Drawer) di sisi kiri Craft_panel.
    /// Semua nilai bisa diubah langsung dari Inspector tanpa mengedit kode.
    /// </summary>
    [System.Serializable]
    public class RecipeDrawerVisualStyle
    {
        [Header("Latar & Bookmark")]
        [Tooltip("Sprite bingkai latar laci. Kosong = kotak warna solid (drawerBackgroundColor).")]
        public Sprite drawerBackgroundSprite;
        public Color drawerBackgroundColor = new Color(0.09f, 0.07f, 0.14f, 0.97f);

        [Tooltip("Sprite gambar bookmark/tab di tepi kiri Craft_panel.")]
        public Sprite bookmarkTabSprite;
        public Color bookmarkTabColor = new Color(0.961f, 0.886f, 0.722f, 1f);

        [Tooltip("Teks pada tab bookmark. '{0}' akan diganti panah arah buka/tutup.")]
        public string bookmarkTabLabelFormat = "RESEP\n{0}";

        [Header("Bingkai Kartu")]
        [Tooltip("Sprite bingkai (9-slice) untuk setiap kartu resep.")]
        public Sprite recipeCardFrameSprite;
        public Color recipeCardFrameColor = new Color(0.22f, 0.15f, 0.34f, 0.96f);

        [Tooltip("Sprite bingkai untuk kartu bahan dasar (sisi kiri panel crafting).")]
        public Sprite ingredientCardFrameSprite;
        public Color ingredientCardFrameColor = new Color(0.14f, 0.17f, 0.23f, 0.95f);

        [Header("Garis Tepi")]
        public Color cardOutlineColor = new Color(0.58f, 0.42f, 0.88f, 0.90f);
        public Vector2 cardOutlineDistance = new Vector2(1.5f, -1.5f);
        public Color bookmarkTabOutlineColor = new Color(0.35f, 0.26f, 0.10f, 0.90f);
    }

    /// <summary>
    /// Satu kartu resep di dalam Laci Resep. Teks & ikon bisa diedit di Inspector.
    /// Sprite ikon TIDAK perlu diisi di sini: sumber tunggalnya adalah
    /// <c>GameplayUIBridge.basePotionSprites</c> / <c>compoundPotionSprites</c>.
    /// </summary>
    [System.Serializable]
    public class RecipeCardData
    {
        [Tooltip("Nama ramuan yang tampil di baris judul kartu.")]
        public string recipeTitle = "";

        [Tooltip("Override opsional. Biarkan kosong agar ikon hasil mengikuti " +
                 "GameplayUIBridge > compoundPotionSprites (sumber tunggal).")]
        public Sprite customPotionIcon;

        [Tooltip("Override opsional untuk ikon Bahan 1. Kosong = ikuti basePotionSprites.")]
        public Sprite customIngredientAIcon;

        [Tooltip("Override opsional untuk ikon Bahan 2. Kosong = ikuti basePotionSprites.")]
        public Sprite customIngredientBIcon;

        [TextArea(2, 4)]
        [Tooltip("Deskripsi ilmiah / reaksi kimia singkat.")]
        public string scientificDescription = "";

        [Tooltip("Target monster yang efektif, mis. 'Efektif: Zombie Api'.")]
        public string effectiveTargetText = "";

        [Header("Warna Teks")]
        public Color titleColor = new Color(1f, 0.88f, 0.48f, 1f);
        public Color descriptionColor = new Color(0.80f, 0.84f, 0.92f, 0.96f);
        public Color targetColor = new Color(0.55f, 1f, 0.62f, 1f);

        [Header("Formula")]
        [Tooltip("Teks panah pada formula. Default '←' (hasil ← bahan1 + bahan2).")]
        public string arrowText = "\u2190";
        public string plusText = "+";
    }

    /// <summary>
    /// Satu kartu bahan dasar. Teks & ikon bisa diedit di Inspector.
    /// </summary>
    [System.Serializable]
    public class IngredientCardData
    {
        [Tooltip("Nama bahan yang tampil di kartu.")]
        public string ingredientTitle = "";

        [Tooltip("Override opsional. Biarkan kosong agar ikon mengikuti " +
                 "GameplayUIBridge > basePotionSprites (sumber tunggal).")]
        public Sprite customIngredientIcon;

        [TextArea(2, 3)]
        [Tooltip("Info kimia singkat (pH, sifat, reaksi).")]
        public string chemicalInfoText = "";

        [Tooltip("Warna tema kartu/ikon bahan ini.")]
        public Color themeColor = Color.white;
    }

    /// <summary>
    /// Data kartu Quick Info untuk satu jenis zombie pada spotlight intro.
    /// Urutan array mengikuti enum ZombieType: Asam, Basa, Api, Besi, Boss.
    /// </summary>
    [System.Serializable]
    public class ZombieIntroCardData
    {
        [Tooltip("Judul kartu (nama & sifat zombie). Kosong = memakai teks bawaan.")]
        public string title = "";

        [Tooltip("Subjudul / nama ilmiah singkat (opsional).")]
        public string subtitle = "";

        [Tooltip("Warna aksen judul. Alpha 0 = pakai warna bawaan zombie.")]
        public Color titleColor = new Color(0f, 0f, 0f, 0f);

        [TextArea(2, 4)]
        [Tooltip("Deskripsi kimia. Kosong = memakai teks bawaan.")]
        public string chemicalDescription = "";

        [TextArea(2, 4)]
        [Tooltip("Teks kelemahan / strategi. Kosong = memakai teks bawaan.")]
        public string weaknessText = "";

        [TextArea(2, 4)]
        [Tooltip("Teks pantangan / risiko mutasi. Kosong = memakai teks bawaan.")]
        public string mutationRiskText = "";

        [Tooltip("Label tombol lanjut. Kosong = 'LANJUT  [SPACE]'.")]
        public string buttonLabel = "";
    }
}
