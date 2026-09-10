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

        public int Killed => VehicleRegistry.DestroyedCount;
        public int Escaped => VehicleRegistry.EscapedCount;
        public int VehiclesLeft => Mathf.Max(0, VehicleRegistry.TotalRegistered
                                   - VehicleRegistry.DestroyedCount - VehicleRegistry.EscapedCount);
        public int VehiclesTotal => VehicleRegistry.TotalRegistered;
        public DroneController ActiveDrone { get; private set; }

        /// <summary>Seconds since the mission became Active (for the briefing fade + score).</summary>
        public float TimeActive { get; private set; }
        /// <summary>Raised with the damage amount whenever the current drone is hit.</summary>
        public event System.Action<float> DroneDamaged;

        public int Score { get; private set; }
        public string Grade { get; private set; } = "";

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
            TimeActive += Time.deltaTime;

            int total = VehicleRegistry.TotalRegistered;
            if (total == 0) return;

            if (VehicleRegistry.DestroyedCount >= total)
                EndMission(MissionState.Won);
            // every vehicle is off the board and you didn't get them all
            else if (VehicleRegistry.DestroyedCount + VehicleRegistry.EscapedCount >= total)
                EndMission(MissionState.Lost);
        }

        void EndMission(MissionState result)
        {
            if (State != MissionState.Active) return;
            State = result;
            if (ActiveDrone) ActiveDrone.ControlsEnabled = false;

            int dronesUsed = droneStock - DronesLeft;
            Score = Killed * 1000
                    - Escaped * 400
                    - dronesUsed * 120
                    - Mathf.RoundToInt(TimeActive) * 2;
            Score = Mathf.Max(0, Score);

            float frac = Killed / (float)Mathf.Max(1, VehiclesTotal);
            Grade = result == MissionState.Won
                ? (dronesUsed <= VehiclesTotal ? "S" : dronesUsed <= VehiclesTotal + 2 ? "A" : "B")
                : (frac >= 0.66f ? "C" : frac >= 0.33f ? "D" : "F");
        }

        void SpawnDrone()
        {
            if (State != MissionState.Active) return;

            if (DronesLeft <= 0)
            {
                EndMission(MissionState.Lost);
                return;
            }

            Vector3 pos = launchPoint ? launchPoint.position : Vector3.up * 30f;
            Quaternion rot = launchPoint ? launchPoint.rotation : Quaternion.identity;

            _currentDroneSpent = false;
            ActiveDrone = Instantiate(dronePrefab, pos, rot);
            int droneLayer = GameLayers.Drone;
            if (droneLayer >= 0) SetLayer(ActiveDrone.gameObject, droneLayer);

            if (cameraRig) cameraRig.Bind(ActiveDrone.transform);
            if (targeting) targeting.viewCamera = cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;

            var assist = ActiveDrone.GetComponent<DiveAssist>();
            if (assist == null) assist = ActiveDrone.gameObject.AddComponent<DiveAssist>();
            assist.targeting = targeting;

            // Warhead detonation (rams a target / ground): warhead destroys itself.
            var warhead = ActiveDrone.GetComponent<DroneWarhead>();
            if (warhead != null) warhead.Detonated += OnDroneSpent;

            // Shot down with no detonation: kill the drone GameObject ourselves.
            var health = ActiveDrone.GetComponent<HealthComponent>();
            if (health != null)
            {
                var deadDrone = ActiveDrone;
                health.Damaged += (amt, _) => DroneDamaged?.Invoke(amt);
                health.Died += _ =>
                {
                    Vector3 where = deadDrone != null ? deadDrone.transform.position : pos;
                    if (deadDrone != null) Destroy(deadDrone.gameObject);
                    OnDroneSpent(where);
                };
            }
        }

        /// <summary>Nearest still-alive vehicle to a world point, or null.</summary>
        public Vehicle NearestLiveVehicle(Vector3 from)
        {
            Vehicle best = null;
            float bestSqr = float.MaxValue;
            foreach (var v in VehicleRegistry.Alive)
            {
                if (v == null || v.IsDestroyed) continue;
                float d = (v.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = v; }
            }
            return best;
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
                EndMission(MissionState.Lost);
                return;
            }
            StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            if (State == MissionState.Active) SpawnDrone();
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
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
