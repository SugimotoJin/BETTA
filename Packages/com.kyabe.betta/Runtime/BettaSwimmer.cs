using UnityEngine;

namespace Betta3D
{
    /// <summary>
    /// Betta-like movement in the XY plane.
    /// Bettas swim in bursts: a few quick tail beats (dart), then glide and slow down, then hover
    /// with the fins slowly undulating and only the pectorals fluttering. They turn around in 3D
    /// and sometimes go up to the surface to breathe air.
    /// "Effort" (0..1) drives how fast and how strongly the fins move.
    /// </summary>
    [RequireComponent(typeof(BettaFish))]
    public class BettaSwimmer : MonoBehaviour
    {
        public BettaFish fish;
        [Tooltip("泳ぐ範囲。空なら親の BettaTank を探し、無ければ下の bounds を使います")]
        public BettaTank tank;
        public Rect bounds = new Rect(-6f, -4f, 12f, 8f);   // world XY area the fish stays in
        public bool observe;                                 // stay near the center for close-up viewing

        enum State { Wander, Hover, Surface, Eat, Curious, Flare, Flee }

        State state;
        float stateTime, stateDur;
        Vector2 target, vel;
        float yaw, yawTarget, yawVel, pitch;
        float surfaceHold, surfaceCooldown, seedPhase, phase, pect, bendSm;
        bool lastObserve;

        // burst swimming
        bool thrust;
        float thrustTime, thrustDur, coastDur;
        float effort;
        float fold, foldVel;
        bool cruise;   // this leg: steady, unhurried swimming instead of a dash
        // U-turns are swum along an arc in depth (toward / away from the viewer), like a real fish:
        // the body keeps moving forward along its heading while it bends around
        float baseZ, depth, turnSpeed;
        bool wasTurning;

        // reactions (desktop): food, the mouse cursor, other bettas
        float hunger = 4f, hungerRegen;
        BettaFood food;
        float curiousCooldown, flareCooldown, flareAmt, thinkTimer, curiousSide;
        BettaSwimmer rival;        // the fish we flare at (null = the cursor)
        Vector2 flareAt;

        void OnEnable() { if (!BettaAquarium.Fish.Contains(this)) BettaAquarium.Fish.Add(this); }
        void OnDisable() { BettaAquarium.Fish.Remove(this); }

        public bool IsFlaring { get { return state == State.Flare; } }
        public Vector2 Mouth { get { return transform.TransformPoint(new Vector3(0.5f, 0f, 0f)); } }

        void Start()
        {
            if (fish == null) fish = GetComponent<BettaFish>();
            if (tank == null) tank = GetComponentInParent<BettaTank>();
            if (tank != null) bounds = tank.WorldRect;
            seedPhase = Random.value * 10f;
            yaw = yawTarget = Random.value < 0.5f ? 0f : 180f;
            phase = Random.value * 10f;
            pect = Random.value * 6f;
            surfaceCooldown = 10f + Random.value * 20f;
            baseZ = transform.position.z;
            NewTarget(false);
        }

        float FacingNow { get { return Mathf.Cos(yaw * Mathf.Deg2Rad); } }
        float FacingGoal { get { return Mathf.Cos(yawTarget * Mathf.Deg2Rad) >= 0f ? 1f : -1f; } }
        bool Turning { get { return Mathf.Abs(yaw - yawTarget) > 1f; } }

        void Face(float sign)
        {
            if (sign == FacingGoal) return;
            if (Turning)
            {
                yawTarget += yawTarget > yaw ? -180f : 180f;   // already turning: turn back
                return;
            }
            // turn toward or away from the viewer - whichever brings the fish back toward its usual depth
            // (moving forward along the heading, world z changes by -sin(yaw))
            float dPlus = -Mathf.Sin((yaw + 90f) * Mathf.Deg2Rad);   // depth direction when turning with +yaw
            float dir;
            if (Mathf.Abs(depth) < 0.05f) dir = Random.value < 0.5f ? 1f : -1f;
            else dir = dPlus * depth > 0f ? -1f : 1f;
            yawTarget = yaw + dir * 180f;
        }

