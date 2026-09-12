using Ironfield.Combat;
using Ironfield.Core;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Mouse-aim arcade flight. A virtual reticle (mouse delta, springs to
    /// centre) says where the pilot wants the nose; the drone continuously yaws
    /// and pitches onto it and flies along the nose. Keyboard is throttle / climb
    /// trim / roll / boost only. Rigidbody based, not an aero sim.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneController : MonoBehaviour
    {
        public DroneTuning tuning;
        [Tooltip("Spinning prop meshes, purely cosmetic.")]
        public Transform[] propSpinners;

        [Header("Mouse aim")]
        [Tooltip("Reticle travel per pixel of mouse movement.")]
        public float aimSensitivity = 0.0016f;
        [Tooltip("How fast the reticle springs back to centre (per second).")]
        public float aimReturn = 2.4f;
        [Tooltip("Nose pitch at full reticle deflection, degrees.")]
        public float pitchRange = 46f;
        [Range(0.1f, 1f)] public float precisionScale = 0.4f;
        [Tooltip("Idle speed fraction with the throttle centred — lowered from "
                + "0.42 after the throttle-mapping fix still felt like too "
                + "narrow an accel/brake range; a slower coast baseline gives "
                + "W and S both more room to actually change something.")]
        [Range(0.05f, 1f)] public float cruiseFraction = 0.22f;

        [Header("Runtime state (read-only)")]
        [SerializeField] float _speed;
        public float Speed => _speed;
        public bool Boosting { get; private set; }
        public bool Precision { get; private set; }
        public Vector2 AimReticle => _aim;          // -1..1 inside the unit circle
        public bool ControlsEnabled { get; set; } = true;
        public float Heading => _heading;

        /// <summary>Test hook: (throttle, aimX, aimY, roll) forced when set.</summary>
        public bool useDebugInput;
        public Vector4 debugInput;

        [Header("Map boundary (set by MissionManager from the terrain's actual bounds)")]
        [Tooltip("XZ centre of the flyable area.")]
        public Vector2 boundaryCentre;
        [Tooltip("Distance from centre where the HUD warns and a gentle inward push starts.")]
        public float boundarySoftRadius = 100000f;
        [Tooltip("Distance from centre that is a hard wall the drone can't cross.")]
        public float boundaryHardRadius = 100000f;
        /// <summary>True once past boundarySoftRadius — HudController shows a warning off this.</summary>
        public bool IsNearBoundary { get; private set; }

        /// <summary>
        /// Auto-attack: when set, overrides manual aim + throttle for this frame —
        /// the flight model steers itself onto this world point at full send
        /// instead of reading the mouse. Set/cleared by DiveAssist, gated on
        /// GameSettings.AutoAttack. Boost/roll/climb trim still pass through from
        /// the player so it doesn't feel like input is entirely taken away.
        /// </summary>
        public Vector3? AutopilotTarget { get; set; }
        public bool AutopilotEngaged => AutopilotTarget.HasValue;

        public void RequestFire() => FireRequested?.Invoke();

        Rigidbody _rb;
        HealthComponent _health;
        DroneInput _in;
        Vector2 _aim;
        float _bank, _pitchVis, _heading;
        float _stuckTimer;

        public System.Action FireRequested;
        public System.Action RecallRequested;
        /// <summary>Fired on the "A" key / gamepad West button — see
        /// CruiseAssist, which subscribes to toggle auto-cruise on/off.</summary>
        public System.Action CruiseToggleRequested;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.freezeRotation = true;
            _health = GetComponent<HealthComponent>();
            if (tuning == null)
            {
                Debug.LogWarning("[Drone] No DroneTuning assigned; using defaults.", this);
                tuning = ScriptableObject.CreateInstance<DroneTuning>();
            }
            _heading = transform.eulerAngles.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool alive = ControlsEnabled && (_health == null || !_health.IsDead);
            _in = alive ? DroneInput.Read() : default;

            Precision = _in.Precision;

            if (AutopilotTarget.HasValue && !useDebugInput)
            {
                UpdateAutopilotAim(dt);
            }
            else
            {
                // --- integrate the mouse reticle --------------------------
                float sens = aimSensitivity * GameSettings.AimSensitivity * (Precision ? precisionScale : 1f);
                Vector2 delta = _in.AimDelta;
                if (GameSettings.InvertY) delta.y = -delta.y;
                _aim += delta * sens;
                if (!useDebugInput)
                {
                    // spring back toward centre so hands-off = fly straight
                    _aim = Vector2.Lerp(_aim, Vector2.zero, 1f - Mathf.Exp(-aimReturn * dt));
                }
                if (_aim.magnitude > 1f) _aim = _aim.normalized;
            }

            if (useDebugInput)
            {
                _in.Throttle = debugInput.x;
                _aim = new Vector2(debugInput.y, debugInput.z);
                _in.Roll = debugInput.w;
            }

            if (_in.FirePressed) FireRequested?.Invoke();
            if (_in.RecallPressed) RecallRequested?.Invoke();
            if (_in.CruiseTogglePressed) CruiseToggleRequested?.Invoke();

            // cosmetic prop spin
            float spin = _speed * 40f + 900f + (Boosting ? 1600f : 0f);
            if (propSpinners != null)
                foreach (var p in propSpinners)
                    if (p) p.Rotate(Vector3.up, spin * dt, Space.World);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Boosting = _in.Boost;

            // --- orientation from the reticle --------------------------
            _heading += _aim.x * tuning.yawRate * dt;
            float pitchCmd = -_aim.y * pitchRange;                     // up on screen = nose up
            Quaternion noseRot = Quaternion.Euler(pitchCmd, _heading, 0f);
            Vector3 nose = noseRot * Vector3.forward;

            // --- speed along the nose --------------------------------
            // Throttle 0 (hands off) sits at cruiseFraction; W pushes it up
            // toward 1 (full speed), S pulls it down toward 0.12 (brake) — a
            // real bug had the W half of this compressed into cruiseFraction
            // (0.42) .. 1.0 mapped from the *midpoint* of a -1..1 range
            // (Clamp01(0.5+0.5*throttle) is 0.5 at neutral, not 0), so neutral
            // throttle was already sitting at 71% speed and full W only added
            // another 29 points — the whole accel/brake range felt squeezed
            // into a narrow band, exactly as reported. Fixed by lerping
            // straight off Throttle itself (already -1..1) instead of that
            // remapped midpoint, so W now sweeps the full cruiseFraction..1
            // range and S the full cruiseFraction..0.12 range.
            float maxSpd = Boosting ? tuning.boostMaxSpeed : tuning.maxSpeed;
            float frac = _in.Throttle >= 0f
                ? Mathf.Lerp(cruiseFraction, 1f, _in.Throttle)
                : Mathf.Lerp(cruiseFraction, 0.05f, -_in.Throttle);
            float speedCmd = frac * maxSpd;

            Vector3 want = nose * speedCmd;
            // gentle collective trim + a touch of sink when flying level hands-off
            float levelness = 1f - Mathf.Clamp01(Mathf.Abs(pitchCmd) / 12f);
            want += Vector3.up * (_in.ClimbTrim * tuning.climbAccel * 0.7f
                                  - levelness * tuning.gravity * 0.18f);

            // --- soft floor -----------------------------------------
            if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
                                out var ground, 9f, Ironfield.Core.GameLayers.EnvironmentMask,
                                QueryTriggerInteraction.Ignore))
            {
                float clearance = transform.position.y - ground.point.y;
                if (clearance < 5f && want.y < 0f)
                    want.y = Mathf.Lerp(0.8f, want.y, Mathf.Clamp01(clearance / 5f));
            }

            // --- map boundary: fly far enough from the launch/combat area and
            // an inward push kicks in (proportional to how far past the soft
            // radius), well before the hard radius that a drone can never
            // actually cross. HudController reads IsNearBoundary for the
            // warning text. Centre/radii default to "effectively off" so this
            // is inert unless MissionManager configures it from the real
            // terrain bounds on spawn.
            Vector2 flatPos = new(transform.position.x, transform.position.z);
            Vector2 fromCentre = flatPos - boundaryCentre;
            float distFromCentre = fromCentre.magnitude;
            IsNearBoundary = distFromCentre > boundarySoftRadius;
            if (IsNearBoundary && distFromCentre > 0.01f)
            {
                Vector2 inward = -fromCentre / distFromCentre;
                float overshoot = Mathf.Clamp01(Mathf.InverseLerp(boundarySoftRadius, boundaryHardRadius, distFromCentre));
                want += new Vector3(inward.x, 0f, inward.y) * (overshoot * tuning.boostMaxSpeed * 1.3f);
            }

            Vector3 v = _rb.linearVelocity;
            float responsiveness = 1f - Mathf.Exp(-tuning.linearDrag * 3.2f * dt);
            v = Vector3.Lerp(v, want, responsiveness);
            if (v.magnitude > tuning.boostMaxSpeed) v = v.normalized * tuning.boostMaxSpeed;

            // hard wall: never actually let the drone cross it, no matter how
            // hard boost/steering fight the push above.
            if (distFromCentre > boundaryHardRadius && distFromCentre > 0.01f)
            {
                Vector2 outward = fromCentre / distFromCentre;
                Vector2 clampedFlat = boundaryCentre + outward * boundaryHardRadius;
                _rb.position = new Vector3(clampedFlat.x, _rb.position.y, clampedFlat.y);
                Vector3 outward3 = new(outward.x, 0f, outward.y);
                float outComp = Vector3.Dot(v, outward3);
                if (outComp > 0f) v -= outward3 * outComp;
            }

            _rb.linearVelocity = v;
            _speed = new Vector2(v.x, v.z).magnitude;

            // --- visual attitude: bank into the turn, nose follows pitch cmd
            float targetBank = -_aim.x * 34f - _in.Roll * 22f;
            float targetPitch = pitchCmd * 0.85f + _in.Throttle * 4f;
            float kk = 1f - Mathf.Exp(-tuning.angularDamp * dt);
            _bank = Mathf.Lerp(_bank, targetBank, kk);
            _pitchVis = Mathf.Lerp(_pitchVis, targetPitch, kk);
            _rb.MoveRotation(Quaternion.Euler(_pitchVis, _heading, _bank));
        }

        /// <summary>
        /// Converts a world point into the same (yaw-rate, pitch) aim space the
        /// mouse normally drives, and full-sends the throttle. Proportional
        /// control on heading error (aim.x integrates into heading over time —
        /// see FixedUpdate) and a direct solve on pitch (aim.y maps straight to
        /// pitch offset), smoothed so the takeover isn't a snap.
        /// </summary>
        void UpdateAutopilotAim(float dt)
        {
            Vector3 to = AutopilotTarget.Value - transform.position;
            Vector3 toFlat = Vector3.ProjectOnPlane(to, Vector3.up);
            float bearing = Vector3.SignedAngle(Vector3.forward, toFlat, Vector3.up);
            float headingErr = Mathf.DeltaAngle(_heading, bearing);
            float aimX = Mathf.Clamp(headingErr / 25f, -1f, 1f);

            float dist = toFlat.magnitude;
            // Floor the horizontal distance well above zero, not just above
            // divide-by-zero: as a close-range dive shrinks toFlat toward 0,
            // atan2's angle races to +/-90 deg for any nonzero height gap,
            // saturating the pitch command hard-over and — because that command
            // pulls the nose away from level, which only *shrinks* the horizontal
            // gap further relative to the height gap — never recovering. Verified
            // as the cause of a real runaway climb-away during autopilot testing
            // (drone climbed from 35m to 780m+ and never re-engaged). A floor on
            // the order of the warhead engagement range keeps commanded elevation
            // bounded during the terminal approach instead of blowing up right
            // when precision matters most.
            float elevation = Mathf.Atan2(to.y, Mathf.Max(10f, dist)) * Mathf.Rad2Deg;
            // NOT negated: positive aim.y climbs (see FixedUpdate: pitchCmd =
            // -aim.y * pitchRange, and Quaternion.Euler's +X convention pitches
            // the nose down for positive pitchCmd — so positive aim.y -> negative
            // pitchCmd -> nose up). A target below has to.y < 0 -> elevation < 0,
            // and we want that to DESCEND (negative aim.y), so aimY must carry
            // elevation's own sign, not flip it. The flipped version silently
            // climbed away from every target below the drone (i.e. almost always,
            // since the drone launches on high ground) instead of diving on it —
            // this is what the runaway-climb bug above actually was; the distance
            // floor was a real secondary issue but not the root cause. An earlier,
            // looser test (large yaw + small pitch offset) didn't catch the wrong
            // sign because yaw alone was enough to pass its alignment check —
            // AutopilotTests now also covers a pitch-dominant case specifically.
            float aimY = Mathf.Clamp(elevation / pitchRange, -1f, 1f);

            // --- obstacle avoidance ----------------------------------
            // Autopilot used to just aim straight at the target with zero
            // awareness of what's in between — the map is scattered with
            // ~2000+ trees and utility poles (ScatterVegetation/
            // ScenePropsPass), dense enough that a long straight run at
            // speed would eventually clip one, and UpdateAutopilotAim kept
            // commanding "more forward" into the same obstacle every frame
            // afterward since it never noticed. Cast a short forward+side
            // probe; a blocked centre nudges pitch up (climb over — works
            // for most single trees/poles, which are short relative to how
            // fast the drone can gain altitude) and yaws toward whichever
            // side is actually clear.
            // Skipped inside the terminal engagement radius (DiveAssist fires
            // the warhead at 6.5m — 20m gives real margin): the target itself
            // is normally sitting on/near the ground, on the same Environment
            // layer the avoidance probe checks, so a close-range dive's own
            // forward ray legitimately hits terrain right around the target
            // it's *supposed* to be diving into. Without this gate, avoidance
            // fought (and could outright cancel) every real terminal dive —
            // caught by Auto_attack_setting_finishes_a_committed_dive_on_its_own
            // regressing while tuning this.
            if (dist > 20f)
            {
                (float avoidX, float avoidY) = ComputeAvoidance();
                aimX = Mathf.Clamp(aimX + avoidX, -1f, 1f);
                // Climb overrides (takes the max), doesn't just add: the
                // target itself is often at or below the drone's altitude, so
                // its own aimY is frequently negative (dive) — summing would
                // let that partially cancel the escape climb right when it
                // matters most. The probe already returns 0 when clear, so
                // this only ever raises aimY while something is actually ahead.
                if (avoidY > 0f) aimY = Mathf.Max(aimY, avoidY);
            }

            // Fallback safety net: if the drone is still barely moving after
            // sustained autopilot control despite full throttle (physically
            // wedged against something the probe above didn't fully clear —
            // e.g. approaching an obstacle edge-on, outside the probe's
            // narrow cone), force a hard climb-and-turn escape regardless of
            // what the raycasts currently say.
            _stuckTimer = _speed < 3f ? _stuckTimer + dt : 0f;
            if (_stuckTimer > 0.6f)
            {
                aimY = 1f;
                aimX = Mathf.Clamp(aimX + (aimX >= 0f ? 1f : -1f), -1f, 1f);
            }

            _aim = Vector2.Lerp(_aim, new Vector2(aimX, aimY), 1f - Mathf.Exp(-8f * dt));
            _in.Throttle = 1f;
        }

        /// <summary>Forward/side probes on the Environment layer (terrain,
        /// trees, poles, buildings — never vehicles, so this never steers the
        /// drone away from its actual target). Returns an additive (yaw, pitch)
        /// nudge in the same -1..1 aim space UpdateAutopilotAim works in,
        /// scaled by how close the obstacle is — (0,0) when the way is clear.</summary>
        (float x, float y) ComputeAvoidance()
        {
            int envMask = Ironfield.Core.GameLayers.EnvironmentMask;
            // Long lookahead relative to how fast the drone can actually gain
            // altitude — a weak/late response reliably meant clipping the
            // obstacle before finishing the climb (verified: an earlier,
            // shorter-lookahead version of this let the drone collide hard
            // enough with a test obstacle to die from the impact damage,
            // exactly the outcome this is supposed to prevent).
            float lookAhead = Mathf.Clamp(18f + _speed * 1.8f, 26f, 70f);
            Vector3 origin = transform.position;
            // Level (heading-only), NOT transform.forward: the target is
            // usually on/near the ground, so a real dive legitimately points
            // the nose down toward terrain near it — probing along the actual
            // 3D nose vector kept reading that as "obstacle ahead" and firing
            // a climb that fought every descent, not just real trees/poles.
            // A level probe only catches things that actually stick up into
            // the flight corridor at the drone's current altitude.
            Vector3 fwd = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;

            if (!Physics.Raycast(origin, fwd, out var centreHit, lookAhead, envMask, QueryTriggerInteraction.Ignore))
                return (0f, 0f);

            // floored, not just proportional to distance: a just-barely-in-
            // range obstacle still gets a real response instead of a token
            // nudge that's too weak to actually climb clear in time.
            float urgency = Mathf.Clamp(1.2f - centreHit.distance / lookAhead, 0.6f, 1f);

            Vector3 right = Quaternion.Euler(0f, _heading, 0f) * Vector3.right;   // level, same reasoning as fwd above
            bool leftBlocked = Physics.Raycast(origin, (fwd - right * 0.6f).normalized,
                lookAhead * 0.7f, envMask, QueryTriggerInteraction.Ignore);
            bool rightBlocked = Physics.Raycast(origin, (fwd + right * 0.6f).normalized,
                lookAhead * 0.7f, envMask, QueryTriggerInteraction.Ignore);

            float sideSteer = 0f;
            if (leftBlocked && !rightBlocked) sideSteer = 1f;         // swerve right
            else if (rightBlocked && !leftBlocked) sideSteer = -1f;   // swerve left

            // climbing over is the default response (works for the common
            // case — a single tree/pole shorter than the drone can climb in
            // the time it takes to reach it); side-step only kicks in once a
            // clear side has actually been found above.
            return (sideSteer * urgency, urgency);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_health == null) return;
            float impact = c.relativeVelocity.magnitude;
            if (impact > 14f && ((1 << c.gameObject.layer) & Ironfield.Core.GameLayers.EnvironmentMask) != 0)
            {
                _health.ApplyDamage(new DamageInfo(impact * 1.5f, DamageType.Collision,
                    c.GetContact(0).point, -c.GetContact(0).normal, c.gameObject));
            }
        }
    }
}
