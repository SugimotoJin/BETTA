using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Betta3D.EditorTools
{
    /// <summary>Betta → デモシーンを作って開く</summary>
    public static class BettaDemoMenu
    {
        [MenuItem("Betta/デモシーンを作って開く")]
        public static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("BettaDemo");
            var demo = go.AddComponent<BettaDemo>();
            demo.bodyShader = Shader.Find("Betta/Body");
            demo.finShader = Shader.Find("Betta/Fin");
            demo.eyeShader = Shader.Find("Betta/Eye");
            demo.waterShader = Shader.Find("Betta/Water");

            if (demo.bodyShader == null || demo.finShader == null || demo.eyeShader == null || demo.waterShader == null)
                Debug.LogError("Betta: シェーダーが見つかりません。Console のシェーダーエラーを確認してください。");

            const string path = "Assets/BettaDemo.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("Betta: " + path + " を作りました。再生ボタンを押してください。");
        }
    }
}
