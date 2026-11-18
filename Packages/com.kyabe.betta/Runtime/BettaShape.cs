using UnityEngine;

namespace Betta3D
{
    public struct TailSpec
    {
        public float A, droop, rb, dorsal, anal, ventral;
        public TailSpec(float a, float d, float r, float dor, float an, float ve)
        {
            A = a; droop = d; rb = r; dorsal = dor; anal = an; ventral = ve;
        }
    }

    /// <summary>
    /// Body / fin proportions (same math as Blender/betta_gen.py).
    /// Fish-local space: +X = head, +Y = up, Z = lateral. Units are body lengths
    /// (snout at x=+0.5, caudal peduncle at x=-0.5).
    /// </summary>
    public static class BettaShape
    {
        public static readonly TailSpec[] Specs = {
            new TailSpec(78f, 0f, 0.62f, 1.15f, 1.12f, 1.0f),  // HalfMoon
            new TailSpec(90f, 0f, 0.66f, 1.22f, 1.17f, 1.0f),  // RoseTail
            new TailSpec(46f, 34f, 0.90f, 0.95f, 1.0f, 1.05f), // VeilTail
            new TailSpec(80f, 0f, 0.66f, 1.0f, 1.0f, 1.0f),    // CrownTail
            new TailSpec(80f, 0f, 0.60f, 1.18f, 0.94f, 0.93f), // DoubleTail
            new TailSpec(58f, 0f, 0.34f, 0.5f, 0.55f, 0.7f),   // Plakat
            new TailSpec(50f, 4f, 0.55f, 0.75f, 0.8f, 0.9f),   // SpadeTail
        };

        public static TailSpec Spec(TailType t) { return Specs[(int)t]; }

        public static float Smoothstep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Per-frame velocity keep at critical damping for a node pulled back by fraction st per frame:
        /// above it the node rings like rubber, below it it settles smoothly like a fin in water.</summary>
        public static float CriticalKeep(float st)
        {
            float r = Mathf.Sqrt(Mathf.Clamp01(st));
            return (1f - r) / (1f + r);
        }

