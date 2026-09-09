using Ironfield.Combat;
using Ironfield.Core;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Single-use warhead. Detonates on contact with a vehicle, or with the
    /// ground/structure if armed and moving fast enough, or on manual trigger
    /// (Fire) when a target is close. Raises <see cref="Detonated"/> so the
    /// mission layer can spend a drone and respawn.
    /// </summary>
    [RequireComponent(typeof(DroneController))]
    public class DroneWarhead : MonoBehaviour
    {
        public Explosion explosionPrefab;
        public float armAfterSeconds = 0.75f;
        public float groundArmSpeed = 12f;
        public float manualTriggerRange = 7f;

        public System.Action<Vector3> Detonated;

        DroneController _drone;
        Rigidbody _rb;
        float _aliveTime;
        bool _spent;

        void Awake()
        {
            _drone = GetComponent<DroneController>();
            _rb = GetComponent<Rigidbody>();
            _drone.FireRequested += OnFire;
        }

        void OnDestroy()
        {
            if (_drone != null) _drone.FireRequested -= OnFire;
        }

        void Update() => _aliveTime += Time.deltaTime;

        bool Armed => _aliveTime >= armAfterSeconds;

        void OnFire()
        {
            if (_spent || !Armed) return;
            // Command detonation if something killable is right in front / nearby.
            var hits = Physics.OverlapSphere(transform.position, manualTriggerRange,
                                             GameLayers.VehicleMask, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0) Detonate(transform.position);
        }

        void OnCollisionEnter(Collision c)
        {
            if (_spent || !Armed) return;
            int layer = c.gameObject.layer;
            bool isVehicle = ((1 << layer) & GameLayers.VehicleMask) != 0;
            bool isWorld = ((1 << layer) & GameLayers.EnvironmentMask) != 0;
            float speed = _rb ? _rb.linearVelocity.magnitude : c.relativeVelocity.magnitude;

            if (isVehicle || (isWorld && speed >= groundArmSpeed))
                Detonate(c.GetContact(0).point);
        }

        void Detonate(Vector3 at)
        {
            if (_spent) return;
            _spent = true;

            var tuning = _drone.tuning;
            if (explosionPrefab != null)
            {
                var ex = Instantiate(explosionPrefab, at, Quaternion.identity);
                ex.damage = tuning != null ? tuning.warheadDamage : 650f;
                ex.radius = tuning != null ? tuning.warheadRadius : 5.5f;
                ex.affects = GameLayers.VehicleMask | GameLayers.EnvironmentMask;
                ex.Fire(gameObject);
            }
            else
            {
                // fallback: direct blast with no FX
                var go = new GameObject("Blast");
                go.transform.position = at;
                var ex = go.AddComponent<Explosion>();
                ex.damage = tuning != null ? tuning.warheadDamage : 650f;
                ex.radius = tuning != null ? tuning.warheadRadius : 5.5f;
                ex.affects = GameLayers.VehicleMask | GameLayers.EnvironmentMask;
                ex.Fire(gameObject);
            }

            Detonated?.Invoke(at);
            Destroy(gameObject);
        }
    }
}
