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
        [Tooltip("Idle forward speed as a fraction of max, with the throttle centred.")]
        [Range(0.1f, 1f)] public float cruiseFraction = 0.42f;

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

        public System.Action FireRequested;
        public System.Action RecallRequested;

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
            float maxSpd = Boosting ? tuning.boostMaxSpeed : tuning.maxSpeed;
            float frac = Mathf.Lerp(cruiseFraction, 1f, Mathf.Clamp01(0.5f + 0.5f * _in.Throttle));
            if (_in.Throttle < 0f) frac = Mathf.Lerp(cruiseFraction, 0.12f, -_in.Throttle);  // S brakes
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

            Vector3 v = _rb.linearVelocity;
            float responsiveness = 1f - Mathf.Exp(-tuning.linearDrag * 3.2f * dt);
            v = Vector3.Lerp(v, want, responsiveness);
            if (v.magnitude > tuning.boostMaxSpeed) v = v.normalized * tuning.boostMaxSpeed;
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

            _aim = Vector2.Lerp(_aim, new Vector2(aimX, aimY), 1f - Mathf.Exp(-8f * dt));
            _in.Throttle = 1f;
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