        public static float Hash(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

        // ---- body profile, s: 0 = snout, 1 = caudal peduncle (slightly beyond 1 = tucked under the tail)
        public static float TopH(float s)
        {
            if (s < 0.34f) return 0.152f * Mathf.Pow(Mathf.Sin(Mathf.PI / 2f * Mathf.Max(0f, s) / 0.34f), 0.9f);
            if (s <= 1f) { float u = (s - 0.34f) / 0.66f; return 0.066f + 0.086f * (0.5f + 0.5f * Mathf.Cos(Mathf.PI * u)); }
            return 0.066f * Mathf.Max(0.3f, 1f - (s - 1f) / 0.04f * 0.7f);
        }

        public static float BotH(float s)
        {
            if (s < 0.3f) return 0.14f * Mathf.Pow(Mathf.Sin(Mathf.PI / 2f * Mathf.Max(0f, s) / 0.3f), 0.7f);
            if (s < 0.5f) return 0.14f + 0.008f * Mathf.Sin(Mathf.PI * (s - 0.3f) / 0.2f);
            if (s <= 1f) { float u = (s - 0.5f) / 0.5f; return 0.066f + 0.074f * (0.5f + 0.5f * Mathf.Cos(Mathf.PI * u)); }
            return 0.066f * Mathf.Max(0.3f, 1f - (s - 1f) / 0.04f * 0.7f);
        }

        // center line: the snout is raised a little (upturned mouth)
        public static float CenterY(float s) { float k = Mathf.Max(0f, 1f - s / 0.12f); return -0.005f + 0.025f * k * k; }

        // half width: compressed from the sides, thin caudal peduncle
        public static float BodyW(float s)
        {
            float w = 0.44f * (TopH(s) + BotH(s)) * 0.5f * (1f - 0.25f * Mathf.Min(s, 1f));
            return w * (1f - 0.6f * Smoothstep(0.82f, 1.04f, s));
        }

        public static float X(float s) { return 0.5f - s; }
        public static float TopY(float s) { return CenterY(s) + TopH(s); }
        public static float BotY(float s) { return CenterY(s) - BotH(s); }

        public static float TailR(TailType type, float th, float A, float Rb, float phase)
        {
            float q = th / A;
            switch (type)
            {
                case TailType.HalfMoon: return Rb * (1f - 0.04f * q * q);
                case TailType.RoseTail: return Rb * (1f - 0.04f * q * q) * (1f + 0.018f * Mathf.Sin(th * 22f + phase * 0.4f) + 0.01f * Mathf.Sin(th * 53f));
                case TailType.VeilTail: { float z = (q - 0.3f) / 0.6f; return Rb * (0.55f + 0.5f * Mathf.Exp(-z * z)); }
                case TailType.CrownTail: return Rb * (1f - 0.05f * q * q);
                case TailType.DoubleTail: { float z = q / 0.17f; return Rb * (1f - 0.45f * Mathf.Exp(-z * z)) * (1f - 0.12f * q * q); }
                case TailType.Plakat: return Rb * (1f - 0.18f * q * q);
                case TailType.SpadeTail: return Rb * (1f - 0.62f * Mathf.Abs(q));
            }
            return Rb;
        }

        // slightly uneven ray lengths along the edge
        public static float Ragged(int c, float seed) { return 1f + 0.012f * Mathf.Sin(c * 0.9f + seed) + 0.01f * (Hash(c + seed) - 0.5f); }

        // crown tail: webbing reaches only part way, rays (in pairs) stick out as spikes
        public static float CrownLen(float q, int rays, float seed, float webBase, float webAmp)
        {
            // one spike per ray (so every ray waves on its own physics chain), lengths vary spike to spike
            float cp = q * (rays - 1f);
            float c = Mathf.Abs(Mathf.Cos(Mathf.PI * cp));
            float web = webBase + webAmp * c * c * c;
            float spike = c * c * c * c;
            float id = Mathf.Round(cp);
            float vary = 1f - 0.22f * Hash(id + seed) - 0.08f * (id % 2f);
            return web + (1f - web) * spike * vary;
        }

        /// <summary>Fraction of a crown-tail column that is webbing (the rest is a bare ray spike).</summary>
        public static float CrownWebFrac(float q, int rays, float seed, float webBase, float webAmp)
        {
            float cp = q * (rays - 1f);
            float c = Mathf.Abs(Mathf.Cos(Mathf.PI * cp));
            return (webBase + webAmp * c * c * c) / CrownLen(q, rays, seed, webBase, webAmp);
        }
    }

    /// <summary>
    /// A large fin (caudal / dorsal / anal) as a fine mesh: pleated along each ray, a ripple travelling
    /// from the root to the edge, curled edges. Physics runs on a coarse grid (one chain per ray) and the
    /// fine mesh follows the coarse grid's displacement, so the fin trails and flows when the fish moves.
    /// </summary>
    public class FinSheet
    {
        public readonly int rays, colsPerRay, cols, coarseSteps, rowsPerStep, rows;
        // per fine column, filled every frame by BettaFish
        public readonly float[] rootX, rootY, rootZ, ang, len, yaw;   // yaw: follows the bent spine at the root
        public readonly float[] web;   // crown tail: fraction of the column that is membrane (1 = all of it)
        readonly Vector2[] uv2;
        public float upSign = 1f, sway, swayPhase, curl, ruffle, pleat, frill, seed;
        public float lean;   // sideways tilt of the whole fin (out-of-plane offset per unit length)
        /// <summary>Optional collision in local space (e.g. keep the pectorals out of the body).</summary>
        public System.Func<Vector3, Vector3> constrainLocal;
        public float stiffRoot = 0.55f, stiffTip = 0.07f, maxDev = 0.5f;
        // In water a fin is overdamped: drag dominates its tiny mass, so it trails and settles without
        // bouncing back. overdamp < 1 keeps every node just past critical damping (1 = critical).
        public float overdamp = 0.9f;
        public float bend = 0.05f;   // rays are thin: only a little bending stiffness
        // membrane between rays lags more than the rays (billows), and the outer edge flutters finely
        public float billow = 0.4f, flutter, flutterPhase;
        public Mesh mesh;

