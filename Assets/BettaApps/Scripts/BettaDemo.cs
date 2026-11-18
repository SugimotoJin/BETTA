using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Betta3D
{
    /// <summary>
    /// Step 1 demo: builds a camera, light and water background, spawns a few bettas,
    /// and shows a panel to change their shape and colors live.
    /// </summary>
    public class BettaDemo : MonoBehaviour
    {
        public Shader bodyShader, finShader, eyeShader, waterShader;

        const float FOV = 25f;
        const float TankHalfHeight = 4f;   // fish of size 1 = 1/8 of the screen height

        Camera cam;
        Transform bg;
        Material bgMat;
        readonly List<BettaSwimmer> fishes = new List<BettaSwimmer>();
        int sel;
        bool observe;
        float zoom = 0.3f;
        float camYaw;
        int bgMode;
        static readonly string[] BgNames = { "黒（写真風）", "水槽", "明るい画面" };
        int presetIdx;
        Vector2 scroll;
        readonly BettaParamsGUI paramsGui = new BettaParamsGUI();

        void Awake()
        {
            BettaFish.BodyShader = bodyShader != null ? bodyShader : Shader.Find("Betta/Body");
            BettaFish.FinShader = finShader != null ? finShader : Shader.Find("Betta/Fin");
            BettaFish.EyeShader = eyeShader != null ? eyeShader : Shader.Find("Betta/Eye");
            if (waterShader == null) waterShader = Shader.Find("Betta/Water");

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = FOV;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.12f, 0.17f);
            cam.allowMSAA = true;
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null && urp.msaaSampleCount < 4) urp.msaaSampleCount = 4;   // smooth fin edges

            GameObject lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(45f, -10f, 0f);   // from the camera side, above
            RenderSettings.sun = sun;

            Shader.SetGlobalColor("_BettaAmbient", new Color(0.30f, 0.38f, 0.46f));
            BettaLook.ShaderTonemap = false;   // the demo uses URP post-processing instead
            BettaLook.Apply();                 // (LateUpdate then sets its own background-dependent values)

            // post-processing: filmic tone mapping, a touch of bloom and vignette
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            GameObject volGo = new GameObject("PostFX");
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            VolumeProfile prof = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = prof;
            Tonemapping tm = prof.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            ColorAdjustments ca = prof.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.35f);
            Bloom bloom = prof.Add<Bloom>(true);
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.0f);
            Vignette vig = prof.Add<Vignette>(true);
            vig.intensity.Override(0.2f);

            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Water";
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
            bg = q.transform;
            if (waterShader != null)
            {
                bgMat = new Material(waterShader);
                q.GetComponent<MeshRenderer>().sharedMaterial = bgMat;
            }
        }

        void Start()
        {
            Spawn(BettaParams.Preset(0));
            Spawn(BettaParams.Preset(1));
            Spawn(BettaParams.Preset(7));
        }

        BettaSwimmer Spawn(BettaParams p)
        {
            GameObject go = new GameObject(p.name);
            float hw = TankHalfHeight * cam.aspect;
            go.transform.position = new Vector3(Random.Range(-hw * 0.6f, hw * 0.6f), Random.Range(-2f, 2f), fishes.Count * 0.15f);
            BettaFish f = go.AddComponent<BettaFish>();
            f.Params = p;
            BettaSwimmer s = go.AddComponent<BettaSwimmer>();
            s.fish = f;
            fishes.Add(s);
            sel = fishes.Count - 1;
            return s;
        }

        void LateUpdate()
        {
            float hw = TankHalfHeight * cam.aspect;
            Rect tank = new Rect(-hw, -TankHalfHeight, hw * 2f, TankHalfHeight * 2f);
            for (int i = 0; i < fishes.Count; i++)
            {
                fishes[i].bounds = tank;
                fishes[i].observe = observe && i == sel;
            }

            float fitDist = TankHalfHeight / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad);
            Vector3 focus = Vector3.zero;
            float dist = fitDist;
            if (observe && sel >= 0 && sel < fishes.Count)
            {
                focus = fishes[sel].transform.position;
                dist = Mathf.Lerp(1.5f, fitDist, zoom) * Mathf.Max(0.3f, fishes[sel].fish.Params.size);
            }
            Quaternion rot = Quaternion.Euler(0f, camYaw, 0f);
            cam.transform.position = focus + rot * new Vector3(0f, 0f, -dist);
            cam.transform.rotation = rot;

            // background quad always fills the view, behind everything
            float bgDist = dist + 25f;
            float h = 2f * bgDist * Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad) * 1.1f;
            bg.position = cam.transform.position + cam.transform.forward * bgDist;
            bg.rotation = cam.transform.rotation;
            bg.localScale = new Vector3(h * cam.aspect, h, 1f);
            Color top, bottom, amb;
            if (bgMode == 0) { top = new Color(0.02f, 0.025f, 0.03f); bottom = new Color(0f, 0f, 0f); amb = new Color(0.16f, 0.18f, 0.2f); }
            else if (bgMode == 1) { top = new Color(0.16f, 0.42f, 0.52f); bottom = new Color(0.02f, 0.10f, 0.15f); amb = new Color(0.30f, 0.38f, 0.46f); }
            else { top = new Color(0.93f, 0.94f, 0.96f); bottom = new Color(0.82f, 0.84f, 0.87f); amb = new Color(0.45f, 0.47f, 0.5f); }
            if (bgMat != null) { bgMat.SetColor("_Top", top); bgMat.SetColor("_Bottom", bottom); }
            Shader.SetGlobalColor("_BettaAmbient", amb);
            // back light (from behind and above) so the fins glow, and the environment the scales reflect
            Shader.SetGlobalVector("_BettaRimDir", new Vector4(0.15f, 0.6f, 0.8f, 0f).normalized);
            Shader.SetGlobalColor("_BettaRimColor", new Color(0.8f, 0.9f, 1.0f) * 1.1f);
            Shader.SetGlobalColor("_BettaEnvTop", bgMode == 2 ? new Color(0.9f, 0.92f, 0.95f) : new Color(0.75f, 0.9f, 1.0f));
            Shader.SetGlobalColor("_BettaEnvBottom", bgMode == 2 ? new Color(0.5f, 0.52f, 0.55f) : new Color(0.02f, 0.06f, 0.09f));
        }

        // ------------------------------------------------------------------ UI
        float Slider(string label, float v, float min, float max, string fmt)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(170));
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            return v;
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float h = Screen.height / scale;

            GUILayout.BeginArea(new Rect(10f, 10f, 360f, h - 20f), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            if (GraphicsSettings.currentRenderPipeline == null)
                GUILayout.Label("※ URP のプロジェクトではありません。「Universal 3D」テンプレートで作ったプロジェクトで開いてください。");

            GUILayout.Label("ベタ 3D 自動生成デモ（v4: Blender版の見た目）");
            observe = GUILayout.Toggle(observe, " 観察モード（選んだベタに寄る）");
            if (observe) zoom = Slider("ズーム", zoom, 0f, 1f, "0.00");
            camYaw = Slider("カメラ角度", camYaw, -50f, 50f, "0");
            GUILayout.Label("背景");
            bgMode = GUILayout.SelectionGrid(bgMode, BgNames, 3);

            GUILayout.Space(8);
            GUILayout.Label("ベタ一覧");
            for (int i = 0; i < fishes.Count; i++)
            {
                bool on = GUILayout.Toggle(sel == i, fishes[i].fish.Params.name, "Button");
                if (on) sel = i;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("おまかせ追加"))
            {
                BettaParams p = new BettaParams();
                p.RandomAll();
                p.name = "ベタ" + (fishes.Count + 1);
                Spawn(p);
            }
            if (GUILayout.Button("削除") && fishes.Count > 0 && sel >= 0 && sel < fishes.Count)
            {
                Destroy(fishes[sel].gameObject);
                fishes.RemoveAt(sel);
                sel = Mathf.Clamp(sel, 0, fishes.Count - 1);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(28))) presetIdx = (presetIdx + BettaParams.PresetNames.Length - 1) % BettaParams.PresetNames.Length;
            GUILayout.Label(BettaParams.PresetNames[presetIdx], GUILayout.Width(190));
            if (GUILayout.Button(">", GUILayout.Width(28))) presetIdx = (presetIdx + 1) % BettaParams.PresetNames.Length;
            if (GUILayout.Button("追加")) Spawn(BettaParams.Preset(presetIdx));
            GUILayout.EndHorizontal();

            if (sel >= 0 && sel < fishes.Count)
            {
                BettaParams p = fishes[sel].fish.Params;
                GUILayout.Space(8);
                paramsGui.Draw(p);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
