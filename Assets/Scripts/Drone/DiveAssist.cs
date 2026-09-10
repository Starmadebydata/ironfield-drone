using Ironfield.Targeting;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Gentle terminal guidance: while there is a hard lock and the drone is
    /// diving toward that target (nose roughly on it, closing fast), bend the
    /// velocity a little toward the target's aim point so kamikaze runs connect.
    /// Capped hard so it assists rather than auto-aims.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DiveAssist : MonoBehaviour
    {
        public TargetingSystem targeting;
        [Tooltip("Max turn the assist can add, deg/s.")]
        public float maxCorrectionDeg = 35f;
        [Tooltip("Only assist within this cone of the current heading.")]
        public float engageConeDeg = 32f;
        public float minSpeed = 10f;
        public float maxRange = 120f;

        Rigidbody _rb;

        void Awake() => _rb = GetComponent<Rigidbody>();

        void FixedUpdate()
        {
            if (targeting == null || !targeting.HasHardLock) return;
            var t = targeting.CurrentTarget;
            if (t == null || t.IsDestroyed) return;

            Vector3 v = _rb.linearVelocity;
            float speed = v.magnitude;
            if (speed < minSpeed) return;

            Vector3 to = t.AimPoint - transform.position;
            float dist = to.magnitude;
            if (dist > maxRange || dist < 1f) return;

            Vector3 dirVel = v / speed;
            Vector3 dirTgt = to / dist;
            float ang = Vector3.Angle(dirVel, dirTgt);
            if (ang > engageConeDeg) return;

            // strengthen as you get closer
            float pull = Mathf.Lerp(0.15f, 1f, 1f - dist / maxRange);
            float maxStep = maxCorrectionDeg * pull * Mathf.Deg2Rad * Time.fixedDeltaTime;
            Vector3 newDir = Vector3.RotateTowards(dirVel, dirTgt, maxStep, 0f);
            _rb.linearVelocity = newDir * speed;
        }
    }
}
