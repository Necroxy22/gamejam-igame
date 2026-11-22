using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialSlideshow : MonoBehaviour
{
    [Header("Daftar Halaman Slide")]
    public GameObject[] slides;

    [Header("Tombol Navigasi")]
    public Button tombolPrev;
    public Button tombolNext;

    [Header("Indikator Halaman (Opsional)")]
    public TextMeshProUGUI teksHalaman;

    [Header("Pengaturan Transisi")]
    public float durasiTransisi = 0.25f; // Kecepatan animasi dalam detik
    public float jarakGeser = 60f;      // Isi 0 jika hanya ingin efek Fade tanpa geser

    private int indexSekarang = 0;
    private CanvasGroup[] canvasGroups;
    private RectTransform[] rectTransforms;
    private Vector2[] posisiAwal;
    private bool sudahInisialisasi = false;

    void Awake()
    {
        Inisialisasi();
    }

    void OnEnable()
    {
        Inisialisasi();
        StopAllCoroutines();

        indexSekarang = 0;
        ResetSemuaSlideKe(indexSekarang);
        UpdateTombolDanTeks();
    }

    private void Inisialisasi()
    {
        if (sudahInisialisasi || slides == null) return;

        canvasGroups = new CanvasGroup[slides.Length];
        rectTransforms = new RectTransform[slides.Length];
        posisiAwal = new Vector2[slides.Length];

        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] != null)
            {
                // Otomatis tambahkan CanvasGroup jika belum ada di objek Slide
                CanvasGroup cg = slides[i].GetComponent<CanvasGroup>();
                if (cg == null) cg = slides[i].AddComponent<CanvasGroup>();
                canvasGroups[i] = cg;

                RectTransform rt = slides[i].GetComponent<RectTransform>();
                rectTransforms[i] = rt;
                posisiAwal[i] = rt.anchoredPosition;
            }
        }

        sudahInisialisasi = true;
    }

    // Hubungkan ke On Click () tombol Next (>)
    public void NextSlide()
    {
        if (indexSekarang < slides.Length - 1)
        {
            MulaiTransisi(indexSekarang + 1, 1);
        }
    }

    // Hubungkan ke On Click () tombol Prev (<)
    public void PrevSlide()
    {
        if (indexSekarang > 0)
        {
            MulaiTransisi(indexSekarang - 1, -1);
        }
    }

    private void MulaiTransisi(int indexBaru, int arah)
    {
        StopAllCoroutines();
        // Rapikan posisi slide lain jika pemain mengklik tombol dengan cepat
        ResetSemuaSlideKe(indexSekarang);

        int indexLama = indexSekarang;
        indexSekarang = indexBaru;
        UpdateTombolDanTeks();

        StartCoroutine(AnimasiTransisi(indexLama, indexBaru, arah));
    }

    private IEnumerator AnimasiTransisi(int indexLama, int indexBaru, int arah)
    {
        GameObject slideLama = slides[indexLama];
        GameObject slideBaru = slides[indexBaru];

        CanvasGroup cgLama = canvasGroups[indexLama];
        CanvasGroup cgBaru = canvasGroups[indexBaru];

        RectTransform rtLama = rectTransforms[indexLama];
        RectTransform rtBaru = rectTransforms[indexBaru];

        Vector2 posAwalLama = posisiAwal[indexLama];
        Vector2 posTargetLama = posAwalLama - new Vector2(arah * jarakGeser, 0f);

        Vector2 posMulaiBaru = posisiAwal[indexBaru] + new Vector2(arah * jarakGeser, 0f);
        Vector2 posTargetBaru = posisiAwal[indexBaru];

        // Siapkan slide baru sebelum animasi dimulai
        slideBaru.SetActive(true);
        cgBaru.alpha = 0f;
        rtBaru.anchoredPosition = posMulaiBaru;

        float waktu = 0f;
        while (waktu < durasiTransisi)
        {
            waktu += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(waktu / durasiTransisi);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Slide lama memudar & bergeser keluar
            cgLama.alpha = 1f - smoothT;
            rtLama.anchoredPosition = Vector2.Lerp(posAwalLama, posTargetLama, smoothT);

            // Slide baru muncul & bergeser masuk ke tengah
            cgBaru.alpha = smoothT;
            rtBaru.anchoredPosition = Vector2.Lerp(posMulaiBaru, posTargetBaru, smoothT);

            yield return null;
        }

        // Pastikan kondisi akhir presisi
        slideLama.SetActive(false);
        cgLama.alpha = 1f;
        rtLama.anchoredPosition = posAwalLama;

        cgBaru.alpha = 1f;
        rtBaru.anchoredPosition = posTargetBaru;
    }

    private void ResetSemuaSlideKe(int indexAktif)
    {
        if (slides == null) return;

        for (int i = 0; i < slides.Length; i++)
        {
            if (slides[i] == null) continue;

            bool aktif = (i == indexAktif);
            slides[i].SetActive(aktif);
            canvasGroups[i].alpha = 1f;
            rectTransforms[i].anchoredPosition = posisiAwal[i];
        }
    }

    private void UpdateTombolDanTeks()
    {
        if (tombolPrev != null) tombolPrev.gameObject.SetActive(indexSekarang > 0);
        if (tombolNext != null) tombolNext.gameObject.SetActive(indexSekarang < slides.Length - 1);

        if (teksHalaman != null)
        {
            teksHalaman.text = (indexSekarang + 1) + " / " + slides.Length;
        }
    }
}