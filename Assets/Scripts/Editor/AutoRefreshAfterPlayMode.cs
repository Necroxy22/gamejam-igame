#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuriesCrucible.Editor
{
    [InitializeOnLoad]
    public static class AutoRefreshAfterPlayMode
    {
        static AutoRefreshAfterPlayMode()
        {
            EditorApplication.delayCall += Refresh;
        }

        static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Scene current = SceneManager.GetActiveScene();
            if (current.path == "Assets/Scenes/SampleScene.unity")
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                Debug.Log("<color=#44FF88>[AutoRefresh] SampleScene berhasil di-reload otomatis dengan perubahan pacing & OnGUI bersih!</color>");
            }
        }
    }
}
#endif
