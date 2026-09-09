using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>Flight-model numbers, kept as data so balancing needs no recompile.</summary>
    [CreateAssetMenu(menuName = "Ironfield/Drone Tuning", fileName = "DroneTuning")]
    public class DroneTuning : ScriptableObject
    {
        [Header("Translation")]
        public float thrustAccel = 22f;         // m/s^2 at full stick
        public float boostMultiplier = 2.1f;
        public float maxSpeed = 32f;
        public float boostMaxSpeed = 55f;
        public float linearDrag = 1.1f;

        [Header("Rotation (deg/s at full stick)")]
        public float pitchRate = 160f;
        public float rollRate = 220f;
        public float yawRate = 120f;
        public float angularDamp = 6f;

        [Header("Assist")]
        [Tooltip("0 = pure acro, 1 = auto-levels roll/pitch when sticks are centred.")]
        [Range(0f, 1f)] public float selfLevel = 0.35f;
        [Tooltip("Downward accel always applied, m/s^2. Lift cancels it at mid throttle.")]
        public float gravity = 9.81f;
        [Tooltip("Vertical accel available from the throttle axis.")]
        public float climbAccel = 18f;

        [Header("Durability")]
        public float maxHealth = 30f;
        public float warheadDamage = 650f;
        public float warheadRadius = 5.5f;
    }
}
