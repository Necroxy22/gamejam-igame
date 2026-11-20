using System;
using UnityEngine;

namespace JejakMaya
{
    /// <summary>
    /// Satu opsi keputusan dalam sebuah skenario.
    /// Diedit langsung di Inspector GameManager (tanpa ScriptableObject eksternal).
    /// </summary>
    [Serializable]
    public class DecisionOption
    {
        [Tooltip("Teks yang tampil pada tombol opsi.")]
        public string buttonLabel;

        [Tooltip("Perubahan reputasi (boleh negatif) ketika opsi ini dipilih.")]
        public int reputationChange;

        [Tooltip("True jika opsi ini adalah pilihan aman / edukatif.")]
        public bool isSafeChoice;

        [Tooltip("Penjelasan edukatif yang muncul di FeedbackPanel.")]
        [TextArea(2, 5)]
        public string feedbackExplanation;
    }

    /// <summary>
    /// Satu kasus/skenario. Berisi judul, deskripsi kasus, dan 3 opsi keputusan.
    /// </summary>
    [Serializable]
    public class Scenario
    {
        [Tooltip("Judul singkat skenario.")]
        public string scenarioTitle;

        [Tooltip("Deskripsi kasus yang dibaca pemain di InspectionPanel.")]
        [TextArea(3, 8)]
        public string caseDescription;

        [Tooltip("Tiga opsi keputusan (indeks 0, 1, 2).")]
        public DecisionOption[] options = new DecisionOption[3];
    }
}