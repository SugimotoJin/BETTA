using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Betta3D
{
    /// <summary>
    /// Generates a betta (body, fins, eyes) from BettaParams and animates it every frame.
    /// Same proportions and fin motion as Blender/betta_gen.py.
    /// Swimming motion (position/rotation, Phase/Swim/Pect/Bend) is driven by BettaSwimmer.
    /// Usage: add BettaFish + BettaSwimmer to an object (or GameObject → Betta → 泳ぐベタ), pick the look
    /// in the Inspector or assign a BettaPreset. Changes made while playing show up immediately.
    /// </summary>
    [DisallowMultipleComponent]
    public class BettaFish : MonoBehaviour
    {
        [Tooltip("ここにプリセットを入れると、その見た目で生まれます（下の設定は上書きされます）")]
        public BettaPreset preset;
        public BettaParams Params = new BettaParams();

        [Header("シェーダー（通常は自動で入ります）")]
        public Shader bodyShader;
        public Shader finShader;
        public Shader eyeShader;

        [System.NonSerialized] public float Phase, Swim, Pect, Bend, Flare;
        /// <summary>0 = fins fully spread, 1 = folded back while swimming fast (can overshoot below 0 when the fish stops).</summary>
        [System.NonSerialized] public float Fold;

        public static Shader BodyShader, FinShader, EyeShader;
        /// <summary>Mesh columns per fin ray (detail of pleats). 5 = close-up quality, 3 = light.</summary>
        public static int ColsPerRay = 5;

        const int NR = 72;   // rings along the body
        const int NS = 36;   // segments around the body

        Mesh bodyMesh;
        Vector3[] bodyVerts;
        float[] ringS;
        readonly List<Vector3> normalList = new List<Vector3>();

        FinSheet caudal, dorsal, anal, pectL, pectR, ventL, ventR;
        MeshRenderer rCaudal, rDorsal, rAnal, rVentL, rVentR, rPectL, rPectR;
        Material bodyMat, finMat, eyeMat;
        MaterialPropertyBlock mpb;
        Transform eyeL, eyeR;
        bool built;

        static readonly int ID_Body = Shader.PropertyToID("_BodyColor");
        static readonly int ID_Fin = Shader.PropertyToID("_FinColor");
        static readonly int ID_Edge = Shader.PropertyToID("_EdgeColor");
        static readonly int ID_PatCol = Shader.PropertyToID("_PatternColor");
        static readonly int ID_SheenCol = Shader.PropertyToID("_SheenColor");
        static readonly int ID_Sheen = Shader.PropertyToID("_Sheen");
        static readonly int ID_Clear = Shader.PropertyToID("_Clear");
        static readonly int ID_Pattern = Shader.PropertyToID("_Pattern");
        static readonly int ID_Seed = Shader.PropertyToID("_Seed");
        static readonly int ID_Rays = Shader.PropertyToID("_RayCount");
        static readonly int ID_Outer = Shader.PropertyToID("_Outer");
        static readonly int ID_Iris = Shader.PropertyToID("_IrisColor");
        static readonly int ID_Skin = Shader.PropertyToID("_SkinColor");

        void Reset()
        {
            // filled in when the component is added in the editor, so builds include the shaders
            bodyShader = Shader.Find("Betta/Body");
            finShader = Shader.Find("Betta/Fin");
            eyeShader = Shader.Find("Betta/Eye");
        }

        void Awake()
        {
            if (preset != null) Params = preset.Create();
            BettaLook.EnsureConfigured();
            Build();
        }

        /// <summary>Make a swimming betta from code. tank may be null (then it keeps its own bounds).</summary>
        public static BettaSwimmer Create(BettaParams p, Vector3 position, Transform parent = null, BettaTank tank = null)
        {
            GameObject go = new GameObject(string.IsNullOrEmpty(p.name) ? "Betta" : p.name);
            go.SetActive(false);   // set everything up before Awake runs
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = position;
            BettaFish f = go.AddComponent<BettaFish>();
            f.Params = p;
            BettaSwimmer s = go.AddComponent<BettaSwimmer>();
            s.fish = f;
            s.tank = tank;
            if (tank != null) s.bounds = tank.WorldRect;
            go.SetActive(true);
            return s;
        }

        static Shader Find(Shader s, string name)
        {
            if (s != null) return s;
            Shader f = Shader.Find(name);
            if (f == null) Debug.LogError("Betta: shader not found: " + name);
            return f;
        }

        void Build()
        {
            if (built) return;
            built = true;

            bodyMat = new Material(Find(bodyShader != null ? bodyShader : BodyShader, "Betta/Body"));
            finMat = new Material(Find(finShader != null ? finShader : FinShader, "Betta/Fin"));
            eyeMat = new Material(Find(eyeShader != null ? eyeShader : EyeShader, "Betta/Eye"));
            mpb = new MaterialPropertyBlock();

            // ---- body mesh topology
            ringS = new float[NR];
            for (int i = 0; i < NR; i++) ringS[i] = 1.04f * Mathf.Pow(i / (NR - 1f), 1.2f);
            int S = NS + 1;
            bodyVerts = new Vector3[NR * S + 1];
            Vector2[] uv = new Vector2[bodyVerts.Length];
            for (int i = 0; i < NR; i++)
                for (int k = 0; k < S; k++)
                    uv[i * S + k] = new Vector2(ringS[i], k / (float)NS);
            uv[NR * S] = new Vector2(1.04f, 0.5f);
            int[] tris = new int[(NR - 1) * NS * 6 + NS * 3];
            int t = 0;
            for (int i = 0; i < NR - 1; i++)
                for (int k = 0; k < NS; k++)
                {
                    int a = i * S + k, b = (i + 1) * S + k, c = a + 1, d = b + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = c; tris[t++] = d; tris[t++] = b;
                }
            int cap = NR * S, last = (NR - 1) * S;
            for (int k = 0; k < NS; k++) { tris[t++] = cap; tris[t++] = last + k + 1; tris[t++] = last + k; }
            bodyMesh = new Mesh();
            bodyMesh.name = "BettaBody";
            bodyMesh.MarkDynamic();
            bodyMesh.vertices = bodyVerts;
            bodyMesh.uv = uv;
            bodyMesh.triangles = tris;
            MakeChild("Body", bodyMesh, bodyMat);

            // ---- fins
            int cpr = Mathf.Clamp(ColsPerRay, 2, 8);
            caudal = new FinSheet(27, cpr, 12, 3);
            dorsal = new FinSheet(15, cpr, 9, 3);
            anal = new FinSheet(19, cpr, 9, 3);
            // ventrals: long narrow fins with a few rays, tapering to a point (no straight ribbon edges)
            ventL = new FinSheet(4, cpr, 10, 2);
            ventR = new FinSheet(4, cpr, 10, 2);
            // pectorals: small rounded fans on a short base line behind the gill cover
            pectL = new FinSheet(11, cpr, 6, 2);
            pectR = new FinSheet(11, cpr, 6, 2);
            // even when the physics lags in a quick turn, the pectorals may not pass into the body
            pectL.constrainLocal = lp => KeepOutOfBody(lp, -1f);
            pectR.constrainLocal = lp => KeepOutOfBody(lp, 1f);

            rCaudal = MakeChild("Caudal", caudal.mesh, finMat);
            rDorsal = MakeChild("Dorsal", dorsal.mesh, finMat);
            rAnal = MakeChild("Anal", anal.mesh, finMat);
            rVentL = MakeChild("VentralL", ventL.mesh, finMat);
            rVentR = MakeChild("VentralR", ventR.mesh, finMat);
            rPectL = MakeChild("PectoralL", pectL.mesh, finMat);
            rPectR = MakeChild("PectoralR", pectR.mesh, finMat);
            rCaudal.sortingOrder = 0; rDorsal.sortingOrder = 1; rAnal.sortingOrder = 1;
            rVentL.sortingOrder = 2; rVentR.sortingOrder = 2; rPectL.sortingOrder = 3; rPectR.sortingOrder = 3;

            eyeR = MakeEye(1f);
            eyeL = MakeEye(-1f);
        }

        MeshRenderer MakeChild(string childName, Mesh m, Material mat)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        Transform MakeEye(float side)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.name = side > 0f ? "EyeR" : "EyeL";
            go.transform.SetParent(transform, false);
            // the eye sits in the head; only a cap shows
            go.transform.localPosition = new Vector3(0.385f, 0.03f, side * BettaShape.BodyW(0.11f) * 0.62f);
            go.transform.localScale = Vector3.one * 0.08f;
            go.transform.localRotation = side > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = eyeMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        public void ResetPhysics()
        {
            if (!built) return;
            caudal.ResetSim(); dorsal.ResetSim(); anal.ResetSim();
            ventL.ResetSim(); ventR.ResetSim(); pectL.ResetSim(); pectR.ResetSim();
        }

        // lateral offset of the spine: tail beats while pushing + a C-shaped bend while turning
        // (head and tail swing to the same side)
        float SpineZ(float s)
        {
            float wave = 0.01f + 0.07f * Swim;     // tail beats only when the fish pushes
            float d = s - 0.45f;
            return wave * s * s * Mathf.Sin(Phase - s * 3f) + Bend * (d * d - 0.05f);
        }

        float SpineSlope(float s)   // dz/ds
        {
            float wave = 0.01f + 0.07f * Swim;
            return wave * (2f * s * Mathf.Sin(Phase - s * 3f) - 3f * s * s * Mathf.Cos(Phase - s * 3f)) + Bend * 2f * (s - 0.45f);
        }

        float FinYaw(float s) { return -Mathf.Atan(SpineSlope(s)); }

        /// <summary>Push a local-space point out to the body surface on the given side (+ a small gap).</summary>
        Vector3 KeepOutOfBody(Vector3 lp, float side)
        {
            float s = 0.5f - lp.x;
            if (s < 0.03f || s > 1f) return lp;
            float yo = lp.y - BettaShape.CenterY(s);
            float H = yo >= 0f ? BettaShape.TopH(s) : BettaShape.BotH(s);
            float st = yo / Mathf.Max(H, 1e-3f);
            if (st <= -1f || st >= 1f) return lp;
            float bw = BettaShape.BodyW(s) * Mathf.Pow(1f - st * st, 0.375f) + 0.006f;
            float zc = SpineZ(s);
            if (side * (lp.z - zc) < bw) lp.z = zc + side * bw;
            return lp;
        }

        void LateUpdate()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            BettaParams p = Params;
            float scale = Mathf.Max(0.05f, p.size);
            transform.localScale = Vector3.one * scale;

            UpdateMaterials();
            UpdateBody();
            // eyes ride on the bending head
            float ez = SpineZ(0.11f), ew = BettaShape.BodyW(0.11f) * 0.62f;
            eyeR.localPosition = new Vector3(0.385f, 0.03f, ez + ew);
            eyeL.localPosition = new Vector3(0.385f, 0.03f, ez - ew);

            TailSpec T = BettaShape.Spec(p.tail);
            float fl = p.finLength;
            float seed = p.seed % 997;
            SetupCaudal(T, fl, seed);
            SetupDorsal(T, fl, seed);
            SetupAnal(T, fl, seed);
            SetupVentral(ventL, T, fl, seed, -1f);
            SetupVentral(ventR, T, fl, seed, 1f);
            SetupPect(pectL, fl, seed, -1f);
            SetupPect(pectR, fl, seed, 1f);

            // soft fins: weak pull back to the rest shape and strong water drag, so long fins trail
            // far behind in turns and bursts and take about a second to settle again
            float loose = 1f / Mathf.Max(0.5f, fl);
            caudal.stiffRoot = 0.3f; caudal.stiffTip = 0.03f * loose; caudal.maxDev = 0.9f;
            dorsal.stiffRoot = 0.3f; dorsal.stiffTip = 0.04f * loose; dorsal.maxDev = 0.8f;
            anal.stiffRoot = 0.3f; anal.stiffTip = 0.04f * loose; anal.maxDev = 0.8f;
            ventL.stiffRoot = ventR.stiffRoot = 0.3f;
            ventL.stiffTip = ventR.stiffTip = 0.04f * loose;
            ventL.maxDev = ventR.maxDev = 0.7f;
            pectL.stiffRoot = pectR.stiffRoot = 0.45f;
            pectL.stiffTip = pectR.stiffTip = 0.12f / Mathf.Max(1f, p.pectSize);
            pectL.maxDev = pectR.maxDev = 0.5f;

            caudal.ComputeRest(); caudal.Simulate(transform, dt, scale); caudal.WriteMesh();
            dorsal.ComputeRest(); dorsal.Simulate(transform, dt, scale); dorsal.WriteMesh();
            anal.ComputeRest(); anal.Simulate(transform, dt, scale); anal.WriteMesh();
            ventL.ComputeRest(); ventL.Simulate(transform, dt, scale); ventL.WriteMesh();
            ventR.ComputeRest(); ventR.Simulate(transform, dt, scale); ventR.WriteMesh();
            pectL.ComputeRest(); pectL.Simulate(transform, dt, scale); pectL.WriteMesh();
            pectR.ComputeRest(); pectR.Simulate(transform, dt, scale); pectR.WriteMesh();
        }

        void Step(FinRig r, float dt, float scale)
        {
            r.Simulate(transform, dt, scale);
            r.WriteMesh(transform);
        }

        void UpdateMaterials()
        {
            BettaParams p = Params;
            float seed = p.seed % 997;
            bodyMat.SetColor(ID_Body, p.body);
            bodyMat.SetColor(ID_PatCol, p.patternColor);
            bodyMat.SetColor(ID_SheenCol, p.sheen);
            bodyMat.SetFloat(ID_Sheen, p.sheenAmount);
            bodyMat.SetFloat(ID_Pattern, (float)(int)p.pattern);
            bodyMat.SetFloat(ID_Seed, seed);

            finMat.SetColor(ID_Fin, p.fin);
            finMat.SetColor(ID_Edge, p.edge);
            finMat.SetColor(ID_PatCol, p.patternColor);
            finMat.SetColor(ID_SheenCol, p.sheen);
            finMat.SetFloat(ID_Sheen, p.sheenAmount);
            finMat.SetFloat(ID_Clear, p.clear);
            finMat.SetFloat(ID_Pattern, (float)(int)p.pattern);

            eyeMat.SetColor(ID_Skin, p.body * 0.6f);
            eyeMat.SetColor(ID_Iris, Color.Lerp(p.body * 0.55f, new Color(0.5f, 0.3f, 0.18f), 0.5f));

            SetBlock(rCaudal, 27f, seed + 1f, 1f);
            SetBlock(rDorsal, 15f, seed + 2f, 0f);
            SetBlock(rAnal, 19f, seed + 3f, 0f);
            SetBlock(rVentL, 4f, seed + 4f, 0f);
            SetBlock(rVentR, 4f, seed + 5f, 0f);
            // pectorals are mostly clear in real bettas
            // (dumbo pectorals are coloured like the other fins)
            float pc = Mathf.Lerp(Mathf.Max(p.clear, 0.6f), p.clear, Mathf.Clamp01((p.pectSize - 1f) / 0.8f));
            SetBlock(rPectL, 11f, seed + 6f, 0f, pc);
            SetBlock(rPectR, 11f, seed + 7f, 0f, pc);
        }

        void SetBlock(Renderer r, float rays, float seed, float outer, float clear = -1f)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(ID_Rays, rays);
            mpb.SetFloat(ID_Seed, seed);
            mpb.SetFloat(ID_Outer, outer);
            if (clear >= 0f) mpb.SetFloat(ID_Clear, clear);
            r.SetPropertyBlock(mpb);
        }

        void UpdateBody()
        {
            int S = NS + 1;
            for (int i = 0; i < NR; i++)
            {
                float s = ringS[i];
                float th0 = BettaShape.TopH(s), bh = BettaShape.BotH(s);
                float w = BettaShape.BodyW(s);
                float x = BettaShape.X(s);
                float cy = BettaShape.CenterY(s);
                float zc = SpineZ(s);
                float gill = Flare * BettaShape.Smoothstep(0.07f, 0.15f, s) * (1f - BettaShape.Smoothstep(0.2f, 0.3f, s));
                for (int k = 0; k < S; k++)
                {
                    float th = 2f * Mathf.PI * k / NS + Mathf.PI * 0.5f; // seam on the back
                    float st = Mathf.Sin(th), ct = Mathf.Cos(th);
                    float y = cy + (st >= 0f ? th0 : bh) * st;
                    float z = w * Mathf.Sign(ct) * Mathf.Pow(Mathf.Abs(ct), 0.75f);   // flatter sides
                    float xx = x;
                    if (s < 0.07f && st < 0f) xx += 0.012f * (1f - s / 0.07f) * (-st);  // lower jaw juts forward
                    if (gill > 0f)
                    {
                        // flaring: the gill covers swing out and the dark membrane ("beard") shows below
                        float low = Mathf.Clamp01(0.6f - st);
                        z *= 1f + 0.55f * gill * low;
                        y -= 0.028f * gill * Mathf.Clamp01(-st);
                    }
                    bodyVerts[i * S + k] = new Vector3(xx, y, zc + z);
                }
            }
            bodyVerts[NR * S] = new Vector3(BettaShape.X(1.04f), BettaShape.CenterY(1.04f), SpineZ(1.04f));
            bodyMesh.vertices = bodyVerts;
            bodyMesh.RecalculateNormals();
            // weld the normals along the UV seam (dorsal ridge) so no shading line shows
            bodyMesh.GetNormals(normalList);
            for (int i = 0; i < NR; i++)
            {
                int a = i * S, b = i * S + NS;
                Vector3 n = (normalList[a] + normalList[b]).normalized;
                normalList[a] = n; normalList[b] = n;
            }
            bodyMesh.SetNormals(normalList);
            bodyMesh.RecalculateBounds();
        }

        void SetupCaudal(TailSpec T, float fl, float seed)
        {
            FinSheet r = caudal;
            int C = r.cols;
            float ph = Phase, flare = Flare;
            float A0 = T.A * Mathf.Deg2Rad;
            float Aa = A0 * (1f + 0.05f * Mathf.Sin(ph * 0.5f) + 0.12f * flare) * (1f - 0.45f * Fold);   // folds while darting
            float droop = T.droop * Mathf.Deg2Rad * (1f - 0.5f * flare);
            float Rb = T.rb * fl;
            float cy1 = BettaShape.CenterY(1f);
            float sz = SpineZ(0.96f);
            float tailYaw = FinYaw(1f);
            bool crown = Params.tail == TailType.CrownTail;
            for (int c = 0; c < C; c++)
            {
                float q = c / (C - 1f);
                float th = -Aa + 2f * Aa * q;
                float ln = BettaShape.TailR(Params.tail, th / (1f + 0.12f * flare), A0, Rb, ph) * BettaShape.Ragged(c, seed);
                if (crown) ln *= BettaShape.CrownLen(q, r.rays, seed, 0.5f, 0.35f);
                r.web[c] = crown ? BettaShape.CrownWebFrac(q, r.rays, seed, 0.5f, 0.35f) : 1f;
                r.rootX[c] = -0.46f;
                r.rootY[c] = 0.055f - 0.11f * q + cy1;
                r.rootZ[c] = sz;
                r.ang[c] = th + droop;
                r.len[c] = ln + 0.03f;
                r.yaw[c] = tailYaw;
            }
            r.upSign = -1f;
            r.sway = 0.04f + 0.14f * Swim;
            r.swayPhase = ph;
            r.curl = 0f;
            r.ruffle = 0.09f * fl * (0.7f + 0.6f * Swim);
            r.pleat = 0.0045f * fl;
            r.frill = 0.035f * fl;
            r.flutter = (0.007f * fl * (0.35f + 0.65f * Swim)) * (crown ? 2f : 1f);
            r.flutterPhase = Time.time * 2.6f + Phase * 1.2f;
            r.seed = seed;
        }

        void SetupDorsal(TailSpec T, float fl, float seed)
        {
            FinSheet r = dorsal;
            int C = r.cols;
            float flare = Flare;
            float d0 = Params.tail == TailType.DoubleTail ? 0.34f : 0.46f;
            float Ld = 0.56f * fl * T.dorsal;
            float front = 0.62f * Params.finFront;
            bool crown = Params.tail == TailType.CrownTail;
            for (int c = 0; c < C; c++)
            {
                float q = c / (C - 1f);
                float s = d0 + (0.9f - d0) * q;
                // rays get longer toward the back so the rear of the dorsal reaches the tail's outer edge
                float ln = Ld * (front * (1f - q) + 1.18f * q) * (q > 0.88f ? 1f - 2f * (q - 0.88f) * (q - 0.88f) : 1f) + 0.02f;
                ln *= BettaShape.Ragged(c, seed + 11f);
                if (crown) ln *= BettaShape.CrownLen(q, r.rays, seed, 0.55f, 0.3f);
                r.web[c] = crown ? BettaShape.CrownWebFrac(q, r.rays, seed, 0.55f, 0.3f) : 1f;
                r.rootX[c] = BettaShape.X(s);
                r.rootY[c] = BettaShape.TopY(s) - 0.02f;
                r.rootZ[c] = SpineZ(s);
                r.yaw[c] = FinYaw(s);
                r.ang[c] = (80f - 22f * q + 12f * flare - 38f * Fold * (0.5f + 0.5f * q)) * Mathf.Deg2Rad;
                r.len[c] = ln;
            }
            r.upSign = 1f;
            r.sway = 0.03f + 0.08f * Swim;
            r.swayPhase = Phase * 0.8f + 1f;
            r.curl = 0.22f;
            r.ruffle = 0.06f * fl * (0.7f + 0.6f * Swim);
            r.pleat = 0.004f * fl;
            r.frill = 0.025f * fl;
            r.flutter = (0.005f * fl * (0.35f + 0.65f * Swim)) * (crown ? 2f : 1f);
            r.flutterPhase = Time.time * 2.3f + Phase + 1f;
            r.seed = seed + 3f;
        }

        void SetupAnal(TailSpec T, float fl, float seed)
        {
            FinSheet r = anal;
            int C = r.cols;
            float flare = Flare;
            float La = 0.55f * fl * T.anal;
            float front = 0.5f * Params.finFront;
            bool crown = Params.tail == TailType.CrownTail;
            for (int c = 0; c < C; c++)
            {
                float q = c / (C - 1f);
                float s = 0.33f + (0.96f - 0.33f) * q;
                // long rear rays: the anal fin's tip meets the lower edge of the tail
                float ln = La * (front * (1f - q) + 1.02f * q) * (q > 0.88f ? 1f - 2f * (q - 0.88f) * (q - 0.88f) : 1f) + 0.02f;
                ln *= BettaShape.Ragged(c, seed + 23f);
                if (crown) ln *= BettaShape.CrownLen(q, r.rays, seed, 0.55f, 0.3f);
                r.web[c] = crown ? BettaShape.CrownWebFrac(q, r.rays, seed, 0.55f, 0.3f) : 1f;
                r.rootX[c] = BettaShape.X(s);
                r.rootY[c] = BettaShape.BotY(s) + 0.02f;
                r.rootZ[c] = SpineZ(s);
                r.yaw[c] = FinYaw(s);
                r.ang[c] = (86f - 30f * q + 8f * flare - 34f * Fold * (0.5f + 0.5f * q)) * Mathf.Deg2Rad;
                r.len[c] = ln;
            }
            r.upSign = -1f;
            r.sway = 0.03f + 0.08f * Swim;
            r.swayPhase = Phase * 0.8f + 2f;
            r.curl = 0.18f;
            r.ruffle = 0.06f * fl * (0.7f + 0.6f * Swim);
            r.pleat = 0.004f * fl;
            r.frill = 0.025f * fl;
            r.flutter = (0.005f * fl * (0.35f + 0.65f * Swim)) * (crown ? 2f : 1f);
            r.flutterPhase = Time.time * 2.4f + Phase + 2f;
            r.seed = seed + 7f;
        }

        // Ventral fin: a short base under the belly, the leading ray longest, the trailing rays shorter,
        // so the fin narrows to a point. Leans out a little; trails and folds back while swimming.
        void SetupVentral(FinSheet r, TailSpec T, float fl, float seed, float side)
        {
            int C = r.cols;
            float Lv = 0.5f * fl * T.ventral;
            float s0 = 0.27f;
            float pz = side * BettaShape.BodyW(s0) * 0.5f + SpineZ(s0);
            float yaw = FinYaw(s0);
            for (int c = 0; c < C; c++)
            {
                float q = c / (C - 1f);                       // 0 = leading ray (front), 1 = trailing
                float s = s0 + 0.018f * q;
                r.rootX[c] = BettaShape.X(s);
                r.rootY[c] = BettaShape.BotY(s) + 0.014f;     // slightly inside the belly
                r.rootZ[c] = pz;
                r.ang[c] = (62f - 6f * q - 34f * Fold) * Mathf.Deg2Rad;
                r.len[c] = Lv * (1f - 0.5f * q * q) * BettaShape.Ragged(c, seed + 51f);
                r.yaw[c] = yaw;
            }
            r.upSign = -1f;
            r.sway = 0.07f + 0.05f * Swim;
            r.swayPhase = Phase * 0.9f + (side > 0f ? 0f : 0.9f);
            r.curl = 0.1f;                                     // tips curve back a little
            r.ruffle = 0.02f * fl;
            r.pleat = 0.001f;
            r.frill = 0.004f * fl;
            r.flutter = 0.002f * fl * (0.35f + 0.65f * Swim);
            r.flutterPhase = Time.time * 2.8f + side;
            r.lean = side * 0.12f;
            r.seed = seed + (side > 0f ? 61f : 67f);
        }

        // Pectoral fin: a rounded fan on a short, slightly slanted base. It sculls constantly - the
        // outward swing travels from the upper (leading) ray to the lower one - and tucks in while darting.
        void SetupPect(FinSheet r, float fl, float seed, float side)
        {
            int C = r.cols;
            float pect = Pect + (side > 0f ? 0f : 1.3f);
            float s0 = 0.26f;
            float cy = BettaShape.CenterY(s0);
            float ps = Mathf.Max(0.5f, Params.pectSize);
            float dumbo = Mathf.Clamp01((ps - 1f) / 1.5f);
            float Lp = 0.15f * (0.85f + 0.15f * fl) * ps;
            float spread = (1f + 0.15f * Mathf.Sin(pect)) * (1f - 0.35f * Fold) * (1f + 0.35f * dumbo);
            float O0 = 32f * (1f - 0.65f * Fold), Oa = 16f * (1f - 0.5f * Fold) * (1f + 0.4f * dumbo);
            float baseLen = 0.055f * (1f + 0.5f * dumbo);
            // the base sits inside the body, so the fin grows out of the skin and its straight root edge stays hidden
            float zb = side * BettaShape.BodyW(s0) * 0.9f + SpineZ(s0);
            float spineYaw = FinYaw(s0 + 0.08f);
            for (int c = 0; c < C; c++)
            {
                float q = c / (C - 1f);
                r.rootX[c] = 0.5f - s0 + 0.012f * (1f - q) + 0.008f * Mathf.Sin(Mathf.PI * q);   // slanted, slightly bowed base
                r.rootY[c] = cy + 0.012f - baseLen * q;
                r.rootZ[c] = zb;
                r.ang[c] = (12f - 58f * q * spread) * Mathf.Deg2Rad;
                r.len[c] = Lp * (0.7f + 0.3f * Mathf.Sin(Mathf.PI * (0.25f + 0.7f * q))) * BettaShape.Ragged(c, seed + 31f);
                float o = O0 + Oa * Mathf.Sin(pect - 2.4f * q);
                // never let the fin swing into the body: the body widens behind the base, so keep at least
                // the angle that clears it (plus a small margin) along the whole ray
                float need = 0f;
                for (int k = 1; k <= 4; k++)
                {
                    float dx = r.len[c] * k / 4f * Mathf.Cos(r.ang[c]);
                    float sx = s0 + dx;
                    // body half-width at the height this part of the ray is at (the body is an oval)
                    float yo = r.rootY[c] + r.len[c] * k / 4f * Mathf.Sin(r.ang[c]) - BettaShape.CenterY(sx);
                    float H = yo >= 0f ? BettaShape.TopH(sx) : BettaShape.BotH(sx);
                    float st = Mathf.Clamp(yo / Mathf.Max(H, 1e-3f), -1f, 1f);
                    float bw = BettaShape.BodyW(sx) * Mathf.Pow(1f - st * st, 0.375f);
                    // (measured from the skin at the base; a little gap that grows toward the tip)
                    float w = bw - BettaShape.BodyW(s0) + 0.003f + 0.008f * k / 4f;
                    need = Mathf.Max(need, Mathf.Atan2(w, dx) * Mathf.Rad2Deg);
                }
                o = Mathf.Max(o, need + 5f);
                // follow the spine too (the body bends in turns and while beating)
                r.yaw[c] = -side * o * Mathf.Deg2Rad + spineYaw;
            }
            r.upSign = 1f;
            r.sway = 0.05f;
            r.swayPhase = pect;
            r.curl = 0.12f;
            r.ruffle = 0.012f * ps;
            r.pleat = 0.0015f * ps;
            r.frill = 0.004f * ps * (1f + dumbo);
            r.flutter = 0.0015f * ps * (1f + dumbo);
            r.flutterPhase = Time.time * 3f + side;
            r.seed = seed + (side > 0f ? 41f : 43f);
        }

        void OnDestroy()
        {
            if (bodyMat != null) Destroy(bodyMat);
            if (finMat != null) Destroy(finMat);
            if (eyeMat != null) Destroy(eyeMat);
            if (bodyMesh != null) Destroy(bodyMesh);
            if (caudal != null)
            {
                Destroy(caudal.mesh); Destroy(dorsal.mesh); Destroy(anal.mesh);
                Destroy(ventL.mesh); Destroy(ventR.mesh); Destroy(pectL.mesh); Destroy(pectR.mesh);
            }
        }
    }
}
