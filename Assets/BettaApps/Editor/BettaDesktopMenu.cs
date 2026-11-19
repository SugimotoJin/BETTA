using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
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
        const string IconDir = "Assets/BettaApps/Icons";
        static readonly int[] IconFiles = { 16, 32, 48, 128, 256, 512, 1024 };   // betta_icon_<size>.png

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
            SetAppIcon();   // the exe icon; the tray icon is taken from the exe

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
                string dir = Path.GetDirectoryName(full);
                StripPdbPaths(Path.Combine(dir, "BettaDesktop_Data", "Managed"));
                CheckForPersonalPaths(dir);
                Debug.Log("Betta: ビルドできました → " + full);
                EditorUtility.RevealInFinder(full);
            }
            else
            {
                Debug.LogError("Betta: ビルドに失敗しました（" + report.summary.result + "）。エラー " + report.summary.totalErrors + " 件");
            }
        }

        /// <summary>Sets the betta pictures as the Windows exe icon, one picture per icon size.</summary>
        static void SetAppIcon()
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            Texture2D[] icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) icons[i] = LoadIcon(sizes[i]);
            if (System.Array.IndexOf(icons, null) >= 0)
            {
                Debug.LogWarning("Betta: アイコン画像が見つかりません（" + IconDir + "）。アイコンは変えずにビルドします。");
                return;
            }
            PlayerSettings.SetIcons(target, icons, IconKind.Application);
        }

        /// <summary>The picture made for this size, or else the next larger one.</summary>
        static Texture2D LoadIcon(int size)
        {
            int pick = IconFiles[IconFiles.Length - 1];
            foreach (int s in IconFiles) if (s >= size) { pick = s; break; }
            string path = IconDir + "/betta_icon_" + pick + ".png";
            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            // keep the pixels exactly as drawn: no resizing, mipmaps or compression
            if (imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None
                || imp.textureCompression != TextureImporterCompression.Uncompressed || !imp.alphaIsTransparency || imp.maxTextureSize < 2048)
            {
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = true;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// The compiler writes the full path of each assembly's .pdb (which includes the Windows
        /// user folder) into the dll. Keep only the file name, so the release does not carry it.
        /// </summary>
        static void StripPdbPaths(string managedDir)
        {
            if (!Directory.Exists(managedDir)) return;
            // only paths inside the user folder: Unity's and Microsoft's own dlls are left untouched
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return;
            foreach (string dll in Directory.GetFiles(managedDir, "*.dll"))
            {
                byte[] b = File.ReadAllBytes(dll);
                bool changed = false;
                for (int i = 0; i + 24 < b.Length; i++)
                {
                    if (b[i] != 'R' || b[i + 1] != 'S' || b[i + 2] != 'D' || b[i + 3] != 'S') continue;
                    int start = i + 24;   // "RSDS", GUID (16 bytes), age (4 bytes), then the path
                    int end = Array.IndexOf(b, (byte)0, start);
                    if (end < 0 || end - start > 2048) continue;
                    string path = Encoding.UTF8.GetString(b, start, end - start);
                    if (!path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!path.Replace('/', '\\').StartsWith(home, StringComparison.OrdinalIgnoreCase)) continue;
                    byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path.Replace('\\', '/')));
                    for (int k = start; k < end; k++) b[k] = k - start < name.Length ? name[k - start] : (byte)0;
                    changed = true;
                }
                if (changed) File.WriteAllBytes(dll, b);
            }
        }

        /// <summary>Warns if any file of the build still contains the user folder path or a debug file.</summary>
        static void CheckForPersonalPaths(string buildDir)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return;
            byte[][] needles = { Encoding.UTF8.GetBytes(home.ToLowerInvariant()), Encoding.Unicode.GetBytes(home.ToLowerInvariant()),
                                 Encoding.UTF8.GetBytes(home.Replace('\\', '/').ToLowerInvariant()) };
            int found = 0;
            foreach (string f in Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories))
            {
                string rel = f.Substring(buildDir.Length + 1);
                if (rel.Contains("DoNotShip")) continue;   // left out of the zip
                if (Path.GetFileName(f).Equals("betta_debug.txt", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning("Betta: 調査用ファイルがあります。zip に入れないでください → " + rel);
                    continue;
                }
                byte[] b = File.ReadAllBytes(f);
                for (int i = 0; i < b.Length; i++) if (b[i] >= 'A' && b[i] <= 'Z') b[i] = (byte)(b[i] + 32);
                foreach (byte[] n in needles)
                    if (IndexOf(b, n) >= 0) { Debug.LogWarning("Betta: ユーザーフォルダのパスが残っています → " + rel); found++; break; }
            }
            if (found == 0) Debug.Log("Betta: ビルドにユーザーフォルダのパスは含まれていません。");
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            if (needle.Length == 0) return -1;
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                if (hay[i] != needle[0]) continue;
                int k = 1;
                while (k < needle.Length && hay[i + k] == needle[k]) k++;
                if (k == needle.Length) return i;
            }
            return -1;
        }
    }
}