        void Enter(State s, float dur)
        {
            state = s; stateTime = 0f; stateDur = dur;
            thrust = false; thrustTime = 0f; coastDur = 0.15f;   // start the next burst soon
        }

        void NewTarget(bool down)
        {
            float L = Mathf.Max(0.05f, fish.Params.size);
            Vector2 pos = transform.position;
            float mx = Mathf.Min(0.9f * L, bounds.width * 0.25f);
            float myTop = Mathf.Min(0.6f * L, bounds.height * 0.25f);
            float myBot = Mathf.Min(0.7f * L, bounds.height * 0.25f);
            float tx = Random.value < 0.35f
                ? pos.x + (Random.value * 2f - 1f) * 3.5f * L
                : BettaAquarium.RandomWaterX(bounds.xMin + mx, bounds.xMax - mx);
            tx = Mathf.Clamp(tx, bounds.xMin + mx, bounds.xMax - mx);
            if (FloorY(tx) + myBot > bounds.yMax - myTop) tx = pos.x;   // no water there (between monitors)
            float ty = down
                ? pos.y - L * (1.2f + Random.value * 2.5f)
                : pos.y + (Random.value * 2f - 1f) * Mathf.Max(bounds.height * 0.3f, 2f * L);
            ty = Mathf.Clamp(ty, Mathf.Max(bounds.yMin, FloorY(tx)) + myBot, bounds.yMax - myTop);
            target = new Vector2(tx, ty);
            cruise = !down && Random.value < 0.5f;
            Enter(State.Wander, 25f);
        }

        void OnArrive(float L)
        {
            float r = Random.value;
            if (surfaceCooldown <= 0f && r < 0.18f)
            {
                target.x = Mathf.Clamp(transform.position.x + FacingGoal * L * (0.3f + Random.value), bounds.xMin + L, bounds.xMax - L);
                surfaceHold = 0f;
                surfaceCooldown = 25f + Random.value * 40f;
                Enter(State.Surface, 20f);
            }
            else if (r < 0.75f) Enter(State.Hover, 2.5f + Random.value * 6f);
            else NewTarget(false);
        }

        /// <summary>Start flaring at a rival fish (or at the cursor when rival is null).</summary>
        public void StartFlare(BettaSwimmer other, Vector2 at)
        {
            rival = other;
            flareAt = at;
            cruise = false;
            Enter(State.Flare, 3f + Random.value * 3f);
            flareCooldown = 20f + Random.value * 25f;
        }

        /// <summary>Decide whether something in the tank (food, the cursor, a rival) takes over.</summary>
        void Think(Vector2 pos, float L, float dt)
        {
            curiousCooldown -= dt;
            flareCooldown -= dt;
            hungerRegen += dt;
            if (hungerRegen > 60f) { hungerRegen = 0f; hunger = Mathf.Min(5f, hunger + 1f); }

            Vector2 mouth = Mouth;
            if (state == State.Flee || state == State.Flare) return;
            if (state == State.Surface && surfaceHold > 0f) return;   // finish the breath first

            // food beats everything else
            if (hunger >= 1f && state != State.Eat)
            {
                float fd;
                BettaFood f = BettaAquarium.NearestFood(mouth, out fd);
                if (f != null && fd < 30f * L)
                {
                    food = f;
                    cruise = false;
                    Enter(State.Eat, 20f);
                    return;
                }
            }
            if (state == State.Eat) return;

            thinkTimer -= dt;
            if (thinkTimer > 0f) return;
            thinkTimer = 0.5f;

            // two bettas meeting face to face flare at each other
            if (flareCooldown <= 0f)
            {
                for (int i = 0; i < BettaAquarium.Fish.Count; i++)
                {
                    BettaSwimmer o = BettaAquarium.Fish[i];
                    if (o == this || o == null || o.flareCooldown > 0f) continue;
                    Vector2 d = (Vector2)o.transform.position - pos;
                    if (Mathf.Abs(d.x) > 1.8f * L || Mathf.Abs(d.y) > 0.6f * L) continue;
                    bool facingIt = FacingGoal * d.x > 0f, itFacing = o.FacingGoal * -d.x > 0f;
                    if (facingIt && itFacing && Random.value < 0.6f)
                    {
                        StartFlare(o, o.transform.position);
                        o.StartFlare(this, pos);
                        return;
                    }
                }
            }

            // a cursor resting near the fish makes it curious
            if (BettaAquarium.CursorActive && state != State.Curious && curiousCooldown <= 0f
                && BettaAquarium.CursorStill > 0.6f && (BettaAquarium.Cursor - pos).magnitude < 5f * L
                && Random.value < 0.35f)
            {
                cruise = true;
                curiousSide = 0f;
                Enter(State.Curious, 8f + Random.value * 8f);
            }
        }

