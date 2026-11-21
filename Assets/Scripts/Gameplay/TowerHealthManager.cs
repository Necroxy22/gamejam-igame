using System;
using UnityEngine;

namespace CuriesCrucible.Gameplay
{
    /// <summary>
    /// HP Menara (default 10). Berkurang setiap kali zombie mencapai ujung
    /// PathFollower25D (event OnReachedEnd) — ZombieChemicalEntity yang meneruskan
    /// damage ke sini. Saat HP habis, game over.
    /// </summary>
    [DisallowMultipleComponent]
    public class TowerHealthManager : MonoBehaviour
    {
        public static TowerHealthManager Instance { get; private set; }

        [Header("HP Menara")]
        [Min(1)] public int maxHealth = 10;

        [Header("Visual (opsional)")]
        public SpriteRenderer towerSprite;
        public Color normalColor = Color.white;
        public Color dangerColor = new Color(1f, 0.35f, 0.30f);

        /// <summary>Terpanggil saat HP berubah: (current, max).</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Terpanggil sekali saat menara hancur.</summary>
        public event Action OnGameOver;

        public int Health { get; private set; }
        public bool IsGameOver { get; private set; }

        public float NormalizedHealth
        {
            get { return maxHealth > 0 ? Health / (float)maxHealth : 0f; }
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Health = Mathf.Max(1, maxHealth);
        }

        void Start()
        {
            FireHealthChanged();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            UpdateVisual();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        public void TakeDamage(int amount)
        {
            if (IsGameOver || amount <= 0)
                return;

            Health = Mathf.Max(0, Health - amount);
            FireHealthChanged();

            Debug.Log("[Tower] HP menara: " + Health + "/" + maxHealth);

            if (Health <= 0)
            {
                IsGameOver = true;

                if (OnGameOver != null)
                    OnGameOver();

                Debug.LogWarning("[Tower] MENARA HANCUR - game over!");
            }
        }

        public void Heal(int amount)
        {
            if (IsGameOver || amount <= 0)
                return;

            Health = Mathf.Min(maxHealth, Health + amount);
            FireHealthChanged();
        }

        public void ResetTower()
        {
            Health = Mathf.Max(1, maxHealth);
            IsGameOver = false;
            FireHealthChanged();
        }

        // ------------------------------------------------------------------
        // Internal
        // ------------------------------------------------------------------

        void FireHealthChanged()
        {
            if (OnHealthChanged != null)
                OnHealthChanged(Health, maxHealth);
        }

        void UpdateVisual()
        {
            if (towerSprite == null)
                return;

            float t = 1f - NormalizedHealth;
            towerSprite.color = Color.Lerp(normalColor, dangerColor, t);
        }
    }
}