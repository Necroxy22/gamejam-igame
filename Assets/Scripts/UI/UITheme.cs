using UnityEngine;

namespace CuriesCrucible.UI
{
    /// <summary>
    /// Palet warna dan styling visual tema Victorian-Steampunk Brass & Wood.
    /// </summary>
    public static class UITheme
    {
        // Palet Logam Kuningan & Emas (Brass / Bronze / Gold)
        public static readonly Color BrassBright = new Color(0.96f, 0.82f, 0.44f); // #F5D170
        public static readonly Color BrassBase   = new Color(0.80f, 0.62f, 0.28f); // #CC9E47
        public static readonly Color BrassDark   = new Color(0.50f, 0.36f, 0.12f); // #805C1F
        public static readonly Color BrassShadow = new Color(0.24f, 0.17f, 0.05f); // #3D2B0D

        // Palet Kayu Mahoni / Walnut Tua (Polished Dark Wood)
        public static readonly Color WoodDark   = new Color(0.12f, 0.08f, 0.05f, 0.95f); // #1E140D
        public static readonly Color WoodMedium = new Color(0.22f, 0.15f, 0.10f, 0.95f); // #382619
        public static readonly Color WoodLight  = new Color(0.32f, 0.22f, 0.14f);        // #523824

        // Kertas Kuno / Perkamen (Victorian Aged Parchment)
        public static readonly Color ParchmentBg    = new Color(0.95f, 0.91f, 0.82f, 0.98f); // #F2E8D1
        public static readonly Color ParchmentBorder= new Color(0.72f, 0.62f, 0.46f);        // #B89E75
        public static readonly Color ParchmentText  = new Color(0.18f, 0.12f, 0.08f);        // #2E1F14

        // Tombol Interaktif
        public static readonly Color BrewButtonNormal      = new Color(0.22f, 0.58f, 0.28f); // Emerald Green
        public static readonly Color BrewButtonHighlighted = new Color(0.30f, 0.75f, 0.38f);
        public static readonly Color BrewButtonPressed     = new Color(0.16f, 0.42f, 0.20f);
        public static readonly Color BrewButtonDisabled    = new Color(0.35f, 0.35f, 0.35f, 0.5f);

        public static readonly Color ClearButtonNormal      = new Color(0.70f, 0.20f, 0.18f); // Crimson Maroon
        public static readonly Color ClearButtonHighlighted = new Color(0.88f, 0.28f, 0.24f);
        public static readonly Color ClearButtonPressed     = new Color(0.48f, 0.12f, 0.10f);

        // Highlight Potion Terpilih
        public static readonly Color SelectionGlow = new Color(1.0f, 0.88f, 0.25f, 1.0f); // Bright Gold Glow
        public static readonly Color NormalBorder   = new Color(0.60f, 0.46f, 0.22f, 0.7f);

        // pH Gradient Stops
        public static readonly Color PHAcidRed   = new Color(0.92f, 0.22f, 0.20f); // pH 0-4
        public static readonly Color PHNeutral   = new Color(0.26f, 0.72f, 0.32f); // pH 7
        public static readonly Color PHBaseBlue  = new Color(0.18f, 0.52f, 0.92f); // pH 10-14
    }
}
