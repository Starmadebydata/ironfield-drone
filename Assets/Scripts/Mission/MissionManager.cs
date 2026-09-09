using System.Collections;
using Ironfield.Combat;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfield.Mission
{
    public enum MissionState { Briefing, Active, Won, Lost }

    /// <summary>
    /// Owns the vertical-slice loop: spawn a drone at the launch point, follow
    /// it with the chase cam, and when it is spent (warhead detonated or shot
    /// down) spend one from the stock and spawn the next. Win when every vehicle
    /// is destroyed; lose when the stock is empty with vehicles still alive.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        [Header("Scene refs")]
        public DroneController dronePrefab;
        public Transform launchPoint;
        public DroneCameraRig cameraRig;
        public TargetingSystem targeting;

        [Header("Rules")]
        public int droneStock = 5;
        public float respawnDelay = 1.5f;

        public MissionState State { get; private set; } = MissionState.Briefing;
        public int DronesLeft { get; private set; }
        bool _currentDroneSpent;
        public int VehiclesLeft => Mathf.Max(0, VehicleRegistry.TotalRegistered - VehicleRegistry.DestroyedCount);
        public int VehiclesTotal => VehicleRegistry.TotalRegistered;
        public DroneController ActiveDrone { get; private set; }

        void Start()
        {
            DronesLeft = droneStock;
            // give vehicles a frame to register
            StartCoroutine(Begin());
        }

        IEnumerator Begin()
        {
            yield return null;
            VehicleRegistry.ResetCounters();
            State = MissionState.Active;
            SpawnDrone();
        }

        void Update()
        {
            if (State != MissionState.Active) return;

            if (VehicleRegistry.DestroyedCount >= VehicleRegistry.TotalRegistered &&
                VehicleRegistry.TotalRegistered > 0)
            {
                State = MissionState.Won;
                if (ActiveDrone) ActiveDrone.ControlsEnabled = false;
            }
        }

        void SpawnDrone()
        {
            if (State != MissionState.Active) return;

            if (DronesLeft <= 0)
            {
                State = MissionState.Lost;
                return;
            }

            Vector3 pos = launchPoint ? launchPoint.position : Vector3.up * 30f;
            Quaternion rot = launchPoint ? launchPoint.rotation : Quaternion.identity;

            _currentDroneSpent = false;
            ActiveDrone = Instantiate(dronePrefab, pos, rot);
            ActiveDrone.gameObject.tag = GameTags.Drone;
            ActiveDrone.gameObject.layer = GameLayers.Drone;

            if (cameraRig) cameraRig.Bind(ActiveDrone.transform);
            if (targeting) targeting.viewCamera = cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;

            // Warhead detonation (rams a target / ground): warhead destroys itself.
            var warhead = ActiveDrone.GetComponent<DroneWarhead>();
            if (warhead != null) warhead.Detonated += OnDroneSpent;

            // Shot down with no detonation: kill the drone GameObject ourselves.
            var health = ActiveDrone.GetComponent<HealthComponent>();
            if (health != null)
            {
                var deadDrone = ActiveDrone;
                health.Died += _ =>
                {
                    Vector3 where = deadDrone != null ? deadDrone.transform.position : pos;
                    if (deadDrone != null) Destroy(deadDrone.gameObject);
                    OnDroneSpent(where);
                };
            }
        }

        void OnDroneSpent(Vector3 at)
        {
            if (_currentDroneSpent) return;          // one spend per drone
            _currentDroneSpent = true;

            if (cameraRig) cameraRig.Shake(0.4f, 0.5f);
            DronesLeft--;

            if (State != MissionState.Active) return;

            if (DronesLeft <= 0 && VehiclesLeft > 0)
            {
                State = MissionState.Lost;
                return;
            }
            StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            if (State == MissionState.Active) SpawnDrone();
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
