using UnityEngine;

namespace Ironfield.Core
{
    /// <summary>Central place for the layer / tag names used across the game.</summary>
    public static class GameLayers
    {
        public const string DroneName = "Drone";
        public const string VehicleName = "Vehicle";
        public const string EnvironmentName = "Environment";
        public const string ProjectileName = "Projectile";

        public static int Drone => LayerMask.NameToLayer(DroneName);
        public static int Vehicle => LayerMask.NameToLayer(VehicleName);
        public static int Environment => LayerMask.NameToLayer(EnvironmentName);
        public static int Projectile => LayerMask.NameToLayer(ProjectileName);

        public static LayerMask VehicleMask => 1 << Vehicle;
        public static LayerMask EnvironmentMask => 1 << Environment;

        /// <summary>Everything the targeting line-of-sight ray should treat as solid.</summary>
        public static LayerMask SightBlockers => (1 << Environment) | (1 << Vehicle) | (1 << Default);

        static int Default => LayerMask.NameToLayer("Default");
    }

    public static class GameTags
    {
        public const string Drone = "Drone";
        public const string Vehicle = "Vehicle";
        public const string LaunchPoint = "LaunchPoint";
    }
}
