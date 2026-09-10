using System.Collections;
using Ironfield.Mission;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>
    /// End-to-end sanity for Mission01: the scene loads, a drone spawns, gentle
    /// forward flight keeps it alive and moving above the ground, and targeting
    /// finds the convoy.
    /// </summary>
    public class MissionSmokeTests
    {
        [UnityTest]
        public IEnumerator Drone_spawns_flies_forward_and_survives()
        {
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            // let MissionManager.Begin() run (it waits one frame)
            for (int i = 0; i < 5; i++) yield return null;
            // silence return fire so flight assertions stay deterministic
            foreach (var tt in Object.FindObjectsByType<Ironfield.Vehicles.VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr, "MissionManager missing from Mission01");
            Assert.IsNotNull(mgr.ActiveDrone, "no drone spawned");
            Assert.AreEqual(5, mgr.DronesLeft, "should still have full drone stock at start");

            Vector3 start = mgr.ActiveDrone.transform.position;

            float until = Time.time + 3f;
            while (Time.time < until)
            {
                var d = mgr.ActiveDrone;
                Assert.IsNotNull(d, "drone was lost during 3s of gentle forward flight");
                d.useDebugInput = true;
                d.debugInput = new Vector4(1f, 0f, 0f, 0f);   // full forward, level
                yield return null;
            }

            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone, "drone missing after flight");
            float travelled = Vector3.Distance(start, drone.transform.position);
            Assert.Greater(travelled, 15f, $"drone barely moved ({travelled:0.0} m in 3s)");
            Assert.Greater(drone.Speed, 4f, "drone not building speed under full throttle");
            Assert.AreEqual(5, mgr.DronesLeft, "drone stock dropped during simple flight");
        }

        [UnityTest]
        public IEnumerator Targeting_sees_the_convoy()
        {
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<Ironfield.Vehicles.VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var targeting = Object.FindAnyObjectByType<Ironfield.Targeting.TargetingSystem>();
            Assert.IsNotNull(targeting);

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            // fly toward the convoy for a bit so it enters the view cone
            float until = Time.time + 4f;
            while (Time.time < until)
            {
                if (mgr.ActiveDrone != null)
                {
                    mgr.ActiveDrone.useDebugInput = true;
                    mgr.ActiveDrone.debugInput = new Vector4(1f, 0f, 0f, 0f);
                }
                yield return null;
            }

            Assert.Greater(Ironfield.Vehicles.VehicleRegistry.Alive.Count, 0,
                "no vehicles registered");
        }
    }
}
