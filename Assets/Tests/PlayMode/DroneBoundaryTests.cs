using System.Collections;
using Ironfield.Drone;
using Ironfield.Mission;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Covers the map-boundary fix (2026-09-11): a player who keeps
    /// flying away from the combat area used to be able to leave the terrain
    /// entirely in a few seconds. MissionManager now configures each spawned
    /// drone's boundary from the actual terrain bounds, and DroneController
    /// pushes back near the edge with a hard wall it can never cross.</summary>
    public class DroneBoundaryTests
    {
        [UnityTest]
        public IEnumerator Spawned_drone_gets_real_boundary_radii_from_terrain()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr);
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);

            // Defaults are a huge inert radius (see DroneController) — a real
            // scene must have replaced them with something derived from the
            // 2048m terrain, not left the boundary effectively switched off.
            Assert.Greater(drone.boundarySoftRadius, 100f);
            Assert.Less(drone.boundarySoftRadius, 2000f, "should be a real radius, not the inert default");
            Assert.Less(drone.boundaryHardRadius, 2000f, "should be a real radius, not the inert default");
            Assert.Greater(drone.boundaryHardRadius, drone.boundarySoftRadius,
                "the hard wall must sit further out than where the warning starts");
        }

        [UnityTest]
        public IEnumerator Drone_flown_past_the_hard_boundary_is_clamped_back_inside_it()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);

            var rb = drone.GetComponent<Rigidbody>();
            Vector3 farOut = new Vector3(
                drone.boundaryCentre.x + drone.boundaryHardRadius + 300f,
                drone.transform.position.y,
                drone.boundaryCentre.y);
            rb.position = farOut;
            // hold still — this isolates the boundary push/clamp from ordinary
            // flight input, which would otherwise also move the drone around.
            drone.useDebugInput = true;
            drone.debugInput = Vector4.zero;

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            float distAfter = Vector2.Distance(
                new Vector2(drone.transform.position.x, drone.transform.position.z),
                drone.boundaryCentre);
            Assert.LessOrEqual(distAfter, drone.boundaryHardRadius + 1f,
                "the hard wall should clamp the drone back inside it, not let it sit beyond the map edge");
        }
    }
}
