using UnityEngine;

namespace CuriesCrucible.Arena25D
{
    /// <summary>
    /// Menjaga framing arena 16:9 tetap utuh di segala rasio layar (penting untuk build WebGL
    /// yang bisa dibuka di jendela browser dengan ukuran apa pun).
    ///
    /// Mode:
    ///  - FixedSize : orthographic size selalu sama (framing 16:9 bisa terpotong di layar lebar/tinggi).
    ///  - FitWidth  : lebar arena selalu terlihat penuh (default, rekomendasi untuk arena statis 16:9).
    ///  - FitHeight : tinggi arena selalu terlihat penuh.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class Arena25DCameraRig : MonoBehaviour
    {
        public enum FitMode
        {
            FixedSize,
            FitWidth,
            FitHeight
        }

        [Header("Framing")]
        public FitMode fitMode = FitMode.FitWidth;

        [Tooltip("Dipakai saat fitMode = FixedSize.")]
        public float fixedSize = 4.8f;

        [Tooltip("Lebar arena yang harus selalu masuk ke layar (world unit).")]
        public float designWidth = 17.2f;

        [Tooltip("Tinggi arena yang harus selalu masuk ke layar (world unit).")]
        public float designHeight = 9.6f;

        [Tooltip("Kosongkan untuk memakai Screen.width / Screen.height.")]
        public Camera targetCamera;

        [Tooltip("Jangan sentuh kamera kalau ukurannya sudah pas.")]
        public float aspectTolerance = 0.002f;

        Camera _camera;

        void OnEnable()
        {
            CacheCamera();
            Apply();
        }

        void Update()
        {
            Apply();
        }

        void CacheCamera()
        {
            if (targetCamera != null)
            {
                _camera = targetCamera;
                return;
            }

            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = Camera.main;
        }

        void Apply()
        {
            if (_camera == null)
            {
                CacheCamera();
                if (_camera == null)
                    return;
            }

            if (!_camera.orthographic)
                return;

            float aspect = GetAspect();
            float size = ResolveSize(aspect);

            if (Mathf.Abs(_camera.orthographicSize - size) > aspectTolerance)
                _camera.orthographicSize = size;
        }

        float ResolveSize(float aspect)
        {
            switch (fitMode)
            {
                case FitMode.FitWidth:
                    return (designWidth * 0.5f) / Mathf.Max(0.01f, aspect);

                case FitMode.FitHeight:
                    return designHeight * 0.5f;

                default:
                    return fixedSize;
            }
        }

        float GetAspect()
        {
            if (_camera != null && _camera.targetTexture != null)
                return Mathf.Max(0.01f, _camera.targetTexture.width / (float)_camera.targetTexture.height);

            return Mathf.Max(0.01f, Screen.width / (float)Mathf.Max(1, Screen.height));
        }

        /// <summary>Orthographic size yang akan dipakai kamera saat ini (berguna untuk tooltip/debug).</summary>
        public float CurrentSize
        {
            get
            {
                if (_camera == null)
                    CacheCamera();
                return _camera != null ? _camera.orthographicSize : 0f;
            }
        }
    }
}