using System;
using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CuriesCrucible.Arena25D
{
    /// <summary>
    /// Kontrak target ramuan. Implementasikan di script enemy (lihat ZombieChemicalEntity),
    /// lalu assign layer enemy ke ArenaThrowTargeting.enemyMask.
    /// </summary>
    public interface IArenaPotionTarget
    {
        void ApplyPotionHit(Vector2 worldPoint, MixedPotion potion, GameObject source);
    }

    /// <summary>
    /// Input & targeting lemparan ramuan Marie Curie.
    ///
    ///   - Mendeteksi kursor di atas "WalkableRoad" memakai Physics2D.Raycast (fallback OverlapPoint).
    ///   - Reticle elips merah (scale.y = scale.x * 0.55) menempel di permukaan jalan, ukurannya
    ///     mengikuti skala perspektif Y layar.
    ///   - Klik kiri = lempar proyektil parabola dari MarieCurie_ThrowOrigin ke titik kursor.
    ///     Bayangan bergerak lurus di tanah, botol melengkung di udara.
    ///
    /// Aman WebGL: memakai legacy Input Manager (activeInputHandler = 0), tanpa coroutine.
    /// </summary>
    [DisallowMultipleComponent]
    public class ArenaThrowTargeting : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Kosongkan untuk otomatis pakai Camera.main.")]
        public Camera arenaCamera;

        [Tooltip("Transform MarieCurie_ThrowOrigin (titik awal lemparan di balkon tengah).")]
        public Transform throwOrigin;

        [Tooltip("Container untuk instance proyektil. Dibuat otomatis kalau kosong.")]
        public Transform projectileRoot;

        [Header("Integrasi Gameplay")]
        [Tooltip("AlchemyManager pemilik stok ramuan. Kalau kosong, lemparan memakai damage/radius default.")]
        public AlchemyManager alchemy;

        [Tooltip("Marie Curie di balkon (untuk memicu animasi lempar).")]
        public MarieCurieController marie;

        [Header("Aim Reticle")]
        [Tooltip("SpriteRenderer indikator AoE (biasanya child 'Aim_Reticle').")]
        public SpriteRenderer reticle;

        [Tooltip("Radius AoE reticle dalam world unit.")]
        public float reticleRadius = 1.6f;

        [Tooltip("Squash perspektif: scale.y = scale.x * nilai ini (0.55 = elips 2.5D).")]
        [Range(0.2f, 1f)]
        public float reticlePerspectiveSquash = Arena25DMath.DefaultPerspectiveSquash;

        [Tooltip("Denyut halus biar reticle terasa hidup (0 = mati).")]
        public float reticlePulseAmplitude = 0.04f;
        public float reticlePulseSpeed = 3.5f;

        public Color reticleValidColor = new Color(1f, 0.25f, 0.22f, 0.9f);

        [Tooltip("Bias sorting reticle (-2 = selalu di bawah zombie pada Y yang sama).")]
        public int reticleSortingBias = -2;

        [Header("Perspective (harus sama dengan PathFollower25D)")]
        public float perspectiveBottomY = Arena25DMath.DefaultNearY;
        public float perspectiveTopY = Arena25DMath.DefaultFarY;
        public float nearScale = Arena25DMath.DefaultNearScale;
        public float farScale = Arena25DMath.DefaultFarScale;

        [Header("Projectile")]
        public Sprite potionSprite;
        public Sprite shadowSprite;

        [Tooltip("Radius botol di dunia.")]
        public float potionRadius = 0.22f;

        [Tooltip("Radius bayangan di dunia.")]
        public float shadowRadius = 0.30f;

        [Tooltip("Durasi terbang (detik).")]
        public float flightTime = 0.55f;

        [Tooltip("Tinggi lengkung minimum / maksimum (world unit).")]
        public float minArcHeight = 1.2f;
        public float maxArcHeight = 3.4f;

        [Tooltip("Jarak lempar yang dianggap 'jauh' untuk memilih tinggi lengkung.")]
        public float maxArcDistance = 10f;

        [Tooltip("Jeda antar lemparan (detik).")]
        public float throwCooldown = 0.3f;

        [Tooltip("Sorting order botol saat terbang (harus di atas foreground balkon = 500).")]
        public int potionSortingOrder = 600;

        [Header("Shadow Sorting")]
        public int shadowSortingBias = -1;
        [Tooltip("Bayangan tidak pernah masuk di belakang background arena.")]
        public int minShadowSortingOrder = -90;

        [Header("Impact")]
        [Tooltip("Sprite splash saat botol pecah (boleh sama dengan reticle).")]
        public Sprite splashSprite;
        public float impactRadius = 1.7f;
        public float impactDamage = 34f;

        [Tooltip("Layer musuh. 0 = tidak menerapkan damage (hanya efek visual).")]
        public LayerMask enemyMask;

        public float splashDuration = 0.35f;
        public int splashSortingOffset = 40;

        [Header("Road Detection")]
        [Tooltip("Layer collider jalan (WalkableRoad).")]
        public LayerMask walkableRoadMask;

        [Tooltip("Abaikan klik saat kursor di atas UI (EventSystem).")]
        public bool blockThrowOverUI = true;

        [Tooltip("Pastikan Physics2D tetap mendeteksi trigger (collider jalan dibuat sebagai trigger).")]
        public bool forceQueriesHitTriggers = true;

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        class Projectile
        {
            public GameObject Potion;
            public GameObject Shadow;
            public SpriteRenderer PotionRenderer;
            public SpriteRenderer ShadowRenderer;
            public Vector2 Start;
            public Vector2 End;
            public float Age;
            public float Duration;
            public float ArcHeight;
            public float PotionBaseScale;
            public float ShadowBaseScale;

            // Data ramuan yang sedang terbang (disalin saat lempar).
            public MixedPotion PotionData;
        }

        class Splash
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public float Age;
            public float Duration;
            public float StartScale;
            public float EndScale;
            public Color Color;
        }

        readonly List<Projectile> _projectiles = new List<Projectile>(8);
        readonly List<Splash> _splashes = new List<Splash>(8);

        Camera _camera;
        Transform _projectileContainer;
        float _nextThrowTime;
        float _pulsePhase;
        Vector2 _aimPoint;
        bool _aimValid;

        /// <summary>Titik AoE terakhir yang valid (world XY).</summary>
        public Vector2 CurrentAimPoint { get { return _aimPoint; } }

        /// <summary>Apakah kursor sekarang berada di atas jalan yang walkable.</summary>
        public bool IsAimValid { get { return _aimValid; } }

        /// <summary>Terpanggil setiap kali botol pecah di tanah.</summary>
        public event Action<Vector2> OnImpact;

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            _camera = arenaCamera != null ? arenaCamera : Camera.main;

            if (forceQueriesHitTriggers)
                Physics2D.queriesHitTriggers = true;
        }

        void Update()
        {
            if (_camera == null)
                _camera = Camera.main;

            _pulsePhase += reticlePulseSpeed * Time.deltaTime;

            UpdateAim();
            UpdateProjectiles();
            UpdateSplashes();
        }

        // ------------------------------------------------------------------
        // Aiming
        // ------------------------------------------------------------------

        void UpdateAim()
        {
            if (Time.timeScale <= 0f || CuriesCrucible.Gameplay.ZombieIntroSpotlight.IsSpotlightActive)
            {
                if (reticle != null)
                    reticle.enabled = false;
                return;
            }

            if (_camera == null)
                _camera = Camera.main;

            // Tanpa kamera, tidak ada yang bisa di-raycast (misal scene belum di-setup).
            if (_camera == null)
                return;

            Vector3 screenPoint = Input.mousePosition;
            Vector3 worldPoint = Arena25DMath.ScreenToWorld(_camera, screenPoint, 0f);
            Vector2 world = new Vector2(worldPoint.x, worldPoint.y);

            bool overUi = blockThrowOverUI
                          && EventSystem.current != null
                          && (EventSystem.current.IsPointerOverGameObject() ||
                              (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)));

            // Proteksi lemparan: abaikan aiming jika kursor sedang mengarah ke gelembung panen pipa menara
            bool overBubble = CuriesCrucible.Gameplay.TowerPipeHarvester.IsPointerOverBubble();

            Collider2D roadCollider = null;
            Vector2 hitPoint = world;

            if (walkableRoadMask.value != 0)
            {
                // Raycast titik (direction nol) = "apakah kursor ada di atas collider jalan".
                RaycastHit2D raycastHit = Physics2D.Raycast(world, Vector2.zero, 0.01f, walkableRoadMask);

                if (raycastHit.collider != null)
                {
                    roadCollider = raycastHit.collider;
                    hitPoint = raycastHit.point;
                }
                else
                {
                    // Fallback untuk kondisi project settings tertentu.
                    roadCollider = Physics2D.OverlapPoint(world, walkableRoadMask);

                    if (roadCollider != null)
                        hitPoint = roadCollider.ClosestPoint(world);
                }
            }

            bool onRoad = roadCollider != null;
            _aimValid = onRoad && !overUi && !overBubble;
            _aimPoint = onRoad ? hitPoint : world;

            if (reticle != null)
            {
                reticle.enabled = _aimValid;

                if (_aimValid)
                    UpdateReticle(_aimPoint);
            }

            if (_aimValid && Input.GetMouseButtonDown(0) && Time.time >= _nextThrowTime)
                ThrowPotion(_aimPoint);
        }

        void UpdateReticle(Vector2 point)
        {
            float perspective = Arena25DMath.PerspectiveScale(
                point.y, perspectiveBottomY, perspectiveTopY, nearScale, farScale);

            float pulse = 1f + Mathf.Sin(_pulsePhase) * reticlePulseAmplitude;

            // Radius reticle mengikuti AoE ramuan yang sedang dipilih (kalau ada alchemy).
            float radius = reticleRadius;
            MixedPotion selected = alchemy != null ? alchemy.SelectedPotion : null;

            if (selected != null && selected.radius > 0f)
                radius = selected.radius;

            reticle.transform.position = new Vector3(point.x, point.y, 0f);
            reticle.transform.localScale = Arena25DMath.ScaleForSpriteRadius(
                reticle.sprite, radius, perspective * pulse, reticlePerspectiveSquash);

            reticle.color = reticleValidColor;
            reticle.sortingOrder = Arena25DMath.GroundSortingOrder(point.y, reticleSortingBias, -90);
        }

        // ------------------------------------------------------------------
        // Throwing
        // ------------------------------------------------------------------

        void ThrowPotion(Vector2 target)
        {
            MixedPotion potion = null;

            if (alchemy != null)
            {
                // Kurangi stok ramuan. Kalau tas kosong, lemparan dibatalkan.
                if (!alchemy.TryConsumeSelected(out potion))
                    return;
            }
            else
            {
                potion = CreateFallbackPotion();
            }

            _nextThrowTime = Time.time + Mathf.Max(0f, throwCooldown);

            Vector3 originWorld = throwOrigin != null ? throwOrigin.position : new Vector3(0f, 1.5f, 0f);
            Vector2 start = new Vector2(originWorld.x, originWorld.y);

            float distance = Vector2.Distance(start, target);
            float arc = Mathf.Lerp(minArcHeight, maxArcHeight,
                Mathf.Clamp01(distance / Mathf.Max(0.001f, maxArcDistance)));

            Transform container = GetProjectileRoot();

            var projectile = new Projectile
            {
                Start = start,
                End = target,
                Duration = Mathf.Max(0.05f, flightTime),
                ArcHeight = arc,
                PotionData = potion
            };

            // --- Botol (melengkung di udara) ---
            var potionGo = new GameObject("Potion_Projectile");
            potionGo.transform.SetParent(container, false);
            potionGo.transform.position = new Vector3(start.x, start.y, 0f);

            var potionRenderer = potionGo.AddComponent<SpriteRenderer>();
            potionRenderer.sprite = potionSprite;
            potionRenderer.sortingOrder = potionSortingOrder;
            potionRenderer.color = Color.Lerp(potion.color, Color.white, 0.35f);

            projectile.Potion = potionGo;
            projectile.PotionRenderer = potionRenderer;

            float potionSpriteSize = potionSprite != null ? Mathf.Max(0.0001f, potionSprite.bounds.size.x) : 1f;
            projectile.PotionBaseScale = (potionRadius * 2f) / potionSpriteSize;

            // --- Bayangan (lurus di tanah) ---
            var shadowGo = new GameObject("Potion_Shadow");
            shadowGo.transform.SetParent(container, false);
            shadowGo.transform.position = new Vector3(start.x, start.y, 0f);

            var shadowRenderer = shadowGo.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = shadowSprite;
            shadowRenderer.color = Color.white;

            projectile.Shadow = shadowGo;
            projectile.ShadowRenderer = shadowRenderer;

            float shadowSpriteSize = shadowSprite != null ? Mathf.Max(0.0001f, shadowSprite.bounds.size.x) : 1f;
            projectile.ShadowBaseScale = (shadowRadius * 2f) / shadowSpriteSize;

            _projectiles.Add(projectile);

            PoseProjectile(projectile, 0f);

            // Picu animasi lempar Marie Curie.
            if (marie != null)
                marie.PlayThrowAnimation();
        }

        MixedPotion CreateFallbackPotion()
        {
            return new MixedPotion
            {
                type = PotionType.GelSabunBeku,
                displayName = AlchemyDatabase.PotionName(PotionType.GelSabunBeku),
                color = new Color(0.6f, 0.9f, 1f),
                damage = impactDamage,
                radius = impactRadius
            };
        }

        void UpdateProjectiles()
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = _projectiles[i];

                if (projectile.Potion == null || projectile.Shadow == null)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                projectile.Age += Time.deltaTime;
                float t = Mathf.Clamp01(projectile.Age / projectile.Duration);

                PoseProjectile(projectile, t);

                if (t >= 1f)
                {
                    Impact(projectile);
                    Destroy(projectile.Potion);
                    Destroy(projectile.Shadow);
                    _projectiles.RemoveAt(i);
                }
            }
        }

        void PoseProjectile(Projectile projectile, float t)
        {
            // Posisi tanah bergerak lurus dari throw origin ke target.
            Vector2 ground = Vector2.Lerp(projectile.Start, projectile.End, t);

            // Lifting parabola: 4h*t*(1-t) -> 0 di ujung, h di tengah.
            float arc = 4f * projectile.ArcHeight * t * (1f - t);

            float perspective = Arena25DMath.PerspectiveScale(
                ground.y, perspectiveBottomY, perspectiveTopY, nearScale, farScale);

            // --- Botol ---
            projectile.Potion.transform.position = new Vector3(ground.x, ground.y + arc, 0f);
            projectile.Potion.transform.localScale = Vector3.one * (projectile.PotionBaseScale * perspective);
            projectile.Potion.transform.localRotation = Quaternion.Euler(0f, 0f, t * 360f);
            projectile.PotionRenderer.sortingOrder = potionSortingOrder;

            // --- Bayangan ---
            float heightRatio = projectile.ArcHeight > 0.001f ? Mathf.Clamp01(arc / projectile.ArcHeight) : 0f;
            float shadowScale = projectile.ShadowBaseScale * perspective * Mathf.Lerp(1f, 0.55f, heightRatio);

            projectile.Shadow.transform.position = new Vector3(ground.x, ground.y, 0f);
            projectile.Shadow.transform.localScale = new Vector3(shadowScale, shadowScale * Arena25DMath.DefaultPerspectiveSquash, 1f);

            Color shadowColor = projectile.ShadowRenderer.color;
            shadowColor.a = Mathf.Lerp(0.55f, 0.22f, heightRatio);
            projectile.ShadowRenderer.color = shadowColor;

            projectile.ShadowRenderer.sortingOrder = Arena25DMath.GroundSortingOrder(
                ground.y, shadowSortingBias, minShadowSortingOrder);
        }

        // ------------------------------------------------------------------
        // Impact
        // ------------------------------------------------------------------

        void Impact(Projectile projectile)
        {
            Vector2 point = projectile.End;
            MixedPotion potion = projectile.PotionData;

            float radius = potion != null ? potion.radius : impactRadius;
            float damage = potion != null ? potion.damage : impactDamage;

            if (damage > 0f)
            {
                int mask = enemyMask.value != 0
                    ? enemyMask.value
                    : Physics2D.DefaultRaycastLayers;

                Collider2D[] hits = Physics2D.OverlapCircleAll(point, radius, mask);

                for (int i = 0; i < hits.Length; i++)
                {
                    if (hits[i] == null)
                        continue;

                    IArenaPotionTarget target = hits[i].GetComponentInParent<IArenaPotionTarget>();
                    if (target != null)
                        target.ApplyPotionHit(point, potion, gameObject);
                }
            }

            Color splashColor = potion != null
                ? potion.color
                : new Color(1f, 0.85f, 0.85f, 0.9f);

            SpawnSplash(point, radius, splashColor);

            if (OnImpact != null)
                OnImpact(point);
        }

        void SpawnSplash(Vector2 point, float radius, Color color)
        {
            Sprite sprite = splashSprite != null ? splashSprite : reticle != null ? reticle.sprite : null;
            if (sprite == null)
                return;

            var splashGo = new GameObject("Potion_Splash");
            splashGo.transform.SetParent(GetProjectileRoot(), false);
            splashGo.transform.position = new Vector3(point.x, point.y, 0f);

            var renderer = splashGo.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Arena25DMath.GroundSortingOrder(point.y, splashSortingOffset, -90);

            float perspective = Arena25DMath.PerspectiveScale(
                point.y, perspectiveBottomY, perspectiveTopY, nearScale, farScale);

            float spriteSize = Mathf.Max(0.0001f, sprite.bounds.size.x);
            float fullScale = (radius * 2f / spriteSize) * perspective;

            _splashes.Add(new Splash
            {
                Root = splashGo,
                Renderer = renderer,
                Duration = Mathf.Max(0.05f, splashDuration),
                StartScale = fullScale * 0.35f,
                EndScale = fullScale * 1.1f,
                Color = color
            });
        }

        void UpdateSplashes()
        {
            for (int i = _splashes.Count - 1; i >= 0; i--)
            {
                Splash splash = _splashes[i];

                if (splash.Root == null)
                {
                    _splashes.RemoveAt(i);
                    continue;
                }

                splash.Age += Time.deltaTime;
                float t = Mathf.Clamp01(splash.Age / splash.Duration);

                float scale = Mathf.Lerp(splash.StartScale, splash.EndScale, t);
                splash.Root.transform.localScale = new Vector3(scale, scale * Arena25DMath.DefaultPerspectiveSquash, 1f);

                Color color = splash.Color;
                color.a = Mathf.Lerp(splash.Color.a, 0f, t);
                splash.Renderer.color = color;

                if (t >= 1f)
                {
                    Destroy(splash.Root);
                    _splashes.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        Transform GetProjectileRoot()
        {
            if (_projectileContainer != null)
                return _projectileContainer;

            _projectileContainer = projectileRoot;

            if (_projectileContainer == null)
            {
                GameObject existing = GameObject.Find("Projectiles");
                _projectileContainer = existing != null
                    ? existing.transform
                    : new GameObject("Projectiles").transform;
            }

            return _projectileContainer;
        }

        void OnDrawGizmosSelected()
        {
            if (throwOrigin == null)
                return;

            Gizmos.color = new Color(1f, 0.1f, 0.9f, 1f);
            Gizmos.DrawWireSphere(throwOrigin.position, 0.18f);
            Gizmos.DrawLine(throwOrigin.position, new Vector3(_aimPoint.x, _aimPoint.y, 0f));
        }
    }
}