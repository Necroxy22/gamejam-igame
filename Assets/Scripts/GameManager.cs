using UnityEngine;
using UnityEngine.SceneManagement;

namespace JejakMaya
{
    /// <summary>
    /// Otak permainan: mengelola reputasi, skenario, dan alur antar panel UI.
    /// Singleton sederhana (tanpa DontDestroyOnLoad agar RestartGame bersih).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Reputation")]
        [SerializeField] private int currentReputation = 50;
        [SerializeField] private int minReputation = 0;
        [SerializeField] private int maxReputation = 100;
        [SerializeField] private int winThreshold = 70;

        [Header("Scenarios")]
        [Tooltip("Daftar skenario. Jika kosong saat Awake, diisi otomatis dengan 3 skenario default.")]
        [SerializeField] private Scenario[] scenarios;

        [Header("UI")]
        [Tooltip("Drag UIManager ke sini.")]
        [SerializeField] private UIManager uiManager;

        private InteractableObject currentInteractable;
        private int completedScenarios;

        public int CurrentReputation => currentReputation;
        public int CompletedScenarios => completedScenarios;

        private void Awake()
        {
            // Cegah duplikasi singleton.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Hormati skenario yang diisi manual di Inspector.
            bool hasManualScenarios = scenarios != null && scenarios.Length > 0;
            if (!hasManualScenarios)
            {
                ScenarioDatabase db = GetComponent<ScenarioDatabase>();
                if (db == null)
                {
                    // ScenarioDatabase tidak ada di scene: buat saat runtime.
                    // Awake-nya akan langsung memuat Resources/data.json.
                    db = gameObject.AddComponent<ScenarioDatabase>();
                }

                if (db != null)
                {
                    Scenario s0 = db.GetRandomScenario(0);
                    Scenario s1 = db.GetRandomScenario(1);
                    Scenario s2 = db.GetRandomScenario(2);

                    if (s0 != null && s1 != null && s2 != null)
                    {
                        scenarios = new Scenario[] { s0, s1, s2 };
                    }
                }
            }

            // Fallback terakhir agar game selalu punya 3 skenario.
            if (scenarios == null || scenarios.Length == 0)
            {
                Debug.LogWarning("Data skenario kosong. Memakai skenario default.");
                scenarios = CreateDefaultScenarios();
            }
        }

        /// <summary>
        /// Dipanggil oleh InteractableObject saat diklik.
        /// Membuka InspectionPanel untuk skenario terkait.
        /// </summary>
        public void OpenScenario(InteractableObject obj)
        {
            if (obj == null || obj.IsCompleted) return;

            Scenario scenario = GetScenario(obj.scenarioIndex);
            if (scenario == null)
            {
                Debug.LogWarning("Skenario dengan index " + obj.scenarioIndex + " tidak ditemukan.");
                return;
            }

            if (uiManager == null)
            {
                Debug.LogError("UIManager belum di-assign pada GameManager.");
                return;
            }

            currentInteractable = obj;
            uiManager.ShowInspectionPanel(scenario);
        }

        /// <summary>
        /// Memproses pilihan pemain: ubah reputasi, tandai selesai, buka FeedbackPanel.
        /// </summary>
        public void SelectOption(int optionIndex)
        {
            if (currentInteractable == null || currentInteractable.IsCompleted) return;
            if (uiManager == null) return;

            Scenario scenario = GetScenario(currentInteractable.scenarioIndex);
            if (scenario == null || scenario.options == null) return;
            if (optionIndex < 0 || optionIndex >= scenario.options.Length) return;

            DecisionOption option = scenario.options[optionIndex];
            if (option == null) return;

            currentReputation = Mathf.Clamp(
                currentReputation + option.reputationChange,
                minReputation,
                maxReputation);

            // Konsekuensi negatif jika pilihan tidak aman.
            if (!option.isSafeChoice)
            {
                currentInteractable.ShowNegativeConsequence();
            }

            currentInteractable.MarkAsCompleted();
            completedScenarios++;

            if (uiManager != null)
            {
                uiManager.UpdateReputation(currentReputation);
                uiManager.ShowFeedbackPanel(option.feedbackExplanation);
            }
        }

