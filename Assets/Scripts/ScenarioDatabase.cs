using System;
using System.Collections.Generic;
using UnityEngine;

namespace JejakMaya
{
    [Serializable]
    public class ScenarioJsonItem
    {
        public string id;
        public string riskLevel;
        public string title;
        public string description;
        public string option1Text;
        public int option1Delta;
        public string option1Feedback;
        public string option2Text;
        public int option2Delta;
        public string option2Feedback;
    }

    [Serializable]
    public class ScenarioCollection
    {
        public List<ScenarioJsonItem> smartphoneScenarios;
        public List<ScenarioJsonItem> monitorScenarios;
        public List<ScenarioJsonItem> packageScenarios;
    }

    public class ScenarioDatabase : MonoBehaviour
    {
        public static ScenarioDatabase Instance { get; private set; }
        private ScenarioCollection loadedData;
        private static HashSet<string> usedIds = new HashSet<string>();

        private void Awake()
        {
            Instance = this;
            usedIds.Clear();
            EnsureLoaded();
        }

        /// <summary>
        /// Memuat JSON secara lazy agar urutan Awake antar komponen tidak masalah.
        /// </summary>
        private void EnsureLoaded()
        {
            if (loadedData != null) return;

            TextAsset jsonFile = Resources.Load<TextAsset>("data");
            if (jsonFile == null)
            {
                Debug.LogError("File Assets/Resources/data.json tidak ditemukan.");
                return;
            }

            try
            {
                loadedData = JsonUtility.FromJson<ScenarioCollection>(jsonFile.text);
            }
            catch (Exception e)
            {
                Debug.LogError("Gagal membaca data.json: " + e.Message);
                loadedData = null;
            }

            if (loadedData == null)
            {
                Debug.LogError("Struktur JSON tidak cocok dengan ScenarioCollection.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Mengosongkan riwayat ID agar RestartGame memilih soal baru.</summary>
        public void ResetHistory()
        {
            usedIds.Clear();
        }

        public Scenario GetRandomScenario(int categoryIndex)
        {
            EnsureLoaded();
            if (loadedData == null) return null;

            List<ScenarioJsonItem> pool = categoryIndex == 0 ? loadedData.smartphoneScenarios :
                                          categoryIndex == 1 ? loadedData.monitorScenarios :
                                                               loadedData.packageScenarios;

            if (pool == null || pool.Count == 0) return null;

            List<ScenarioJsonItem> available = pool.FindAll(s => s != null && !usedIds.Contains(s.id));
            if (available.Count == 0)
            {
                usedIds.Clear();
                available = pool;
            }

            ScenarioJsonItem chosen = available[UnityEngine.Random.Range(0, available.Count)];
            if (chosen == null) return null;
            usedIds.Add(chosen.id);

            // Konversi otomatis ke class Scenario & DecisionOption milik GameData.cs
            return new Scenario
            {
                scenarioTitle = string.IsNullOrEmpty(chosen.riskLevel) ? chosen.title : $"[{chosen.riskLevel}] {chosen.title}",
                caseDescription = chosen.description,
                options = new DecisionOption[]
                {
                    new DecisionOption
                    {
                        buttonLabel = chosen.option1Text,
                        reputationChange = chosen.option1Delta,
                        isSafeChoice = chosen.option1Delta > 0,
                        feedbackExplanation = chosen.option1Feedback
                    },
                    new DecisionOption
                    {
                        buttonLabel = chosen.option2Text,
                        reputationChange = chosen.option2Delta,
                        isSafeChoice = chosen.option2Delta > 0,
                        feedbackExplanation = chosen.option2Feedback
                    }
                }
            };
        }
    }
}