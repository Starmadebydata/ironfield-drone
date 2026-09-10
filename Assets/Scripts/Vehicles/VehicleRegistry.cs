using System.Collections.Generic;

namespace Ironfield.Vehicles
{
    /// <summary>Cheap global list of vehicles still in play, for targeting and the mission counter.</summary>
    public static class VehicleRegistry
    {
        static readonly List<Vehicle> _alive = new();
        public static IReadOnlyList<Vehicle> Alive => _alive;

        public static int TotalRegistered { get; private set; }
        public static int DestroyedCount { get; private set; }
        public static int EscapedCount { get; private set; }

        /// <summary>Fired once per vehicle when it is knocked out.</summary>
        public static event System.Action<Vehicle> AnyDestroyed;
        /// <summary>Fired once per vehicle when it drives off the far end of the road.</summary>
        public static event System.Action<Vehicle> AnyEscaped;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _alive.Clear();
            TotalRegistered = 0;
            DestroyedCount = 0;
            EscapedCount = 0;
            AnyDestroyed = null;
            AnyEscaped = null;
        }

        public static void ResetCounters()
        {
            TotalRegistered = _alive.Count;
            DestroyedCount = 0;
            EscapedCount = 0;
        }

        internal static void Register(Vehicle v)
        {
            if (v != null && !_alive.Contains(v))
            {
                _alive.Add(v);
                TotalRegistered = System.Math.Max(TotalRegistered, _alive.Count);
            }
        }

        internal static void MarkDestroyed(Vehicle v)
        {
            if (_alive.Remove(v))
            {
                DestroyedCount++;
                AnyDestroyed?.Invoke(v);
            }
        }

        internal static void MarkEscaped(Vehicle v)
        {
            if (_alive.Remove(v))
            {
                EscapedCount++;
                AnyEscaped?.Invoke(v);
            }
        }

        internal static void Unregister(Vehicle v) => _alive.Remove(v);
    }
}
