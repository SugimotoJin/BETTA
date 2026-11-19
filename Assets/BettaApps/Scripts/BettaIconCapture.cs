#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Betta3D
{
    /// <summary>
    /// Editor only (not in builds): renders each preset betta alone on a transparent background,
    /// resting and flaring, and saves the PNGs to Builds/BettaIcon for making the app icon.
    /// Started from the menu "Betta → アイコン用のベタを撮影".
    /// </summary>
    public class BettaIconCapture : MonoBehaviour
    {
        public int resolution = 2048;
        public int seed = 1234;
        public float settleSeconds = 2.5f;

        Camera cam;

        void Awake()
        {
            BettaFish.BodyShader = Shader.Find("Betta/Body");
            BettaFish.FinShader = Shader.Find("Betta/Fin");
            BettaFish.EyeShader = Shader.Find("Betta/Eye");

            GameObject camGo = new GameObject("Icon Camera");
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;   // the HDR buffer has no alpha channel
            cam.allowMSAA = true;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            GameObject lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(45f, -10f, 0f);
            RenderSettings.sun = sun;

            // same look as the desktop app
            BettaLook.ShaderTonemap = true;
            BettaLook.Exposure = Mathf.Pow(2f, 0.35f);
            BettaLook.Apply();
        }

        IEnumerator Start()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/BettaIcon"));
            Directory.CreateDirectory(dir);

            RenderTexture rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 8;
            RenderTexture resolved = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;

            int count = 0;
            for (int i = 0; i < BettaParams.PresetNames.Length; i++)
            {
                for (int flare = 0; flare < 2; flare++)
                {
                    BettaParams p = BettaParams.Preset(i);
                    p.seed = seed;
                    p.size = 1f;
                    GameObject go = new GameObject(p.name);
                    go.SetActive(false);
                    BettaFish f = go.AddComponent<BettaFish>();
                    f.Params = p;
                    go.SetActive(true);

                    // hold still and let the soft fins settle into their rest (or flared) shape
                    float t = 0f;
                    while (t < settleSeconds)
                    {
                        f.Phase = 0f; f.Swim = 0f; f.Pect = 0f; f.Bend = 0f; f.Fold = 0f;
                        f.Flare = flare;
                        Frame(go);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    yield return new WaitForEndOfFrame();

                    string file = Path.Combine(dir, string.Format("betta_{0:00}_{1}.png", i, flare == 1 ? "flare" : "rest"));
                    bool clipped = Save(rt, resolved, file);
                    Debug.Log("Betta: " + p.name + (flare == 1 ? "（フレア）" : "") + " → " + Path.GetFileName(file)
                              + (clipped ? "  ※端で切れています" : ""));
                    count++;
                    Destroy(go);
                    yield return null;
                }
            }

            cam.targetTexture = null;
            rt.Release(); resolved.Release();
            Debug.Log("Betta: アイコン用の画像を " + count + " 枚書き出しました → " + dir);
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        /// <summary>Point the camera at the fish and make it fit, with a margin (the PNG is cropped afterwards).</summary>
        void Frame(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);
            cam.transform.position = new Vector3(b.center.x, b.center.y, b.center.z - 20f);
            cam.transform.rotation = Quaternion.identity;
            cam.orthographicSize = Mathf.Max(0.5f, Mathf.Max(b.extents.x, b.extents.y) * 1.25f);
        }

        /// <summary>Read the frame, undo the premultiplied alpha, crop to the fish, pad to a square and save.</summary>
        static bool Save(RenderTexture rt, RenderTexture resolved, string file)
        {
            Graphics.Blit(rt, resolved);   // resolves MSAA
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = resolved;
            int n = resolved.width;
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Color32[] px = tex.GetPixels32();
            int x0 = n, y0 = n, x1 = -1, y1 = -1;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int idx = y * n + x;
                    Color32 c = px[idx];
                    if (c.a > 0 && c.a < 255)
                    {
                        // fins blend as (src * a) into a clear buffer, so the colour is premultiplied
                        float inv = 255f / c.a;
                        c.r = (byte)Mathf.Min(255f, c.r * inv);
                        c.g = (byte)Mathf.Min(255f, c.g * inv);
                        c.b = (byte)Mathf.Min(255f, c.b * inv);
                        px[idx] = c;
                    }
                    if (c.a > 6)
                    {
                        if (x < x0) x0 = x; if (x > x1) x1 = x;
                        if (y < y0) y0 = y; if (y > y1) y1 = y;
                    }
                }
            Object.Destroy(tex);
            if (x1 < 0) { Debug.LogWarning("Betta: 何も写っていません → " + file); return false; }
            bool clipped = x0 == 0 || y0 == 0 || x1 == n - 1 || y1 == n - 1;

            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            int side = Mathf.CeilToInt(Mathf.Max(w, h) * 1.04f);
            int ox = (side - w) / 2, oy = (side - h) / 2;
            Color32[] outPx = new Color32[side * side];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    outPx[(y + oy) * side + (x + ox)] = px[(y + y0) * n + (x + x0)];
            Texture2D outTex = new Texture2D(side, side, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPx);
            outTex.Apply();
            File.WriteAllBytes(file, outTex.EncodeToPNG());
            Object.Destroy(outTex);
            return clipped;
        }
    }
}
#endif
