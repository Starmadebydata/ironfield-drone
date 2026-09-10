using Ironfield.Combat;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Arcade quad flight: stick torques for pitch/roll/yaw, a throttle axis for
    /// climb, body-forward thrust from pitch attitude, light self-levelling, and
    /// a speed clamp. Rigidbody based but not a real aero sim.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DroneController : MonoBehaviour
    {
        public DroneTuning tuning;
        [Tooltip("Spinning prop meshes, purely cosmetic.")]
        public Transform[] propSpinners;
        public float propSpinSpeed = 3200f;
        public float mouseSensitivity = 1f;

        [Header("Runtime state (read-only)")]
        [SerializeField] float _speed;
        public float Speed => _speed;
        public bool Boosting { get; private set; }
        public bool ControlsEnabled { get; set; } = true;

        /// <summary>Test hook: when set, overrides pilot input with (throttle, yaw, pitch, roll).</summary>
        public bool useDebugInput;
        public Vector4 debugInput;

        Rigidbody _rb;
        HealthComponent _health;
        DroneInput _in;

        public System.Action FireRequested;
        public System.Action RecallRequested;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;                 // handled manually via tuning.gravity
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.freezeRotation = true;              // rotation is driven by MoveRotation
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
            _in = ControlsEnabled && (_health == null || !_health.IsDead)
                ? DroneInput.Read(mouseSensitivity)
                : default;

            if (useDebugInput)
            {
                _in.Throttle = debugInput.x;
                _in.Yaw = debugInput.y;
                _in.Pitch = debugInput.z;
                _in.Roll = debugInput.w;
            }

            if (_in.FirePressed) FireRequested?.Invoke();
            if (_in.RecallPressed) RecallRequested?.Invoke();

            float spin = (_speed * 40f + 800f + (Boosting ? 1500f : 0f));
            if (propSpinners != null)
                foreach (var p in propSpinners)
                    if (p) p.Rotate(Vector3.up, spin * Time.deltaTime, Space.Self);
        }

        // smoothed visual attitude
        float _bank, _pitchVis, _heading;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Boosting = _in.Boost;

            // --- heading (yaw) ------------------------------------------
            _heading += _in.Yaw * tuning.yawRate * dt;
            Quaternion headingRot = Quaternion.Euler(0f, _heading, 0f);
            Vector3 fwd = headingRot * Vector3.forward;
            Vector3 right = headingRot * Vector3.right;

            // --- target horizontal velocity ---------------------------
            float maxSpd = Boosting ? tuning.boostMaxSpeed : tuning.maxSpeed;
            Vector3 wantHoriz = fwd * (_in.Throttle * maxSpd)
                              + right * (_in.Roll * maxSpd * 0.45f);

            // --- vertical: pitch stick drives climb rate, mild sink ----
            float wantVert = _in.Pitch * tuning.climbAccel
                             - (Mathf.Approximately(_in.Pitch, 0f) ? tuning.gravity * 0.25f : 0f);

            // soft floor: within 5 m of the ground, stop pushing further down
            if (Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down,
                                out var ground, 8f, Ironfield.Core.GameLayers.EnvironmentMask,
                                QueryTriggerInteraction.Ignore))
            {
                float clearance = transform.position.y - ground.point.y;
                if (clearance < 5f && wantVert < 0f)
                    wantVert = Mathf.Lerp(0.5f, wantVert, Mathf.Clamp01(clearance / 5f));
            }

            Vector3 want = new Vector3(wantHoriz.x, wantVert, wantHoriz.z);
            Vector3 v = _rb.linearVelocity;
            float responsiveness = 1f - Mathf.Exp(-tuning.linearDrag * 3f * dt);
            v = Vector3.Lerp(v, want, responsiveness);
            if (v.magnitude > tuning.boostMaxSpeed) v = v.normalized * tuning.boostMaxSpeed;
            _rb.linearVelocity = v;
            _speed = new Vector2(v.x, v.z).magnitude;

            // --- visual attitude: bank into turns / roll, pitch to climb
            float targetBank = -_in.Roll * 28f - _in.Yaw * 14f;
            float targetPitch = -_in.Pitch * 22f + _in.Throttle * 8f;
            float k = 1f - Mathf.Exp(-tuning.angularDamp * dt);
            _bank = Mathf.Lerp(_bank, targetBank, k);
            _pitchVis = Mathf.Lerp(_pitchVis, targetPitch, k);
            _rb.MoveRotation(Quaternion.Euler(_pitchVis, _heading, _bank));
        }

        void OnCollisionEnter(Collision c)
        {
            // Slamming into the world at speed hurts (warhead handles vehicles).
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
