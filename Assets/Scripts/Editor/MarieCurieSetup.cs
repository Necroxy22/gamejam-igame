using CuriesCrucible.Arena25D;
using CuriesCrucible.Player;
using UnityEditor;
using UnityEngine;

namespace CuriesCrucible.EditorTools
{
    /// <summary>
    /// Menu: Tools → Curie's Crucible → 2. Setup Marie Curie Player
    /// Membuat MarieCurie_Player di Environment_Layers, memasang sprite placeholder
    /// Victorian-steampunk, menghubungkan MarieCurie_ThrowOrigin & ArenaThrowTargeting.
    /// </summary>
    static class MarieCurieSetup
    {
        const string ArenaRootName = "Arena_25D";
        const string EnvironmentName = "Environment_Layers";
        const string ThrowOriginName = "MarieCurie_ThrowOrigin";
        const string PlayerName = "MarieCurie_Player";

        [MenuItem("Tools/Curie's Crucible/2. Setup Marie Curie Player", false, 20)]
        static void SetupMarie()
        {
            GameObject arena = GameObject.Find(ArenaRootName);

            if (arena == null)
            {
                EditorUtility.DisplayDialog(
                    "Marie Curie Setup",
                    "Hierarki arena belum ada. Jalankan dulu:\nTools → Curie's Crucible → 1. Generate 2.5D Arena Hierarchy",
                    "OK");
                return;
            }

            Transform environment = arena.transform.Find(EnvironmentName);

            if (environment == null)
            {
                EditorUtility.DisplayDialog(
                    "Marie Curie Setup",
                    "Environment_Layers tidak ditemukan di dalam " + ArenaRootName + ".",
                    "OK");
                return;
            }

            // Pastikan titik lempar ada.
            Transform throwOrigin = environment.Find(ThrowOriginName);
            bool createdOrigin = false;

            if (throwOrigin == null)
            {
                var originGo = new GameObject(ThrowOriginName);
                originGo.transform.SetParent(environment, false);
                originGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                Undo.RegisterCreatedObjectUndo(originGo, "Create Throw Origin");
                throwOrigin = originGo.transform;
                createdOrigin = true;
            }

            // GameObject Marie.
            Transform existing = environment.Find(PlayerName);

            GameObject playerGo = existing != null
                ? existing.gameObject
                : new GameObject(PlayerName);

            playerGo.transform.SetParent(environment, false);
            playerGo.transform.localPosition = throwOrigin.localPosition;
            playerGo.transform.localRotation = Quaternion.identity;

            // Sprite.
            var renderer = playerGo.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = playerGo.AddComponent<SpriteRenderer>();

            if (renderer.sprite == null)
                renderer.sprite = Arena25DSetup.ArtFactory.EnsureMariePlaceholder();

            renderer.sortingOrder = 450;

            // Controller.
            var controller = playerGo.GetComponent<MarieCurieController>();
            if (controller == null)
                controller = playerGo.AddComponent<MarieCurieController>();

            controller.throwOrigin = throwOrigin;
            controller.spriteRenderer = renderer;
            controller.sortingOrder = 450;
            controller.minX = -0.85f;
            controller.maxX = 0.85f;
            controller.moveSpeed = 3.0f;

            // Hubungkan ke ArenaThrowTargeting.
            ArenaThrowTargeting targeting = FindTargets.FindTargeting();

            if (targeting != null)
            {
                targeting.throwOrigin = throwOrigin;
                targeting.marie = controller;
            }

            Undo.RegisterCreatedObjectUndo(playerGo, "Create Marie Curie Player");
            EditorUtility.SetDirty(playerGo);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(controller);

            if (createdOrigin)
                EditorUtility.SetDirty(throwOrigin.gameObject);

            Selection.activeGameObject = playerGo;
            SceneView.FrameLastActiveSceneView();

            EditorUtility.DisplayDialog(
                "Marie Curie Setup",
                "MarieCurie_Player siap.\n\n" +
                "• A / D atau panah kiri-kanan untuk menggeser.\n" +
                "• Marie menghadap kursor mouse.\n" +
                "• MarieCurie_ThrowOrigin mengikuti posisi tangan.\n" +
                "• Animasi lempar dipicu otomatis oleh ArenaThrowTargeting.",
                "OK");
        }
    }
}