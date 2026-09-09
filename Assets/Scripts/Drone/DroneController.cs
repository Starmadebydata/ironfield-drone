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
            _health = GetComponent<HealthComponent>();
            if (tuning == null)
            {
                Debug.LogWarning("[Drone] No DroneTuning assigned; using defaults.", this);
                tuning = ScriptableObject.CreateInstance<DroneTuning>();
            }
        }

        void Update()
        {
            _in = ControlsEnabled && (_health == null || !_health.IsDead)
                ? DroneInput.Read(mouseSensitivity)
                : default;

            if (_in.FirePressed) FireRequested?.Invoke();
            if (_in.RecallPressed) RecallRequested?.Invoke();

            float spin = (_speed * 40f + 800f + (Boosting ? 1500f : 0f));
            if (propSpinners != null)
                foreach (var p in propSpinners)
                    if (p) p.Rotate(Vector3.up, spin * Time.deltaTime, Space.Self);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Boosting = _in.Boost && _in.Throttle > -0.2f;

            // --- rotation --------------------------------------------------
            Vector3 torque = new Vector3(
                _in.Pitch * tuning.pitchRate,
                _in.Yaw * tuning.yawRate,
                -_in.Roll * tuning.rollRate) * Mathf.Deg2Rad;

            _rb.angularVelocity = Vector3.Lerp(
                _rb.angularVelocity,
                transform.TransformDirection(torque),
                1f - Mathf.Exp(-tuning.angularDamp * dt));

            // gentle auto-level of roll & pitch when the stick is near centre
            if (tuning.selfLevel > 0f && Mathf.Abs(_in.Pitch) < 0.15f && Mathf.Abs(_in.Roll) < 0.15f)
            {
                Quaternion flat = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, flat,
                    tuning.selfLevel * dt * 2.5f));
            }

            // --- translation --------------------------------------------
            float maxSpd = Boosting ? tuning.boostMaxSpeed : tuning.maxSpeed;
            float accel = tuning.thrustAccel * (Boosting ? tuning.boostMultiplier : 1f);

            Vector3 v = _rb.linearVelocity;
            // forward push follows where the nose points
            v += transform.forward * (accel * Mathf.Max(0f, -_in.Pitch * 0.5f + 0.5f) * dt) * 0.35f;
            // climb / descend from throttle axis, and fight gravity
            v += Vector3.up * ((_in.Throttle * tuning.climbAccel) - tuning.gravity + tuning.gravity * Mathf.Clamp01(_in.Throttle + 0.5f)) * dt;
            // strafe a little with roll for responsiveness
            v += transform.right * (_in.Roll * accel * 0.15f * dt);

            // drag
            v -= v * (tuning.linearDrag * dt);

            if (v.magnitude > maxSpd) v = v.normalized * maxSpd;
            _rb.linearVelocity = v;
            _speed = v.magnitude;
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