        float FloorY(float x) { return BettaAquarium.FloorAt(x, bounds.yMin, bounds.yMax); }

        Vector2 ClampToTank(Vector2 v, float L)
        {
            return new Vector2(Mathf.Clamp(v.x, bounds.xMin + L, bounds.xMax - L),
                               Mathf.Clamp(v.y, bounds.yMin + 0.6f * L, bounds.yMax - 0.5f * L));
        }

        /// <summary>
        /// Burst-and-glide toward a goal. Returns the desired velocity; sets thrust on/off.
        /// </summary>
        Vector2 Burst(Vector2 pos, Vector2 goal, float L, float spd, float dt, out bool arrived, bool autoFace = true)
        {
            Vector2 d = goal - pos;
            float dist = d.magnitude;
            arrived = dist < 0.3f * L && vel.magnitude < 0.5f * L;
            if (autoFace && Mathf.Abs(d.x) > 0.35f * L) Face(d.x > 0f ? 1f : -1f);

            thrustTime += dt;
            if (thrust)
            {
                if (thrustTime > thrustDur)
                {
                    thrust = false; thrustTime = 0f;
                    coastDur = cruise ? 0.25f + Random.value * 0.4f : 0.6f + Random.value * 1.0f;
                }
            }
            else if (thrustTime > coastDur && dist > 0.35f * L && !Turning)
            {
                thrust = true; thrustTime = 0f;
                thrustDur = cruise
                    ? (1.8f + Random.value * 2f)                    // cruising: long, steady strokes
                    : Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01(dist / (5f * L))) * (0.8f + 0.4f * Random.value);
            }
            if (!thrust || dist < 1e-4f) return vel;          // gliding: keep momentum (water drag slows it)
            float burst = (cruise ? 0.75f : 1.6f) * L * spd * Mathf.Clamp01(dist / (1.5f * L) + 0.35f);
            return d / dist * burst;
        }

