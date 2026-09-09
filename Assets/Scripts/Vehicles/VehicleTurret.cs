using Ironfield.Combat;
using Ironfield.Core;
using UnityEngine;

namespace Ironfield.Vehicles
{
    /// <summary>
    /// Optional return fire. When the drone is in range and visible the turret
    /// tracks it and fires tracer bursts with spread; each hit chips the drone's
    /// small health pool so loitering in the open gets you shot down. Disabled
    /// by default via <see cref="enabled"/> so it can be toggled per build.
    /// </summary>
    public class VehicleTurret : MonoBehaviour, IVehicleSystem
    {
        public Transform yaw;         // rotates around local up
        public Transform pitch;       // elevates
        public Transform muzzle;
        public LineRenderer tracerPrefab;

        [Header("Engagement")]
        public float range = 180f;
        public float traverseDeg = 90f;
        public float fireInterval = 0.18f;
        public int burst = 4;
        public float burstPause = 1.4f;
        public float spreadDeg = 2.5f;
        public float damagePerHit = 6f;

        Transform _drone;
        float _fireTimer;
        int _inBurst;
        float _pause;
        bool _dead;

        void Start()
        {
            var d = GameObject.FindGameObjectWithTag(GameTags.Drone);
            if (d) _drone = d.transform;
        }

        void Update()
        {
            if (_dead) return;
            if (_drone == null)
            {
                var d = GameObject.FindGameObjectWithTag(GameTags.Drone);
                if (d) _drone = d.transform; else return;
            }

            Vector3 toDrone = _drone.position - (muzzle ? muzzle.position : transform.position);
            float dist = toDrone.magnitude;
            bool visible = dist <= range && !Physics.Raycast(
                (muzzle ? muzzle.position : transform.position), toDrone.normalized,
                dist * 0.95f, GameLayers.EnvironmentMask, QueryTriggerInteraction.Ignore);

            if (!visible) { _inBurst = 0; return; }

            Track(toDrone);

            _pause -= Time.deltaTime;
            if (_pause > 0f) return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = fireInterval;
                Shoot(toDrone.normalized);
                if (++_inBurst >= burst) { _inBurst = 0; _pause = burstPause; }
            }
        }

        void Track(Vector3 toDrone)
        {
            if (yaw)
            {
                Vector3 flat = Vector3.ProjectOnPlane(toDrone, yaw.up);
                if (flat.sqrMagnitude > 0.01f)
                {
                    Quaternion want = Quaternion.LookRotation(flat, yaw.up);
                    yaw.rotation = Quaternion.RotateTowards(yaw.rotation, want, 120f * Time.deltaTime);
                }
            }
            if (pitch)
            {
                float ang = Mathf.Clamp(
                    Vector3.SignedAngle(Vector3.ProjectOnPlane(toDrone, pitch.right), toDrone, pitch.right),
                    -10f, 60f);
                pitch.localRotation = Quaternion.Slerp(pitch.localRotation,
                    Quaternion.Euler(-ang, 0f, 0f), 6f * Time.deltaTime);
            }
        }

        void Shoot(Vector3 dir)
        {
            Vector3 origin = muzzle ? muzzle.position : transform.position + Vector3.up;
            Vector3 spread = Quaternion.Euler(
                Random.Range(-spreadDeg, spreadDeg),
                Random.Range(-spreadDeg, spreadDeg), 0f) * dir;

            Vector3 end = origin + spread * range;
            if (Physics.Raycast(origin, spread, out var hit, range,
                                GameLayers.SightBlockers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var health = hit.collider.GetComponentInParent<HealthComponent>();
                if (health != null && hit.collider.GetComponentInParent<Vehicle>() == null)
                {
                    health.ApplyDamage(new DamageInfo(damagePerHit, DamageType.Kinetic,
                        hit.point, spread, gameObject));
                }
            }

            if (tracerPrefab)
            {
                var tr = Instantiate(tracerPrefab);
                tr.positionCount = 2;
                tr.SetPosition(0, origin);
                tr.SetPosition(1, end);
                Destroy(tr.gameObject, 0.05f);
            }
        }

        public void OnVehicleDestroyed()
        {
            _dead = true;
            enabled = false;
        }
    }
}
