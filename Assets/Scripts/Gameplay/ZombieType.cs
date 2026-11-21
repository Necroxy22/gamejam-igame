using UnityEngine;

namespace CuriesCrucible.Gameplay
{
    /// <summary>Lima jenis zombie (4 normal + 1 boss dua tahap).</summary>
    public enum ZombieType
    {
        ZombieAsam = 0,
        ZombieBasa = 1,
        ZombieApi = 2,
        ZombieBesi = 3,
        BossUnstableAmalgam = 4
    }

    /// <summary>Nama, warna, dan deskripsi tiap zombie.</summary>
    public static class ZombieDatabase
    {
        public static string Name(ZombieType type)
        {
            switch (type)
            {
                case ZombieType.ZombieAsam: return "Zombie Asam";
                case ZombieType.ZombieBasa: return "Zombie Basa";
                case ZombieType.ZombieApi: return "Zombie Api";
                case ZombieType.ZombieBesi: return "Zombie Besi";
                case ZombieType.BossUnstableAmalgam: return "Boss Unstable Amalgam";
                default: return type.ToString();
            }
        }

        public static Color ColorOf(ZombieType type)
        {
            switch (type)
            {
                case ZombieType.ZombieAsam: return new Color(0.62f, 0.86f, 0.30f);
                case ZombieType.ZombieBasa: return new Color(0.45f, 0.62f, 1.00f);
                case ZombieType.ZombieApi: return new Color(1.00f, 0.52f, 0.20f);
                case ZombieType.ZombieBesi: return new Color(0.62f, 0.63f, 0.68f);
                case ZombieType.BossUnstableAmalgam: return new Color(0.82f, 0.32f, 0.92f);
                default: return Color.white;
            }
        }

        public static string Description(ZombieType type)
        {
            switch (type)
            {
                case ZombieType.ZombieAsam:
                    return "Hancur oleh basa & netralisasi. Tahan terhadap asam.";
                case ZombieType.ZombieBasa:
                    return "Hancur oleh asam. Tahan terhadap basa.";
                case ZombieType.ZombieApi:
                    return "Sangat lemah terhadap pendingin & es.";
                case ZombieType.ZombieBesi:
                    return "Lambat & keras. Hanya Asam Pelarut Karat yang efektif.";
                case ZombieType.BossUnstableAmalgam:
                    return "Dinginkan 3x sampai Chilled, lalu netralkan dengan Bom Netralisasi.";
                default:
                    return "";
            }
        }

        /// <summary>Stat default per tipe zombie.</summary>
        public static void ApplyDefaults(ZombieType type, ref int health, ref float speed, ref int towerDamage)
        {
            switch (type)
            {
                case ZombieType.ZombieAsam:
                    health = 30; speed = 1.00f; towerDamage = 1; break;
                case ZombieType.ZombieBasa:
                    health = 40; speed = 0.90f; towerDamage = 1; break;
                case ZombieType.ZombieApi:
                    health = 26; speed = 1.35f; towerDamage = 1; break;
                case ZombieType.ZombieBesi:
                    health = 65; speed = 0.65f; towerDamage = 2; break;
                case ZombieType.BossUnstableAmalgam:
                    health = 550; speed = 0.55f; towerDamage = 5; break;
            }
        }
    }
}
