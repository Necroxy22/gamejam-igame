using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Menu: Tools → Curie's Crucible → 3. Generate Recipes &amp; Wave Prefabs
    ///
    /// Membuat:
    ///   Assets/Data/Recipes/*.asset       -> 6 PotionRecipeSO
    ///   Assets/Prefabs/Arena25D/*.prefab  -> 4 zombie + 1 boss
    ///   Game_Manager                      -> AlchemyManager + TowerHealthManager + WaveManager
    /// </summary>
    static class AlchemyContentSetup
    {
        const string RecipeFolder = "Assets/Data/Recipes";
        const string PrefabFolder = "Assets/Prefabs/Arena25D";
        const string ManagerName = "Game_Manager";

        [MenuItem("Tools/Curie's Crucible/3. Generate Recipes & Wave Prefabs", false, 30)]
        static void GenerateContent()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Recipes");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Arena25D");

            int enemyLayer = Arena25DSetup.Builder.EnsureLayer("Enemy");

            List<PotionRecipeSO> recipes = CreateRecipes();
            Dictionary<ZombieType, ZombieChemicalEntity> zombies = CreateZombiePrefabs(enemyLayer);

            bool hasArena = CreateGameManager(recipes, zombies, enemyLayer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message =
                "Selesai.\n\n" +
                "• 6 PotionRecipeSO  -> " + RecipeFolder + "\n" +
                "• 5 prefab zombie   -> " + PrefabFolder + "\n" +
                "• Game_Manager (Alchemy + Tower + Wave)\n";

            if (!hasArena)
                message += "\nPERINGATAN: Arena_25D / ArenaThrowTargeting tidak ditemukan.\n" +
                           "Jalankan menu 1 & 2 terlebih dahulu, lalu ulangi menu ini.";

            EditorUtility.DisplayDialog("Generate Recipes & Wave Prefabs", message, "OK");
        }

        // ------------------------------------------------------------------
        // Recipes
        // ------------------------------------------------------------------

        static List<PotionRecipeSO> CreateRecipes()
        {
            var list = new List<PotionRecipeSO>();

            list.Add(CreateRecipe(
                "GelSabunBeku",
                PotionType.GelSabunBeku,
                IngredientType.BahanBasa_Biru,
                IngredientType.BahanPendingin_Cyan,
                11.0f, 12.0f,
                12f, 1.8f,
                3f, 0.55f, 3f, 0f, 1.0f,
                "Melapisi zombie dengan gel beku: root 3 detik + lambat."));

            list.Add(CreateRecipe(
                "KabutAsamDingin",
                PotionType.KabutAsamDingin,
                IngredientType.BahanAsam_Merah,
                IngredientType.BahanPendingin_Cyan,
                2.0f, 3.0f,
                18f, 2.2f,
                0f, 0.60f, 3f, 0f, 0.5f,
                "Kabut asam dingin: damage area + lambat, bagus vs zombie basa."));

            list.Add(CreateRecipe(
                "LedakanEsInstan",
                PotionType.LedakanEsInstan,
                IngredientType.BahanPendingin_Cyan,
                IngredientType.BahanKatalis_Kuning,
                6.5f, 7.5f,
                30f, 2.0f,
                2f, 0.8f, 1.5f, 0f, 0.5f,
                "Ledakan kristal es: damage besar + root singkat."));

            list.Add(CreateRecipe(
                "AsamPelarutKarat",
                PotionType.AsamPelarutKarat,
                IngredientType.BahanAsam_Merah,
                IngredientType.BahanKatalis_Kuning,
                2.0f, 3.0f,
                24f, 1.7f,
                0f, 1f, 0f, 0f, 0.5f,
                "Asam pekat pelarut karat: sangat efektif vs ZombieBesi."));

            list.Add(CreateRecipe(
                "BomNetralisasi",
                PotionType.BomNetralisasi,
                IngredientType.BahanAsam_Merah,
                IngredientType.BahanBasa_Biru,
                6.5f, 7.5f,
                26f, 2.4f,
                0f, 1f, 0f, 0.6f, 4.0f,
                "Netralisasi asam-basa: damage besar, kunci tahap kedua Boss."));

            list.Add(CreateRecipe(
                "BusaSabunMengembang",
                PotionType.BusaSabunMengembang,
                IngredientType.BahanBasa_Biru,
                IngredientType.BahanKatalis_Kuning,
                11.0f, 12.0f,
                10f, 2.2f,
                0f, 0.7f, 2.5f, 1.2f, 1.0f,
                "Busa sabun mengembang: knockback + lambat, bagus vs ZombieAsam."));

            return list;
        }

        static PotionRecipeSO CreateRecipe(
            string name,
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
            string path = RecipeFolder + "/" + name + ".asset";

            var recipe = AssetDatabase.LoadAssetAtPath<PotionRecipeSO>(path);
            bool created = recipe == null;

            if (created)
                recipe = ScriptableObject.CreateInstance<PotionRecipeSO>();

            recipe.potionType = type;
            recipe.displayName = "";
            recipe.description = description;
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

            if (created)
                AssetDatabase.CreateAsset(recipe, path);
            else
                EditorUtility.SetDirty(recipe);

            return recipe;
        }

        // ------------------------------------------------------------------
        // Zombie prefabs
        // ------------------------------------------------------------------

        static Dictionary<ZombieType, ZombieChemicalEntity> CreateZombiePrefabs(int enemyLayer)
        {
            var result = new Dictionary<ZombieType, ZombieChemicalEntity>();

            Sprite sprite = Arena25DSetup.ArtFactory.EnsureZombiePlaceholder();

            result[ZombieType.ZombieAsam] = CreateZombiePrefab(
                "Zombie_Asam", ZombieType.ZombieAsam, 30, 0.52f, 1, 1.00f, sprite, enemyLayer);

            result[ZombieType.ZombieBasa] = CreateZombiePrefab(
                "Zombie_Basa", ZombieType.ZombieBasa, 40, 0.48f, 1, 1.00f, sprite, enemyLayer);

            result[ZombieType.ZombieApi] = CreateZombiePrefab(
                "Zombie_Api", ZombieType.ZombieApi, 26, 0.65f, 1, 0.95f, sprite, enemyLayer);

            result[ZombieType.ZombieBesi] = CreateZombiePrefab(
                "Zombie_Besi", ZombieType.ZombieBesi, 65, 0.38f, 2, 1.15f, sprite, enemyLayer);

            result[ZombieType.BossUnstableAmalgam] = CreateZombiePrefab(
                "Boss_UnstableAmalgam",
                ZombieType.BossUnstableAmalgam,
                550, 0.32f, 5, 1.60f, sprite, enemyLayer);

            return result;
        }

        static ZombieChemicalEntity CreateZombiePrefab(
            string name,
            ZombieType type,
            int health,
            float speed,
            int towerDamage,
            float scale,
            Sprite sprite,
            int enemyLayer)
        {
            string path = PrefabFolder + "/" + name + ".prefab";

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                ZombieChemicalEntity existingEntity = existing.GetComponent<ZombieChemicalEntity>();
                PathFollower25D existingFollower = existing.GetComponent<PathFollower25D>();
                if (existingEntity != null)
                {
                    existingEntity.moveSpeed = speed;
                    existingEntity.maxHealth = health;
                    if (existingFollower != null)
                    {
                        existingFollower.moveSpeed = speed;
                        existingFollower.adjustSpeedByPerspective = true;
                    }
                    EditorUtility.SetDirty(existing);
                    return existingEntity;
                }
            }

            var go = new GameObject(name);
            go.layer = enemyLayer;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = ZombieDatabase.ColorOf(type);
            renderer.sortingOrder = 0;

            var follower = go.AddComponent<PathFollower25D>();
            follower.useCatmullRomSpline = true;
            follower.splineSamplesPerSegment = 24;
            follower.moveSpeed = speed;
            follower.adjustSpeedByPerspective = true;
            follower.baseScale = scale;

            var capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.isTrigger = true;
            capsule.size = new Vector2(0.55f * scale, 1.0f * scale);
            capsule.offset = new Vector2(0f, 0.5f * scale);

            var entity = go.AddComponent<ZombieChemicalEntity>();
            entity.zombieType = type;
            entity.maxHealth = health;
            entity.moveSpeed = speed;
            entity.towerDamage = towerDamage;
            entity.ingredientDropCount = 1;
            entity.baseColor = ZombieDatabase.ColorOf(type);
            entity.spriteRenderer = renderer;
            entity.follower = follower;

            ApplyAffinities(entity, type);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            ZombieChemicalEntity prefabEntity = prefab != null
                ? prefab.GetComponent<ZombieChemicalEntity>()
                : null;

            if (prefabEntity == null)
                Debug.LogError("[Setup] Gagal membuat prefab " + path);

            return prefabEntity;
        }

        static void ApplyAffinities(ZombieChemicalEntity entity, ZombieType type)
        {
            switch (type)
            {
                case ZombieType.ZombieAsam:
                    entity.acidVulnerability = 0.40f;
                    entity.baseVulnerability = 2.00f;
                    entity.freezeVulnerability = 1.00f;
                    entity.neutralizeVulnerability = 2.50f;
                    break;

                case ZombieType.ZombieBasa:
                    entity.acidVulnerability = 2.20f;
                    entity.baseVulnerability = 0.50f;
                    entity.freezeVulnerability = 1.00f;
                    entity.neutralizeVulnerability = 0.60f;
                    break;

                case ZombieType.ZombieApi:
                    entity.acidVulnerability = 1.00f;
                    entity.baseVulnerability = 0.80f;
                    entity.freezeVulnerability = 2.50f;
                    entity.neutralizeVulnerability = 1.00f;
                    break;

                case ZombieType.ZombieBesi:
                    entity.acidVulnerability = 2.00f;
                    entity.baseVulnerability = 0.60f;
                    entity.freezeVulnerability = 0.50f;
                    entity.neutralizeVulnerability = 0.60f;
                    break;

                case ZombieType.BossUnstableAmalgam:
                    entity.acidVulnerability = 0.15f;
                    entity.baseVulnerability = 0.15f;
                    entity.freezeVulnerability = 1.00f;
                    entity.neutralizeVulnerability = 1.00f;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Game Manager
        // ------------------------------------------------------------------

        static bool CreateGameManager(
            List<PotionRecipeSO> recipes,
            Dictionary<ZombieType, ZombieChemicalEntity> zombies,
            int enemyLayer)
        {
            GameObject go = GameObject.Find(ManagerName);
            bool created = go == null;

            if (created)
                go = new GameObject(ManagerName);

            AlchemyManager alchemy = go.GetComponent<AlchemyManager>();
            if (alchemy == null)
                alchemy = go.AddComponent<AlchemyManager>();

            TowerHealthManager tower = go.GetComponent<TowerHealthManager>();
            if (tower == null)
                tower = go.AddComponent<TowerHealthManager>();

            WaveManager waves = go.GetComponent<WaveManager>();
            if (waves == null)
                waves = go.AddComponent<WaveManager>();

            ChemicalFeedbackManager feedback = go.GetComponent<ChemicalFeedbackManager>();
            if (feedback == null)
                feedback = go.AddComponent<ChemicalFeedbackManager>();

            // --- Alchemy ---
            alchemy.recipes = new List<PotionRecipeSO>(recipes);
            alchemy.maxPotionSlots = 6;
            alchemy.startingStockPerIngredient = 3;

            for (int i = 0; i < alchemy.ingredientStock.Length && i < 4; i++)
                alchemy.ingredientStock[i] = 3;

            // --- Targeting ---
            ArenaThrowTargeting targeting = FindTargets.FindTargeting();

            bool hasArena = targeting != null;

            if (targeting != null)
            {
                targeting.alchemy = alchemy;
                targeting.enemyMask = 1 << enemyLayer;

                var marie = FindTargets.FindMarie();
                if (marie != null)
                    targeting.marie = marie;

                alchemy.targeting = targeting;
            }

            // --- Tower ---
            tower.maxHealth = 10;

            // --- Waves ---
            waves.tower = tower;
            waves.alchemy = alchemy;
            waves.leftPath = FindTargets.FindPathRoot("Left_Path_Waypoints");
            waves.rightPath = FindTargets.FindPathRoot("Right_Path_Waypoints");
            waves.autoStart = true;
            waves.initialDelay = 8f;
            waves.betweenWaveDelay = 6f;
            waves.waves = BuildWaves(zombies);

            // --- Spotlight Intro ---
            ZombieIntroSpotlight spotlight = go.GetComponent<ZombieIntroSpotlight>();
            if (spotlight == null)
                spotlight = go.AddComponent<ZombieIntroSpotlight>();
            spotlight.mainCamera = Camera.main;

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(alchemy);
            EditorUtility.SetDirty(tower);
            EditorUtility.SetDirty(waves);
            EditorUtility.SetDirty(spotlight);
            EditorUtility.SetDirty(feedback);

            if (targeting != null)
                EditorUtility.SetDirty(targeting);

            if (created)
                Undo.RegisterCreatedObjectUndo(go, "Create Game Manager");

            return hasArena;
        }

        static List<WaveDefinition> BuildWaves(Dictionary<ZombieType, ZombieChemicalEntity> zombies)
        {
            var list = new List<WaveDefinition>();

            ZombieChemicalEntity asam = Get(zombies, ZombieType.ZombieAsam);
            ZombieChemicalEntity basa = Get(zombies, ZombieType.ZombieBasa);
            ZombieChemicalEntity api = Get(zombies, ZombieType.ZombieApi);
            ZombieChemicalEntity besi = Get(zombies, ZombieType.ZombieBesi);
            ZombieChemicalEntity boss = Get(zombies, ZombieType.BossUnstableAmalgam);

            // Wave 0 - Tutorial
            var wave0 = new WaveDefinition
            {
                waveName = "Tutorial",
                subtitle = "Racik ramuan: 1-4 bahan, R untuk brew, klik jalan untuk melempar.",
                dropMultiplier = 1,
                bonusPotions = 2
            };
            wave0.entries = new[]
            {
                new WaveSpawnEntry { prefab = basa, count = 3, interval = 1.8f }
            };
            list.Add(wave0);

            // Wave 1 - Easy (+2 starter Mixed Potions, drop 1x)
            var wave1 = new WaveDefinition
            {
                waveName = "Easy",
                subtitle = "Zombie asam mulai muncul. Stok ramuan bertambah 2.",
                dropMultiplier = 1,
                bonusPotions = 2
            };
            wave1.entries = new[]
            {
                new WaveSpawnEntry { prefab = asam, count = 5, interval = 1.3f }
            };
            list.Add(wave1);

            // Wave 2 - Normal (drop 2x)
            var wave2 = new WaveDefinition
            {
                waveName = "Normal",
                subtitle = "Campuran asam, basa, dan api. Hati-hati Zombie Api!",
                dropMultiplier = 2,
                mutationChance = 0.3f,
                mutateEnlarge = true
            };
            wave2.entries = new[]
            {
                new WaveSpawnEntry { prefab = asam, count = 4, interval = 1.1f },
                new WaveSpawnEntry { prefab = basa, count = 3, interval = 1.1f },
                new WaveSpawnEntry { prefab = api, count = 2, interval = 1.0f }
            };
            list.Add(wave2);

            // Wave 3 - Hard (drop 3x)
            var wave3 = new WaveDefinition
            {
                waveName = "Hard",
                subtitle = "Mutasi dipercepat. Zombie Besi butuh Asam Pelarut Karat.",
                dropMultiplier = 3,
                mutationChance = 0.5f,
                mutateSpeedUp = true,
                mutateHarden = true,
                mutateAoEBuff = true
            };
            wave3.entries = new[]
            {
                new WaveSpawnEntry { prefab = api, count = 3, interval = 0.9f },
                new WaveSpawnEntry { prefab = besi, count = 2, interval = 1.4f },
                new WaveSpawnEntry { prefab = basa, count = 3, interval = 1.0f },
                new WaveSpawnEntry { prefab = asam, count = 2, interval = 0.9f }
            };
            list.Add(wave3);

            // Wave 4 - Boss
            var wave4 = new WaveDefinition
            {
                waveName = "Boss",
                subtitle = "Dinginkan Boss 3x sampai Chilled, lalu netralkan dengan Bom Netralisasi!",
                dropMultiplier = 5,
                isBossWave = true
            };
            wave4.entries = new[]
            {
                new WaveSpawnEntry { prefab = besi, count = 3, interval = 1.5f },
                new WaveSpawnEntry { prefab = boss, count = 1, interval = 2f, alternatePaths = false }
            };
            list.Add(wave4);

            return list;
        }

        static ZombieChemicalEntity Get(Dictionary<ZombieType, ZombieChemicalEntity> zombies, ZombieType type)
        {
            ZombieChemicalEntity entity;
            return zombies.TryGetValue(type, out entity) ? entity : null;
        }

        [MenuItem("Tools/Curie's Crucible/5. Setup Zombie Intro Spotlight & Tuning", false, 50)]
        public static void SetupZombieIntroSpotlightAndTuning()
        {
            GameObject gm = GameObject.Find(ManagerName);
            if (gm == null)
            {
                gm = new GameObject(ManagerName);
                Undo.RegisterCreatedObjectUndo(gm, "Create Game_Manager");
            }

            ZombieIntroSpotlight spotlight = gm.GetComponent<ZombieIntroSpotlight>();
            if (spotlight == null)
                spotlight = gm.AddComponent<ZombieIntroSpotlight>();

            spotlight.mainCamera = Camera.main;
            GameObject bgGo = GameObject.Find("BG_Arena");
            if (bgGo != null)
                spotlight.arenaBackground = bgGo.GetComponent<SpriteRenderer>();

            spotlight.ResolveCamera();
            spotlight.ResolveArenaBounds();

            int enemyLayer = Arena25DSetup.Builder.EnsureLayer("Enemy");
            CreateZombiePrefabs(enemyLayer);

            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(spotlight);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog(
                "Zombie Intro Spotlight & Speed Tuning",
                "Berhasil mengonfigurasi ZombieIntroSpotlight pada Game_Manager dan menyeimbangkan kecepatan zombie (Wave 1 berkisar 20-26 detik, ~0.45 - 0.65 units/detik).",
                "OK");
        }

        // ------------------------------------------------------------------
        // Folder
        // ------------------------------------------------------------------

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string parentName = System.IO.Path.GetFileName(parent);
                string grandparent = System.IO.Path.GetDirectoryName(parent);
                grandparent = grandparent == null ? "Assets" : grandparent.Replace('\\', '/');

                AssetDatabase.CreateFolder(grandparent, parentName);
            }

            string path = parent + "/" + child;

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}