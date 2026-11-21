using System;
using UnityEngine;

namespace CuriesCrucible.Alchemy
{
    /// <summary>Empat bahan dasar di kuali.</summary>
    public enum IngredientType
    {
        BahanBasa_Biru = 0,
        BahanPendingin_Cyan = 1,
        BahanAsam_Merah = 2,
        BahanKatalis_Kuning = 3
    }

    /// <summary>Enam ramuan hasil racikan.</summary>
    public enum PotionType
    {
        GelSabunBeku = 0,
        KabutAsamDingin = 1,
        LedakanEsInstan = 2,
        AsamPelarutKarat = 3,
        BomNetralisasi = 4,
        BusaSabunMengembang = 5
    }

    /// <summary>Hasil racikan yang sudah jadi (nilai hasil resep saat di-brew).</summary>
    [Serializable]
    public class MixedPotion
    {
        public PotionType type;
        public string displayName;
        public Color color = Color.green;

        public float damage = 20f;
        public float radius = 1.7f;

        public float rootDuration = 0f;
        public float slowFactor = 1f;
        public float slowDuration = 0f;
        public float knockback = 0f;
        public float bossMultiplier = 1f;

        public MixedPotion Clone()
        {
            return (MixedPotion)MemberwiseClone();
        }
    }

    /// <summary>Nama, warna, dan efek pH untuk setiap bahan / ramuan.</summary>
    public static class AlchemyDatabase
    {
        public const float NeutralPH = 7f;

        public static string IngredientName(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.BahanBasa_Biru: return "Bahan Basa (Biru)";
                case IngredientType.BahanPendingin_Cyan: return "Bahan Pendingin (Cyan)";
                case IngredientType.BahanAsam_Merah: return "Bahan Asam (Merah)";
                case IngredientType.BahanKatalis_Kuning: return "Bahan Katalis (Kuning)";
                default: return type.ToString();
            }
        }

        public static Color IngredientColor(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.BahanBasa_Biru: return new Color(0.30f, 0.55f, 1.00f);
                case IngredientType.BahanPendingin_Cyan: return new Color(0.30f, 0.95f, 0.95f);
                case IngredientType.BahanAsam_Merah: return new Color(1.00f, 0.30f, 0.30f);
                case IngredientType.BahanKatalis_Kuning: return new Color(1.00f, 0.85f, 0.25f);
                default: return Color.white;
            }
        }

        /// <summary>Dampak bahan ke meteran pH (Asam -4.5, Basa +4.5, Pendingin/Katalis 0).</summary>
        public static float PHModifier(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.BahanAsam_Merah: return -4.5f;
                case IngredientType.BahanBasa_Biru: return 4.5f;
                default: return 0f;
            }
        }

        public static string PotionName(PotionType type)
        {
            switch (type)
            {
                case PotionType.GelSabunBeku: return "Gel Sabun Beku";
                case PotionType.KabutAsamDingin: return "Kabut Asam Dingin";
                case PotionType.LedakanEsInstan: return "Ledakan Es Instan";
                case PotionType.AsamPelarutKarat: return "Asam Pelarut Karat";
                case PotionType.BomNetralisasi: return "Bom Netralisasi";
                case PotionType.BusaSabunMengembang: return "Busa Sabun Mengembang";
                default: return type.ToString();
            }
        }

        public static Color PotionColor(PotionType type)
        {
            switch (type)
            {
                case PotionType.GelSabunBeku: return new Color(0.60f, 0.90f, 1.00f);
                case PotionType.KabutAsamDingin: return new Color(0.50f, 0.85f, 0.55f);
                case PotionType.LedakanEsInstan: return new Color(0.75f, 0.95f, 1.00f);
                case PotionType.AsamPelarutKarat: return new Color(0.95f, 0.55f, 0.20f);
                case PotionType.BomNetralisasi: return new Color(0.85f, 0.45f, 0.95f);
                case PotionType.BusaSabunMengembang: return new Color(0.95f, 0.95f, 0.75f);
                default: return Color.white;
            }
        }

        /// <summary>Apakah ramuan ini termasuk kategori "pendingin" (menambah chill stack boss).</summary>
        public static bool IsFreezing(PotionType type)
        {
            return type == PotionType.GelSabunBeku
                   || type == PotionType.KabutAsamDingin
                   || type == PotionType.LedakanEsInstan;
        }

        /// <summary>Apakah ramuan ini bersifat basa / netralisasi.</summary>
        public static bool IsNeutralizing(PotionType type)
        {
            return type == PotionType.BomNetralisasi
                   || type == PotionType.BusaSabunMengembang;
        }

        public static bool IsAcid(PotionType type)
        {
            return type == PotionType.KabutAsamDingin
                   || type == PotionType.AsamPelarutKarat;
        }
    }
}