        readonly Vector3[] rest;                    // fine rest shape (local), cols x (rows+1)
        readonly Vector3[] verts, normals;
        readonly Vector3[] cPos, cPrev, cTgt;      // coarse nodes in world space, rays x (coarseSteps+1)
        readonly Vector3[] cDisp;                  // coarse displacement (local)
        bool init;

        public FinSheet(int rays, int colsPerRay, int coarseSteps, int rowsPerStep)
        {
            this.rays = rays; this.colsPerRay = colsPerRay;
            this.coarseSteps = coarseSteps; this.rowsPerStep = rowsPerStep;
            cols = (rays - 1) * colsPerRay + 1;
            rows = coarseSteps * rowsPerStep;
            rootX = new float[cols]; rootY = new float[cols]; rootZ = new float[cols]; ang = new float[cols]; len = new float[cols];
            yaw = new float[cols];
            web = new float[cols];
            for (int c = 0; c < cols; c++) web[c] = 1f;
            int n = cols * (rows + 1);
            uv2 = new Vector2[n];
            rest = new Vector3[n]; verts = new Vector3[n]; normals = new Vector3[n];
            int cn = rays * (coarseSteps + 1);
            cPos = new Vector3[cn]; cPrev = new Vector3[cn]; cTgt = new Vector3[cn]; cDisp = new Vector3[cn];
            BuildMesh();
        }

