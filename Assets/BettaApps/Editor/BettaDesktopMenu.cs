using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Betta3D.EditorTools
{
    /// <summary>Betta → デスクトップ版のシーン / ビルド</summary>
    public static class BettaDesktopMenu
    {
        const string DesktopScene = "Assets/BettaDesktop.unity";
        const string BuildPath = "Builds/BettaDesktop/BettaDesktop.exe";

        [MenuItem("Betta/デスクトップ版のシーンを作って開く")]
        public static void CreateDesktopSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateDesktopScene();
            Debug.Log("Betta: " + DesktopScene + " を作りました。再生するとエディタ上で試せます（透明化はビルド版のみ）。");
        }

        static bool CreateDesktopScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("BettaDesktop");
            var app = go.AddComponent<BettaDesktop>();
            app.bodyShader = Shader.Find("Betta/Body");
            app.finShader = Shader.Find("Betta/Fin");
            app.eyeShader = Shader.Find("Betta/Eye");
            app.foodShader = Shader.Find("Betta/Food");
            bool ok = app.bodyShader != null && app.finShader != null && app.eyeShader != null && app.foodShader != null;
            if (!ok) Debug.LogError("Betta: シェーダーが見つかりません。Console のシェーダーエラーを確認してください。");
            EditorSceneManager.SaveScene(scene, DesktopScene);
            return ok;
        }

        [MenuItem("Betta/デスクトップ版をビルド（Windows）")]
        public static void BuildDesktop()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!CreateDesktopScene()) return;

            // a transparent, borderless, always-running window
            PlayerSettings.productName = "BettaDesktop";
            PlayerSettings.bundleVersion = BettaDesktop.Version;   // shown in the exe's file properties
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = Mathf.Max(800, Display.main.systemWidth);
            PlayerSettings.defaultScreenHeight = Mathf.Max(600, Display.main.systemHeight);
            PlayerSettings.resizableWindow = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.useFlipModelSwapchain = false;   // required for a see-through window
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            try { PlayerSettings.SplashScreen.show = false; } catch { }

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { DesktopScene },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                string full = Path.GetFullPath(BuildPath);
                Debug.Log("Betta: ビルドできました → " + full);
                EditorUtility.RevealInFinder(full);
            }
            else
            {
                Debug.LogError("Betta: ビルドに失敗しました（" + report.summary.result + "）。エラー " + report.summary.totalErrors + " 件");
            }
        }
    }
}
