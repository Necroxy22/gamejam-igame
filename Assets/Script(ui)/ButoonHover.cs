using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 1. Tambahkan IPointerEnterHandler dan IPointerExitHandler di sini
public class ButoonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Skala Hover")]
    public float SkalaHover = 1.1f;
    public float KecepatanTransisi = 10f;

    private Vector3 skalaAwal;
    private Vector3 skalaTarget;

    void Awake() {
        skalaAwal = transform.localScale;
        skalaTarget = skalaAwal;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, skalaTarget, Time.unscaledDeltaTime * KecepatanTransisi);
    }

    // 2. Gunakan huruf 'P' kapital pada OnPointerEnter
    public void OnPointerEnter(PointerEventData eventData)
    {
        skalaTarget = skalaAwal * SkalaHover;
    }

    // 3. Gunakan huruf 'P' kapital pada OnPointerExit
    public void OnPointerExit(PointerEventData eventData)
    {
        skalaTarget = skalaAwal;
    }

    private void OnDisable()
    {
        transform.localScale = skalaAwal;
        skalaTarget = skalaAwal;
    }
}