using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Betta3D.EditorTools
{
    /// <summary>Betta → アイコン用のベタを撮影</summary>
    public static class BettaIconMenu
    {
        [MenuItem("Betta/アイコン用のベタを撮影")]
        public static void CaptureIcons()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // an unsaved, empty scene with only the capture object; play mode exits by itself when done
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BettaIconCapture").AddComponent<BettaIconCapture>();
            Debug.Log("Betta: アイコン用の撮影を始めます（1匹 約2.5秒 × 26枚）。終わると再生が自動で止まります。");
            EditorApplication.EnterPlaymode();
        }
    }
}
