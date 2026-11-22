using System;
using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using CuriesCrucible.Audio;
using UnityEngine;

namespace CuriesCrucible.Gameplay
{
    /// <summary>Satu jenis zombie dalam sebuah wave.</summary>
    [Serializable]
    public class WaveSpawnEntry
    {
        public ZombieChemicalEntity prefab;

        [Min(1)] public int count = 5;

        [Tooltip("Jeda antar spawn (detik). Default 5.0f agar tempo lebih santai.")]
        [Min(0.1f)] public float interval = 5.0f;

        [Tooltip("Gantian jalur kiri/kanan tiap spawn.")]
        public bool alternatePaths = true;

        [Tooltip("Spawn pertama mulai dari kiri (kalau alternatePaths aktif).")]
        public bool startFromLeft = true;
    }

    /// <summary>Definisi satu wave.</summary>
    [Serializable]
    public class WaveDefinition
    {
        public string waveName = "Wave";

        [TextArea(1, 3)]
        public string subtitle = "";

        public WaveSpawnEntry[] entries = new WaveSpawnEntry[0];

        [Header("Hadiah")]
        [Min(0)] public int bonusPotions = 0;
        [Min(0)] public int bonusIngredients = 0;

        [Header("Drop bahan per zombie")]
        [Min(1)] public int dropMultiplier = 1;

        [Header("Mutasi (chance 0..1)")]
        [Range(0f, 1f)] public float mutationChance = 0f;
        public bool mutateEnlarge = false;
        public bool mutateSpeedUp = false;
        public bool mutateHarden = false;
        public bool mutateAoEBuff = false;

        [Header("Info")]
        public bool isBossWave = false;
    }

