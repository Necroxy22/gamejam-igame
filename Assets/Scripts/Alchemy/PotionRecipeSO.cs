using UnityEngine;

namespace CuriesCrucible.Alchemy
{
    /// <summary>
    /// Resep alkimia: kombinasi 2 bahan + rentang pH menghasilkan satu jenis ramuan.
    /// Urutan bahan tidak penting (A+B = B+A).
    /// </summary>
    [CreateAssetMenu(
        fileName = "PotionRecipe",
        menuName = "Curie's Crucible/Potion Recipe",
        order = 0)]
    public class PotionRecipeSO : ScriptableObject
    {
        [Header("Hasil")]
        public PotionType potionType = PotionType.GelSabunBeku;

        [Tooltip("Kosongkan untuk memakai nama bawaan dari AlchemyDatabase.")]
        public string displayName = "";

        [TextArea(2, 4)]
        public string description = "";

        [Header("Bahan (urutan bebas)")]
        public IngredientType ingredientA = IngredientType.BahanBasa_Biru;
        public IngredientType ingredientB = IngredientType.BahanPendingin_Cyan;

        [Header("Syarat pH")]
        [Range(0f, 14f)] public float minPH = 0f;
        [Range(0f, 14f)] public float maxPH = 14f;

        [Header("Efek Lempar")]
        public float damage = 12f;
        public float radius = 1.8f;

        [Header("Efek Status")]
        public float rootDuration = 0f;
        [Range(0f, 1f)] public float slowFactor = 1f;
        public float slowDuration = 0f;
        public float knockback = 0f;

        [Tooltip("Pengali damage khusus untuk BossUnstableAmalgam.")]
        public float bossMultiplier = 1f;

        [Header("Visual")]
        public Color color = Color.green;

        public string ResolvedDisplayName
        {
            get
            {
                return string.IsNullOrEmpty(displayName)
                    ? AlchemyDatabase.PotionName(potionType)
                    : displayName;
            }
        }

        /// <summary>Cocok dengan 2 bahan (urutan bebas) dan pH berada di rentang resep.</summary>
        public bool Matches(IngredientType a, IngredientType b, float ph)
        {
            bool ingredientMatch =
                (ingredientA == a && ingredientB == b) ||
                (ingredientA == b && ingredientB == a);

            if (!ingredientMatch)
                return false;

            return ph >= minPH && ph <= maxPH;
        }

        /// <summary>Buat instance ramuan hasil racikan (nilai disalin dari resep).</summary>
        public MixedPotion CreatePotion()
        {
            return new MixedPotion
            {
                type = potionType,
                displayName = ResolvedDisplayName,
                color = color,
                damage = damage,
                radius = radius,
                rootDuration = rootDuration,
                slowFactor = slowFactor,
                slowDuration = slowDuration,
                knockback = knockback,
                bossMultiplier = bossMultiplier
            };
        }
    }
}