        void BuildMesh()
        {
            mesh = new Mesh();
            mesh.name = "FinSheet";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.MarkDynamic();
            int R = rows + 1;
            Vector2[] uv = new Vector2[cols * R];
            for (int c = 0; c < cols; c++)
                for (int j = 0; j < R; j++)
                    uv[c * R + j] = new Vector2(c / (cols - 1f), j / (float)rows);
            int[] tris = new int[(cols - 1) * rows * 6];
            int t = 0;
            for (int c = 0; c < cols - 1; c++)
                for (int j = 0; j < rows; j++)
                {
                    int a = c * R + j, b = (c + 1) * R + j;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.uv2 = uv;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        }

        public void ResetSim() { init = false; }

        /// <summary>Fine rest shape from the per-column roots/angles/lengths.</summary>
        public void ComputeRest()
        {
            int R = rows + 1;
            float twoPi = Mathf.PI * 2f;
            for (int c = 0; c < cols; c++)
            {
                float q = c / (cols - 1f);
                float ri = q * (rays - 1);
                float a0 = ang[c] + 0.035f * Mathf.Sin(ri * 0.7f + seed) + 0.02f * Mathf.Sin(ri * 1.9f + seed * 2.1f);
                float px = rootX[c], py = rootY[c], pz = rootZ[c];
                rest[c * R] = new Vector3(px, py, pz);
                float seg = len[c] / rows;
                float pleatK = pleat * (0.6f + 0.4f * Mathf.Sin(ri * 0.37f + seed)) * Mathf.Cos(twoPi * ri);
                float drape = 0.5f * ruffle * Mathf.Sin(ri * 0.55f + seed);
                float frillK = frill * (Mathf.Sin(ri * 0.9f + seed) + 0.5f * Mathf.Sin(ri * 2.3f + seed * 1.7f));
                float cy = Mathf.Cos(yaw[c]), sy = Mathf.Sin(yaw[c]);
                float rx0 = rootX[c];
                for (int j = 1; j < R; j++)
                {
                    float f = j / (float)rows;
                    // the fin is driven at the root; the wave travels outward and fades toward the tip
                    float env = f * (1.6f - 1.2f * f);
                    // in-plane sway
                    float sw = sway * env * 1.4f * Mathf.Sin(swayPhase - f * 4f + ri * 0.12f);
                    float a = a0 + sw - curl * Mathf.Pow(f, 1.5f);
                    px += -Mathf.Cos(a) * seg;
                    py += upSign * Mathf.Sin(a) * seg;
                    // out-of-plane ripple travelling from the root to the edge (+ still drape, pleats, curled edge)
                    float lat = pz + ruffle * env * 1.6f * Mathf.Sin(7.5f * f - swayPhase + 0.15f * ri)
                                   + drape * f * f
                                   + pleatK * (0.25f + 0.75f * f)
                                   + frillK * f * f * f * f
                                   + lean * f * len[c];
                    // turn the fin with the spine (C-bend in turns, tail beats)
                    float ox = px - rx0, oz = lat - pz;
                    rest[c * R + j] = new Vector3(rx0 + ox * cy - oz * sy, py, pz + ox * sy + oz * cy);
                }
            }
        }

        public void Simulate(Transform t, float dt, float scale)
        {
            int R = rows + 1, S = coarseSteps + 1;
            for (int i = 0; i < rays; i++)
            {
                int c = i * colsPerRay;
                for (int k = 0; k < S; k++) cTgt[i * S + k] = t.TransformPoint(rest[c * R + k * rowsPerStep]);
            }
            if (!init)
            {
                for (int n = 0; n < cTgt.Length; n++) { cPos[n] = cTgt[n]; cPrev[n] = cTgt[n]; }
                init = true;
            }
            else if (dt > 0f)
            {
                dt = Mathf.Min(dt, 0.05f);
                float frames = dt * 60f;
                float bendK = 1f - Mathf.Pow(1f - bend, frames);
                for (int i = 0; i < rays; i++)
                {
                    int b = i * S;
                    cPos[b] = cTgt[b]; cPrev[b] = cTgt[b];
                    for (int k = 1; k < S; k++)
                    {
                        int n = b + k;
                        float f = k / (float)coarseSteps;
                        float st = Mathf.Lerp(stiffRoot, stiffTip, Mathf.Pow(f, 0.7f));
                        float dampF = Mathf.Pow(BettaShape.CriticalKeep(st) * overdamp, frames);
                        Vector3 vel = (cPos[n] - cPrev[n]) * dampF;
                        cPrev[n] = cPos[n];
                        Vector3 p = cPos[n] + vel;
                        float kk = 1f - Mathf.Pow(Mathf.Clamp01(1f - st), frames);
                        p += (cTgt[n] - p) * kk;
                        float L = (cTgt[n] - cTgt[n - 1]).magnitude;
                        // bending stiffness: lean toward the straight continuation of the ray (in rest-shape terms)
                        if (k >= 2)
                        {
                            Vector3 restBend = (cTgt[n] - cTgt[n - 1]) - (cTgt[n - 1] - cTgt[n - 2]);
                            Vector3 ext = cPos[n - 1] + (cPos[n - 1] - cPos[n - 2]) + restBend;
                            p = Vector3.Lerp(p, ext, bendK);
                        }
                        Vector3 d = p - cPos[n - 1];
                        float dm = d.magnitude;
                        if (dm > 1e-6f) p = cPos[n - 1] + d * (L / dm);
                        Vector3 dev = p - cTgt[n];
                        float maxD = maxDev * scale * f;
                        if (dev.sqrMagnitude > maxD * maxD) p = cTgt[n] + dev.normalized * maxD;
                        if (constrainLocal != null)
                        {
                            Vector3 lp = t.InverseTransformPoint(p);
                            Vector3 cp = constrainLocal(lp);
                            if (cp != lp) p = t.TransformPoint(cp);
                        }
                        cPos[n] = p;
                    }
                }
            }
            // coarse displacement in local space
            for (int i = 0; i < rays; i++)
            {
                int c = i * colsPerRay;
                for (int k = 0; k < S; k++)
                {
                    int n = i * S + k;
                    cDisp[n] = t.InverseTransformPoint(cPos[n]) - rest[c * R + k * rowsPerStep];
                }
            }
        }

        public void WriteMesh()
        {
            int R = rows + 1, S = coarseSteps + 1;
            for (int c = 0; c < cols; c++)
            {
                int i0 = Mathf.Min(c / colsPerRay, rays - 2);
                float fu = (c - i0 * colsPerRay) / (float)colsPerRay;
                for (int j = 0; j < R; j++)
                {
                    int k0 = Mathf.Min(j / rowsPerStep, coarseSteps - 1);
                    float fv = (j - k0 * rowsPerStep) / (float)rowsPerStep;
                    Vector3 d00 = cDisp[i0 * S + k0], d01 = cDisp[i0 * S + k0 + 1];
                    Vector3 d10 = cDisp[(i0 + 1) * S + k0], d11 = cDisp[(i0 + 1) * S + k0 + 1];
                    Vector3 d = Vector3.Lerp(Vector3.Lerp(d00, d01, fv), Vector3.Lerp(d10, d11, fv), fu);
                    float f = j / (float)rows;
                    float mem = Mathf.Sin(Mathf.PI * fu);          // 0 on a ray, 1 midway between rays
                    d *= 1f + billow * mem * f;                     // soft membrane trails behind its rays
                    Vector3 v = rest[c * R + j] + d;
                    // fine flutter at the edge (cloth-like), not a whole-sheet wobble
                    v.z += flutter * f * f * f * (0.6f + 0.4f * mem)
                           * Mathf.Sin(22f * f - flutterPhase + 1.3f * c + seed);
                    verts[c * R + j] = v;
                }
            }
            // grid normals (the shader flips them for the back side)
            for (int c = 0; c < cols; c++)
            {
                int cl = Mathf.Max(c - 1, 0), cr = Mathf.Min(c + 1, cols - 1);
                for (int j = 0; j < R; j++)
                {
                    int jd = Mathf.Max(j - 1, 0), ju = Mathf.Min(j + 1, rows);
                    Vector3 du = verts[cr * R + j] - verts[cl * R + j];
                    Vector3 dv = verts[c * R + ju] - verts[c * R + jd];
                    Vector3 nrm = Vector3.Cross(dv, du);
                    float m = nrm.magnitude;
                    normals[c * R + j] = m > 1e-12f ? nrm / m : Vector3.forward;
                }
            }
            // pattern coordinates: x = colour ramp (1 at the membrane edge, >1 on crown spikes),
            // y = physical distance from the root, so patterns stay continuous across long and short columns
            float maxLen = 1e-4f;
            for (int c = 0; c < cols; c++) maxLen = Mathf.Max(maxLen, len[c]);
            for (int c = 0; c < cols; c++)
            {
                float w = Mathf.Max(0.05f, web[c]), lr = len[c] / maxLen;
                for (int j = 0; j < R; j++)
                {
                    float f = j / (float)rows;
                    uv2[c * R + j] = new Vector2(f / w, f * lr);
                }
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv2 = uv2;
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// Small fins: ventral (ribbon) and pectoral (kinematic fan). Rest shape is filled by BettaFish;
    /// points follow it with spring physics.
    /// </summary>
    public class FinRig
    {
        public readonly int rays, steps;
        public readonly bool ribbon;
        public readonly Vector3[] rest, pos, prev, tgt;
        public float stiffRoot = 0.5f, stiffTip = 0.06f, maxDev = 0.5f;
        public float overdamp = 0.9f, bend = 0.05f;
        public float ribbonWidth = 0.022f;
        public Mesh mesh;
        Vector3[] verts;
        bool init;

        public FinRig(int rays, int steps, bool ribbon)
        {
            this.rays = rays; this.steps = steps; this.ribbon = ribbon;
            int n = rays * (steps + 1);
            rest = new Vector3[n]; pos = new Vector3[n]; prev = new Vector3[n]; tgt = new Vector3[n];
            BuildMesh();
        }

        void BuildMesh()
        {
            mesh = new Mesh();
            mesh.name = "Fin";
            mesh.MarkDynamic();
            int S = steps + 1;
            Vector2[] uv;
            int[] tris;
            if (!ribbon)
            {
                verts = new Vector3[rays * S];
                uv = new Vector2[rays * S];
                for (int i = 0; i < rays; i++)
                    for (int j = 0; j < S; j++)
                        uv[i * S + j] = new Vector2(rays > 1 ? i / (rays - 1f) : 0.5f, j / (float)steps);
                tris = new int[(rays - 1) * steps * 6];
                int t = 0;
                for (int i = 0; i < rays - 1; i++)
                    for (int j = 0; j < steps; j++)
                    {
                        int a = i * S + j, b = (i + 1) * S + j, c = a + 1, d = b + 1;
                        tris[t++] = a; tris[t++] = b; tris[t++] = c;
                        tris[t++] = c; tris[t++] = b; tris[t++] = d;
                    }
            }
            else
            {
                verts = new Vector3[2 * S];
                uv = new Vector2[2 * S];
                for (int j = 0; j < S; j++)
                {
                    float f = j / (float)steps;
                    uv[2 * j] = new Vector2(0f, f);
                    uv[2 * j + 1] = new Vector2(1f, f);
                }
                tris = new int[steps * 6];
                int t = 0;
                for (int j = 0; j < steps; j++)
                {
                    int a = 2 * j, b = 2 * j + 1, c = 2 * j + 2, d = 2 * j + 3;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = c; tris[t++] = b; tris[t++] = d;
                }
            }
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.uv2 = uv;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        }

        public void ResetSim() { init = false; }

        public void Simulate(Transform t, float dt, float scale)
        {
            int n = rest.Length;
            for (int k = 0; k < n; k++) tgt[k] = t.TransformPoint(rest[k]);
            if (!init || dt <= 0f)
            {
                if (!init)
                {
                    for (int k = 0; k < n; k++) { pos[k] = tgt[k]; prev[k] = tgt[k]; }
                    init = true;
                }
                return;
            }
            dt = Mathf.Min(dt, 0.05f);
            float frames = dt * 60f;
            float bendK = 1f - Mathf.Pow(1f - bend, frames);
            int S = steps + 1;
            for (int i = 0; i < rays; i++)
            {
                int b = i * S;
                pos[b] = tgt[b]; prev[b] = tgt[b];
                for (int j = 1; j <= steps; j++)
                {
                    int k = b + j;
                    float f = j / (float)steps;
                    float st = Mathf.Lerp(stiffRoot, stiffTip, Mathf.Pow(f, 0.7f));
                    float dampF = Mathf.Pow(BettaShape.CriticalKeep(st) * overdamp, frames);
                    Vector3 vel = (pos[k] - prev[k]) * dampF;
                    prev[k] = pos[k];
                    Vector3 p = pos[k] + vel;
                    float kk = 1f - Mathf.Pow(Mathf.Clamp01(1f - st), frames);
                    p += (tgt[k] - p) * kk;
                    float L = (tgt[k] - tgt[k - 1]).magnitude;
                    if (j >= 2)
                    {
                        Vector3 restBend = (tgt[k] - tgt[k - 1]) - (tgt[k - 1] - tgt[k - 2]);
                        Vector3 ext = pos[k - 1] + (pos[k - 1] - pos[k - 2]) + restBend;
                        p = Vector3.Lerp(p, ext, bendK);
                    }
                    Vector3 d = p - pos[k - 1];
                    float dm = d.magnitude;
                    if (dm > 1e-6f) p = pos[k - 1] + d * (L / dm);
                    Vector3 dev = p - tgt[k];
                    float maxD = maxDev * scale * f;
                    if (dev.sqrMagnitude > maxD * maxD) p = tgt[k] + dev.normalized * maxD;
                    pos[k] = p;
                }
            }
        }

        public void WriteMesh(Transform t)
        {
            if (!ribbon)
            {
                for (int k = 0; k < pos.Length; k++) verts[k] = t.InverseTransformPoint(pos[k]);
            }
            else
            {
                int S = steps + 1;
                for (int j = 0; j < S; j++)
                {
                    Vector3 c = t.InverseTransformPoint(pos[j]);
                    Vector3 a = t.InverseTransformPoint(pos[Mathf.Max(j - 1, 0)]);
                    Vector3 b = t.InverseTransformPoint(pos[Mathf.Min(j + 1, steps)]);
                    Vector3 dir = b - a;
                    Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                    if (perp.sqrMagnitude < 1e-10f) perp = Vector3.up; else perp.Normalize();
                    float f = j / (float)steps;
                    float w = ribbonWidth * (1f - f) + 0.004f;
                    verts[2 * j] = c - perp * w;
                    verts[2 * j + 1] = c + perp * w;
                }
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
