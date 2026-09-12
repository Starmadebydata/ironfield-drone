using Ironfield.Mission;
using Ironfield.Targeting;
using UnityEngine;

namespace Ironfield.Drone
{
    /// <summary>
    /// Auto-cruise: toggled with the A key (DroneController.CruiseToggleRequested),
    /// autonomously flies the drone toward the nearest live vehicle using the
    /// same obstacle-avoiding DroneController.AutopilotTarget steering the
    /// auto-attack dive already uses — hands-off navigation between fights, not
    /// hands-off combat. Hands control back once a real hard lock forms (the
    /// player's own aim, DiveAssist, or GameSettings.AutoAttack take the
    /// terminal engagement from there — this never fires the warhead itself),
    /// and automatically resumes hunting the next nearest target once that
    /// engagement resolves (lock lost or target destroyed). Stays on until the
    /// player presses A again.
    /// </summary>
    [RequireComponent(typeof(DroneController))]
    public class CruiseAssist : MonoBehaviour
    {
        public MissionManager mission;
        public TargetingSystem targeting;
        [Tooltip("Height above the target's aim point cruise steers toward "
                + "while in transit, so it flies a proper level cruise instead "
                + "of nosing straight down at a ground-level vehicle from far "
                + "away — DiveAssist/the player handle the actual dive down "
                + "to it once a hard lock hands control back.")]
        public float cruiseAltitude = 22f;

        public bool Engaged { get; private set; }

        DroneController _drone;

        void Awake() => _drone = GetComponent<DroneController>();

        void OnEnable()
        {
            if (_drone == null) _drone = GetComponent<DroneController>();
            _drone.CruiseToggleRequested += Toggle;
        }

        void OnDisable()
        {
            if (_drone != null) _drone.CruiseToggleRequested -= Toggle;
        }

        void Toggle()
        {
            Engaged = !Engaged;
            // Turning off mid-navigation shouldn't leave the flight model
            // still committed to the last target — give aim back immediately.
            if (!Engaged && _drone.AutopilotTarget.HasValue)
                _drone.AutopilotTarget = null;
        }

        /// <summary>Test hook: toggle exactly like pressing A.</summary>
        public void DebugToggle() => Toggle();

        void Update()
        {
            if (!Engaged || mission == null) return;

            // A real hard lock means the player (or DiveAssist/AutoAttack once
            // it independently engages) has the terminal approach from here —
            // cruise's job was just getting the drone pointed at something.
            if (targeting != null && targeting.HasHardLock) return;

            var target = mission.NearestLiveVehicle(transform.position);
            // Aim well above the vehicle, not straight at its ground-level
            // AimPoint — the old direct-aim version was reported flying low
            // enough to look like it was hugging the terrain even from far
            // away, since UpdateAutopilotAim just points the nose straight at
            // whatever AutopilotTarget is. This is transit, not the dive.
            _drone.AutopilotTarget = target != null
                ? target.AimPoint + Vector3.up * cruiseAltitude
                : (Vector3?)null;
        }
    }
}
