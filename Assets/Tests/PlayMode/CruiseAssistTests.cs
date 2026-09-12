using System.Collections;
using Ironfield.Drone;
using Ironfield.Mission;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Covers auto-cruise (2026-09-12, user: "给无人增加自动巡航模式，
    /// 比如按键"A",就自动巡航"): toggled by DroneController.CruiseToggleRequested
    /// (the A key), CruiseAssist autonomously flies toward the nearest live
    /// vehicle until a real hard lock forms, at which point it hands off and
    /// stops touching AutopilotTarget.</summary>
    public class CruiseAssistTests
    {
        [UnityTest]
        public IEnumerator Toggling_on_steers_the_drone_toward_the_nearest_vehicle()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<Ironfield.Vehicles.VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);
            var cruise = drone.GetComponent<CruiseAssist>();
            Assert.IsNotNull(cruise, "MissionManager should have wired a CruiseAssist onto the spawned drone");
            Assert.IsFalse(cruise.Engaged, "should start disengaged");

            var target = mgr.NearestLiveVehicle(drone.transform.position);
            Assert.IsNotNull(target, "no live vehicle to cruise toward");

            cruise.DebugToggle();
            Assert.IsTrue(cruise.Engaged);
            yield return null; // let Update run once to pick a target

            Assert.IsTrue(drone.AutopilotEngaged, "cruise should hand navigation to AutopilotTarget");

            Vector3 start = drone.transform.position;
            float startDist = Vector3.Distance(start, target.AimPoint);
            for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate(); // ~1.5s

            float endDist = Vector3.Distance(drone.transform.position, target.AimPoint);
            Assert.Less(endDist, startDist, "cruise should actually close distance to the nearest target");
        }

        [UnityTest]
        public IEnumerator Toggling_off_releases_autopilot_control()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<Ironfield.Vehicles.VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            var cruise = drone.GetComponent<CruiseAssist>();

            cruise.DebugToggle();
            yield return null;
            Assert.IsTrue(drone.AutopilotEngaged);

            cruise.DebugToggle();
            Assert.IsFalse(cruise.Engaged);
            Assert.IsFalse(drone.AutopilotEngaged, "toggling off should immediately release AutopilotTarget");
        }

        [UnityTest]
        public IEnumerator Hands_off_once_a_real_hard_lock_forms()
        {
            // Cruise's job is getting the drone pointed at something — once a
            // real lock exists, the player (or DiveAssist/AutoAttack) owns the
            // terminal approach; cruise must stop overwriting AutopilotTarget
            // so it doesn't fight whichever of those takes over next.
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<Ironfield.Vehicles.VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            var cruise = drone.GetComponent<CruiseAssist>();
            var targeting = Object.FindAnyObjectByType<Ironfield.Targeting.TargetingSystem>();

            cruise.DebugToggle();
            yield return null;
            Assert.IsTrue(drone.AutopilotEngaged);

            // force a hard lock and drive AutopilotTarget to something cruise
            // did NOT choose, so a passing test can only mean cruise really
            // stopped touching it (not coincidentally set the same value)
            var target = mgr.NearestLiveVehicle(drone.transform.position);
            targeting.DebugForceLockTarget = target;
            Vector3 sentinel = drone.transform.position + Vector3.up * 500f;
            drone.AutopilotTarget = sentinel;

            yield return null;

            Assert.AreEqual(sentinel, drone.AutopilotTarget,
                "cruise should not overwrite AutopilotTarget once a hard lock exists");
        }
    }
}
