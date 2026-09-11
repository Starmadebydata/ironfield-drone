using Ironfield.Core;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Terminal guidance while diving on a hard-locked target the drone is
    /// already roughly pointed at (nose within <see cref="engageConeDeg"/>,
    /// closing fast). Both modes below share that same gate, so engagement only
    /// ever kicks in once the player has clearly committed to the dive — it
    /// never yanks the drone off a course the player hasn't already chosen.
    ///
    ///   - Default (Ironfield.Core.GameSettings.AutoAttack off): a capped
    ///     velocity nudge toward the target's aim point — assist, not autopilot.
    ///   - Auto-attack on: full autopilot. Hands aim + throttle to
    ///     DroneController.AutopilotTarget and fires the warhead once in range,
    ///     so a committed dive reliably finishes itself without further input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DiveAssist : MonoBehaviour
    {
        public TargetingSystem targeting;
        [Tooltip("Max turn the non-autopilot assist can add, deg/s.")]
        public float maxCorrectionDeg = 35f;
        [Tooltip("Only engage within this cone of the current heading.")]
        public float engageConeDeg = 32f;
        public float minSpeed = 10f;
        public float maxRange = 120f;
        [Tooltip("Auto-attack fires the warhead once the target is within this range.")]
        public float autoFireRange = 6.5f;

        Rigidbody _rb;
        DroneController _drone;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _drone = GetComponent<DroneController>();
        }

        void FixedUpdate()
        {
            var target = Engaged(out float dist);
            if (target == null)
            {
                if (_drone != null) _drone.AutopilotTarget = null;
                return;
            }

            if (GameSettings.AutoAttack && _drone != null)
            {
                _drone.AutopilotTarget = target.AimPoint;
                if (dist <= autoFireRange) _drone.RequestFire();
            }
            else
            {
                if (_drone != null) _drone.AutopilotTarget = null;
                Vector3 v = _rb.linearVelocity;
                float speed = v.magnitude;
                Vector3 to = target.AimPoint - transform.position;

                float pull = Mathf.Lerp(0.15f, 1f, 1f - dist / maxRange);
                float maxStep = maxCorrectionDeg * pull * Mathf.Deg2Rad * Time.fixedDeltaTime;
                Vector3 newDir = Vector3.RotateTowards(v / speed, to.normalized, maxStep, 0f);
                _rb.linearVelocity = newDir * speed;
            }
        }

        /// <summary>Shared engagement gate: hard lock on a live target, moving
        /// fast enough, in range, and already roughly pointed at it.</summary>
        Vehicle Engaged(out float dist)
        {
            dist = 0f;
            if (targeting == null || !targeting.HasHardLock) return null;
            var t = targeting.CurrentTarget;
            if (t == null || t.IsDestroyed) return null;

            Vector3 v = _rb.linearVelocity;
            float speed = v.magnitude;
            if (speed < minSpeed) return null;

            Vector3 to = t.AimPoint - transform.position;
            dist = to.magnitude;
            if (dist > maxRange || dist < 1f) return null;

            float ang = Vector3.Angle(v / speed, to / dist);
            if (ang > engageConeDeg) return null;

            return t;
        }
    }
}
