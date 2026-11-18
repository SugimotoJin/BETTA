using System.Collections.Generic;
using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// Shared state of the tank that the fish react to: food pellets, the mouse cursor, the other fish.
    /// Filled by BettaDesktop (or left empty in the demo, where the fish just swim).
    /// </summary>
    public static class BettaAquarium
    {
        public static readonly List<BettaFood> Food = new List<BettaFood>();
        public static readonly List<BettaSwimmer> Fish = new List<BettaSwimmer>();

        // mouse cursor in world space (z = 0 plane)
        public static bool CursorActive;      // the fish may react to the cursor
        public static Vector2 Cursor;
        public static Vector2 CursorVel;      // world units / s, smoothed
        public static float CursorStill;      // seconds the cursor has been nearly still

        static Vector2 lastCursor;
        static bool lastValid;

        /// <summary>
        /// Tell the fish where the pointer is (world units, in the plane they swim in). Call every frame.
        /// active = false: the fish ignore it. BettaInteraction does this for a normal camera.
        /// </summary>
        public static void SetCursor(Vector2 world, bool active, float dt)
        {
            dt = Mathf.Max(dt, 1e-4f);
            if (active && lastValid)
            {
                Vector2 v = (world - lastCursor) / dt;
                CursorVel = Vector2.Lerp(CursorVel, v, Mathf.Min(1f, 12f * dt));
                CursorStill = v.magnitude < 0.6f ? CursorStill + dt : 0f;
            }
            else
            {
                CursorVel = Vector2.zero;
                CursorStill = 0f;
            }
            lastCursor = world;
            lastValid = active;
            Cursor = world;
            CursorActive = active;
        }

        /// <summary>Drop a few food pellets onto the water surface of the tank above x.</summary>
        public static void Feed(float x, Rect tank, int count = 3, float size = 0.06f)
        {
            float spread = size * 10f;
            for (int k = 0; k < count; k++)
            {
                float px = Mathf.Clamp(x + (Random.value - 0.5f) * spread, tank.xMin + size, tank.xMax - size);
                BettaFood.Drop(new Vector2(px, tank.yMax - 0.05f), tank, size);
            }
        }

        public static void Feed(float x, BettaTank tank, int count = 3, float size = 0.06f)
        {
            if (tank != null) Feed(x, tank.WorldRect, count, size);
        }

        // the tank bottom can differ per monitor: segments [x0, x1] with their own floor height
        static readonly List<Vector3> floors = new List<Vector3>();   // (x0, x1, floorY) in world units

        public static void SetFloors(List<Vector3> segs)
        {
            floors.Clear();
            if (segs != null) floors.AddRange(segs);
        }

        /// <summary>Floor height at x. No segments: fallback. Between monitors (no screen there): noWater.</summary>
        public static float FloorAt(float x, float fallback, float noWater)
        {
            if (floors.Count == 0) return fallback;
            for (int i = 0; i < floors.Count; i++)
                if (x >= floors[i].x && x <= floors[i].y) return floors[i].z;
            return noWater;
        }

        /// <summary>A random x that has water under it.</summary>
        public static float RandomWaterX(float xMin, float xMax)
        {
            for (int k = 0; k < 20; k++)
            {
                float x = Random.Range(xMin, xMax);
                if (floors.Count == 0 || FloorAt(x, 0f, float.NaN) == FloorAt(x, 0f, float.NaN)) return x;   // not NaN
            }
            return floors.Count > 0 ? (floors[0].x + floors[0].y) * 0.5f : 0f;
        }

        public static BettaFood NearestFood(Vector2 p, out float dist)
        {
            BettaFood best = null;
            dist = float.MaxValue;
            for (int i = 0; i < Food.Count; i++)
            {
                BettaFood f = Food[i];
                if (f == null || f.eaten) continue;
                float d = ((Vector2)f.transform.position - p).magnitude;
                if (d < dist) { dist = d; best = f; }
            }
            return best;
        }
    }
}
