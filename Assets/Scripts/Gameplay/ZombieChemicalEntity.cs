using System;
using System.Collections.Generic;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Arena25D;
using UnityEngine;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// Zombie dengan afinitas kimia. Mengimplementasikan IArenaPotionTarget sehingga
    /// ramuan dari ArenaThrowTargeting langsung bereaksi di sini: damage, root, slow,
    /// knockback, mutasi, drop bahan, dan mekanik dua tahap BossUnstableAmalgam.
    ///
    /// Matriks reaksi (semua 5 tipe x 6 ramuan) diuraikan di HandleNormalZombieHit / HandleBossHit.
    /// Setiap reaksi memunculkan Floating Chemical Feedback Text (ChemicalFeedbackManager):
    ///   - Efektif/benar  -> hijau/cyan.
    ///   - Salah/mutasi   -> merah-oranye + tanda peringatan.
    ///   - Netral         -> putih/kuning.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZombieChemicalEntity : MonoBehaviour, IArenaPotionTarget
    {
        [Header("Identitas")]
        public ZombieType zombieType = ZombieType.ZombieBasa;

        [Min(1)] public int maxHealth = 40;
        [SerializeField]
        [Tooltip("Kecepatan dasar jalan zombie (world units/detik). Default tempo santai: 0.10f - 0.20f.")]
        public float moveSpeed = 0.15f;
        [Min(0)] public int towerDamage = 1;

        [Header("Afinitas Kimia (pengali damage)")]
        [Range(0f, 5f)] public float acidVulnerability = 1f;
        [Range(0f, 5f)] public float baseVulnerability = 1f;
        [Range(0f, 5f)] public float freezeVulnerability = 1f;
        [Range(0f, 5f)] public float neutralizeVulnerability = 1f;

        [Header("Drop Bahan")]
        [Min(0)] public int ingredientDropCount = 1;
        [Range(0f, 1f)] public float dropChance = 1f;

        [Header("Visual")]
        public SpriteRenderer spriteRenderer;
        public Color baseColor = Color.white;
        public Color chilledColor = new Color(0.55f, 0.85f, 1f, 1f);

        [Tooltip("Warna tint saat zombie BEKU / ROOT (biru es).")]
        public Color frozenColor = new Color(0.45f, 0.85f, 1f, 1f);

        [Header("Referensi")]
        public PathFollower25D follower;
        public AlchemyManager alchemy;

        // ------------------------------------------------------------------
        // Palet warna feedback (Victorian / alkimia)
        // ------------------------------------------------------------------

        static readonly Color EmeraldColor = new Color(0.29f, 0.87f, 0.50f);     // #4ADE80 hijau zamrud
        static readonly Color IceBlueColor = new Color(0.22f, 0.74f, 0.97f);     // #38BDF8 biru es
        static readonly Color GoldColor = new Color(0.98f, 0.80f, 0.08f);        // #FACC15 emas
        static readonly Color WarningRed = new Color(0.97f, 0.44f, 0.44f);       // #F87171 merah
        static readonly Color WarningOrange = new Color(0.98f, 0.57f, 0.24f);    // #FB923C oranye
        static readonly Color WarmWhite = new Color(0.97f, 0.98f, 0.99f);        // #F8FAFC putih hangat

        // ------------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------------

        int _health;
        bool _dead;

        float _rootedUntil;
        float _slowUntil;
        float _slowFactor = 1f;

        float _knockbackRemaining;
        float _externalSpeedBuff = 1f;
        float _externalBuffUntil;

        // Boss chill: _chillStacks (0..3) + _chillUntil (status Chilled / siap dinetralkan).
        float _chillUntil;
        int _chillStacks;
        float _chillStackExpire;   // jendela waktu akumulasi stack parsial (1/3, 2/3).

        float _flashUntil;
        float _auraTimer;

        bool _hasHarden;
        bool _hasAoEBuff;

        // --- Status & mutasi kimia ---
        int _hardenedArmorCharges;        // Zombie_Besi: zirah mengeras, menahan 40% damage berikutnya.
        float _frozenUntil;               // Zombie_Api: tint biru es selama BEKU.
        bool _isSlippery;                 // Zombie_Basa: efek licin (speed +40%, sekali, tidak menumpuk).

        // --- Skala mutasi mulus (dipicu ramuan Asam pada Zombie_Asam -> 1.45f) ---
        float _mutationScaleCurrent = 1f;
        float _mutationScaleTarget = 1f;
        float _lastAppliedMutationScale = 1f;

        /// <summary>Semua zombie aktif (dipakai aura Mutate_AoEBuff).</summary>
        public static readonly List<ZombieChemicalEntity> Active = new List<ZombieChemicalEntity>();

        /// <summary>Terpanggil saat zombie keluar dari permainan (mati / sampai menara).</summary>
        public static event Action<ZombieChemicalEntity> OnZombieRemoved;

        // ------------------------------------------------------------------
        // Properties
        // ------------------------------------------------------------------

        public int Health { get { return _health; } }
        public bool IsDead { get { return _dead; } }

        public bool IsChilled
        {
            get { return zombieType == ZombieType.BossUnstableAmalgam && Time.time < _chillUntil; }
        }

        public int ChillStacks { get { return _chillStacks; } }

        public bool IsRooted
        {
            get { return Time.time < _rootedUntil; }
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            _health = Mathf.Max(1, maxHealth);

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
                baseColor = spriteRenderer.color;

            if (follower == null)
                follower = GetComponent<PathFollower25D>();

            // Inisialisasi skala mutasi dari nilai awal follower (biasanya 1 = normal).
            if (follower != null)
            {
                _mutationScaleCurrent = follower.mutationScale;
                _mutationScaleTarget = follower.mutationScale;
                _lastAppliedMutationScale = follower.mutationScale;
            }

            if (alchemy == null)
                alchemy = AlchemyManager.Instance;
        }

        void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
        }

        void Start()
        {
            if (follower == null)
                follower = GetComponent<PathFollower25D>();

            if (follower != null)
                follower.OnReachedEnd += HandleReachedEnd;

            // Pastikan font teks nama zombie menggunakan font dari Resources/Fonts
            var nameTexts = GetComponentsInChildren<UnityEngine.UI.Text>(true);
            if (nameTexts != null && nameTexts.Length > 0)
            {
                Font font = ZombieIntroSpotlight.GetSafeFont();
                if (font != null)
                {
                    for (int i = 0; i < nameTexts.Length; i++)
                    {
                        if (nameTexts[i] != null)
                            nameTexts[i].font = font;
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (follower != null)
                follower.OnReachedEnd -= HandleReachedEnd;

            Active.Remove(this);
        }

        void Update()
        {
            if (_dead)
                return;

            UpdateEffects();
            UpdateSpeed();
            UpdateMutationScale();
            UpdateVisuals();
            UpdateAura();
        }

        // ------------------------------------------------------------------
        // Effects
        // ------------------------------------------------------------------

        void UpdateEffects()
        {
            // Chilled (boss siap dinetralkan) kedaluwarsa -> reset total.
            if (_chillStacks > 0 && _chillUntil > 0f && Time.time >= _chillUntil)
            {
                _chillStacks = 0;
                _chillUntil = 0f;
                _chillStackExpire = 0f;
            }
            // Stack parsial (1/3, 2/3) yang tidak dilanjutkan kedaluwarsa -> reset.
            else if (_chillStacks > 0 && _chillStacks < 3 && Time.time >= _chillStackExpire)
            {
                _chillStacks = 0;
            }

            // Buff eksternal kedaluwarsa.
            if (Time.time >= _externalBuffUntil)
                _externalSpeedBuff = 1f;
        }

        void UpdateSpeed()
        {
            if (follower == null)
                return;

            float multiplier = 1f;

            if (Time.time < _rootedUntil)
                multiplier = 0f;
            else if (Time.time < _slowUntil)
                multiplier = _slowFactor;

            if (Time.time < _externalBuffUntil)
                multiplier *= _externalSpeedBuff;

            follower.moveSpeed = moveSpeed * multiplier;
            follower.adjustSpeedByPerspective = true;

            // Knockback bergerak mundur secara bertahap (halus).
            if (_knockbackRemaining > 0f)
            {
                float step = Mathf.Min(_knockbackRemaining, 7f * Time.deltaTime);
                follower.PushBack(step);
                _knockbackRemaining -= step;
            }
        }

        /// <summary>
        /// Menggerakkan mutationScale secara mulus menuju target (mis. 1.45 saat mutasi membesar).
        /// PathFollower25D TIDAK menulis ulang mutationScale di Update(), jadi nilai ini tidak
        /// akan tertimpa balik ke 1.0f selama zombie hidup.
        /// </summary>
        void UpdateMutationScale()
        {
            if (follower == null)
                return;

            if (Mathf.Abs(_mutationScaleCurrent - _mutationScaleTarget) <= 0.0005f)
                return;

            _mutationScaleCurrent = Mathf.MoveTowards(
                _mutationScaleCurrent, _mutationScaleTarget, 1.4f * Time.deltaTime);

            if (Mathf.Abs(_mutationScaleCurrent - _lastAppliedMutationScale) > 0.0005f)
            {
                _lastAppliedMutationScale = _mutationScaleCurrent;
                follower.SetMutationScale(_mutationScaleCurrent);
            }
        }

        void UpdateVisuals()
        {
            if (spriteRenderer == null)
                return;

            Color target;

            if (Time.time < _flashUntil)
            {
                target = Color.Lerp(baseColor, Color.white, 0.85f);
            }
            else if (Time.time < _frozenUntil)
            {
                // BEKU: biru es pekat.
                target = Color.Lerp(baseColor, frozenColor, 0.85f);
            }
            else if (Time.time < _chillUntil)
            {
                target = Color.Lerp(baseColor, chilledColor, 0.8f);
            }
            else if (_chillStacks > 0)
            {
                target = Color.Lerp(baseColor, chilledColor, 0.15f * _chillStacks);
            }
            else if (Time.time < _rootedUntil)
            {
                target = Color.Lerp(baseColor, chilledColor, 0.45f);
            }
            else
            {
                target = baseColor;
            }

            spriteRenderer.color = target;
        }

        void UpdateAura()
        {
            if (!_hasAoEBuff)
                return;

            _auraTimer -= Time.deltaTime;
            if (_auraTimer > 0f)
                return;

            _auraTimer = 0.5f;

            Vector3 origin = transform.position;
            const float radiusSq = 2.5f * 2.5f;

            for (int i = 0; i < Active.Count; i++)
            {
                ZombieChemicalEntity other = Active[i];

                if (other == null || other == this || other._dead)
                    continue;

                if ((other.transform.position - origin).sqrMagnitude <= radiusSq)
                    other.ApplyExternalSpeedBuff(1.25f, 0.8f);
            }
        }

        void ApplyExternalSpeedBuff(float multiplier, float duration)
        {
            _externalSpeedBuff = Mathf.Max(_externalSpeedBuff, multiplier);
            _externalBuffUntil = Mathf.Max(_externalBuffUntil, Time.time + duration);
        }

        // ------------------------------------------------------------------
        // Potion hit (IArenaPotionTarget)
        // ------------------------------------------------------------------

        public void ApplyPotionHit(Vector2 worldPoint, MixedPotion potion, GameObject source)
        {
            if (_dead || potion == null)
                return;

            if (zombieType == ZombieType.BossUnstableAmalgam)
                HandleBossHit(potion);
            else
                HandleNormalZombieHit(potion);
        }

        // ==================================================================
        // Matriks reaksi: 4 zombie normal
        // ==================================================================

        void HandleNormalZombieHit(MixedPotion potion)
        {
            switch (zombieType)
            {
                case ZombieType.ZombieAsam:
                    HandleAsamHit(potion);
                    break;
                case ZombieType.ZombieBasa:
                    HandleBasaHit(potion);
                    break;
                case ZombieType.ZombieApi:
                    HandleApiHit(potion);
                    break;
                case ZombieType.ZombieBesi:
                    HandleBesiHit(potion);
                    break;
                default:
                    ApplyDefaultReaction(potion);
                    break;
            }
        }

        // --- Zombie_Asam: tahan asam (malah MUTASI), hancur oleh basa & pendingin. ---
        void HandleAsamHit(MixedPotion potion)
        {
            PotionType type = potion.type;

            // HANYA dua ramuan Asam asli yang boleh memicu mutasi membesar pada Zombie_Asam:
            //   - Kabut Asam Dingin (asam + pendingin)
            //   - Asam Pelarut Karat (asam pekat)
            // -> 0 DMG, pulih +15% HP, mutationScale -> 1.45 (mulus). Badge: MUTASI MEMBESAR.
            // CATATAN PENTING: Gel Sabun Beku & Busa Sabun Mengembang itu BASA, BUKAN asam,
            // jadi TIDAK PERNAH masuk cabang ini (tidak memicu Enlarge).
            if (IsAcidPotion(type))
            {
                int heal = Mathf.Max(1, Mathf.RoundToInt(maxHealth * 0.15f));
                _health = Mathf.Min(maxHealth, _health + heal);
                _mutationScaleTarget = 1.45f;
                _flashUntil = 0f;

                Feedback("<b>MEMBESAR! (0 DMG)</b>", WarningRed);
                return;
            }

            // Gel Sabun Beku = racikan Basa + Pendingin (sifat Basa + Dingin), BUKAN asam:
            // reaksi efektif -> damage bonus + Slow. Feedback: DINGIN! (tidak memicu mutasi).
            if (type == PotionType.GelSabunBeku)
            {
                int applied = ApplyDamage(potion.damage * 1.5f);
                ApplyGelChill(potion);

                Feedback("<b>DINGIN!</b> " + Dmg(-applied), IceBlueColor);
                return;
            }

            // Ramuan Basa lainnya (Busa Sabun Mengembang / Bom Netralisasi):
            // 2.5x DMG + efek lambat (Slow). Feedback: STABIL!
            if (IsBasePotion(type))
            {
                int applied = ApplyDamage(potion.damage * 2.5f);
                ApplySharedEffects(potion);
                EnsureSlow(potion, 0.6f, 2.6f);

                Feedback("<b>STABIL!</b> " + Dmg(-applied), EmeraldColor);
                return;
            }

            // Default (netral) — tidak pernah memicu mutasi.
            ApplyDefaultReaction(potion);
        }

        // --- Zombie_Basa: tahan basa (licin), hancur oleh asam. ---
        void HandleBasaHit(MixedPotion potion)
        {
            // Terkena ramuan Basa (Busa Sabun Mengembang / Bom Netralisasi):
            // 0 DMG, licin -> moveSpeed +40% (sekali). Badge: TERLALU LICIN! (MutasiSalah).
            if (IsBasePotion(potion.type))
            {
                if (!_isSlippery)
                {
                    _isSlippery = true;
                    moveSpeed *= 1.4f;
                }

                Feedback("<b>LICIN! +SPD</b>", WarningOrange);
                return;
            }

            // Terkena ramuan Asam (Kabut Asam Dingin / Asam Pelarut Karat):
            // 2.5x DMG + efek lambat (Slow). Feedback: NETRAL!
            if (IsAcidPotion(potion.type))
            {
                int applied = ApplyDamage(potion.damage * 2.5f);
                ApplySharedEffects(potion);
                EnsureSlow(potion, 0.6f, 2.6f);

                Feedback("<b>NETRAL!</b> " + Dmg(-applied), EmeraldColor);
                return;
            }

            ApplyDefaultReaction(potion);
        }

        // --- Zombie_Api: lemah pendingin/es (BEKU), netralisasi eksotermik = pemicu panas. ---
        void HandleApiHit(MixedPotion potion)
        {
            // Terkena ramuan Pendingin (Ledakan Es Instan / Gel Sabun Beku):
            // 3.0x DMG + BEKU / ROOT 3 detik + tint biru es. Badge: PADAM & BEKU!
            if (IsIcePotion(potion.type))
            {
                int applied = ApplyDamage(potion.damage * 3.0f);
                _rootedUntil = Mathf.Max(_rootedUntil, Time.time + 3f);
                _frozenUntil = Mathf.Max(_frozenUntil, Time.time + 3f);
                ApplySharedEffects(potion);

                Feedback("<b>PADAM!</b> " + Dmg(-applied), IceBlueColor);
                return;
            }

            // Terkena ramuan Katalis / Pemicu Panas (lihat IsHeatCatalystPotion):
            // 0 DMG + aura percepatan (+25% speed) ke zombie lain. Feedback: MELEDAK! +SPD
            if (IsHeatCatalystPotion(potion.type))
            {
                _hasAoEBuff = true;
                _auraTimer = 0f;

                Feedback("<b>MELEDAK! +SPD</b>", WarningOrange);
                return;
            }

            ApplyDefaultReaction(potion);
        }

        // --- Zombie_Besi: hanya Asam Pelarut Karat yang efektif (karat); non-asam mengeraskan zirah. ---
        void HandleBesiHit(MixedPotion potion)
        {
            // Terkena Asam Pelarut Karat: 3.0x DMG + hapus status zirah keras (efek karat).
            // Badge: ZIRAH KOROSI!
            if (potion.type == PotionType.AsamPelarutKarat)
            {
                _hasHarden = false;
                _hardenedArmorCharges = 0;

                int applied = ApplyDamage(potion.damage * 3.0f);

                Feedback("<b>BERKARAT!</b> " + Dmg(-applied), EmeraldColor);
                return;
            }

            // Terkena ramuan Non-Asam (Basa / Sabun): 0 DMG + zirah mengeras
            // (menahan 40% damage berikutnya). Feedback: KEBAL! (0 DMG)
            if (IsBasePotion(potion.type))
            {
                _hardenedArmorCharges = Mathf.Max(_hardenedArmorCharges, 1);

                Feedback("<b>KEBAL! (0 DMG)</b>", WarningRed);
                return;
            }

            ApplyDefaultReaction(potion);
        }

        // --- Reaksi default (damage netral + efek bawaan ramuan). ---
        void ApplyDefaultReaction(MixedPotion potion)
        {
            float multiplier = GetDamageMultiplier(potion);
            float damage = potion.damage * multiplier;

            int applied = damage > 0f ? ApplyDamage(damage) : 0;
            ApplySharedEffects(potion);

            Feedback(applied > 0 ? ("<b>-" + applied + "</b>") : "<b>0 DMG</b>", WarmWhite);
        }

        // ==================================================================
        // Matriks reaksi: Boss Unstable Amalgam (2 tahap)
        // ==================================================================

        void HandleBossHit(MixedPotion potion)
        {
            PotionType type = potion.type;

            // Tahap 1: kumpulkan 3x efek dingin/es -> Chilled.
            if (AlchemyDatabase.IsFreezing(type))
            {
                AddChillStack();
                ApplyDamage(potion.damage * 0.15f);

                if (_chillStacks >= 3)
                {
                    // Chilled (3/3): siap dinetralkan.
                    Feedback("<b>TITIK BEKU!</b>", GoldColor);
                }
                else
                {
                    // Progres pendingin 1/3, 2/3.
                    Feedback("<b>SUHU TURUN (" + _chillStacks + "/3)</b>", IceBlueColor);
                }

                return;
            }

            // Tahap 2: Bom Netralisasi adalah kunci eksekusi.
            if (type == PotionType.BomNetralisasi)
            {
                if (IsChilled)
                {
                    // Sudah Chilled -> 4.0x DMG besar + hapus status Chilled. Feedback: REAKSI PUNCAK!
                    float multiplier = Mathf.Max(1f, potion.bossMultiplier); // resep BomNetralisasi = 4.0
                    int applied = ApplyDamage(potion.damage * multiplier);
                    ClearChilled();
                    ApplySharedEffects(potion);

                    Feedback("<b>REAKSI PUNCAK!</b> <size=62><b>-" + applied + "</b></size>", GoldColor);
                }
                else
                {
                    // Belum Chilled -> 0 DMG + Boss AMUK (+35% speed 4 detik). Feedback: TAK STABIL!
                    ApplyExternalSpeedBuff(1.35f, 4f);

                    Feedback("<b>TAK STABIL! +SPD</b>", WarningOrange);
                }

                return;
            }

            // Potion lain (asam / sabun): sangat tahan; sedikit lebih terbuka saat Chilled.
            float bodyMultiplier = IsChilled ? 1.25f : 0.12f;
            int bodyDamage = ApplyDamage(potion.damage * bodyMultiplier);

            Feedback(bodyDamage > 0 ? ("<b>-" + bodyDamage + "</b>") : "<b>0 DMG</b>", WarmWhite);
        }

        void AddChillStack()
        {
            _chillStacks = Mathf.Min(_chillStacks + 1, 3);

            if (_chillStacks >= 3)
            {
                _chillUntil = Time.time + 8f;          // Chilled selama 8 detik.
                _chillStackExpire = _chillUntil;
                Debug.Log("[Boss] Unstable Amalgam Chilled (3/3) - netralkan dengan Bom Netralisasi!");
            }
            else
            {
                // Belum Chilled: jaga jendela akumulasi stack parsial, dan pastikan
                // _chillUntil = 0 agar UpdateEffects tidak salah menganggap status sudah kedaluwarsa.
                _chillUntil = 0f;
                _chillStackExpire = Time.time + 6f;
            }
        }

        void ClearChilled()
        {
            _chillStacks = 0;
            _chillUntil = 0f;
            _chillStackExpire = 0f;
        }

        // ------------------------------------------------------------------
        // Damage & efek bersama
        // ------------------------------------------------------------------

        float GetDamageMultiplier(MixedPotion potion)
        {
            // Kasus spesifik: hanya Asam Pelarut Karat yang benar-benar melarutkan besi.
            if (zombieType == ZombieType.ZombieBesi && potion.type == PotionType.AsamPelarutKarat)
                return 3f;

            if (AlchemyDatabase.IsAcid(potion.type))
                return acidVulnerability;

            if (AlchemyDatabase.IsNeutralizing(potion.type))
                return neutralizeVulnerability;

            if (AlchemyDatabase.IsFreezing(potion.type))
                return freezeVulnerability;

            return baseVulnerability;
        }

        void ApplySharedEffects(MixedPotion potion)
        {
            if (potion.rootDuration > 0f)
                _rootedUntil = Mathf.Max(_rootedUntil, Time.time + potion.rootDuration);

            if (potion.slowDuration > 0f)
            {
                _slowUntil = Mathf.Max(_slowUntil, Time.time + potion.slowDuration);
                _slowFactor = Mathf.Clamp01(potion.slowFactor);
            }

            if (potion.knockback > 0f)
                _knockbackRemaining += Mathf.Max(0f, potion.knockback);
        }

        /// <summary>Pastikan ada efek lambat, walau ramuan tidak membawa slow sendiri.</summary>
        void EnsureSlow(MixedPotion potion, float fallbackFactor, float fallbackDuration)
        {
            if (potion.slowDuration <= 0f)
                ApplySlow(fallbackFactor, fallbackDuration);
        }

        void ApplySlow(float factor, float duration)
        {
            float f = Mathf.Clamp01(factor);
            _slowFactor = (Time.time < _slowUntil) ? Mathf.Min(_slowFactor, f) : f;
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        /// <summary>
        /// Efek dingin dari Gel Sabun Beku (racikan Basa + Pendingin) pada Zombie_Asam:
        /// memperlambat gerak (Slow) + tint biru es sesaat. Tidak menyentuh status mutasi.
        /// </summary>
        void ApplyGelChill(MixedPotion potion)
        {
            float factor = (potion.slowFactor > 0f && potion.slowFactor < 1f) ? potion.slowFactor : 0.55f;
            float duration = potion.slowDuration > 0f ? potion.slowDuration : 3f;
            ApplySlow(factor, duration);

            _frozenUntil = Mathf.Max(_frozenUntil, Time.time + 1.2f);
        }

        /// <summary>Terapkan damage (menghormati zirah keras & mutasi Harden). Mengembalikan damage final.</summary>
        int ApplyDamage(float amount)
        {
            if (_dead || amount <= 0f)
                return 0;

            if (_hasHarden)
                amount *= 0.6f;

            if (_hardenedArmorCharges > 0)
            {
                amount *= 0.6f;             // zirah mengeras Zombie_Besi menahan 40% damage berikutnya.
                _hardenedArmorCharges--;
            }

            int applied = Mathf.Max(1, Mathf.RoundToInt(amount));
            _health -= applied;
            _flashUntil = Time.time + 0.08f;

            if (_health <= 0)
                Die(true);

            return applied;
        }

        // ------------------------------------------------------------------
        // Kategori ramuan (lokal per-zombie).
        // Catatan penting soal dual-nature:
        //   - KabutAsamDingin = Asam + Dingin  -> Diperlakukan sebagai ASAM oleh IsAcidPotion.
        //   - GelSabunBeku     = Basa + Dingin  -> ADALAH BASA (soap), BUKAN asam. Karena Zapin
        //     Zombie_Asam lewat IsIcePotion (memicu BEKU), sedangkan Zombie_Asam menanganinya
        //     eksplisit sebagai reaksi "NETRAL & DINGIN!" (lihat HandleAsamHit). Gel TIDAK PERNAH
        //     memicu mutasi Enlarge.
        // ------------------------------------------------------------------

        // Asam: hanya dua ini yang boleh memicu mutasi Enlarge pada Zombie_Asam.
        static bool IsAcidPotion(PotionType type)
        {
            return type == PotionType.KabutAsamDingin || type == PotionType.AsamPelarutKarat;
        }

        // Pendingin / Es (termasuk GelSabunBeku yang juga dingin).
        static bool IsIcePotion(PotionType type)
        {
            return type == PotionType.GelSabunBeku || type == PotionType.LedakanEsInstan;
        }

        // Basa (soap/bom netralisasi). GelSabunBeku sengaja TIDAK dimasukkan di sini agar tidak
        // mengubah perilaku zombie lain; Zombie_Asam sudah menangani Gel secara eksplisit.
        static bool IsBasePotion(PotionType type)
        {
            return type == PotionType.BomNetralisasi || type == PotionType.BusaSabunMengembang;
        }

        /// <summary>
        /// Ramuan "Katalis / Pemicu Panas" untuk Zombie Api. Saat ini tidak ada PotionType panas
        /// khusus, sehingga BomNetralisasi (reaksi netralisasi asam-basa bersifat eksotermik /
        /// melepaskan panas) dipakai sebagai pemicu panas bagi Zombie Api. Ubah di sini bila kelak
        /// ada ramuan panas/katalis dedicated.
        /// </summary>
        static bool IsHeatCatalystPotion(PotionType type)
        {
            return type == PotionType.BomNetralisasi;
        }

        /// <summary>Posisi dunia di atas kepala zombie (anchor teks feedback, ~1.15 unit di atas).</summary>
        Vector3 HeadWorldPosition()
        {
            float scaleY = transform != null ? Mathf.Abs(transform.localScale.y) : 1f;
            return transform.position + Vector3.up * (1.15f * Mathf.Max(1f, scaleY));
        }

        // ------------------------------------------------------------------
        // Feedback teks melayang (Rich Text Victorian)
        // ------------------------------------------------------------------

        /// <summary>Tampilkan teks feedback Rich Text di atas zombie (jitter X ditangani manager).</summary>
        void Feedback(string richText, Color color)
        {
            ChemicalFeedbackManager.Spawn(HeadWorldPosition(), richText, color);
        }

        /// <summary>Format angka damage dengan ukuran lebih besar (Rich Text).</summary>
        static string Dmg(int amount)
        {
            return "<size=58><b>" + amount + "</b></size>";
        }

        // ------------------------------------------------------------------
        // Death & tower
        // ------------------------------------------------------------------

        void HandleReachedEnd(PathFollower25D source)
        {
            if (_dead)
                return;

            TowerHealthManager tower = TowerHealthManager.Instance;
            if (tower != null)
                tower.TakeDamage(towerDamage);

            Die(false);
        }

        void Die(bool dropLoot)
        {
            if (_dead)
                return;

            _dead = true;

            if (dropLoot && ingredientDropCount > 0 && UnityEngine.Random.value <= dropChance)
            {
                AlchemyManager manager = alchemy != null ? alchemy : AlchemyManager.Instance;

                if (manager != null)
                    manager.AddRandomIngredient(ingredientDropCount);
            }

            if (spriteRenderer != null)
                spriteRenderer.enabled = false;

            Collider2D collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            if (OnZombieRemoved != null)
                OnZombieRemoved(this);

            Destroy(gameObject);
        }

        // ------------------------------------------------------------------
        // Mutasi (dipanggil WaveManager / sistem mutasi)
        // ------------------------------------------------------------------

        /// <summary>Mutasi: badan membesar, HP naik 50%.</summary>
        public void Mutate_Enlarge()
        {
            // PathFollower25D menulis localScale tiap frame, jadi skala mutasi harus lewat baseScale.
            if (follower == null)
                follower = GetComponent<PathFollower25D>();

            if (follower != null)
                follower.baseScale *= 1.3f;
            else
                transform.localScale *= 1.3f;

            maxHealth = Mathf.RoundToInt(maxHealth * 1.5f);
            _health = maxHealth;
        }

        /// <summary>Mutasi: kecepatan jalan naik 40%.</summary>
        public void Mutate_SpeedUp()
        {
            moveSpeed *= 1.4f;
        }

        /// <summary>Mutasi: kulit mengeras, damage yang masuk berkurang 40%.</summary>
        public void Mutate_Harden()
        {
            _hasHarden = true;
        }

        /// <summary>Mutasi: aura AoE yang mempercepat zombie di sekitarnya.</summary>
        public void Mutate_AoEBuff()
        {
            _hasAoEBuff = true;
        }

        // ------------------------------------------------------------------
        // Gizmos
        // ------------------------------------------------------------------

        void OnDrawGizmosSelected()
        {
            Gizmos.color = ZombieDatabase.ColorOf(zombieType);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
        }
    }
}