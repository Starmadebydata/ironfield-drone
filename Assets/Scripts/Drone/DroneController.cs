using Ironfield.Combat;
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

        /// <summary>Test hook: (throttle, aimX, aimY, roll) forced when set.</summary>
        public bool useDebugInput;
        public Vector4 debugInput;

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

            // --- integrate the mouse reticle ------------------------------
            float sens = aimSensitivity * (Precision ? precisionScale : 1f);
            _aim += _in.AimDelta * sens;
            if (!useDebugInput)
            {
                // spring back toward centre so hands-off = fly straight
                _aim = Vector2.Lerp(_aim, Vector2.zero, 1f - Mathf.Exp(-aimReturn * dt));
            }
            if (_aim.magnitude > 1f) _aim = _aim.normalized;

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
