using Ironfield.Combat;
using UnityEngine;

namespace Ironfield.Vehicles
{
    /// <summary>
    /// Follows a shared list of waypoints at a fixed speed, keeping a gap to the
    /// vehicle ahead. When a convoy member dies the survivors briefly halt then
    /// press on faster (simple morale/urgency reaction).
    /// </summary>
    [RequireComponent(typeof(Vehicle))]
    public class VehicleConvoyAI : MonoBehaviour, IVehicleSystem
    {
        public Transform[] waypoints;
        public float speed = 6f;
        public float turnRateDeg = 45f;
        public float waypointTolerance = 3f;
        [Tooltip("Optional: the vehicle directly ahead in the column.")]
        public Vehicle vehicleAhead;
        public float followGap = 9f;

        [Header("Reaction")]
        public float haltOnAllyLostSeconds = 1.5f;
        public float panicSpeedMultiplier = 1.7f;

        int _wp;
        float _haltTimer;
        float _speedMul = 1f;
        bool _dead;
        Rigidbody _rb;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            SnapToGround();
        }

        void FixedUpdate()
        {
            if (_dead || waypoints == null || waypoints.Length == 0) return;

            if (_haltTimer > 0f)
            {
                _haltTimer -= Time.fixedDeltaTime;
                return;
            }

            Transform tgt = waypoints[_wp];
            if (tgt == null) return;

            Vector3 flatPos = transform.position;
            Vector3 flatTgt = new Vector3(tgt.position.x, flatPos.y, tgt.position.z);
            Vector3 toTgt = flatTgt - flatPos;

            if (toTgt.magnitude <= waypointTolerance)
            {
                if (_wp >= waypoints.Length - 1)
                {
                    // reached the far end of the road — broke through
                    _dead = true;
                    enabled = false;
                    var veh = GetComponent<Vehicle>();
                    if (veh != null) VehicleRegistry.MarkEscaped(veh);
                    Destroy(gameObject);
                    return;
                }
                _wp++;
                return;
            }

            // keep distance to the vehicle ahead
            float move = speed * _speedMul;
            if (vehicleAhead != null && !vehicleAhead.IsDestroyed)
            {
                float gap = Vector3.Distance(transform.position, vehicleAhead.transform.position);
                if (gap < followGap) move *= Mathf.Clamp01(gap / followGap);
            }

            Quaternion want = Quaternion.LookRotation(toTgt.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want,
                turnRateDeg * Time.fixedDeltaTime);

            Vector3 step = transform.forward * (move * Time.fixedDeltaTime);
            if (_rb != null) _rb.MovePosition(_rb.position + step);
            else transform.position += step;

            SnapToGround();
        }

        void SnapToGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down,
                                out var hit, 20f, Ironfield.Core.GameLayers.EnvironmentMask))
            {
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(p.y, hit.point.y, 0.5f);
                transform.position = p;
            }
        }

        public void ReactToAllyLost()
        {
            if (_dead) return;
            _haltTimer = haltOnAllyLostSeconds * Random.Range(0.5f, 1f);
            _speedMul = panicSpeedMultiplier;
        }

        public void OnVehicleDestroyed()
        {
            _dead = true;
            enabled = false;
            // tell the rest of the column
            foreach (var other in FindObjectsByType<VehicleConvoyAI>(FindObjectsSortMode.None))
                if (other != this) other.ReactToAllyLost();
        }
    }
}
