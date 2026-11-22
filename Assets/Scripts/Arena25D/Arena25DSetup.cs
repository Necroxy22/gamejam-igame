using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace CuriesCrucible.Arena25D
{
    /// <summary>
    /// Tool editor untuk merakit hierarki arena 2.5D secara otomatis:
    ///
    ///   Arena_25D
    ///   â”œâ”€â”€ Environment_Layers
    ///   â”‚   â”œâ”€â”€ BG_Arena              (sortingOrder -100)
    ///   â”‚   â”œâ”€â”€ Tower_Balcony_FG      (sortingOrder  500)
    ///   â”‚   â””â”€â”€ MarieCurie_ThrowOrigin (0, 1.5, 0)
    ///   â”œâ”€â”€ Left_Path_Waypoints       (6 titik kurva kiri)
    ///   â”œâ”€â”€ Right_Path_Waypoints      (6 titik kurva kanan)
    ///   â”œâ”€â”€ Road_Clickable_Area       (PolygonCollider2D, layer WalkableRoad)
    ///   â”œâ”€â”€ Aim_Reticle               (sprite elips merah, nonaktif sampai kursor di jalan)
    ///   â””â”€â”€ Projectiles               (container runtime)
    ///
    /// Semua kode editor dibungkus #if UNITY_EDITOR, jadi komponen ini tetap aman
    /// ditinggal di scene saat build WebGL (tidak ada "missing script").
    /// </summary>
    [DisallowMultipleComponent]
    public class Arena25DSetup : MonoBehaviour
    {
        [Header("Road Collider")]
        [Tooltip("Setengah lebar jalan (world unit) yang dipakai untuk PolygonCollider2D.")]
        public float roadHalfWidth = 0.85f;

        [Header("Throw Origin")]
        [Tooltip("Posisi lokal MarieCurie_ThrowOrigin (arena root ada di 0,0,0).")]
        public Vector3 throwOriginLocalPosition = new Vector3(0f, 1.5f, 0f);

        [Header("Camera")]
        public bool configureCameraOnGenerate = true;
        public float designCameraSize = 4.8f;

        [Header("Placeholder Art")]
        [Tooltip("Buat sprite placeholder (reticle, bayangan, botol, background arena) kalau belum ada.")]
        public bool generatePlaceholderArt = true;

#if UNITY_EDITOR
        // ==================================================================
        // Layout default (samakan dengan deskripsi arena / gambar background)
        // ==================================================================

        public const string RootObjectName = "Arena_25D";
        public const string WalkableRoadLayerName = "WalkableRoad";

        static readonly Vector3[] LeftWaypointLayout =
        {
            new Vector3(-7.50f,  1.00f, 0f), // gerbang kiri atas
            new Vector3(-6.40f, -0.15f, 0f),
            new Vector3(-5.45f, -1.60f, 0f),
            new Vector3(-4.45f, -2.90f, 0f),
            new Vector3(-3.45f, -3.75f, 0f), // tikungan bawah-kiri
            new Vector3(-0.80f, -2.20f, 0f)  // depan pintu gerbang menara
        };

        static readonly Vector3[] RightWaypointLayout =
        {
            new Vector3( 7.50f,  1.00f, 0f),
            new Vector3( 6.40f, -0.15f, 0f),
            new Vector3( 5.45f, -1.60f, 0f),
            new Vector3( 4.45f, -2.90f, 0f),
            new Vector3( 3.45f, -3.75f, 0f),
            new Vector3( 0.80f, -2.20f, 0f)
        };

        // ==================================================================
        // Menu
        // ==================================================================

        [ContextMenu("Generate 2.5D Arena Hierarchy")]
        public void Generate2_5DArenaHierarchy()
        {
            Builder.Build(this);
        }

        [MenuItem("Tools/Curie's Crucible/1. Generate 2.5D Arena Hierarchy", false, 10)]
        static void MenuGenerateArena()
        {
            Arena25DSetup config = FindExistingSetup();
            Builder.Build(config);
        }

        [MenuItem("Tools/Curie's Crucible/2. Rebuild Road Polygon", false, 20)]
        static void MenuRebuildRoadPolygon()
        {
            Arena25DSetup config = FindExistingSetup();
            if (config == null)
            {
                EditorUtility.DisplayDialog("Arena 2.5D", "Generate hierarki arena terlebih dahulu.", "OK");
                return;
            }

            config.RebuildRoadPolygon();
            EditorUtility.DisplayDialog("Arena 2.5D", "PolygonCollider2D jalan berhasil di-update dari waypoint.", "OK");
        }

        [MenuItem("Tools/Curie's Crucible/3. Regenerate Placeholder Art", false, 30)]
        static void MenuRegenerateArt()
        {
            if (!EditorUtility.DisplayDialog(
                    "Regenerate Placeholder Art",
                    "Sprite placeholder (reticle, bayangan, botol, background) akan dibuat ulang.\n" +
                    "Art milik Anda sendiri di file yang sama akan ditimpa. Lanjutkan?",
                    "Ya, regenerate", "Batal"))
            {
                return;
            }

            ArtFactory.DeletePlaceholders();
            Builder.Build(FindExistingSetup());
        }

        [MenuItem("Tools/Curie's Crucible/4. Create Test Zombie (Left Path)", false, 40)]
        static void MenuCreateTestZombie()
        {
            GameObject arena = GameObject.Find(RootObjectName);
            Transform left = arena != null ? arena.transform.Find("Left_Path_Waypoints") : null;

            if (left == null)
            {
                EditorUtility.DisplayDialog("Arena 2.5D", "Generate hierarki arena terlebih dahulu.", "OK");
                return;
            }

            var zombie = new GameObject("Zombie_Test");

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                ArtFactory.Folder + "/ZombiePlaceholder.png");

            var renderer = zombie.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;

            var follower = zombie.AddComponent<PathFollower25D>();

            var points = new List<Transform>(left.childCount);
            for (int i = 0; i < left.childCount; i++)
                points.Add(left.GetChild(i));

            follower.waypoints = points.ToArray();
            follower.moveSpeed = 1.1f;

            if (left.childCount > 0)
                zombie.transform.position = left.GetChild(0).position;

            Undo.RegisterCreatedObjectUndo(zombie, "Create Test Zombie");
            EditorUtility.SetDirty(follower);
            Selection.activeGameObject = zombie;
            SceneView.FrameLastActiveSceneView();
        }

        [MenuItem("Tools/Curie's Crucible/Auto Rebuild Road Polygon", false, 60)]
        static void MenuToggleAutoRebuild()
        {
            bool enabled = !AutoRebuildEnabled;
            EditorPrefs.SetBool(PrefAutoRebuild, enabled);
            Menu.SetChecked("Tools/Curie's Crucible/Auto Rebuild Road Polygon", enabled);
        }

        [MenuItem("Tools/Curie's Crucible/Auto Rebuild Road Polygon", true)]
        static bool MenuToggleAutoRebuildValidate()
        {
            Menu.SetChecked("Tools/Curie's Crucible/Auto Rebuild Road Polygon", AutoRebuildEnabled);
            return true;
        }

        static bool AutoRebuildEnabled
        {
            get { return EditorPrefs.GetBool(PrefAutoRebuild, true); }
        }

        const string PrefAutoRebuild = "Arena25D.AutoRebuildRoad";

        /// <summary>Cari komponen setup yang sudah ada di scene (kompatibel Unity 2022 &amp; Unity 6).</summary>
        public static Arena25DSetup FindExistingSetup()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<Arena25DSetup>();
#else
            return UnityEngine.Object.FindObjectOfType<Arena25DSetup>();
#endif
        }

        // ==================================================================
        // Public editor helpers (dipakai juga oleh scene GUI tools)
        // ==================================================================

        /// <summary>Baca ulang waypoint di scene lalu tulis ke PolygonCollider2D.</summary>
        public void RebuildRoadPolygon()
        {
            Transform root = transform.parent != null ? transform.parent : transform;

            Transform left = root.Find("Left_Path_Waypoints");
            Transform right = root.Find("Right_Path_Waypoints");

            if (left == null || right == null)
                return;

            PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
            if (polygon == null)
                polygon = gameObject.AddComponent<PolygonCollider2D>();

            Builder.ApplyRoadPaths(polygon, left, right, Mathf.Max(0.2f, roadHalfWidth));
        }

        void OnDrawGizmos()
        {
            Transform root = transform.parent != null ? transform.parent : transform;

            Transform left = root.Find("Left_Path_Waypoints");
            Transform right = root.Find("Right_Path_Waypoints");

            DrawPathGizmo(left, new Color(1f, 0.82f, 0.15f, 1f));
            DrawPathGizmo(right, new Color(1f, 0.82f, 0.15f, 1f));

            // Outline collider jalan (hijau) supaya mudah dicek terhadap gambar background.
            PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
            if (polygon != null && polygon.pathCount > 0)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.85f);

                for (int p = 0; p < polygon.pathCount; p++)
                {
                    Vector2[] path = polygon.GetPath(p);
                    if (path == null || path.Length < 2)
                        continue;

                    Vector3 previous = transform.TransformPoint(path[path.Length - 1]);
                    for (int i = 0; i < path.Length; i++)
                    {
                        Vector3 current = transform.TransformPoint(path[i]);
                        Gizmos.DrawLine(previous, current);
                        previous = current;
                    }
                }
            }

            // Titik lempar Marie Curie.
            Transform origin = root.Find("Environment_Layers/MarieCurie_ThrowOrigin");
            if (origin != null)
            {
                Gizmos.color = new Color(1f, 0.1f, 0.9f, 1f);
                Gizmos.DrawWireSphere(origin.position, 0.16f);
            }
        }

        void DrawPathGizmo(Transform root, Color color)
        {
            if (root == null || root.childCount < 2)
                return;

            // Kurva halus (Catmull-Rom) sebagai preview arah jalan.
            var points = new List<Vector3>(root.childCount * 6);

            for (int i = 0; i < root.childCount - 1; i++)
            {
                Vector3 p0 = root.GetChild(Mathf.Max(0, i - 1)).position;
                Vector3 p1 = root.GetChild(i).position;
                Vector3 p2 = root.GetChild(i + 1).position;
                Vector3 p3 = root.GetChild(Mathf.Min(root.childCount - 1, i + 2)).position;

                const int steps = 6;
                for (int s = 0; s < steps; s++)
                    points.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }

            points.Add(root.GetChild(root.childCount - 1).position);

            Gizmos.color = color;
            for (int i = 1; i < points.Count; i++)
                Gizmos.DrawLine(points[i - 1], points[i]);

            // Titik waypoint.
            Gizmos.color = new Color(1f, 1f, 0.2f, 1f);
            for (int i = 0; i < root.childCount; i++)
                Gizmos.DrawWireSphere(root.GetChild(i).position, 0.11f);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ==================================================================
        // Builder
        // ==================================================================

        public static class Builder
        {
            public static GameObject Build(Arena25DSetup config)
            {
                int roadLayer = EnsureLayer(WalkableRoadLayerName);

                float halfWidth = config != null ? Mathf.Max(0.2f, config.roadHalfWidth) : 0.85f;
                Vector3 originPosition = config != null
                    ? config.throwOriginLocalPosition
                    : new Vector3(0f, 1.5f, 0f);

                bool buildArt = config == null || config.generatePlaceholderArt;
                bool buildCamera = config == null || config.configureCameraOnGenerate;
                float cameraSize = config != null ? config.designCameraSize : 4.8f;

                GameObject root = GetOrCreateRoot();

                // ---------------- Waypoints (dibuat dulu, supaya art mengikuti posisi terbaru) ----
                Transform leftWaypoints = BuildWaypointRoot(root.transform, "Left_Path_Waypoints", LeftWaypointLayout);
                Transform rightWaypoints = BuildWaypointRoot(root.transform, "Right_Path_Waypoints", RightWaypointLayout);

                ArtSet art = buildArt
                    ? ArtFactory.EnsureAll(
                        halfWidth,
                        Arena25DMath.ReadPath2D(leftWaypoints),
                        Arena25DMath.ReadPath2D(rightWaypoints))
                    : ArtFactory.LoadExisting();

                // ---------------- Environment_Layers ----------------
                Transform environment = GetOrCreateChild(root.transform, "Environment_Layers");

                Transform background = GetOrCreateChild(environment, "BG_Arena");
                var backgroundRenderer = GetOrAdd<SpriteRenderer>(background.gameObject);
                backgroundRenderer.sortingOrder = -100;
                backgroundRenderer.color = Color.white;
                if (backgroundRenderer.sprite == null)
                    backgroundRenderer.sprite = art.Background;
                background.localPosition = Vector3.zero;

                Transform balcony = GetOrCreateChild(environment, "Tower_Balcony_FG");
                var balconyRenderer = GetOrAdd<SpriteRenderer>(balcony.gameObject);
                balconyRenderer.sortingOrder = 500;
                balcony.localPosition = Vector3.zero;

                Transform throwOrigin = GetOrCreateChild(environment, "MarieCurie_ThrowOrigin");
                throwOrigin.localPosition = originPosition;
                throwOrigin.localRotation = Quaternion.identity;

                // ---------------- Waypoints ----------------
                // (sudah dibuat di awal agar background placeholder mengikuti posisi waypoint saat ini)

                // ---------------- Road clickable area ----------------
                Transform road = GetOrCreateChild(root.transform, "Road_Clickable_Area");
                road.gameObject.layer = roadLayer;
                road.localPosition = Vector3.zero;

                var polygon = GetOrAdd<PolygonCollider2D>(road.gameObject);
                polygon.isTrigger = true;
                ApplyRoadPaths(polygon, leftWaypoints, rightWaypoints, halfWidth);

                // ---------------- Reticle ----------------
                Transform reticleTransform = GetOrCreateChild(root.transform, "Aim_Reticle");
                var reticleRenderer = GetOrAdd<SpriteRenderer>(reticleTransform.gameObject);
                reticleRenderer.sortingOrder = 0;
                reticleRenderer.enabled = false;
                if (reticleRenderer.sprite == null)
                    reticleRenderer.sprite = art.Reticle;
                reticleTransform.localPosition = new Vector3(0f, -2.5f, 0f);
                reticleTransform.localScale = new Vector3(1.6f, 0.88f, 1f);

                // ---------------- Projectiles container ----------------
                Transform projectiles = GetOrCreateChild(root.transform, "Projectiles");
                projectiles.localPosition = Vector3.zero;

                // ---------------- Setup component ----------------
                var setup = road.GetComponent<Arena25DSetup>();
                if (setup == null)
                    setup = road.gameObject.AddComponent<Arena25DSetup>();

                if (config != null && setup != config)
                    CopySettings(config, setup);

                setup.roadHalfWidth = halfWidth;
                setup.throwOriginLocalPosition = originPosition;
                setup.configureCameraOnGenerate = buildCamera;
                setup.designCameraSize = cameraSize;
                setup.generatePlaceholderArt = buildArt;

                // ---------------- Camera ----------------
                Camera camera = ConfigureCamera(buildCamera, cameraSize);

                // ---------------- Throw targeting ----------------
                var targeting = root.GetComponent<ArenaThrowTargeting>();
                if (targeting == null)
                    targeting = root.gameObject.AddComponent<ArenaThrowTargeting>();

                targeting.arenaCamera = camera;
                targeting.throwOrigin = throwOrigin;
                targeting.projectileRoot = projectiles;
                targeting.reticle = reticleRenderer;
                targeting.walkableRoadMask = roadLayer;
                targeting.perspectiveBottomY = Arena25DMath.DefaultNearY;
                targeting.perspectiveTopY = Arena25DMath.DefaultFarY;
                targeting.nearScale = Arena25DMath.DefaultNearScale;
                targeting.farScale = Arena25DMath.DefaultFarScale;

                if (targeting.potionSprite == null)
                    targeting.potionSprite = art.Potion;
                if (targeting.shadowSprite == null)
                    targeting.shadowSprite = art.Shadow;
                if (targeting.splashSprite == null)
                    targeting.splashSprite = art.Reticle;

                MarkDirty(root);
                MarkDirty(road.gameObject);
                MarkDirty(targeting);

                EditorUtility.SetDirty(setup);
                Selection.activeGameObject = root;

                return root;
            }

            static void CopySettings(Arena25DSetup from, Arena25DSetup to)
            {
                to.roadHalfWidth = from.roadHalfWidth;
                to.throwOriginLocalPosition = from.throwOriginLocalPosition;
                to.configureCameraOnGenerate = from.configureCameraOnGenerate;
                to.designCameraSize = from.designCameraSize;
                to.generatePlaceholderArt = from.generatePlaceholderArt;
            }

            static Transform BuildWaypointRoot(Transform parent, string name, Vector3[] layout)
            {
                Transform root = GetOrCreateChild(parent, name);

                for (int i = 0; i < layout.Length; i++)
                {
                    Transform waypoint = GetOrCreateChild(root, "WP_" + i.ToString("00"), out bool created);

                    // Jangan timpa posisi waypoint yang sudah diatur manual di Scene View.
                    if (!created)
                        continue;

                    waypoint.localPosition = layout[i];
                    waypoint.localRotation = Quaternion.identity;
                    waypoint.localScale = Vector3.one;
                }

                return root;
            }

            public static void ApplyRoadPaths(
                PolygonCollider2D polygon,
                Transform leftWaypoints,
                Transform rightWaypoints,
                float halfWidth)
            {
                if (polygon == null)
                    return;

                Vector2[] leftPts = Arena25DMath.ReadPath2D(leftWaypoints);
                Vector2[] rightPts = Arena25DMath.ReadPath2D(rightWaypoints);

                if (leftPts.Length < 2 || rightPts.Length < 2)
                    return;

                // Bangun jalur polyline kontinu dari gerbang kiri (L1) melintasi tengah menara ke gerbang kanan (R1)
                System.Collections.Generic.List<Vector2> continuousPath = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i < leftPts.Length; i++)
                    continuousPath.Add(leftPts[i]);

                // Titik tengah penghubung di depan balkon menara
                Vector2 centerPoint = new Vector2(0f, (leftPts[leftPts.Length - 1].y + rightPts[rightPts.Length - 1].y) * 0.5f + 0.25f);
                continuousPath.Add(centerPoint);

                for (int i = rightPts.Length - 1; i >= 0; i--)
                    continuousPath.Add(rightPts[i]);

                Vector2[] fullOutline = Arena25DMath.RoadOutline(continuousPath, halfWidth);
                if (fullOutline.Length < 3)
                    return;

                Undo.RecordObject(polygon, "Rebuild Road Polygon");

                polygon.pathCount = 1;
                polygon.SetPath(0, fullOutline);

                EditorUtility.SetDirty(polygon);
            }

            static Camera ConfigureCamera(bool configure, float size)
            {
                Camera camera = Camera.main;

                if (camera == null)
                {
                    var cameraGo = new GameObject("Main Camera");
                    cameraGo.tag = "MainCamera";
                    camera = cameraGo.AddComponent<Camera>();
                    cameraGo.AddComponent<AudioListener>();
                    Undo.RegisterCreatedObjectUndo(cameraGo, "Create Main Camera");
                }

                if (!configure)
                    return camera;

                camera.orthographic = true;
                camera.orthographicSize = size;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;

                var rig = camera.GetComponent<Arena25DCameraRig>();
                if (rig == null)
                    rig = camera.gameObject.AddComponent<Arena25DCameraRig>();

                rig.fitMode = Arena25DCameraRig.FitMode.FitWidth;
                rig.fixedSize = size;
                rig.designWidth = size * 2f * (16f / 9f);
                rig.designHeight = size * 2f;

                EditorUtility.SetDirty(camera);

                return camera;
            }

            static GameObject GetOrCreateRoot()
            {
                GameObject existing = GameObject.Find(RootObjectName);
                if (existing != null)
                    return existing;

                var root = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create Arena Root");
                return root;
            }

            static Transform GetOrCreateChild(Transform parent, string name)
            {
                return GetOrCreateChild(parent, name, out _);
            }

            static Transform GetOrCreateChild(Transform parent, string name, out bool created)
            {
                Transform existing = parent.Find(name);
                if (existing != null)
                {
                    created = false;
                    return existing;
                }

                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(child, "Create Arena Object");
                created = true;
                return child.transform;
            }

            static T GetOrAdd<T>(GameObject target) where T : Component
            {
                T component = target.GetComponent<T>();
                if (component != null)
                    return component;

                component = target.AddComponent<T>();
                Undo.RegisterCreatedObjectUndo(component, "Add Arena Component");
                return component;
            }

            static void MarkDirty(Object target)
            {
                if (target == null)
                    return;

                EditorUtility.SetDirty(target);

                if (target is Component component)
                {
                    Scene scene = component.gameObject.scene;
                    if (scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(scene);
                }
                else if (target is GameObject gameObject)
                {
                    Scene scene = gameObject.scene;
                    if (scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            // ------------------------------------------------------------------
            // Layer
            // ------------------------------------------------------------------

            public static int EnsureLayer(string layerName)
            {
                int existing = LayerMask.NameToLayer(layerName);
                if (existing >= 0)
                    return existing;

                Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                if (assets == null || assets.Length == 0)
                    return 0;

                var tagManager = new SerializedObject(assets[0]);
                SerializedProperty layers = tagManager.FindProperty("layers");

                if (layers == null)
                    return 0;

                for (int i = 8; i < 32; i++)
                {
                    SerializedProperty element = layers.GetArrayElementAtIndex(i);
                    if (element == null || !string.IsNullOrEmpty(element.stringValue))
                        continue;

                    element.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }

                Debug.LogWarning("[Arena25D] Slot layer kosong tidak tersedia, pakai layer Default.");
                return 0;
            }
        }

        // ==================================================================
        // ArtFactory - sprite placeholder prosedural (dibuat sekali, lalu file PNG)
        // ==================================================================

        public static class ArtFactory
        {
            public const string Folder = "Assets/Art/Arena25D";

            static Vector2[] _leftPath = new Vector2[0];
            static Vector2[] _rightPath = new Vector2[0];

            public static ArtSet EnsureAll(float halfWidth, Vector2[] leftPath, Vector2[] rightPath)
            {
                EnsureFolder();

                _leftPath = leftPath ?? To2D(LeftWaypointLayout);
                _rightPath = rightPath ?? To2D(RightWaypointLayout);

                var art = new ArtSet
                {
                    Reticle = EnsureSprite("AoEReticle", 256, 256, ReticlePixel),
                    Shadow = EnsureSprite("PotionShadow", 128, 128, ShadowPixel),
                    Potion = EnsureSprite("PotionBottle", 96, 128, PotionPixel),
                    Zombie = EnsureZombiePlaceholder(),
                    Background = EnsureBackground("ArenaBackground", halfWidth)
                };

                return art;
            }

            public static ArtSet LoadExisting()
            {
                return new ArtSet
                {
                    Reticle = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/AoEReticle.png"),
                    Shadow = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/PotionShadow.png"),
                    Potion = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/PotionBottle.png"),
                    Zombie = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ZombiePlaceholder.png"),
                    Background = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/ArenaBackground.png")
                };
            }

            public static void DeletePlaceholders()
            {
                string[] files =
                {
                    "AoEReticle.png",
                    "PotionShadow.png",
                    "PotionBottle.png",
                    "ZombiePlaceholder.png",
                    "ArenaBackground.png"
                };

                for (int i = 0; i < files.Length; i++)
                {
                    string path = Folder + "/" + files[i];
                    if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                        AssetDatabase.DeleteAsset(path);
                }

                AssetDatabase.Refresh();
            }

            static void EnsureFolder()
            {
                if (AssetDatabase.IsValidFolder(Folder))
                    return;

                if (!AssetDatabase.IsValidFolder("Assets/Art"))
                    AssetDatabase.CreateFolder("Assets", "Art");

                AssetDatabase.CreateFolder("Assets/Art", "Arena25D");
            }

            // ------------------------------------------------------------------
            // PNG helpers
            // ------------------------------------------------------------------

            static Sprite EnsureSprite(
                string name,
                int width,
                int height,
                System.Func<float, float, Color> pixel,
                Vector2? pivot = null)
            {
                string path = Folder + "/" + name + ".png";

                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null)
                    return existing;

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                var pixels = new Color[width * height];

                for (int y = 0; y < height; y++)
                {
                    float v = ((y + 0.5f) / height) * 2f - 1f;

                    for (int x = 0; x < width; x++)
                    {
                        float u = ((x + 0.5f) / width) * 2f - 1f;
                        pixels[y * width + x] = pixel(u, v);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureSprite(path, 100f, pivot);

                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            /// <summary>Sprite placeholder Marie Curie (gaun Victorian + goggles tembaga, pivot bawah-tengah).</summary>
            public static Sprite EnsureMariePlaceholder()
            {
                EnsureFolder();

                string path = Folder + "/MarieCuriePlaceholder.png";
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (existing != null)
                {
                    // Pastikan pivot di kaki (bottom-center).
                    if (existing.pivot.y > 8f)
                    {
                        ConfigureSprite(path, 100f, new Vector2(0.5f, 0.04f));
                        existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }

                    return existing;
                }

                return EnsureSprite(
                    "MarieCuriePlaceholder",
                    160,
                    192,
                    MariePixel,
                    new Vector2(0.5f, 0.04f));
            }

            /// <summary>Sprite placeholder zombie (pivot di kaki, supaya posisi transform = tanah).</summary>
            public static Sprite EnsureZombiePlaceholder()
            {
                EnsureFolder();

                string path = Folder + "/ZombiePlaceholder.png";
                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (existing != null)
                {
                    // Perbaiki sprite lama yang masih pakai pivot tengah.
                    if (existing.pivot.y > 8f)
                    {
                        ConfigureSprite(path, 100f, new Vector2(0.5f, 0.06f));
                        existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }

                    return existing;
                }

                return EnsureSprite(
                    "ZombiePlaceholder",
                    96,
                    128,
                    ZombiePixel,
                    new Vector2(0.5f, 0.06f));
            }

            static Sprite EnsureBackground(string name, float halfWidth)
            {
                string path = Folder + "/" + name + ".png";

                Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing != null)
                    return existing;

                const int width = 1360;
                const int height = 768;
                const float pixelsPerUnit = 80f;

                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                var pixels = new Color[width * height];

                float halfWidthWorld = width / (2f * pixelsPerUnit);
                float halfHeightWorld = height / (2f * pixelsPerUnit);

                for (int y = 0; y < height; y++)
                {
                    float wy = (y + 0.5f) / pixelsPerUnit - halfHeightWorld;

                    for (int x = 0; x < width; x++)
                    {
                        float wx = (x + 0.5f) / pixelsPerUnit - halfWidthWorld;
                        pixels[y * width + x] = ArenaPixel(new Vector2(wx, wy), halfWidth);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply();

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ConfigureSprite(path, pixelsPerUnit);

                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            static void ConfigureSprite(string path, float pixelsPerUnit, Vector2? pivot = null)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                if (pivot.HasValue)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = pivot.Value;
                    importer.SetTextureSettings(settings);
                }

                importer.SaveAndReimport();
            }

            // ------------------------------------------------------------------
            // Marie Curie placeholder (Victorian steampunk)
            // Ruang: x -1..1, y -1.2..1.2 (rasio 160x192)
            // ------------------------------------------------------------------

            static Color MariePixel(float u, float v)
            {
                float x = u;
                float y = v * 1.2f;

                Color color = Color.clear;

                Color dressDark = new Color(0.09f, 0.15f, 0.21f, 1f);
                Color dressMid = new Color(0.15f, 0.27f, 0.35f, 1f);
                Color apron = new Color(0.76f, 0.78f, 0.76f, 1f);
                Color skin = new Color(0.92f, 0.77f, 0.63f, 1f);
                Color hair = new Color(0.24f, 0.16f, 0.13f, 1f);
                Color brass = new Color(0.82f, 0.63f, 0.21f, 1f);
                Color glow = new Color(0.42f, 1f, 0.55f, 0.9f);
                Color eye = new Color(0.10f, 0.10f, 0.14f, 1f);

                // --- Gaun A-line ---
                float dress = InsideTrapezoid(x, y, -1.20f, -0.12f, 0.94f, 0.40f, 0.035f);
                color = Over(color, dressDark, dress);

                float dressHighlight = InsideTrapezoid(x, y, -1.18f, -0.14f, 0.56f, 0.28f, 0.05f);
                color = Over(color, dressMid, dressHighlight * 0.55f);

                // --- Apron ---
                float apronShape = InsideTrapezoid(x, y, -0.96f, -0.20f, 0.34f, 0.30f, 0.03f);
                color = Over(color, apron, apronShape * 0.92f);

                // --- Bodice ---
                float bodice = InsideTrapezoid(x, y, -0.18f, 0.34f, 0.41f, 0.34f, 0.03f);
                color = Over(color, dressMid, bodice);

                // --- Lengan kiri (di belakang) ---
                float armLeft = InsideBox(x, y, -0.44f, 0.04f, 0.10f, 0.31f, 0.05f);
                color = Over(color, dressDark, armLeft);

                // --- Lengan kanan terulur memegang ramuan ---
                float armRight = InsideBox(x, y, 0.60f, 0.14f, 0.30f, 0.085f, 0.05f);
                color = Over(color, dressMid, armRight);

                float hand = InsideEllipse(x, y, 0.90f, 0.14f, 0.10f, 0.10f, 0.04f);
                color = Over(color, skin, hand);

                float flask = InsideEllipse(x, y, 0.92f, 0.29f, 0.085f, 0.11f, 0.04f);
                color = Over(color, glow, flask * 0.9f);

                // --- Leher ---
                float neck = InsideBox(x, y, 0f, 0.40f, 0.09f, 0.09f, 0.03f);
                color = Over(color, skin, neck);

                // --- Rambut belakang + sanggul ---
                float hairBack = InsideEllipse(x, y, 0f, 0.68f, 0.30f, 0.34f, 0.04f);
                color = Over(color, hair, hairBack);

                float bun = InsideEllipse(x, y, 0f, 1.03f, 0.15f, 0.13f, 0.03f);
                color = Over(color, hair, bun);

                // --- Wajah ---
                float face = InsideEllipse(x, y, 0f, 0.62f, 0.23f, 0.27f, 0.03f);
                color = Over(color, skin, face);

                // --- poni ---
                float fringe = InsideEllipse(x, y, 0f, 0.81f, 0.26f, 0.17f, 0.03f);
                color = Over(color, hair, fringe);

                // --- tali goggles ---
                float strap = InsideBox(x, y, 0f, 0.82f, 0.285f, 0.032f, 0.02f);
                color = Over(color, new Color(0.28f, 0.22f, 0.16f, 1f), strap);

                // --- lensa goggles (kuningan) ---
                float lens = Mathf.Max(
                    InsideEllipse(x, y, -0.12f, 0.82f, 0.088f, 0.088f, 0.02f),
                    InsideEllipse(x, y, 0.12f, 0.82f, 0.088f, 0.088f, 0.02f));
                color = Over(color, brass, lens);

                float glass = Mathf.Max(
                    InsideEllipse(x, y, -0.12f, 0.82f, 0.055f, 0.055f, 0.02f),
                    InsideEllipse(x, y, 0.12f, 0.82f, 0.055f, 0.055f, 0.02f));
                color = Over(color, new Color(0.62f, 0.92f, 1f, 0.85f), glass * 0.9f);

                // --- mata ---
                float eyes = Mathf.Max(
                    InsideEllipse(x, y, -0.09f, 0.58f, 0.028f, 0.035f, 0.01f),
                    InsideEllipse(x, y, 0.09f, 0.58f, 0.028f, 0.035f, 0.01f));
                color = Over(color, eye, eyes);

                // --- mulut ---
                float mouth = InsideBox(x, y, 0f, 0.48f, 0.05f, 0.012f, 0.01f);
                color = Over(color, new Color(0.55f, 0.26f, 0.26f, 1f), mouth);

                return color;
            }

            static float InsideTrapezoid(
                float x, float y,
                float yBottom, float yTop,
                float halfBottom, float halfTop,
                float soft)
            {
                if (y < yBottom - soft || y > yTop + soft)
                    return 0f;

                float t = Mathf.Clamp01((y - yBottom) / Mathf.Max(0.0001f, yTop - yBottom));
                float half = Mathf.Lerp(halfBottom, halfTop, t);
                float d = Mathf.Abs(x) - half;

                return 1f - Mathf.SmoothStep(-soft, 0f, d);
            }

            static Vector2[] To2D(Vector3[] source)
            {
                var result = new Vector2[source.Length];
                for (int i = 0; i < source.Length; i++)
                    result[i] = new Vector2(source[i].x, source[i].y);
                return result;
            }

            // ------------------------------------------------------------------
            // Pixel shaders (u, v dalam rentang -1..1)
            // ------------------------------------------------------------------

            static Color ReticlePixel(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);

                float ring = Mathf.SmoothStep(0.70f, 0.80f, r) * (1f - Mathf.SmoothStep(0.90f, 1.00f, r));
                float fill = (1f - Mathf.SmoothStep(0f, 0.75f, r)) * 0.18f;

                float alpha = Mathf.Clamp01(ring + fill);
                return new Color(1f, 0.25f, 0.22f, alpha);
            }

            static Color ShadowPixel(float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f) * 0.55f;
                return new Color(0f, 0f, 0f, alpha);
            }

            static Color PotionPixel(float u, float v)
            {
                Color color = Color.clear;

                float body = InsideEllipse(u, v + 0.15f, 0f, 0f, 0.52f, 0.50f, 0.06f);
                float neck = InsideBox(u, v - 0.38f, 0f, 0f, 0.15f, 0.24f, 0.03f);
                float cork = InsideBox(u, v - 0.68f, 0f, 0f, 0.20f, 0.09f, 0.03f);

                if (body > 0.001f)
                {
                    Color liquid = new Color(0.22f, 0.72f, 0.36f, 0.95f);
                    Color glass = new Color(0.60f, 0.95f, 0.90f, 0.55f);
                    color = Over(color, v < 0f ? liquid : glass, body * (v < 0f ? 1f : 0.75f));
                }

                if (neck > 0.001f)
                    color = Over(color, new Color(0.65f, 0.95f, 0.90f, 0.65f), neck * 0.8f);

                if (cork > 0.001f)
                    color = Over(color, new Color(0.55f, 0.35f, 0.18f, 1f), cork);

                return color;
            }

            static Color ZombiePixel(float u, float v)
            {
                float body = InsideBox(u, v + 0.28f, 0f, 0f, 0.34f, 0.46f, 0.08f);
                float head = InsideEllipse(u, v - 0.40f, 0f, 0f, 0.30f, 0.28f, 0.05f);

                float mask = Mathf.Max(body, head);
                if (mask <= 0.001f)
                    return Color.clear;

                Color color = new Color(0.46f, 0.60f, 0.32f, 1f);
                float shade = Mathf.Lerp(0.75f, 1.05f, Mathf.Clamp01((v + 1f) * 0.5f));
                color = new Color(color.r * shade, color.g * shade, color.b * shade, 1f);

                float eyeLeft = InsideEllipse(u, v - 0.44f, -0.11f, 0f, 0.055f, 0.06f, 0.02f);
                float eyeRight = InsideEllipse(u, v - 0.44f, 0.11f, 0f, 0.055f, 0.06f, 0.02f);
                float eyes = Mathf.Max(eyeLeft, eyeRight);

                color = Color.Lerp(color, new Color(0.05f, 0.08f, 0.05f, 1f), eyes);
                color.a = mask;

                return color;
            }

            // ------------------------------------------------------------------
            // Arena background (placeholder)
            // ------------------------------------------------------------------

            static Color ArenaPixel(Vector2 world, float halfWidth)
            {
                // Langit / lantai arena.
                float t = Mathf.InverseLerp(-4.8f, 4.8f, world.y);
                Color color = Color.Lerp(
                    new Color(0.05f, 0.06f, 0.09f),
                    new Color(0.11f, 0.13f, 0.18f),
                    t);

                // Siluet menara lab di tengah.
                if (Mathf.Abs(world.x) < 1.55f && world.y > -2.55f && world.y < 3.55f)
                {
                    Color brick = new Color(0.26f, 0.17f, 0.15f);

                    float row = Mathf.Floor((world.y + 10f) * 5.5f);
                    float offset = Mathf.Repeat(row, 2f) < 1f ? 0f : 0.5f;
                    float bx = Mathf.Repeat((world.x + offset) * 3.5f, 1f);
                    float by = Mathf.Repeat((world.y + 10f) * 5.5f, 1f);

                    brick *= (bx < 0.05f || by < 0.07f) ? 0.62f : 1f;
                    color = brick;
                }

                // Lantai balkon Marie Curie.
                if (Mathf.Abs(world.x) < 1.85f && world.y > 1.12f && world.y < 1.42f)
                    color = new Color(0.42f, 0.38f, 0.33f);

                // Jalan batu (mengikuti waypoint, sama seperti collider).
                float distance = Mathf.Min(
                    DistanceToPolyline(world, _leftPath),
                    DistanceToPolyline(world, _rightPath));

                float outer = halfWidth + 0.30f;
                if (distance < outer)
                {
                    float blend = Mathf.Clamp01((outer - distance) / 0.30f);
                    color = Color.Lerp(color, StoneColor(world), blend);
                }

                return color;
            }

            static Color StoneColor(Vector2 world)
            {
                float cell = Mathf.Repeat(Mathf.Floor(world.x * 3.2f) + Mathf.Floor(world.y * 3.2f), 2f);

                Color color = cell < 1f
                    ? new Color(0.34f, 0.32f, 0.29f)
                    : new Color(0.28f, 0.27f, 0.25f);

                float fx = Mathf.Repeat(world.x * 3.2f, 1f);
                float fy = Mathf.Repeat(world.y * 3.2f, 1f);

                if (fx < 0.05f || fy < 0.05f)
                    color *= 0.70f;

                return color;
            }

            static float DistanceToPolyline(Vector2 point, Vector2[] points)
            {
                if (points == null || points.Length < 2)
                    return float.MaxValue;

                float best = float.MaxValue;

                for (int i = 0; i < points.Length - 1; i++)
                {
                    float d = Arena25DMath.DistancePointToSegmentSq(point, points[i], points[i + 1]);
                    if (d < best)
                        best = d;
                }

                return Mathf.Sqrt(best);
            }

            // ------------------------------------------------------------------
            // SDF kecil untuk placeholder art
            // ------------------------------------------------------------------

            static Color Over(Color destination, Color source, float alpha)
            {
                alpha = Mathf.Clamp01(alpha);
                float outAlpha = alpha + destination.a * (1f - alpha);

                if (outAlpha <= 0.0001f)
                    return Color.clear;

                return new Color(
                    (source.r * alpha + destination.r * destination.a * (1f - alpha)) / outAlpha,
                    (source.g * alpha + destination.g * destination.a * (1f - alpha)) / outAlpha,
                    (source.b * alpha + destination.b * destination.a * (1f - alpha)) / outAlpha,
                    outAlpha);
            }

            static float InsideEllipse(float u, float v, float cx, float cy, float radiusX, float radiusY, float soft)
            {
                float dx = (u - cx) / Mathf.Max(0.0001f, radiusX);
                float dy = (v - cy) / Mathf.Max(0.0001f, radiusY);
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                return 1f - Mathf.SmoothStep(1f - soft, 1f, d);
            }

            static float InsideBox(float u, float v, float cx, float cy, float halfWidth, float halfHeight, float soft)
            {
                float dx = Mathf.Abs(u - cx) - halfWidth;
                float dy = Mathf.Abs(v - cy) - halfHeight;
                float d = Mathf.Max(dx, dy);

                return 1f - Mathf.SmoothStep(-soft, 0f, d);
            }
        }

        public class ArtSet
        {
            public Sprite Background;
            public Sprite Reticle;
            public Sprite Shadow;
            public Sprite Potion;
            public Sprite Zombie;
        }

        public static class Arena25DStyles
        {
            public static readonly GUIStyle WaypointLabel = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.9f, 0.35f) }
            };

            public static readonly GUIStyle OriginLabel = new GUIStyle
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.45f, 0.95f) }
            };
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Scene View helpers: handle geser untuk setiap waypoint (kiri/kanan) + titik lempar Marie.
    /// PolygonCollider2D otomatis di-refresh setiap kali waypoint digeser (bisa dimatikan di menu).
    /// </summary>
    [InitializeOnLoad]
    static class Arena25DSetupSceneTools
    {
        const string PrefAutoRebuild = "Arena25D.AutoRebuildRoad";

        static Arena25DSetup s_Setup;

        static Arena25DSetupSceneTools()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
        }

        static void OnSceneGui(SceneView sceneView)
        {
            Arena25DSetup setup = FindSetup();
            if (setup == null)
                return;

            Transform root = setup.transform.parent != null ? setup.transform.parent : setup.transform;

            bool moved = false;

            moved |= DrawWaypointHandles(root.Find("Left_Path_Waypoints"), "L");
            moved |= DrawWaypointHandles(root.Find("Right_Path_Waypoints"), "R");

            Transform origin = root.Find("Environment_Layers/MarieCurie_ThrowOrigin");
            if (origin != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(origin.position, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(origin, "Move Throw Origin");
                    origin.position = new Vector3(newPosition.x, newPosition.y, 0f);
                    EditorUtility.SetDirty(origin);

                    // Sinkronkan ke config supaya "Generate" ulang tidak menimpa posisi hasil geser.
                    setup.throwOriginLocalPosition = origin.localPosition;
                    EditorUtility.SetDirty(setup);

                    moved = true;
                }

                Handles.color = new Color(1f, 0.45f, 0.95f, 1f);
                Handles.DrawWireDisc(origin.position, Vector3.forward, 0.14f);
                Handles.Label(origin.position + new Vector3(0.28f, 0.22f, 0f),
                    "Marie - Throw Origin", Arena25DSetup.Arena25DStyles.OriginLabel);
            }

            if (!moved)
                return;

            if (EditorPrefs.GetBool(PrefAutoRebuild, true))
                setup.RebuildRoadPolygon();

            sceneView.Repaint();
        }

        static bool DrawWaypointHandles(Transform root, string prefix)
        {
            if (root == null)
                return false;

            bool moved = false;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform waypoint = root.GetChild(i);

                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = Handles.PositionHandle(waypoint.position, Quaternion.identity);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(waypoint, "Move Arena Waypoint");
                    waypoint.position = new Vector3(newPosition.x, newPosition.y, 0f);
                    EditorUtility.SetDirty(waypoint);
                    moved = true;
                }

                Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                Handles.DrawWireDisc(waypoint.position, Vector3.forward, 0.10f);
                Handles.Label(waypoint.position + new Vector3(0.16f, 0.16f, 0f),
                    prefix + (i + 1), Arena25DSetup.Arena25DStyles.WaypointLabel);
            }

            return moved;
        }

        static Arena25DSetup FindSetup()
        {
            if (s_Setup != null)
                return s_Setup;

            s_Setup = Arena25DSetup.FindExistingSetup();
            return s_Setup;
        }
    }
#endif
}
