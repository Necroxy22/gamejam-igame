using System.Collections.Generic;
using UnityEngine;

namespace CuriesCrucible.Arena25D
{
    /// <summary>
    /// Shared math for the 2.5D arena: perspective-by-Y scaling, depth sorting and road geometry.
    /// Satu sumber angka yang sama dipakai oleh zombie, reticle, proyektil, dan tool editor,
    /// supaya semua elemen arena selalu konsisten (Y bawah = besar/depan, Y atas = kecil/belakang).
    /// </summary>
    public static class Arena25DMath
    {
        /// <summary>Setiap 1 unit Y menggeser sorting order sebanyak ini.</summary>
        public const int DepthSortingStep = 100;

        /// <summary>Perbandingan sumbu Y elips untuk sprite yang "tergeletak" di tanah (reticle, bayangan).</summary>
        public const float DefaultPerspectiveSquash = 0.55f;

        /// <summary>Nilai default arena: Y = -4.0 paling dekat kamera, Y = 1.5 paling jauh.</summary>
        public const float DefaultNearY = -4.0f;
        public const float DefaultFarY = 1.5f;
        public const float DefaultNearScale = 1.15f;
        public const float DefaultFarScale = 0.65f;

        // ------------------------------------------------------------------
        // Perspektif Y
        // ------------------------------------------------------------------

        /// <summary>Mengubah koordinat Y layar menjadi 0 (paling jauh) .. 1 (paling dekat).</summary>
        public static float PerspectiveT(float worldY, float bottomY = DefaultNearY, float topY = DefaultFarY)
        {
            if (Mathf.Abs(topY - bottomY) < 0.0001f)
                return 0f;
            return Mathf.Clamp01((worldY - bottomY) / (topY - bottomY));
        }

        /// <summary>Skala sprite pada ketinggian Y tertentu (1.15x di Y=-4, 0.65x di Y=1.5 secara default).</summary>
        public static float PerspectiveScale(
            float worldY,
            float bottomY = DefaultNearY,
            float topY = DefaultFarY,
            float nearScale = DefaultNearScale,
            float farScale = DefaultFarScale)
        {
            return Mathf.Lerp(nearScale, farScale, PerspectiveT(worldY, bottomY, topY));
        }

        /// <summary>Scale (x,y) sebuah sprite lingkaran agar radius-nya pas di dunia, dengan squash perspektif.</summary>
        public static Vector3 ScaleForSpriteRadius(Sprite sprite, float radius, float perspective, float squash)
        {
            float spriteSize = sprite != null ? Mathf.Max(0.0001f, sprite.bounds.size.x) : 1f;
            float s = (radius * 2f / spriteSize) * perspective;
            return new Vector3(s, s * squash, 1f);
        }

        // ------------------------------------------------------------------
        // Sorting order (2.5D depth)
        // ------------------------------------------------------------------

        /// <summary>Sorting order standar: semakin ke bawah layar (Y kecil) semakin di depan.</summary>
        public static int SortingOrderByY(float worldY, int bias = 0)
        {
            return Mathf.RoundToInt(-worldY * DepthSortingStep) + bias;
        }

        /// <summary>Sorting order untuk elemen yang menempel di tanah, tidak pernah masuk di belakang background.</summary>
        public static int GroundSortingOrder(float worldY, int bias = 0, int minOrder = -90)
        {
            return Mathf.Max(minOrder, SortingOrderByY(worldY, bias));
        }

        // ------------------------------------------------------------------
        // Konversi layar -> dunia
        // ------------------------------------------------------------------

        /// <summary>Konversi posisi layar ke world XY pada bidang Z tertentu (default bidang tanah Z = 0).</summary>
        public static Vector3 ScreenToWorld(Camera cam, Vector3 screenPoint, float planeZ = 0f)
        {
            screenPoint.z = Mathf.Abs(cam.transform.position.z - planeZ);
            return cam.ScreenToWorldPoint(screenPoint);
        }

        // ------------------------------------------------------------------
        // Geometri jalan
        // ------------------------------------------------------------------

        /// <summary>Jarak kuadrat titik ke segmen garis.</summary>
        public static float DistancePointToSegmentSq(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f)
                return (p - a).sqrMagnitude;

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 projection = a + ab * t;
            return (p - projection).sqrMagnitude;
        }

        /// <summary>
        /// Membuat outline tertutup (kiri-maju + kanan-mundur) dari sebuah polyline waypoint.
        /// Dipakai untuk PolygonCollider2D jalan yang mengikuti kurva waypoint.
        /// Ujung polyline diperpanjang sedikit (default 0.75 * halfWidth) supaya area
        /// gerbang muncul & pintu menara tetap bisa diklik.
        /// </summary>
        public static Vector2[] RoadOutline(IList<Vector2> points, float halfWidth, float endExtensionFactor = 0.4f)
        {
            if (points == null || points.Count < 2)
                return new Vector2[0];

            int n = points.Count;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
                pts[i] = points[i];

            float extension = halfWidth * Mathf.Clamp01(endExtensionFactor);
            if (extension > 0.0001f)
            {
                Vector2 startDirection = pts[0] - pts[1];
                if (startDirection.sqrMagnitude > 1e-8f)
                    pts[0] += startDirection.normalized * extension;

                Vector2 endDirection = pts[n - 1] - pts[n - 2];
                if (endDirection.sqrMagnitude > 1e-8f)
                    pts[n - 1] += endDirection.normalized * extension;
            }

            var left = new Vector2[n];
            var right = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                Vector2 current = pts[i];
                Vector2 previous = pts[i > 0 ? i - 1 : i];
                Vector2 next = pts[i < n - 1 ? i + 1 : i];

                Vector2 dirPrev = current - previous;
                Vector2 dirNext = next - current;

                if (dirPrev.sqrMagnitude < 1e-8f) dirPrev = dirNext;
                if (dirNext.sqrMagnitude < 1e-8f) dirNext = dirPrev;
                if (dirPrev.sqrMagnitude < 1e-8f) dirPrev = Vector2.right;
                if (dirNext.sqrMagnitude < 1e-8f) dirNext = Vector2.right;

                dirPrev.Normalize();
                dirNext.Normalize();

                // Normal kiri dari masing-masing segmen, lalu dirata-ratakan (miter).
                Vector2 normalPrev = new Vector2(-dirPrev.y, dirPrev.x);
                Vector2 normalNext = new Vector2(-dirNext.y, dirNext.x);
                Vector2 normal = normalPrev + normalNext;
                if (normal.sqrMagnitude < 1e-6f)
                    normal = normalPrev; // belokan 180 derajat
                normal.Normalize();

                // Miter dibatasi supaya tikungan tajam tidak meledak jauh.
                float miter = halfWidth / Mathf.Max(0.35f, Vector2.Dot(normal, normalPrev));

                left[i] = current + normal * miter;
                right[i] = current - normal * miter;
            }

            var outline = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
                outline[i] = left[i];
            for (int i = 0; i < n; i++)
                outline[n + i] = right[n - 1 - i];

            return outline;
        }

        /// <summary>Polyline XY dari child transform (dipakai editor tool untuk membaca waypoint).</summary>
        public static Vector2[] ReadPath2D(Transform root)
        {
            if (root == null)
                return new Vector2[0];

            var points = new Vector2[root.childCount];
            for (int i = 0; i < root.childCount; i++)
            {
                Vector3 p = root.GetChild(i).position;
                points[i] = new Vector2(p.x, p.y);
            }
            return points;
        }
    }
}