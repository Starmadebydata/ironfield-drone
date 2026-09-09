using Ironfield.Combat;
using UnityEngine;

namespace Ironfield.Vehicles
{
    public enum VehicleClass { Tank, IFV, Truck }

    /// <summary>
    /// A targetable enemy vehicle. Wraps a <see cref="HealthComponent"/>, tells
    /// the registry when it lives and dies, and on death hands off to
    /// <see cref="Wreck"/> for the visual swap.
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class Vehicle : MonoBehaviour
    {
        public VehicleClass vehicleClass = VehicleClass.Tank;
        [Tooltip("Where the reticle should sit / where turrets aim. Falls back to renderer bounds centre.")]
        public Transform aimPoint;
        [Tooltip("Optional label for the HUD.")]
        public string displayName = "Armoured target";

        public HealthComponent Health { get; private set; }
        public bool IsDestroyed { get; private set; }

        public System.Action<Vehicle, DamageInfo> Destroyed;

        Vector3 _fallbackAim;

        public Vector3 AimPoint => aimPoint ? aimPoint.position : transform.TransformPoint(_fallbackAim);

        void Awake()
        {
            Health = GetComponent<HealthComponent>();
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                _fallbackAim = transform.InverseTransformPoint(b.center);
            }
            else _fallbackAim = Vector3.up;
        }

        void OnEnable()
        {
            VehicleRegistry.Register(this);
            Health.Died += OnDied;
        }

        void OnDisable()
        {
            Health.Died -= OnDied;
            VehicleRegistry.Unregister(this);
        }

        void OnDied(DamageInfo info)
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            VehicleRegistry.MarkDestroyed(this);

            foreach (var ai in GetComponentsInChildren<IVehicleSystem>())
                ai.OnVehicleDestroyed();

            GetComponent<Wreck>()?.Trigger(info);
            Destroyed?.Invoke(this, info);
        }
    }

    /// <summary>Implemented by AI / turret parts so they can shut down on death.</summary>
    public interface IVehicleSystem
    {
        void OnVehicleDestroyed();
    }
}
