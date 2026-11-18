using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// Lets the bettas react to the mouse: they come up to a resting pointer and slowly follow it,
    /// and a click drops food onto the water surface above the pointer.
    /// Put one in the scene (GameObject → Betta → マウスへの反応). Works with the Input System and the old Input Manager.
    /// </summary>
    [AddComponentMenu("Betta/Betta Interaction")]
    public class BettaInteraction : MonoBehaviour
    {
        [Tooltip("マウス位置を調べるカメラ。空ならメインカメラ")]
        public Camera targetCamera;
        [Tooltip("泳ぐ範囲。空ならシーンの最初の水槽")]
        public BettaTank tank;

        [Header("カーソル")]
        [Tooltip("カーソルを止めておくと寄ってきて、ゆっくり動かすとついてくる")]
        public bool followCursor = true;

        [Header("エサやり")]
        [Tooltip("クリックした所の水面にエサを落とす")]
        public bool clickToFeed = true;
        [Range(1, 8)] public int pelletsPerClick = 3;
        public float pelletSize = 0.06f;
        public Shader foodShader;

        void Reset() { foodShader = Shader.Find("Betta/Food"); }

        void Awake()
        {
            if (foodShader != null && BettaFood.SharedMaterial == null) BettaFood.SharedMaterial = new Material(foodShader);
        }

        void Update()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;
            if (tank == null) tank = FindFirstObjectByType<BettaTank>();

            Vector2 sp;
            bool has = BettaPointer.Position(out sp) && sp.x >= 0f && sp.y >= 0f && sp.x < Screen.width && sp.y < Screen.height;
            float planeZ = tank != null ? tank.transform.position.z : 0f;
            Vector2 world = has ? ScreenToPlane(cam, sp, planeZ, out has) : Vector2.zero;

            BettaAquarium.SetCursor(world, followCursor && has, Time.deltaTime);

            if (clickToFeed && has && BettaPointer.ClickDown())
            {
                Rect r = TankRect();
                if (world.x >= r.xMin && world.x <= r.xMax) BettaAquarium.Feed(world.x, r, pelletsPerClick, pelletSize);
            }
        }

        void OnDisable() { BettaAquarium.SetCursor(Vector2.zero, false, Time.deltaTime); }

        Rect TankRect()
        {
            if (tank != null) return tank.WorldRect;
            if (BettaAquarium.Fish.Count > 0 && BettaAquarium.Fish[0] != null) return BettaAquarium.Fish[0].bounds;
            return new Rect(-6f, -4f, 12f, 8f);
        }

        /// <summary>Screen point → point on the plane z = planeZ (the plane the fish swim in).</summary>
        public static Vector2 ScreenToPlane(Camera cam, Vector2 sp, float planeZ, out bool ok)
        {
            Ray r = cam.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            ok = Mathf.Abs(r.direction.z) > 1e-5f;
            if (!ok) return Vector2.zero;
            float t = (planeZ - r.origin.z) / r.direction.z;
            ok = t > 0f;
            Vector3 p = r.origin + r.direction * t;
            return new Vector2(p.x, p.y);
        }
    }

    /// <summary>Mouse position / click through whichever input system the project uses.</summary>
    public static class BettaPointer
    {
        public static bool Position(out Vector2 p)
        {
#if ENABLE_INPUT_SYSTEM && BETTA_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) { p = Vector2.zero; return false; }
            p = m.position.ReadValue();
            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            p = Input.mousePosition;
            return Input.mousePresent;
#else
            p = Vector2.zero;
            return false;
#endif
        }

        public static bool ClickDown()
        {
#if ENABLE_INPUT_SYSTEM && BETTA_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }
    }
}