    /// <summary>
    /// Wave spawner: Tutorial -> Easy -> Normal -> Hard -> Boss.
    /// Spawn zombie dari prefab, memasang waypoint kiri/kanan, memberi hadiah ramuan,
    /// dan menandai wave selesai saat semua zombie hilang.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveManager : MonoBehaviour
    {
        [Header("Referensi")]
        public TowerHealthManager tower;
        public AlchemyManager alchemy;

        [Tooltip("Root waypoint kiri (Left_Path_Waypoints).")]
        public Transform leftPath;

        [Tooltip("Root waypoint kanan (Right_Path_Waypoints).")]
        public Transform rightPath;

        [Header("Daftar Wave")]
        public List<WaveDefinition> waves = new List<WaveDefinition>();

        /// <summary>Terpanggil setiap kali zombie baru berhasil di-spawn.</summary>
        public static event Action<ZombieChemicalEntity> OnZombieSpawned;

        [Header("Jadwal")]
        public bool autoStart = true;
        [Tooltip("Waktu tunggu awal sebelum wave pertama dimulai (detik).")]
        public float initialDelay = 8f;
        [Tooltip("Jeda persiapan antar-wave setelah semua zombie di wave sebelumnya dikalahkan (detik, default 6.0f).")]
        public float betweenWaveDelay = 6f;
        [Tooltip("Interval spawn default (detik).")]
        public float spawnInterval = 3.0f;

        [Header("Ritme Spawn Bertahap (Pacing Alkimia)")]
        [Tooltip("Batas maksimal zombie yang boleh hidup bersamaan di arena (default 5). Jika tercapai, timer spawn ditahan.")]
        public int maxAliveEnemies = 5;

        [Tooltip("Interval dasar spawn pada Wave 1 (detik, default 3.2f).")]
        public float wave1SpawnInterval = 3.2f;

        [Tooltip("Interval dasar spawn pada Wave terakhir (detik, default 2.2f, tidak boleh di bawah 2.0s).")]
        public float finalWaveSpawnInterval = 2.2f;

        [Tooltip("Variasi acak tipis interval spawn (+/- detik, misal 0.3f untuk rentang 2.7s - 3.3s pada tempo 3.0s).")]
        public float spawnIntervalJitter = 0.3f;

        [Header("Tutorial Hook")]
        [Tooltip("Jika true, WaveManager menahan jalannya wave sampai tutorial selesai.")]
        public bool isPausedByTutorial = false;

        public void ResumeAfterTutorial()
        {
            isPausedByTutorial = false;
            ResetSpawningState();
            _nextWaveTimer = 0.5f;
            _waitingForNextWave = true;
        }

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        class SpawnItem
        {
            public ZombieChemicalEntity prefab;
            public bool leftSide;
        }

        readonly List<SpawnItem> _queue = new List<SpawnItem>();
        readonly List<ZombieChemicalEntity> _activeZombies = new List<ZombieChemicalEntity>();

        float _spawnTimer;
        float _nextWaveTimer;
        bool _waitingForNextWave;

        /// <summary>Index wave saat ini (-1 = belum mulai).</summary>
        public int CurrentWaveIndex { get; private set; }

        /// <summary>Jumlah zombie yang masih hidup di arena.</summary>
        public int AliveZombies
        {
            get
            {
                _activeZombies.RemoveAll(z => z == null || z.IsDead || !z.gameObject.activeInHierarchy);
                return _activeZombies.Count;
            }
        }

        public bool IsWaveActive { get; private set; }
        public bool IsWaitingForNextWave { get { return _waitingForNextWave; } }
        public bool IsGameComplete { get; private set; }
        public int TotalWaves { get { return waves != null ? waves.Count : 0; } }

        public WaveDefinition CurrentWave
        {
            get
            {
                if (waves == null || CurrentWaveIndex < 0 || CurrentWaveIndex >= waves.Count)
                    return null;
                return waves[CurrentWaveIndex];
            }
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ZombieChemicalEntity.OnZombieRemoved += HandleZombieRemoved;
            ResolveReferences();
            if (tower != null)
                tower.OnGameOver += HandleTowerGameOver;
        }

        void OnDisable()
        {
            ZombieChemicalEntity.OnZombieRemoved -= HandleZombieRemoved;
            if (tower != null)
                tower.OnGameOver -= HandleTowerGameOver;
            ResetSpawningState();
        }

        void HandleTowerGameOver()
        {
            GameAudioManager.Instance?.PlayContinueBGM();
        }

        void OnDestroy()
        {
            ResetSpawningState();
        }

        public void ResetSpawningState()
        {
            StopAllCoroutines();
            _queue.Clear();
            _activeZombies.Clear();
            _spawnTimer = 0f;
            _nextWaveTimer = 0f;
            _waitingForNextWave = false;
            IsWaveActive = false;
        }

        void Start()
        {
            ResolveReferences();

            if (autoStart)
            {
                _nextWaveTimer = Mathf.Max(0.5f, initialDelay);
                GameAudioManager.Instance?.PlayMenuBGM();
            }
        }

        void Update()
        {
            if (isPausedByTutorial)
                return;

            if (IsGameComplete)
                return;

            if (tower != null && tower.IsGameOver)
                return;

            if (Input.GetKeyDown(KeyCode.Space) && _waitingForNextWave)
            {
                _waitingForNextWave = false;
                _nextWaveTimer = 0f;
            }

            if (!IsWaveActive)
            {
                if (!autoStart && !_waitingForNextWave)
                    return;

                _nextWaveTimer -= Time.deltaTime;

                if (_nextWaveTimer <= 0f)
                    StartNextWave();

                return;
            }

            UpdateSpawning();

            if (_queue.Count == 0 && AliveZombies <= 0)
                CompleteWave();
        }

        void ResolveReferences()
        {
            if (tower == null)
                tower = TowerHealthManager.Instance;

            if (alchemy == null)
                alchemy = AlchemyManager.Instance;

            if (leftPath == null)
                leftPath = FindPathRoot("Left_Path_Waypoints");

            if (rightPath == null)
                rightPath = FindPathRoot("Right_Path_Waypoints");
        }

        Transform FindPathRoot(string name)
        {
            GameObject arena = GameObject.Find("Arena_25D");
            if (arena == null)
                return null;

            Transform found = arena.transform.Find(name);
            return found;
        }

        // ------------------------------------------------------------------
        // Wave control
        // ------------------------------------------------------------------

        public void StartNextWave()
        {
            ResolveReferences();

            if (waves == null || waves.Count == 0)
            {
                Debug.LogWarning("[Wave] Tidak ada wave yang dikonfigurasi.");
                return;
            }

            if (CurrentWaveIndex >= waves.Count - 1)
            {
                IsGameComplete = true;
                Debug.Log("[Wave] SEMUA WAVE SELESAI!");
                return;
            }

            // Bersihkan timer dan antrean lama untuk mencegah double-spawning
            StopAllCoroutines();
            _queue.Clear();

            CurrentWaveIndex++;

            WaveDefinition wave = waves[CurrentWaveIndex];

            IsWaveActive = true;
            _waitingForNextWave = false;

            Debug.Log("[Wave] " + wave.waveName + " dimulai" +
                      (string.IsNullOrEmpty(wave.subtitle) ? "" : " - " + wave.subtitle));

            GiveWaveBonuses(wave);
            BuildQueue(wave);

            // Mainkan BGM pertempuran wave sesuai nomor urut (1-based)
            GameAudioManager.Instance?.PlayWaveBGM(CurrentWaveIndex + 1);

            // Jeda awal sebelum zombie pertama wave ini muncul (mengikuti tempo wave)
            _spawnTimer = GetNextSpawnInterval();
        }

        void GiveWaveBonuses(WaveDefinition wave)
        {
            if (alchemy == null)
                return;

            if (wave.bonusPotions > 0)
                alchemy.AddRandomPotions(wave.bonusPotions);

            if (wave.bonusIngredients > 0)
                alchemy.AddRandomIngredient(wave.bonusIngredients);
        }

        void BuildQueue(WaveDefinition wave)
        {
            if (wave == null || wave.entries == null)
                return;

            bool leftSide = true;

            int totalWaveLimit = int.MaxValue;
            if (CurrentWaveIndex == 0)
            {
                // Wave 1: batasi jumlah sedikit (maksimal 5 zombie) agar pemain sempat adaptasi & meracik
                totalWaveLimit = 5;
            }

            int enqueued = 0;
            for (int e = 0; e < wave.entries.Length; e++)
            {
                WaveSpawnEntry entry = wave.entries[e];

                if (entry == null || entry.prefab == null)
                    continue;

                int count = entry.count;
                for (int i = 0; i < count; i++)
                {
                    if (enqueued >= totalWaveLimit)
                        break;

                    bool useLeft = entry.alternatePaths ? leftSide : entry.startFromLeft;

                    _queue.Add(new SpawnItem
                    {
                        prefab = entry.prefab,
                        leftSide = useLeft
                    });

                    enqueued++;

                    if (entry.alternatePaths)
                        leftSide = !leftSide;
                }

                if (enqueued >= totalWaveLimit)
                    break;
            }

            // Jika Wave 1 memiliki prefab tetapi jumlah entri di bawah 4 (misal default 3),
            // tambahkan sampai minimal 4 agar berada di rentang 4–5 zombie
            if (CurrentWaveIndex == 0 && enqueued < 4 && _queue.Count > 0)
            {
                ZombieChemicalEntity fallbackPrefab = _queue[0].prefab;
                while (enqueued < 4)
                {
                    _queue.Add(new SpawnItem
                    {
                        prefab = fallbackPrefab,
                        leftSide = leftSide
                    });
                    leftSide = !leftSide;
                    enqueued++;
                }
            }
        }

        /// <summary>
        /// Hitung interval jeda spawn zombie berikutnya secara bertahap & ritmis:
        /// - Wave 1: jeda ~3.2 detik (tempo santai).
        /// - Wave akhir: jeda minimal ~2.2 detik (meningkat kesulitannya).
        /// - Variasi acak tipis 2.7f s/d 3.3f detik per spawn pada tempo 3.0s.
        /// - Clamping ketat: interval tidak pernah di bawah 2.0 detik.
        /// </summary>
        public float GetNextSpawnInterval()
        {
            float progress = 0f;
            if (TotalWaves > 1)
            {
                progress = Mathf.Clamp01((float)CurrentWaveIndex / (TotalWaves - 1));
            }

            // Interpolasi jeda dasar dari Wave 1 ke Wave akhir
            float baseInterval = Mathf.Lerp(wave1SpawnInterval, finalWaveSpawnInterval, progress);

            // Variasi acak tipis (+/- spawnIntervalJitter)
            float jitter = UnityEngine.Random.Range(-spawnIntervalJitter, spawnIntervalJitter);
            float calculated = baseInterval + jitter;

            // Jangan pernah di bawah 2.0 detik
            return Mathf.Max(2.0f, calculated);
        }

        void UpdateSpawning()
        {
            if (_queue.Count == 0)
                return;

            // Batas maksimal zombie hidup bersamaan di arena (maxAliveEnemies).
            // Jika sudah mencapai batas, tahan timer spawn sampai ada zombie yang mati.
            if (AliveZombies >= maxAliveEnemies)
                return;

            _spawnTimer -= Time.deltaTime;

            if (_spawnTimer > 0f)
                return;

            SpawnItem item = _queue[0];
            _queue.RemoveAt(0);

            SpawnZombie(item.prefab, item.leftSide);

            // Jadwalkan spawn berikutnya secara ritmis
            _spawnTimer = GetNextSpawnInterval();
        }

        void SpawnZombie(ZombieChemicalEntity prefab, bool leftSide)
        {
            if (prefab == null)
                return;

            Transform path = leftSide ? leftPath : rightPath;

            if (path == null)
            {
                Debug.LogWarning("[Wave] Path root tidak ditemukan (Left/Right_Path_Waypoints).");
                return;
            }

            WaveDefinition wave = CurrentWave;

            Vector3 spawnPosition = path.childCount > 0
                ? path.GetChild(0).position
                : transform.position;

            GameObject instance = Instantiate(prefab.gameObject, spawnPosition, Quaternion.identity);
            instance.name = prefab.gameObject.name;

            ZombieChemicalEntity entity = instance.GetComponent<ZombieChemicalEntity>();
            if (entity == null)
                entity = instance.AddComponent<ZombieChemicalEntity>();

            PathFollower25D follower = instance.GetComponent<PathFollower25D>();
            if (follower == null)
                follower = instance.AddComponent<PathFollower25D>();

            // Pasang waypoint sesuai sisi jalur.
            var points = new List<Transform>(path.childCount);
            for (int i = 0; i < path.childCount; i++)
                points.Add(path.GetChild(i));

            follower.waypoints = points.ToArray();
            follower.loopPath = false;
            follower.moveSpeed = entity.moveSpeed;
            follower.adjustSpeedByPerspective = true;

            entity.follower = follower;
            entity.alchemy = alchemy;

            // Collider agar kena OverlapCircleAll dari ramuan.
            if (instance.GetComponent<Collider2D>() == null)
            {
                var capsule = instance.AddComponent<CapsuleCollider2D>();
                capsule.isTrigger = true;
                capsule.size = new Vector2(0.55f, 1.0f);
                capsule.offset = new Vector2(0f, 0.5f);
            }

            ApplyWaveConfig(entity, wave);
            follower.Restart();

            _activeZombies.Add(entity);
            OnZombieSpawned?.Invoke(entity);
        }

        void ApplyWaveConfig(ZombieChemicalEntity entity, WaveDefinition wave)
        {
            if (wave == null)
                return;

            entity.ingredientDropCount = Mathf.Max(1, wave.dropMultiplier);

            if (wave.mutationChance <= 0f || UnityEngine.Random.value > wave.mutationChance)
                return;

            if (wave.mutateEnlarge)
                entity.Mutate_Enlarge();

            if (wave.mutateSpeedUp)
                entity.Mutate_SpeedUp();

            if (wave.mutateHarden)
                entity.Mutate_Harden();

            if (wave.mutateAoEBuff)
                entity.Mutate_AoEBuff();
        }

        void CompleteWave()
        {
            StopAllCoroutines();
            IsWaveActive = false;
            _queue.Clear();
            _spawnTimer = 0f;

            WaveDefinition wave = CurrentWave;

            if (wave != null)
                Debug.Log("[Wave] " + wave.waveName + " selesai.");

            if (CurrentWaveIndex >= waves.Count - 1)
            {
                IsGameComplete = true;
                Debug.Log("[Wave] SEMUA WAVE SELESAI - menara bertahan!");
                GameAudioManager.Instance?.PlayEndingBGM();
                return;
            }

            _waitingForNextWave = true;
            _nextWaveTimer = Mathf.Max(1f, betweenWaveDelay);

            // Jeda antar-wave / interlude (crafting ramuan)
            GameAudioManager.Instance?.PlayInterludeBGM();
        }

        void HandleZombieRemoved(ZombieChemicalEntity zombie)
        {
            if (zombie != null)
                _activeZombies.Remove(zombie);
        }
    }
}
