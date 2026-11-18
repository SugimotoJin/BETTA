using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Betta3D
{
    /// <summary>
    /// Desktop app: bettas swim over the whole screen in a transparent, click-through window.
    /// Task tray menu: settings, feeding, add a fish, mouse reactions, quit. Settings are saved.
    /// In the editor a small toolbar stands in for the tray menu.
    /// </summary>
    public class BettaDesktop : MonoBehaviour
    {
        public Shader bodyShader, finShader, eyeShader, foodShader;

        const float FOV = 25f;

        Camera cam;
        BettaSave save;
        readonly List<BettaSwimmer> fishes = new List<BettaSwimmer>();
        readonly BettaParamsGUI paramsGui = new BettaParamsGUI();
        Rect tank;
        float halfH = 4f;

        bool settingsOpen, feeding;
        float feedIdle;
        int sel, presetIdx;
        Vector2 scroll;
        Rect panelGui;            // settings panel in GUI coordinates (top-left origin, unscaled pixels)
        bool dirty;
        float saveTimer;


        // monitors: the overlay covers the band all chosen monitors share (one continuous tank)
        BettaMonitor[] monitors;
        bool[] monitorPick;
        RectInt area;
        float refH = 1080f;       // pixel height the fish size is based on (tallest chosen monitor)
        readonly List<Vector3> floorSegs = new List<Vector3>();
        float diagTimer = 3f;

        void Awake()
        {
            BettaFish.BodyShader = bodyShader != null ? bodyShader : Shader.Find("Betta/Body");
            BettaFish.FinShader = finShader != null ? finShader : Shader.Find("Betta/Fin");
            BettaFish.EyeShader = eyeShader != null ? eyeShader : Shader.Find("Betta/Eye");
            if (foodShader == null) foodShader = Shader.Find("Betta/Food");
            if (foodShader != null) BettaFood.SharedMaterial = new Material(foodShader);

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = FOV;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // fully transparent: the desktop shows through
            cam.allowHDR = false;
            cam.allowMSAA = true;
            UniversalAdditionalCameraData cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = false;                     // post FX would lose the alpha channel
            UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null && urp.msaaSampleCount < 4) urp.msaaSampleCount = 4;

            GameObject lightGo = new GameObject("Sun");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(45f, -10f, 0f);
            RenderSettings.sun = sun;

            // the default aquarium look; tone mapping is done in the shaders (no post-processing here)
            BettaLook.ShaderTonemap = true;
            BettaLook.Exposure = Mathf.Pow(2f, 0.35f);
            BettaLook.Apply();
        }

        void Start()
        {
            save = BettaSave.Load();
            if (save == null)
            {
                save = new BettaSave();
                save.fish.Add(BettaParams.Preset(0));
                save.fish.Add(BettaParams.Preset(1));
                save.fish.Add(BettaParams.Preset(7));
                dirty = true;
            }
            Application.targetFrameRate = Mathf.Clamp(save.fps, 15, 144);
            BettaWindow.MouseReact = save.mouseReact;
            monitors = BettaWindow.GetMonitors();
            monitorPick = new bool[monitors.Length];
            for (int i = 0; i < monitors.Length; i++) monitorPick[i] = save.monitors.Contains(monitors[i].name);
            area = ComputeArea();
            UpdateTank();
            foreach (BettaParams p in save.fish) Spawn(p);
            StartCoroutine(BettaWindow.Setup(area));
        }

        /// <summary>
        /// The part of the desktop the fish live in: from the leftmost to the rightmost chosen monitor, and
        /// vertically only where all of them overlap, so the water surface is one level across all of them.
        /// </summary>
        RectInt ComputeArea()
        {
            int left = int.MaxValue, right = int.MinValue, top = int.MinValue, bottom = int.MaxValue, lowest = int.MinValue;
            int first = -1, primary = 0;
            refH = 0f;
            for (int i = 0; i < monitors.Length; i++)
            {
                if (monitors[i].primary) primary = i;
                if (!monitorPick[i]) continue;
                if (first < 0) first = i;
                RectInt r = monitors[i].rect;
                left = Mathf.Min(left, r.xMin); right = Mathf.Max(right, r.xMax);
                top = Mathf.Max(top, r.yMin); bottom = Mathf.Min(bottom, r.yMax);
                lowest = Mathf.Max(lowest, r.yMax);
                refH = Mathf.Max(refH, r.height);
            }
            if (first < 0 || bottom - top < 200)
            {
                // nothing chosen (or the chosen monitors don't share a height): the main monitor only
                int k = first < 0 ? primary : first;
                for (int i = 0; i < monitorPick.Length; i++) monitorPick[i] = i == k;
                refH = monitors[k].rect.height;
                return monitors[k].rect;
            }
            // one water surface (the lowest top edge); each monitor keeps its own bottom
            return new RectInt(left, top, right - left, lowest - top);
        }

        /// <summary>Floors per chosen monitor in world units (gaps between monitors have no water).</summary>
        void UpdateFloors(float hw)
        {
            floorSegs.Clear();
            if (BettaWindow.Active && area.width > 0 && area.height > 0)
            {
                for (int i = 0; i < monitors.Length; i++)
                {
                    if (!monitorPick[i]) continue;
                    RectInt r = monitors[i].rect;
                    float x0 = ((r.xMin - area.x) / (float)area.width - 0.5f) * 2f * hw;
                    float x1 = ((r.xMax - area.x) / (float)area.width - 0.5f) * 2f * hw;
                    float fy = (0.5f - (r.yMax - area.y) / (float)area.height) * 2f * halfH;
                    floorSegs.Add(new Vector3(x0, x1, fy));
                }
            }
            BettaAquarium.SetFloors(floorSegs);
        }

        void ApplyMonitors()
        {
            area = ComputeArea();
            save.monitors.Clear();
            for (int i = 0; i < monitors.Length; i++) if (monitorPick[i]) save.monitors.Add(monitors[i].name);
            dirty = true;
            StartCoroutine(BettaWindow.Place(area));
            diagTimer = 3f;
        }

        void UpdateTank()
        {
            // fish of size 1 = fishScale / 8 of the (tallest chosen) monitor's height, the same on every monitor
            float refPx = BettaWindow.Active ? refH : Screen.height;
            float ppu = refPx * Mathf.Max(0.2f, save.fishScale) / 8f;   // screen pixels per world unit
            halfH = Screen.height * 0.5f / ppu;
            float hw = halfH * cam.aspect;
            // the water surface sits a little below the top of the screen so floating food stays visible
            float top = halfH - 2f * halfH * Mathf.Clamp(save.surface, 0f, 0.4f);
            tank = new Rect(-hw, -halfH, hw * 2f, top + halfH);
            UpdateFloors(hw);
            float dist = halfH / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad);
            cam.transform.position = new Vector3(0f, 0f, -dist);
            cam.transform.rotation = Quaternion.identity;
        }

        BettaSwimmer Spawn(BettaParams p)
        {
            GameObject go = new GameObject(p.name);
            go.transform.position = new Vector3(BettaAquarium.RandomWaterX(tank.xMin * 0.8f, tank.xMax * 0.8f),
                                                Random.Range(tank.yMin * 0.6f, tank.yMax * 0.6f), fishes.Count * 0.15f);
            BettaFish f = go.AddComponent<BettaFish>();
            f.Params = p;
            BettaSwimmer s = go.AddComponent<BettaSwimmer>();
            s.fish = f;
            s.bounds = tank;
            fishes.Add(s);
            return s;
        }

        void AddRandom()
        {
            BettaParams p = new BettaParams();
            p.RandomAll();
            p.name = "ベタ" + (fishes.Count + 1);
            save.fish.Add(p);
            Spawn(p);
            sel = fishes.Count - 1;
            dirty = true;
        }

        void Remove(int i)
        {
            if (i < 0 || i >= fishes.Count) return;
            Destroy(fishes[i].gameObject);
            fishes.RemoveAt(i);
            save.fish.RemoveAt(i);
            sel = Mathf.Clamp(sel, 0, fishes.Count - 1);
            dirty = true;
        }

        void Command(int c)
        {
            switch (c)
            {
                case BettaWindow.CmdSettings: settingsOpen = !settingsOpen; if (!settingsOpen) dirty = true; break;
                case BettaWindow.CmdFeed: feeding = true; feedIdle = 0f; break;
                case BettaWindow.CmdAdd: AddRandom(); break;
                case BettaWindow.CmdMouse: save.mouseReact = !save.mouseReact; dirty = true; break;
                case BettaWindow.CmdQuit: Quit(); break;
            }
        }

        void Quit()
        {
            save.Write();
            BettaWindow.Shutdown();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            int cmd = BettaWindow.PendingCommand;
            if (cmd != 0) { BettaWindow.PendingCommand = 0; Command(cmd); }
            BettaWindow.SettingsOpen = settingsOpen;
            BettaWindow.MouseReact = save.mouseReact;

            UpdateTank();
            for (int i = 0; i < fishes.Count; i++) fishes[i].bounds = tank;

            // ---- mouse cursor in the tank
            Vector2 sp;
            bool onScreen = BettaWindow.GetCursor(out sp);
            Vector2 gui = new Vector2(sp.x, Screen.height - sp.y);
            bool overPanel = settingsOpen && panelGui.Contains(gui);
            Vector2 world = ScreenToTank(sp);
            BettaAquarium.SetCursor(world, save.mouseReact && onScreen && !overPanel && !feeding, Time.deltaTime);

            // ---- feeding: the window takes clicks; each click drops pellets onto the surface above it
            if (feeding)
            {
                feedIdle += Time.deltaTime;
                if (BettaInput.LeftDown() && !overPanel && onScreen)
                {
                    feedIdle = 0f;
                    BettaAquarium.Feed(world.x, tank, 2 + Random.Range(0, 3), 0.06f);
                }
                if (BettaInput.RightDown() || BettaInput.EscDown() || feedIdle > 10f) feeding = false;
            }

            // ---- click-through everywhere except the settings panel (and the whole screen while feeding)
            BettaWindow.SetClickThrough(!(feeding || overPanel));

            // ---- diagnostics next to the exe (window size, monitors) a few seconds after (re)placing
            if (diagTimer > 0f)
            {
                diagTimer -= Time.deltaTime;
                if (diagTimer <= 0f) WriteDiagnostics();
            }

            // ---- autosave a little after changes
            saveTimer += Time.deltaTime;
            if (dirty && saveTimer > 2f) { save.Write(); dirty = false; saveTimer = 0f; }
        }

        void WriteDiagnostics()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(System.DateTime.Now.ToString());
                sb.AppendLine("screen " + Screen.width + "x" + Screen.height + "  area " + area);
                sb.AppendLine(BettaWindow.Describe());
                for (int i = 0; i < monitors.Length; i++)
                    sb.AppendLine("monitor " + (i + 1) + " " + monitors[i].name + " " + monitors[i].rect + (monitors[i].primary ? " primary" : "") + (monitorPick[i] ? " [chosen]" : ""));
                sb.AppendLine("tank " + tank + "  floors " + floorSegs.Count);
                foreach (Vector3 f in floorSegs) sb.AppendLine("  floor x " + f.x.ToString("0.00") + ".." + f.y.ToString("0.00") + " y " + f.z.ToString("0.00"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "betta_debug.txt"), sb.ToString());
            }
            catch { }
        }

        Vector2 ScreenToTank(Vector2 sp)
        {
            Ray r = cam.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            if (Mathf.Abs(r.direction.z) < 1e-5f) return Vector2.zero;
            float t = -r.origin.z / r.direction.z;
            Vector3 p = r.origin + r.direction * t;
            return new Vector2(p.x, p.y);
        }

        void OnApplicationQuit()
        {
            if (save != null) save.Write();
            BettaWindow.Shutdown();
        }

        // ------------------------------------------------------------------ UI
        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 1000f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float W = Screen.width / scale, H = Screen.height / scale;

            if (!BettaWindow.Active)
            {
                // editor: stand-in for the task tray menu
                GUILayout.BeginArea(new Rect(10f, 10f, 640f, 30f));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(settingsOpen ? "設定を閉じる" : "設定を開く")) Command(BettaWindow.CmdSettings);
                if (GUILayout.Button("エサをあげる")) Command(BettaWindow.CmdFeed);
                if (GUILayout.Button("ベタを増やす")) Command(BettaWindow.CmdAdd);
                if (GUILayout.Button(save.mouseReact ? "マウス反応: ON" : "マウス反応: OFF")) Command(BettaWindow.CmdMouse);
                if (GUILayout.Button("終了")) Command(BettaWindow.CmdQuit);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            if (feeding)
            {
                Rect r = new Rect(W * 0.5f - 230f, 14f, 460f, 34f);
                GUI.Box(r, "");
                GUI.Label(new Rect(r.x + 12f, r.y + 7f, r.width - 24f, 22f),
                          "クリックした所の水面にエサを落とします（右クリック / Esc で終わる）");
            }

            if (!settingsOpen) { panelGui = new Rect(0, 0, 0, 0); return; }

            float pw = 380f;
            Rect area = new Rect(W - pw - 16f, 50f, pw, H - 110f);
            panelGui = new Rect(area.x * scale, area.y * scale, area.width * scale, area.height * scale);
            GUILayout.BeginArea(area, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("ベタの設定");
            float fs = BettaParamsGUI.Slider("画面での大きさ", save.fishScale, 0.25f, 1.5f, "0.00");
            if (fs != save.fishScale) { save.fishScale = fs; dirty = true; }
            float sf = BettaParamsGUI.Slider("水面の高さ", save.surface, 0f, 0.3f, "0.00");
            if (sf != save.surface) { save.surface = sf; dirty = true; }
            bool mr = GUILayout.Toggle(save.mouseReact, " マウスに反応する（寄ってくる・ついてくる）");
            if (mr != save.mouseReact) { save.mouseReact = mr; dirty = true; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("なめらかさ", GUILayout.Width(80));
            int fpsIdx = save.fps >= 60 ? 1 : 0;
            int nf = GUILayout.SelectionGrid(fpsIdx, new[] { "30fps（軽い）", "60fps" }, 2);
            if (nf != fpsIdx) { save.fps = nf == 1 ? 60 : 30; Application.targetFrameRate = save.fps; dirty = true; }
            GUILayout.EndHorizontal();

            if (monitors != null && monitors.Length > 1)
            {
                GUILayout.Space(6);
                GUILayout.Label("泳がせるモニター（水面は全部で同じ高さ）");
                for (int i = 0; i < monitors.Length; i++)
                {
                    BettaMonitor m = monitors[i];
                    monitorPick[i] = GUILayout.Toggle(monitorPick[i],
                        " " + (i + 1) + ": " + m.rect.width + "×" + m.rect.height + (m.primary ? "（メイン）" : ""));
                }
                if (GUILayout.Button("このモニターで泳がせる")) ApplyMonitors();
            }

            GUILayout.Space(8);
            GUILayout.Label("ベタ一覧");
            for (int i = 0; i < fishes.Count; i++)
            {
                bool on = GUILayout.Toggle(sel == i, fishes[i].fish.Params.name, "Button");
                if (on) sel = i;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("おまかせ追加")) AddRandom();
            if (GUILayout.Button("削除") && fishes.Count > 0) Remove(sel);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(28))) presetIdx = (presetIdx + BettaParams.PresetNames.Length - 1) % BettaParams.PresetNames.Length;
            GUILayout.Label(BettaParams.PresetNames[presetIdx], GUILayout.Width(200));
            if (GUILayout.Button(">", GUILayout.Width(28))) presetIdx = (presetIdx + 1) % BettaParams.PresetNames.Length;
            if (GUILayout.Button("追加"))
            {
                BettaParams p = BettaParams.Preset(presetIdx);
                save.fish.Add(p);
                Spawn(p);
                sel = fishes.Count - 1;
                dirty = true;
            }
            GUILayout.EndHorizontal();

            if (sel >= 0 && sel < fishes.Count)
            {
                GUILayout.Space(8);
                if (paramsGui.Draw(fishes[sel].fish.Params)) dirty = true;
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("閉じる")) { settingsOpen = false; dirty = true; }
            if (GUILayout.Button("終了")) Quit();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