        /// <summary>
        /// Menutup FeedbackPanel. Jika semua skenario selesai, tampilkan ResultPanel.
        /// </summary>
        public void CloseFeedbackAndCheckProgress()
        {
            if (uiManager != null)
            {
                uiManager.HideFeedbackPanel();
            }

            currentInteractable = null;

            int total = scenarios != null ? scenarios.Length : 0;
            if (total > 0 && completedScenarios >= total)
            {
                bool win = currentReputation >= winThreshold;
                if (uiManager != null)
                {
                    uiManager.ShowResultPanel(win, currentReputation);
                }
            }
        }

        /// <summary>Memuat ulang scene aktif (reset penuh).</summary>
        public void RestartGame()
        {
            if (ScenarioDatabase.Instance != null)
            {
                ScenarioDatabase.Instance.ResetHistory();
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Skenario cadangan jika Resources/data.json gagal dimuat.
        /// Tetap memakai struktur Scenario/DecisionOption yang sama.
        /// </summary>
        private Scenario[] CreateDefaultScenarios()
        {
            return new Scenario[]
            {
                new Scenario
                {
                    scenarioTitle = "Pesan Hadiah Mengaku dari Kurir",
                    caseDescription = "Kamu dapat SMS yang mengaku kurir dan minta biaya kirim lewat tautan. Tautannya meminta data kartu dan OTP.",
                    options = new DecisionOption[]
                    {
                        new DecisionOption
                        {
                            buttonLabel = "Isi data kartu dan OTP",
                            reputationChange = -25,
                            isSafeChoice = false,
                            feedbackExplanation = "Data kartu dan OTP bisa dipakai untuk menguras rekening. Jangan pernah membagikannya lewat tautan SMS."
                        },
                        new DecisionOption
                        {
                            buttonLabel = "Cek resi di aplikasi resmi",
                            reputationChange = 15,
                            isSafeChoice = true,
                            feedbackExplanation = "Verifikasi lewat kanal resmi mencegah penipuan. Kurir resmi tidak menagih lewat tautan SMS."
                        }
                    }
                },
                new Scenario
                {
                    scenarioTitle = "Tautan Login di Email",
                    caseDescription = "Email mengaku dari sekolah dan minta login akun lewat tautan. Halamannya mirip, tapi domainnya berbeda.",
                    options = new DecisionOption[]
                    {
                        new DecisionOption
                        {
                            buttonLabel = "Login lewat tautan email",
                            reputationChange = -25,
                            isSafeChoice = false,
                            feedbackExplanation = "Halaman palsu bisa meniru tampilan situs resmi. Periksa domain sebelum memasukkan password."
                        },
                        new DecisionOption
                        {
                            buttonLabel = "Buka situs resmi manual",
                            reputationChange = 15,
                            isSafeChoice = true,
                            feedbackExplanation = "Membuka situs resmi secara manual menghindari tautan palsu. Aktifkan juga verifikasi dua langkah."
                        }
                    }
                },
                new Scenario
                {
                    scenarioTitle = "Foto KTP di Grup Chat",
                    caseDescription = "Teman meminta foto KTP dan selfie untuk daftar lomba. Kamu belum tahu siapa yang akan menyimpan data itu.",
                    options = new DecisionOption[]
                    {
                        new DecisionOption
                        {
                            buttonLabel = "Kirim foto KTP asli",
                            reputationChange = -25,
                            isSafeChoice = false,
                            feedbackExplanation = "KTP dan selfie bisa disalahgunakan untuk pinjol atau penipuan. Jangan kirim dokumen asli ke pihak tak terverifikasi."
                        },
                        new DecisionOption
                        {
                            buttonLabel = "Tolak dan tawarkan data non-sensitif",
                            reputationChange = 15,
                            isSafeChoice = true,
                            feedbackExplanation = "Menolak berbagi dokumen sensitif adalah langkah aman. Cek dulu pihak yang meminta dan kebijakan penyimpanannya."
                        }
                    }
                }
            };
        }

        /// <summary>True jika ada panel UI yang sedang terbuka (untuk memblokir interaksi).</summary>
        public bool IsUIActive()
        {
            return uiManager != null && uiManager.IsAnyPanelOpen();
        }

        private Scenario GetScenario(int index)
        {
            if (scenarios == null) return null;
            if (index < 0 || index >= scenarios.Length) return null;
            return scenarios[index];
        }
    }
}