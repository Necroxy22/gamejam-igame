using CuriesCrucible.Arena25D;
using CuriesCrucible.Player;
using UnityEngine;

namespace CuriesCrucible.EditorTools
{
    /// <summary>Helper pencarian objek scene yang kompatibel Unity 2022 &amp; Unity 6.</summary>
    static class FindTargets
    {
        public const string ArenaRootName = "Arena_25D";

        public static GameObject ArenaRoot
        {
            get { return GameObject.Find(ArenaRootName); }
        }

        public static ArenaThrowTargeting FindTargeting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ArenaThrowTargeting>();
#else
            return Object.FindObjectOfType<ArenaThrowTargeting>();
#endif
        }

        public static MarieCurieController FindMarie()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<MarieCurieController>();
#else
            return Object.FindObjectOfType<MarieCurieController>();
#endif
        }

        public static Transform FindPathRoot(string name)
        {
            GameObject arena = ArenaRoot;
            return arena != null ? arena.transform.Find(name) : null;
        }
    }
}