using UnityEngine;

namespace Ironfield.Combat
{
    public enum DamageType
    {
        Kinetic,     // turret rounds hitting the drone
        Explosive,   // warhead / blast
        Collision,   // ramming
    }

    /// <summary>One hit's worth of damage, passed to <see cref="HealthComponent"/>.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, DamageType type, Vector3 point,
                          Vector3 direction, GameObject source)
        {
            Amount = amount;
            Type = type;
            Point = point;
            Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
            Source = source;
        }

        public static DamageInfo Explosive(float amount, Vector3 point, GameObject source)
            => new DamageInfo(amount, DamageType.Explosive, point, Vector3.zero, source);
    }
}
