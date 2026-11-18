using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// The area bettas swim in: a rectangle in this object's XY plane (the fish swim in the plane and turn
    /// toward / away from the camera). Fish that are children of the tank, or that point to it, stay inside.
    /// The top edge is the water surface (where they come up to breathe).
    /// </summary>
    public class BettaTank : MonoBehaviour
    {
        [Tooltip("泳ぐ範囲の幅と高さ（このオブジェクトの位置が中心）")]
        public Vector2 size = new Vector2(12f, 8f);

        /// <summary>Swim area in world XY (position + size, scaled by the object's scale).</summary>
        public Rect WorldRect
        {
            get
            {
                Vector3 c = transform.position;
                Vector3 s = transform.lossyScale;
                float w = size.x * Mathf.Abs(s.x), h = size.y * Mathf.Abs(s.y);
                return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
            }
        }

        /// <summary>Make a swimming betta in this tank.</summary>
        public BettaSwimmer Spawn(BettaParams p)
        {
            Rect r = WorldRect;
            Vector3 pos = new Vector3(Random.Range(r.xMin + r.width * 0.2f, r.xMax - r.width * 0.2f),
                                      Random.Range(r.yMin + r.height * 0.2f, r.yMax - r.height * 0.2f),
                                      transform.position.z);
            return BettaFish.Create(p, pos, transform, this);
        }

        void OnDrawGizmos()
        {
            Rect r = WorldRect;
            float z = transform.position.z;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            Vector3 a = new Vector3(r.xMin, r.yMin, z), b = new Vector3(r.xMax, r.yMin, z);
            Vector3 c = new Vector3(r.xMax, r.yMax, z), d = new Vector3(r.xMin, r.yMax, z);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(d, a);
            Gizmos.color = new Color(0.5f, 0.9f, 1f, 1f);   // water surface
            Gizmos.DrawLine(d, c);
            Gizmos.DrawLine(d + Vector3.down * 0.02f, c + Vector3.down * 0.02f);
        }
    }
}