        void Update()
        {
            if (fish == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            BettaParams p = fish.Params;
            float L = Mathf.Max(0.05f, p.size);
            float spd = p.speed;
            if (tank != null) bounds = tank.WorldRect;
            stateTime += dt;
            surfaceCooldown -= dt;

            if (observe != lastObserve)
            {
                lastObserve = observe;
                if (observe) Enter(State.Hover, 4f); else NewTarget(false);
            }

            Vector2 pos = transform.position;
            Vector2 desired = vel;
            float pitchOverride = float.NaN;
            bool arrived;
            float t = Time.time;
            float bob = Mathf.Sin(t * 0.9f + seedPhase) * 0.04f * L;

            if (observe)
            {
                if (state == State.Wander)
                {
                    desired = Burst(pos, target, L, spd * 0.7f, dt, out arrived);
                    if (arrived) Enter(State.Hover, 4f + Random.value * 4f);
                }
                else
                {
                    thrust = false;
                    desired = new Vector2(0f, bob);
                    if (stateTime > stateDur)
                    {
                        // turn around, then dart a short way (the camera follows the fish)
                        Face(-FacingGoal);
                        float dir = FacingGoal;
                        target = new Vector2(Mathf.Clamp(pos.x + dir * 3f * L, -3f * L, 3f * L),
                                             Mathf.Clamp(pos.y + (Random.value - 0.5f) * 0.6f * L, -L, L));
                        Enter(State.Wander, 10f);
                    }
                }
            }
            else
            {
                Think(pos, L, dt);
                switch (state)
                {
                    case State.Eat:
                        {
                            if (food == null || food.eaten) { food = null; Enter(State.Hover, 0.4f + Random.value * 0.6f); break; }
                            Vector2 fp = food.transform.position;
                            Vector2 mouth = Mouth;
                            Vector2 toFood = fp - mouth;
                            if (Mathf.Abs(fp.x - pos.x) > 0.3f * L) Face(fp.x > pos.x ? 1f : -1f);
                            // aim so that the mouth (not the body centre) reaches the pellet
                            Vector2 goal = fp - (mouth - pos);
                            desired = Burst(pos, goal, L, spd * 1.15f, dt, out arrived);
                            if (toFood.magnitude < 1.5f * L)
                                pitchOverride = Mathf.Clamp(Mathf.Atan2(toFood.y, Mathf.Abs(toFood.x) + 0.2f * L), -0.6f, 0.9f);
                            if (toFood.magnitude < 0.16f * L)
                            {
                                food.Eat();
                                food = null;
                                hunger -= 1f;
                                vel *= 0.3f;
                                Enter(State.Hover, 0.5f + Random.value * 0.8f);   // gulp, then look for the next one
                            }
                            else if (stateTime > stateDur) { food = null; NewTarget(false); }
                            break;
                        }

                    case State.Curious:
                        {
                            Vector2 cur = BettaAquarium.Cursor;
                            if (!BettaAquarium.CursorActive || stateTime > stateDur
                                || (cur - pos).magnitude > 7f * L)
                            {
                                curiousCooldown = 10f + Random.value * 15f;
                                curiousSide = 0f;
                                NewTarget(false);
                                break;
                            }
                            // Stay on the side of the cursor the fish is already on (it only switches sides when
                            // the cursor really crosses over) and look at it from about half a body length away.
                            // Deciding the side from the fish, not from where it should stand, stops it from
                            // turning back and forth when the cursor is right above or below it.
                            float rel = pos.x - cur.x;
                            if (curiousSide == 0f || Mathf.Abs(rel) > 0.8f * L) curiousSide = rel >= 0f ? 1f : -1f;
                            Face(-curiousSide);
                            Vector2 goal = new Vector2(cur.x + curiousSide * 1.05f * L, cur.y);
                            Vector2 toGoal = goal - pos;
                            if (toGoal.magnitude > 1.5f * L && !Turning)
                                desired = Burst(pos, goal, L, spd * 0.6f, dt, out arrived, false);
                            else
                            {
                                // close by: hover and drift into place with the pectorals (bettas can back up slowly)
                                thrust = false;
                                desired = Vector2.ClampMagnitude(toGoal * 1.5f, 0.6f * L) + new Vector2(0f, bob);
                            }
                            break;
                        }

                    case State.Flare:
                        {
                            if (rival != null) flareAt = rival.transform.position;
                            else if (BettaAquarium.CursorActive) flareAt = BettaAquarium.Cursor;
                            if (Mathf.Abs(flareAt.x - pos.x) > 0.2f * L) Face(flareAt.x > pos.x ? 1f : -1f);
                            // hold position side-on, with small jerky lunges toward the rival
                            thrust = false;
                            float lunge = Mathf.Max(0f, Mathf.Sin(stateTime * 4.2f + seedPhase)) * 0.5f * L;
                            float gap = (flareAt.x - pos.x) * FacingGoal;
                            desired = new Vector2(FacingGoal * (gap > 1.0f * L ? lunge : -0.15f * L), (flareAt.y - pos.y) * 0.5f + bob);
                            vel += (desired - vel) * Mathf.Min(1f, 3f * dt);
                            desired = vel;
                            if (stateTime > stateDur)
                            {
                                rival = null;
                                Enter(State.Hover, 1f + Random.value * 2f);
                            }
                            break;
                        }

                    case State.Flee:
                        desired = Burst(pos, target, L, spd * 1.3f, dt, out arrived);
                        if (arrived || stateTime > stateDur) Enter(State.Hover, 1.5f + Random.value * 2f);
                        break;

                    case State.Wander:
                        desired = Burst(pos, target, L, spd, dt, out arrived);
                        if (arrived) OnArrive(L);
                        else if (stateTime > stateDur) NewTarget(false);
                        break;

                    case State.Hover:
                        thrust = false;
                        desired = new Vector2(0f, bob);
                        if (stateTime > stateDur) NewTarget(false);
                        break;

                    case State.Surface:
                        {
                            Vector3 mouth = transform.TransformPoint(new Vector3(0.5f, 0f, 0f));
                            float top = bounds.yMax;
                            if (surfaceHold <= 0f)
                            {
                                float gy = top - (mouth.y - pos.y) - 0.01f * L;
                                desired = Burst(pos, new Vector2(target.x, gy), L, spd * 0.8f, dt, out arrived);
                                pitchOverride = 0.5f;
                                if (mouth.y > top - 0.06f * L) surfaceHold = 0.8f + Random.value * 1.2f;
                                if (stateTime > stateDur) NewTarget(true);
                            }
                            else
                            {
                                thrust = false;
                                desired = Vector2.zero;
                                pitchOverride = 0.55f;
                                surfaceHold -= dt;
                                if (surfaceHold <= 0f) NewTarget(true);
                            }
                            break;
                        }
                }
            }

            // bettas don't swim backwards; turning costs speed
            float align = FacingNow * FacingGoal;
            if (thrust)
            {
                float tf = 0.2f + 0.8f * Mathf.Max(0f, align);
                desired.x *= tf;
                if (desired.x * FacingNow < 0f && Mathf.Abs(FacingNow) > 0.3f) desired.x *= 0.2f;
            }

            if (thrust)
            {
                // quick acceleration during a burst
                Vector2 dv = desired - vel;
                float lim = (cruise ? 1.8f : 5f) * L * Mathf.Max(0.5f, spd) * dt;
                if (dv.magnitude > lim) dv = dv.normalized * lim;
                vel += dv;
            }
            else
            {
                // gliding / hovering: water drag slows the fish, small corrections only
                vel *= Mathf.Exp(-1.6f * dt);
                Vector2 dv = desired - vel;
                float lim = 0.4f * L * dt;
                if (dv.magnitude > lim) dv = dv.normalized * lim;
                vel += dv;
            }
            // a turn is a U-turn in an arc: the fish keeps drifting forward while it bends around
            bool turning = Turning;
            if (turning)
            {
                if (!wasTurning) turnSpeed = Mathf.Clamp(Mathf.Abs(vel.x), 0.45f * L, 1.4f * L * spd);
                turnSpeed = Mathf.Max(0.45f * L, turnSpeed * Mathf.Exp(-0.35f * dt));
                // velocity follows the heading: the fish swims around the arc instead of pivoting in place
                float yr = yaw * Mathf.Deg2Rad;
                vel.x = turnSpeed * Mathf.Cos(yr);
                depth = Mathf.Clamp(depth - turnSpeed * Mathf.Sin(yr) * dt, -2f * L, 2f * L);
            }
            wasTurning = turning;
            float prevX = pos.x;
            pos += vel * dt;

            float bx = 0.4f * L;
            if (pos.x < bounds.xMin + bx) { pos.x = bounds.xMin + bx; if (vel.x < 0f) vel.x = 0f; }
            if (pos.x > bounds.xMax - bx) { pos.x = bounds.xMax - bx; if (vel.x > 0f) vel.x = 0f; }
            if (pos.y < bounds.yMin + 0.3f * L) { pos.y = bounds.yMin + 0.3f * L; if (vel.y < 0f) vel.y = 0f; }
            // each monitor has its own bottom: a shallower neighbour is a wall below its floor
            float floorHere = FloorY(pos.x) + 0.3f * L;
            if (pos.y < floorHere)
            {
                float floorPrev = FloorY(prevX) + 0.3f * L;
                if (floorPrev < floorHere && pos.y >= floorPrev) { pos.x = prevX; vel.x = 0f; }
                else { pos.y = floorHere; if (vel.y < 0f) vel.y = 0f; }
            }
            if (pos.y > bounds.yMax - 0.15f * L) { pos.y = bounds.yMax - 0.15f * L; if (vel.y > 0f) vel.y = 0f; }

            // yaw (3D turn around the vertical axis): speeds up and slows down smoothly
            // turn rate follows from the forward speed and a turn radius of ~0.4 body lengths
            float maxRate = Turning ? Mathf.Clamp(turnSpeed / (0.4f * L) * Mathf.Rad2Deg, 110f, 280f) : 280f;
            float accel = 700f;
            float remaining = yawTarget - yaw;
            float wantRate = Mathf.Sign(remaining) * Mathf.Min(maxRate, Mathf.Sqrt(2f * accel * Mathf.Abs(remaining)));
            yawVel = Mathf.MoveTowards(yawVel, wantRate, accel * dt);
            yaw += yawVel * dt;
            if (Mathf.Abs(yawTarget - yaw) < 0.5f && Mathf.Abs(yawVel) < 40f)
            {
                float ny = Mathf.Repeat(yawTarget, 360f);
                yaw = yawTarget = ny;
                yawVel = 0f;
            }
            float yawRate = yawVel;

            float pt = Mathf.Atan2(vel.y, Mathf.Abs(vel.x) + 0.3f * L);
            pt = Mathf.Clamp(pt, -0.6f, 0.6f);
            if (!float.IsNaN(pitchOverride)) pt = pitchOverride;
            pitch += (pt - pitch) * Mathf.Min(1f, 2.5f * dt);

            // effort: rises fast when the fish pushes, falls slowly when it glides
            float effortTarget = thrust ? (cruise ? 0.5f : 1f) : (Turning ? 0.55f : 0f);
            float k = effortTarget > effort ? 10f : 1.4f;
            effort += (effortTarget - effort) * Mathf.Min(1f, k * dt);

            // fin wave speed: very slow when resting, fast tail beats when darting
            phase += dt * (1.3f + 15f * effort);
            if (phase > 62.831853f) phase -= 62.831853f;
            // pectorals flutter, fastest while hovering
            pect += dt * (14f + 8f * (1f - effort)) / (0.55f + 0.45f * Mathf.Max(1f, p.pectSize));   // big (dumbo) pectorals beat slower
            if (pect > 6.2831853f) pect -= 6.2831853f;
            // strong C-shaped bend while turning (head and tail to the same side)
            bendSm = Mathf.Lerp(bendSm, Mathf.Clamp(-yawRate * 0.0042f, -1.1f, 1.1f), Mathf.Min(1f, 12f * dt));

            transform.position = new Vector3(pos.x, pos.y, baseZ + depth);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, pitch * Mathf.Rad2Deg);

            // fins fold back with speed; when the fish stops they unfurl slowly with an ease-in/ease-out
            // (two smoothing stages, no spring, so nothing bounces back)
            float foldTarget = Mathf.Clamp01(vel.magnitude / (1.05f * L));
            float rate = foldTarget > foldVel ? 6f : 1.6f;          // fold quickly, open slowly
            foldVel += (foldTarget - foldVel) * (1f - Mathf.Exp(-rate * dt));
            fold += (foldVel - fold) * (1f - Mathf.Exp(-rate * 1.5f * dt));

            // flaring: gill covers out, every fin spread wide
            float flareT = state == State.Flare ? 1f : 0f;
            flareAmt += (flareT - flareAmt) * Mathf.Min(1f, (flareT > flareAmt ? 5f : 1.5f) * dt);

            fish.Phase = phase;
            fish.Swim = effort;
            fish.Fold = fold * (1f - flareAmt);
            fish.Flare = flareAmt;
            fish.Pect = pect;
            fish.Bend = bendSm;
        }
    }
}
