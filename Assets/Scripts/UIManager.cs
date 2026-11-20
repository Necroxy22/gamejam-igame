using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JejakMaya
{
    /// <summary>
    /// Mengelola seluruh referensi UI (standar UnityEngine.UI, tanpa TextMeshPro)
    /// agar aman untuk build WebGL tanpa dependency font asset tambahan.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Reputation")]
        [SerializeField] private Slider reputationSlider;
        [SerializeField] private TMP_Text reputationValueText;

        [Header("Panels")]
        [SerializeField] private GameObject inspectionPanel;
        [SerializeField] private GameObject feedbackPanel;
        [SerializeField] private GameObject resultPanel;

        [Header("Inspection Panel")]
        [SerializeField] private TMP_Text inspectionTitleText;
        [SerializeField] private TMP_Text inspectionDescriptionText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private TMP_Text[] optionLabels;

        [Header("Feedback Panel")]
        [SerializeField] private TMP_Text feedbackExplanationText;
        [SerializeField] private Button closeFeedbackButton;

        [Header("Result Panel")]
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultDetailText;
        [SerializeField] private Button restartButton;

        private bool buttonsWired;

        private void Start()
        {
            // Sembunyikan semua panel saat mulai.
            if (inspectionPanel != null) inspectionPanel.SetActive(false);
            if (feedbackPanel != null) feedbackPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);

            if (reputationSlider != null)
            {
                reputationSlider.minValue = 0;
                reputationSlider.maxValue = 100;
            }

            WireButtons();
            PopulateOptionLabelsIfNeeded();

            int initialRep = GameManager.Instance != null
                ? GameManager.Instance.CurrentReputation
                : 50;
            UpdateReputation(initialRep);
        }

        /// <summary>Memasang listener tombol secara terprogram (cukup drag referensi).</summary>
        private void WireButtons()
        {
            if (buttonsWired) return;
            buttonsWired = true;

            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                {
                    int index = i;
                    Button button = optionButtons[i];
                    if (button != null)
                    {
                        button.onClick.AddListener(() => OnOptionSelected(index));
                    }
                }
            }

            if (closeFeedbackButton != null)
            {
                closeFeedbackButton.onClick.AddListener(OnCloseFeedback);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestart);
            }
        }

        /// <summary>
        /// Jika optionLabels belum diisi di Inspector, ambil otomatis komponen
        /// TMP_Text (TextMeshProUGUI) dari masing-masing tombol pada optionButtons.
        /// </summary>
        private void PopulateOptionLabelsIfNeeded()
        {
            if (optionButtons == null || optionButtons.Length == 0) return;

            bool needFill = optionLabels == null || optionLabels.Length != optionButtons.Length;
            if (!needFill)
            {
                for (int i = 0; i < optionLabels.Length; i++)
                {
                    if (optionLabels[i] == null)
                    {
                        needFill = true;
                        break;
                    }
                }
            }

            if (!needFill) return;

            optionLabels = new TMP_Text[optionButtons.Length];
            for (int i = 0; i < optionButtons.Length; i++)
            {
                Button button = optionButtons[i];
                optionLabels[i] = button != null
                    ? button.GetComponentInChildren<TMP_Text>(true)
                    : null;
            }
        }

        public void UpdateReputation(int value)
        {
            if (reputationSlider != null)
            {
                reputationSlider.value = value;
            }

            if (reputationValueText != null)
            {
                reputationValueText.text = value.ToString();
            }
        }

        public void ShowInspectionPanel(Scenario scenario)
        {
            if (scenario == null) return;

            PopulateOptionLabelsIfNeeded();

            if (inspectionPanel != null)
            {
                inspectionPanel.SetActive(true);
            }

            if (inspectionTitleText != null)
            {
                inspectionTitleText.text = scenario.scenarioTitle;
            }

            if (inspectionDescriptionText != null)
            {
                inspectionDescriptionText.text = scenario.caseDescription;
            }

            int optionCount = scenario.options != null ? scenario.options.Length : 0;

            if (optionButtons != null && optionCount > optionButtons.Length)
            {
                Debug.LogWarning("Skenario punya " + optionCount + " opsi, tetapi hanya " + optionButtons.Length + " tombol tersedia.");
            }

            for (int i = 0; i < optionCount; i++)
            {
                DecisionOption option = scenario.options[i];
                if (option == null) continue;

                if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
                {
                    optionButtons[i].gameObject.SetActive(true);
                    optionButtons[i].interactable = true;
                }

                if (optionLabels != null && i < optionLabels.Length && optionLabels[i] != null)
                {
                    optionLabels[i].text = option.buttonLabel;
                }
            }

            // Sembunyikan tombol yang berlebih (jika jumlah opsi < jumlah tombol).
            if (optionButtons != null)
            {
                for (int i = optionCount; i < optionButtons.Length; i++)
                {
                    if (optionButtons[i] != null)
                    {
                        optionButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        public void ShowFeedbackPanel(string explanation)
        {
            if (inspectionPanel != null)
            {
                inspectionPanel.SetActive(false);
            }

            if (feedbackExplanationText != null)
            {
                feedbackExplanationText.text = explanation;
            }

            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(true);
            }
        }

        public void HideFeedbackPanel()
        {
            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(false);
            }
        }

        /// <summary>True jika salah satu panel (inspeksi/feedback/hasil) sedang aktif.</summary>
        public bool IsAnyPanelOpen()
        {
            return (inspectionPanel != null && inspectionPanel.activeSelf)
                || (feedbackPanel != null && feedbackPanel.activeSelf)
                || (resultPanel != null && resultPanel.activeSelf);
        }

        public void ShowResultPanel(bool win, int reputation)
        {
            if (inspectionPanel != null)
            {
                inspectionPanel.SetActive(false);
            }

            if (feedbackPanel != null)
            {
                feedbackPanel.SetActive(false);
            }

            if (resultTitleText != null)
            {
                resultTitleText.text = win
                    ? "Selamat, Kamu Melek Digital!"
                    : "Reputasi Menurun...";
            }

            if (resultDetailText != null)
            {
                resultDetailText.text = "Reputasi akhirmu: " + reputation + "/100.";
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }
        }

        // ---------- Callback tombol (dipanggil via listener di Start) ----------

        private void OnOptionSelected(int index)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SelectOption(index);
            }
        }

        private void OnCloseFeedback()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CloseFeedbackAndCheckProgress();
            }
        }

        private void OnRestart()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }
    }
}