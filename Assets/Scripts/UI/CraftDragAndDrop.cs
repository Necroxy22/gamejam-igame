using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CuriesCrucible.Alchemy;
using CuriesCrucible.Gameplay;

namespace CuriesCrucible.UI
{
    public enum CraftDragSource
    {
        Stock,
        Cauldron
    }

    public class CraftDragPayload
    {
        public CraftDragSource Source;
        public IngredientType Ingredient;
        public int CauldronSlot; // 0 = slot1, 1 = slot2
    }

    /// <summary>
    /// Komponen Drag & Drop interaktif untuk bahan dasar di Craft_panel.
    /// Mendukung drag dari Kartu Bahan atau Slot Kuali ke slot tujuan, serta drag ke luar jendela untuk membuang (discard).
    /// Juga mendukung klik cepat untuk memasukkan ke kuali atau mengeluarkan dari kuali.
    /// Tanpa Coroutine (aman WebGL).
    /// </summary>
    [DisallowMultipleComponent]
    public class CraftIngredientDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [Header("Konfigurasi Drag")]
        public CraftDragSource source = CraftDragSource.Stock;
        public IngredientType ingredientType = IngredientType.BahanBasa_Biru;
        public int cauldronSlot = 0;

        [Header("Referensi")]
        public RectTransform craftPanelRect;
        public Canvas rootCanvas;

        // Static payload saat sesi drag berlangsung
        public static CraftDragPayload CurrentPayload { get; private set; }
        public static bool DragHandled { get; set; }

        GameObject _ghostObj;
        RectTransform _ghostRt;
        bool _isDragging;

