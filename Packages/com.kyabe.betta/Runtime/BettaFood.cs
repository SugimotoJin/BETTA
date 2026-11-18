using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// A food pellet. Floats at the surface for a while (like betta pellets), then soaks up water
    /// and sinks slowly, rests on the bottom and finally fades away.
    /// </summary>
    public class BettaFood : MonoBehaviour
    {
        public static Material SharedMaterial;
        public static Mesh SharedMesh;

        public Rect bounds;          // tank in world units (top = water surface)
        public float size = 0.05f;
        [System.NonSerialized] public bool eaten;

        Vector2 vel;
        float age, floatTime, restTime, seed;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        static readonly int ID_Fade = Shader.PropertyToID("_Fade");

        public static BettaFood Drop(Vector2 at, Rect bounds, float size)
        {
            if (SharedMaterial == null)
            {
                Shader sh = Shader.Find("Betta/Food");
                if (sh != null) SharedMaterial = new Material(sh);
            }
            GameObject go = new GameObject("Food");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = Random.rotation;
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh();
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = SharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            BettaFood f = go.AddComponent<BettaFood>();
            f.bounds = bounds;
            f.size = size;
            f.mr = r;
            BettaAquarium.Food.Add(f);
            return f;
        }

        static Mesh GetMesh()
        {
            if (SharedMesh != null) return SharedMesh;
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            return SharedMesh;
        }

        void Start()
        {
            seed = Random.value * 10f;
            floatTime = 15f + Random.value * 15f;
            restTime = 25f;
            mpb = new MaterialPropertyBlock();
            vel = new Vector2((Random.value - 0.5f) * 0.3f, -0.6f);   // a little splash when it lands
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            age += dt;
            Vector3 p = transform.position;
            float surface = bounds.yMax - size * 0.6f;
            float bottom = BettaAquarium.FloorAt(p.x, bounds.yMin, bounds.yMin) + size * 0.6f;
            float fade = 1f;

            if (age < floatTime)
            {
                // floating: pulled back up to the surface, bobbing, drifting a little
                vel.y += ((surface - p.y) * 6f - vel.y * 3f) * dt;
                vel.x += (Mathf.Sin(age * 0.7f + seed) * 0.05f - vel.x) * dt;
                p.y += Mathf.Sin(age * 2.3f + seed) * 0.002f;
            }
            else if (p.y > bottom)
            {
                // soaked: sinks slowly and wobbles
                vel.y += (-0.25f - vel.y) * 1.5f * dt;
                vel.x += (Mathf.Sin(age * 1.3f + seed) * 0.06f - vel.x) * 2f * dt;
            }
            else
            {
                vel = Vector2.zero;
                p.y = bottom;
                restTime -= dt;
                fade = Mathf.Clamp01(restTime / 5f);
                if (restTime <= 0f) { Remove(); return; }
            }
            p.x += vel.x * dt;
            p.y += vel.y * dt;
            p.x = Mathf.Clamp(p.x, bounds.xMin + size, bounds.xMax - size);
            transform.position = p;
            transform.Rotate(0f, 0f, 20f * dt * Mathf.Sin(seed));

            if (mr != null)
            {
                mr.GetPropertyBlock(mpb);
                mpb.SetFloat(ID_Fade, fade);
                mr.SetPropertyBlock(mpb);
            }
        }

        public void Eat()
        {
            eaten = true;
            Remove();
        }

        void Remove()
        {
            BettaAquarium.Food.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() { BettaAquarium.Food.Remove(this); }
    }
}
