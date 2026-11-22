using UnityEngine;

namespace CuriesCrucible.Player
{
    /// <summary>
    /// Marie Curie di balkon menara.
    ///
    ///   - Geser horizontal di balkon (minX..maxX, default -0.85..0.85, kecepatan 3.0).
    ///   - `MarieCurie_ThrowOrigin` (sibling di Environment_Layers) mengikuti posisi tangan.
    ///   - flipX menghadap kursor mouse.
    ///   - Idle breathing + throw animation prosedural (squash/stretch + hop Y).
    ///
    /// Script ini tidak melempar sendiri; ArenaThrowTargeting memanggil PlayThrowAnimation()
    /// setiap kali botol dilempar.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarieCurieController : MonoBehaviour
    {
        [Header("Balkon")]
        [Tooltip("Batas kiri balkon (local X).")]
        public float minX = -0.85f;

        [Tooltip("Batas kanan balkon (local X).")]
        public float maxX = 0.85f;

        public float moveSpeed = 3.0f;

        [Tooltip("Jika false, pemain tidak dapat menggeser posisi Marie (dipakai saat cutscene / dialog).")]
        public bool enableInput = true;

        [Header("Referensi")]
        [Tooltip("Transform MarieCurie_ThrowOrigin (sibling di Environment_Layers).")]
        public Transform throwOrigin;

        public SpriteRenderer spriteRenderer;

        [Header("Tangan (offset dari kaki Marie)")]
        public float handReach = 0.70f;
        public float handHeight = 1.0f;

        [Header("Idle Breathing")]
        public float breathingAmplitude = 0.02f;
        public float breathingSpeed = 2.0f;

        [Header("Throw Animation")]
        public float throwDuration = 0.28f;
        public float throwSquash = 0.16f;
        public float throwHop = 0.12f;

        [Header("Sorting")]
        [Tooltip("450 = di depan BG_Arena (-100) dan di belakang pagar Tower_Balcony_FG (500).")]
        public int sortingOrder = 450;

        // ------------------------------------------------------------------
        // Runtime
        // ------------------------------------------------------------------

        Camera _camera;
        Vector3 _baseLocalPosition;
        Vector3 _baseScale = Vector3.one;

        float _breathPhase;
        float _throwTimer = -1f;
        float _currentHop;
        bool _facingRight = true;

        public bool IsFacingRight { get { return _facingRight; } }
        public bool IsThrowing { get { return _throwTimer >= 0f; } }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = sortingOrder;

            _camera = Camera.main;
            _baseLocalPosition = transform.localPosition;
            _baseScale = transform.localScale;
        }

        void Update()
        {
            HandleMovement();
            HandleFacing();
            HandleAnimation();
            UpdateThrowOrigin();
        }

        // ------------------------------------------------------------------
        // Movement & facing
        // ------------------------------------------------------------------

        void HandleMovement()
        {
            if (!enableInput)
                return;

            float input = Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                input = -1f;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                input = 1f;

            if (Mathf.Abs(input) < 0.01f)
                return;

            Vector3 position = _baseLocalPosition;
            position.x = Mathf.Clamp(
                position.x + input * moveSpeed * Time.deltaTime,
                minX,
                maxX);

            _baseLocalPosition = position;
        }

        void HandleFacing()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                return;

            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            _facingRight = mouseWorld.x >= transform.position.x;

            if (spriteRenderer != null)
                spriteRenderer.flipX = !_facingRight;
        }

        // ------------------------------------------------------------------
        // Procedural animation
        // ------------------------------------------------------------------

        void HandleAnimation()
        {
            float scaleX = 1f;
            float scaleY = 1f;
            float hop = 0f;

            // Idle breathing.
            _breathPhase += breathingSpeed * Time.deltaTime;
            scaleY += Mathf.Sin(_breathPhase) * breathingAmplitude;

            // Throw: ayunan cepat squash -> stretch + lompatan kecil.
            if (_throwTimer >= 0f)
            {
                _throwTimer += Time.deltaTime;

                float t = Mathf.Clamp01(_throwTimer / Mathf.Max(0.01f, throwDuration));
                float punch = Mathf.Sin(t * Mathf.PI);

                scaleY += punch * throwSquash;
                scaleX -= punch * throwSquash * 0.6f;
                hop += punch * throwHop;

                if (t >= 1f)
                    _throwTimer = -1f;
            }

            _currentHop = hop;

            transform.localScale = new Vector3(
                _baseScale.x * scaleX,
                _baseScale.y * scaleY,
                _baseScale.z);

            transform.localPosition = _baseLocalPosition + new Vector3(0f, hop, 0f);
        }

        void UpdateThrowOrigin()
        {
            if (throwOrigin == null)
                return;

            float direction = _facingRight ? 1f : -1f;

            Vector3 localPosition = _baseLocalPosition + new Vector3(
                handReach * direction,
                handHeight + _currentHop,
                0f);

            throwOrigin.localPosition = localPosition;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Picu animasi ayunan lempar (dipanggil ArenaThrowTargeting).</summary>
        public void PlayThrowAnimation()
        {
            _throwTimer = 0f;
        }

        /// <summary>Pindahkan Marie ke posisi X tertentu (world/local X).</summary>
        public void SetBalconyPosition(float localX)
        {
            Vector3 position = _baseLocalPosition;
            position.x = Mathf.Clamp(localX, minX, maxX);
            _baseLocalPosition = position;
            transform.localPosition = position + new Vector3(0f, _currentHop, 0f);
        }
    }
}