        void Awake()
        {
            if (craftPanelRect == null)
            {
                Transform p = transform;
                while (p != null)
                {
                    if (p.name.Equals("Craft_panel"))
                    {
                        craftPanelRect = p as RectTransform;
                        break;
                    }
                    p = p.parent;
                }
            }

            if (rootCanvas == null)
            {
                rootCanvas = GetComponentInParent<Canvas>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            AlchemyManager am = AlchemyManager.Instance;
            if (am == null) return;

            // Validasi: hanya bisa di-drag jika stok ada atau slot terisi
            if (source == CraftDragSource.Stock)
            {
                if (am.ingredientStock == null || (int)ingredientType >= am.ingredientStock.Length)
                    return;
                if (am.ingredientStock[(int)ingredientType] <= 0)
                    return;
            }
            else if (source == CraftDragSource.Cauldron)
            {
                if (cauldronSlot == 0 && !am.slot1Filled) return;
                if (cauldronSlot == 1 && !am.slot2Filled) return;
                ingredientType = (cauldronSlot == 0) ? am.slot1 : am.slot2;
            }

            _isDragging = true;
            DragHandled = false;
            CurrentPayload = new CraftDragPayload
            {
                Source = source,
                Ingredient = ingredientType,
                CauldronSlot = cauldronSlot
            };

            CreateGhostIcon(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _ghostRt == null) return;

            Canvas c = rootCanvas != null ? rootCanvas : GetComponentInParent<Canvas>();
            RectTransform parentRt = _ghostRt.parent as RectTransform;
            Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? eventData.pressEventCamera : null;

            if (parentRt != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, cam, out Vector2 localPoint))
            {
                _ghostRt.anchoredPosition = localPoint;
            }
            else
            {
                _ghostRt.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;

            DestroyGhostIcon();

            // Jika belum di-handle oleh drop zone (misal dilepas di luar slot kuali)
            if (!DragHandled)
            {
                Canvas c = rootCanvas != null ? rootCanvas : GetComponentInParent<Canvas>();
                Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? eventData.pressEventCamera : null;

                bool insidePanel = false;
                if (craftPanelRect != null)
                {
                    insidePanel = RectTransformUtility.RectangleContainsScreenPoint(craftPanelRect, eventData.position, cam);
                }

                // Jika dilepas DI LUAR batas Craft_panel -> DISCARD (Membuang bahan)!
                if (!insidePanel)
                {
                    AlchemyManager am = AlchemyManager.Instance;
                    if (am != null)
                    {
                        if (source == CraftDragSource.Stock)
                        {
                            am.DiscardFromStock(ingredientType);
                        }
                        else if (source == CraftDragSource.Cauldron)
                        {
                            am.DiscardFromCauldron(cauldronSlot);
                        }
                    }

                    if (GameplayUIBridge.Instance != null)
                    {
                        GameplayUIBridge.Instance.RefreshCraftPanel();
                        GameplayUIBridge.Instance.RefreshUI(true);
                    }
                }
            }

            CurrentPayload = null;
            DragHandled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Jangan eksekusi klik jika sedang atau baru saja drag
            if (_isDragging || eventData.dragging) return;

            AlchemyManager am = AlchemyManager.Instance;
            if (am == null) return;

            if (source == CraftDragSource.Stock)
            {
                // Klik kartu stok -> tambahkan ke slot kuali kosong pertama
                am.AddIngredient(ingredientType);
            }
            else if (source == CraftDragSource.Cauldron)
            {
                // Klik slot kuali -> kembalikan ke stok pemain (refund)
                am.RemoveIngredientFromSlot(cauldronSlot);
            }

            if (GameplayUIBridge.Instance != null)
            {
                GameplayUIBridge.Instance.RefreshCraftPanel();
                GameplayUIBridge.Instance.RefreshUI(true);
            }
        }

        void CreateGhostIcon(PointerEventData eventData)
        {
            DestroyGhostIcon();

            _ghostObj = new GameObject("Drag_Ghost_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            _ghostObj.layer = 5; // Layer UI

            Transform parent = (rootCanvas != null) ? rootCanvas.transform : transform.root;
            _ghostObj.transform.SetParent(parent, false);
            _ghostObj.transform.SetAsLastSibling();

            _ghostRt = _ghostObj.GetComponent<RectTransform>();
            _ghostRt.sizeDelta = new Vector2(64f, 64f);
            _ghostRt.pivot = new Vector2(0.5f, 0.5f);

            Image img = _ghostObj.GetComponent<Image>();
            img.color = AlchemyDatabase.IngredientColor(ingredientType);
            img.raycastTarget = false;

            CanvasGroup cg = _ghostObj.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 0.88f;

            OnDrag(eventData);
        }

        void DestroyGhostIcon()
        {
            if (_ghostObj != null)
            {
                Destroy(_ghostObj);
                _ghostObj = null;
                _ghostRt = null;
            }
        }
    }

    /// <summary>
    /// Drop zone pada slot kuali (slot1 / slot2).
    /// Menerima drop bahan dasar dari kartu stok atau drop swap dari slot kuali lainnya.
    /// </summary>
    [DisallowMultipleComponent]
    public class CauldronSlotDropZone : MonoBehaviour, IDropHandler
    {
        [Tooltip("0 untuk slot1, 1 untuk slot2")]
        public int slotIndex = 0;

        public void OnDrop(PointerEventData eventData)
        {
            CraftDragPayload payload = CraftIngredientDraggable.CurrentPayload;
            if (payload == null) return;

            AlchemyManager am = AlchemyManager.Instance;
            if (am == null) return;

            if (payload.Source == CraftDragSource.Stock)
            {
                // Pasang bahan ke slot ini (dan refund bahan lama jika slot sudah terisi)
                bool ok = am.SetCauldronSlot(slotIndex, payload.Ingredient);
                if (ok)
                {
                    CraftIngredientDraggable.DragHandled = true;
                }
            }
            else if (payload.Source == CraftDragSource.Cauldron)
            {
                if (payload.CauldronSlot != slotIndex)
                {
                    // Pindah atau tukar posisi kedua slot
                    am.SwapCauldronSlots();
                    CraftIngredientDraggable.DragHandled = true;
                }
                else
                {
                    // Di-drop ke slot yang sama
                    CraftIngredientDraggable.DragHandled = true;
                }
            }

            if (GameplayUIBridge.Instance != null)
            {
                GameplayUIBridge.Instance.RefreshCraftPanel();
                GameplayUIBridge.Instance.RefreshUI(true);
            }
        }
    }
}
