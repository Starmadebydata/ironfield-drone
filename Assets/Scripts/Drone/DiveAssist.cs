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
        bool _wasEngaged;

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
        /// fast enough, in range, and already roughly pointed at it. Once
        /// actually committed, re-checks with a looser speed/cone tolerance
        /// (hysteresis) instead of the strict entry gate — see _wasEngaged.</summary>
        Vehicle Engaged(out float dist)
        {
            dist = 0f;
            if (targeting == null || !targeting.HasHardLock) { _wasEngaged = false; return null; }
            var t = targeting.CurrentTarget;
            if (t == null || t.IsDestroyed) { _wasEngaged = false; return null; }

            Vector3 to = t.AimPoint - transform.position;
            dist = to.magnitude;
            if (dist > maxRange || dist < 1f) { _wasEngaged = false; return null; }

            // Already within kill range: fire regardless of current speed or
            // heading. Real bug this fixed — obstacle avoidance/terrain/the
            // drone's own flight model can bleed speed off right at the end of
            // a committed dive; the old minSpeed check below ran unconditionally
            // and would silently disengage a drone sitting right on top of its
            // target just because it had slowed down, stranding it there
            // instead of detonating (found while testing an unrelated change —
            // see Auto_attack_setting_finishes_a_committed_dive_on_its_own).
            if (dist <= autoFireRange) { _wasEngaged = true; return t; }

            Vector3 v = _rb.linearVelocity;
            float speed = v.magnitude;
            // Hysteresis: the strict thresholds are for deciding whether to
            // COMMIT to a dive in the first place (so it only ever kicks in on
            // a course the player already chose). Once genuinely committed,
            // a moving convoy target or a brief speed dip from the flight
            // model/avoidance shouldn't be enough to drop it and strand the
            // drone mid-approach — that flicker (engage this frame, drop the
            // next, re-engage a few frames later) is what actually produced
            // the stall this whole method's comment history is about.
            float speedGate = _wasEngaged ? minSpeed * 0.35f : minSpeed;
            float coneGate = _wasEngaged ? engageConeDeg * 1.75f : engageConeDeg;

            if (speed < speedGate) { _wasEngaged = false; return null; }

            float ang = Vector3.Angle(v / speed, to / dist);
            if (ang > coneGate) { _wasEngaged = false; return null; }

            _wasEngaged = true;
            return t;
        }
    }
}
