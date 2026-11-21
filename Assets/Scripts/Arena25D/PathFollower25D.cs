using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuriesCrucible.Arena25D
{
    /// <summary>
    /// Menggerakkan zombie menyusuri waypoint dengan kurva halus (Catmull-Rom spline),
    /// sekaligus mengurus "ilusi 2.5D":
    ///   - localScale mengikuti posisi Y layar (Y = -4 -> 1.15x, Y = 1.5 -> 0.65x) dikali multiplier mutasi.
    ///   - SpriteRenderer.sortingOrder = RoundToInt(-Y * 100) supaya zombie depan menutupi zombie belakang.
    ///   - flipX sprite mengikuti arah gerak horizontal.
    ///
    /// Catatan WebGL: semua perhitungan dilakukan di Update (tanpa coroutine) sehingga aman di build WebGL.
    /// </summary>
    [DisallowMultipleComponent]
    public class PathFollower25D : MonoBehaviour
    {
        [Header("Path")]
        [Tooltip("Urutan waypoint dari gerbang muncul sampai titik serang di depan pintu menara.")]
        public Transform[] waypoints = new Transform[0];

        [Tooltip("Pakai Catmull-Rom spline agar tikungan jalan terasa mulus. Nonaktif = interpolasi linear antar waypoint.")]
        public bool useCatmullRomSpline = true;

        [Tooltip("Jumlah sampel per segmen waypoint (semakin besar semakin halus, tapi lebih berat).")]
        [Min(2)]
        public int splineSamplesPerSegment = 24;

        [Tooltip("Ulangi path dari awal setiap kali mencapai ujung (mode continuous wave).")]
        public bool loopPath = false;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Kecepatan dasar jalan zombie (world units/detik). Default tempo santai: 0.10f - 0.20f.")]
        public float moveSpeed = 0.15f;

        [Tooltip("Menyesuaikan kecepatan jalan dengan perspektif 2.5D (sedikit lebih pelan saat di area jauh/atas).")]
        public bool adjustSpeedByPerspective = true;

        public float fixedZ = 0f;

        [Header("2.5D Perspective")]
        public float perspectiveBottomY = Arena25DMath.DefaultNearY;
        public float perspectiveTopY = Arena25DMath.DefaultFarY;
        public float nearScale = Arena25DMath.DefaultNearScale;
        public float farScale = Arena25DMath.DefaultFarScale;
        public float baseScale = 1f;

        [Tooltip("Pengali skala dari sistem mutasi zombie (1 = normal, 1.3 = mutasi besar).")]
        public float mutationScale = 1f;

        [Header("Sorting")]
        [Tooltip("Offset sorting order tambahan (0 = pakai rumus -Y * 100).")]
        public int sortingOrderBias = 0;

        [Header("Facing")]
        [Tooltip("Balik sprite mengikuti arah horizontal.")]
        public bool autoFlip = true;

        [Tooltip("Sprite default menghadap ke kanan? Kalau sprite asli menghadap kiri, set false.")]
        public bool spriteFacesRightByDefault = true;

        [Tooltip("Ikutkan SpriteRenderer anak (multi-sprite rig). Matikan kalau anaknya punya bayangan sendiri.")]
        public bool includeChildRenderers = true;

        [Header("Waddle (opsional)")]
        [Tooltip("Sudut goyang saat jalan (derajat). 0 = mati.")]
        public float waddleAngle = 0f;
        public float waddleSpeed = 6f;

        // ------------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------------

        readonly List<Vector2> _samples = new List<Vector2>(256);
        readonly List<float> _distances = new List<float>(256);

        SpriteRenderer[] _renderers;
        float _totalLength;
        float _distance;
        bool _pathDirty;
        bool _atEnd;
        bool _endNotified;
        bool _flipped;
        float _waddlePhase;
        float _lastScale = float.NaN;

        /// <summary>Terpanggil sekali saat zombie mencapai ujung path (titik serang gerbang).</summary>
        public event Action<PathFollower25D> OnReachedEnd;

        /// <summary>0 = baru muncul, 1 = sampai di depan pintu menara.</summary>
        public float Progress
        {
            get { return _totalLength > 0.0001f ? Mathf.Clamp01(_distance / _totalLength) : 0f; }
        }

        public bool IsAtEnd
        {
            get { return _atEnd; }
        }

        public float TotalLength
        {
            get { return _totalLength; }
        }

        public Transform[] Waypoints
        {
            get { return waypoints; }
        }

        // ------------------------------------------------------------------
        // Unity lifecycle
        // ------------------------------------------------------------------

        void Awake()
        {
            CacheRenderers();
            RebuildPath();
            SnapToDistance(_distance);
        }

        void OnEnable()
        {
            _pathDirty = true;
        }

        void OnValidate()
        {
            _pathDirty = true;
            _renderers = null; // paksa cache ulang SpriteRenderer
        }

        void Update()
        {
            if (_pathDirty)
                RebuildPath();

            // Catatan: mutationScale TIDAK boleh ditulis ulang / di-reset ke 1.0f di sini.
            // Nilainya dimiliki secara eksternal (mis. ZombieChemicalEntity.UpdateMutationScale untuk
            // mutasi membesar 1.45f yang mulus). ApplySortingAndScale hanya MEMBACA mutationScale,
            // sehingga mutasi tidak akan tertimpa balik selama zombie hidup.
            Advance();
            UpdateTransform();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Ganti path saat runtime (misal: zombie dialihkan ke jalur kanan).</summary>
        public void SetPath(Transform[] newWaypoints)
        {
            waypoints = newWaypoints ?? new Transform[0];
            RebuildPath();
            SnapToDistance(0f);
        }

        /// <summary>Ulang dari titik muncul.</summary>
        public void Restart()
        {
            _distance = 0f;
            _atEnd = false;
            _endNotified = false;
            RebuildPath();
            SnapToDistance(0f);
        }

        /// <summary>Teleport ke posisi tertentu pada path (0..1).</summary>
        public void TeleportToProgress(float normalized)
        {
            _distance = Mathf.Clamp01(normalized) * _totalLength;
            _atEnd = normalized >= 1f;
            SnapToDistance(_distance);
        }

        /// <summary>
        /// Dorong zombie mundur sejauh <paramref name="worldDistance"/> unit di sepanjang path.
        /// Dipakai oleh knockback ramuan. Aman dipanggil bertahap tiap frame.
        /// </summary>
        public void PushBack(float worldDistance)
        {
            if (worldDistance <= 0f || _totalLength <= 0.0001f)
                return;

            _distance = Mathf.Clamp(_distance - worldDistance, 0f, _totalLength);
            if (_distance < _totalLength)
                _atEnd = false;

            SnapToDistance(_distance);
        }

        /// <summary>Ganti pengali skala mutasi (misal dari sistem evolusi zombie).</summary>
        public void SetMutationScale(float scale)
        {
            mutationScale = Mathf.Max(0.01f, scale);
            _lastScale = float.NaN; // paksa update skala di frame berikutnya
        }

        // ------------------------------------------------------------------
        // Path building
        // ------------------------------------------------------------------

        void CacheRenderers()
        {
            _renderers = includeChildRenderers
                ? GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponents<SpriteRenderer>();
        }

        void RebuildPath()
        {
            _pathDirty = false;
            _samples.Clear();
            _distances.Clear();
            _totalLength = 0f;
            _atEnd = false;
            _endNotified = false;

            var valid = new List<Transform>(waypoints != null ? waypoints.Length : 0);
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    if (waypoints[i] != null)
                        valid.Add(waypoints[i]);
                }
            }

            if (valid.Count < 2)
                return;

            if (useCatmullRomSpline)
                BuildSplineSamples(valid);
            else
                BuildLinearSamples(valid);

            if (_samples.Count < 2)
                return;

            // Tabel jarak kumulatif: _distances[i] = panjang path dari titik 0 sampai sampel i.
            _distances.Add(0f);
            float total = 0f;
            for (int i = 1; i < _samples.Count; i++)
            {
                total += Vector2.Distance(_samples[i - 1], _samples[i]);
                _distances.Add(total);
            }

            _totalLength = total;
            _distance = Mathf.Clamp(_distance, 0f, _totalLength);
        }

        void BuildSplineSamples(List<Transform> valid)
        {
            int samplesPerSegment = Mathf.Max(2, splineSamplesPerSegment);

            for (int i = 0; i < valid.Count - 1; i++)
            {
                Vector2 p0 = ToVector2(valid[Mathf.Max(0, i - 1)].position);
                Vector2 p1 = ToVector2(valid[i].position);
                Vector2 p2 = ToVector2(valid[i + 1].position);
                Vector2 p3 = ToVector2(valid[Mathf.Min(valid.Count - 1, i + 2)].position);

                for (int s = 0; s < samplesPerSegment; s++)
                {
                    float t = s / (float)samplesPerSegment;
                    _samples.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }

            _samples.Add(ToVector2(valid[valid.Count - 1].position));
        }

        void BuildLinearSamples(List<Transform> valid)
        {
            for (int i = 0; i < valid.Count; i++)
                _samples.Add(ToVector2(valid[i].position));
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector2 ToVector2(Vector3 v)
        {
            return new Vector2(v.x, v.y);
        }

        // ------------------------------------------------------------------
        // Movement
        // ------------------------------------------------------------------

        void Advance()
        {
            if (_totalLength <= 0.0001f)
                return;

            if (moveSpeed > 0f && !_atEnd)
            {
                float effectiveSpeed = moveSpeed;
                if (adjustSpeedByPerspective)
                {
                    float perspective = Arena25DMath.PerspectiveScale(
                        transform.position.y, perspectiveBottomY, perspectiveTopY, nearScale, farScale);
                    effectiveSpeed *= perspective;
                }

                _distance += effectiveSpeed * Time.deltaTime;

                if (_distance >= _totalLength)
                {
                    if (loopPath)
                    {
                        _distance = Mathf.Repeat(_distance, _totalLength);
                    }
                    else
                    {
                        _distance = _totalLength;
                        _atEnd = true;
                    }
                }
            }

            if (_atEnd && !_endNotified)
            {
                _endNotified = true;
                OnReachedEnd?.Invoke(this);
            }
        }

        void UpdateTransform()
        {
            Vector2 position;
            Vector2 direction;
            SamplePath(_distance, out position, out direction);

            transform.position = new Vector3(position.x, position.y, fixedZ);

            ApplyFacing(direction);
            ApplySortingAndScale(position.y);
            ApplyWaddle();
        }

        void SnapToDistance(float distance)
        {
            Vector2 position;
            Vector2 direction;
            SamplePath(distance, out position, out direction);
            transform.position = new Vector3(position.x, position.y, fixedZ);
            ApplyFacing(direction);
            ApplySortingAndScale(position.y);
        }

        void SamplePath(float distance, out Vector2 position, out Vector2 direction)
        {
            position = _samples.Count > 0 ? _samples[_samples.Count - 1] : ToVector2(transform.position);
            direction = Vector2.right;

            if (_samples.Count < 2 || _distances.Count < 2 || _totalLength <= 0.0001f)
                return;

            // Cari segmen dengan binary search di tabel jarak kumulatif.
            int lo = 0;
            int hi = _distances.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (_distances[mid] <= distance)
                    lo = mid;
                else
                    hi = mid;
            }

            float segmentLength = _distances[hi] - _distances[lo];
            float t = segmentLength > 1e-6f
                ? Mathf.Clamp01((distance - _distances[lo]) / segmentLength)
                : 0f;

            position = Vector2.Lerp(_samples[lo], _samples[hi], t);

            direction = _samples[hi] - _samples[lo];
            if (direction.sqrMagnitude < 1e-8f)
                direction = Vector2.right;
        }

        // ------------------------------------------------------------------
        // 2.5D presentation
        // ------------------------------------------------------------------

        void ApplyFacing(Vector2 direction)
        {
            if (!autoFlip)
                return;
            if (Mathf.Abs(direction.x) < 0.001f)
                return;

            bool shouldFlip = (direction.x < 0f) == spriteFacesRightByDefault;
            if (shouldFlip == _flipped)
                return;

            _flipped = shouldFlip;

            if (_renderers == null || _renderers.Length == 0)
                CacheRenderers();

            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].flipX = shouldFlip;
            }
        }

        void ApplySortingAndScale(float worldY)
        {
            float perspective = Arena25DMath.PerspectiveScale(
                worldY, perspectiveBottomY, perspectiveTopY, nearScale, farScale);

            float scale = perspective * baseScale * mutationScale;
            if (float.IsNaN(_lastScale) || Mathf.Abs(scale - _lastScale) > 0.0001f)
            {
                _lastScale = scale;
                transform.localScale = new Vector3(scale, scale, 1f);
            }

            int sortingOrder = Arena25DMath.SortingOrderByY(worldY, sortingOrderBias);

            if (_renderers == null || _renderers.Length == 0)
                CacheRenderers();

            if (_renderers == null)
                return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].sortingOrder = sortingOrder;
            }
        }

        void ApplyWaddle()
        {
            if (Mathf.Abs(waddleAngle) < 0.001f)
            {
                transform.localRotation = Quaternion.identity;
                return;
            }

            _waddlePhase += waddleSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_waddlePhase) * waddleAngle);
        }

        // ------------------------------------------------------------------
        // Gizmos
        // ------------------------------------------------------------------

        void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length < 2)
                return;

            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);

            Vector3 previous = waypoints[0] != null ? waypoints[0].position : transform.position;
            for (int i = 1; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                    continue;

                Vector3 current = waypoints[i].position;
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.08f);
        }
    }
}