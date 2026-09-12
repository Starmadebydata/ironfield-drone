using System.Collections;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Mission;
using Ironfield.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>End-to-end coverage for the auto-attack feature: DroneController's
    /// autopilot steering math in isolation, and the full lock -> autopilot ->
    /// detonate pipeline (DiveAssist + GameSettings.AutoAttack) in practice.</summary>
    public class AutoAttackTests
    {
        [TearDown]
        public void ResetAutoAttackSetting() => GameSettings.SetAutoAttack(false);

        [UnityTest]
        public IEnumerator AutopilotTarget_steers_the_drone_toward_it()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);

            // build up some speed first so there's velocity for the autopilot to steer
            drone.useDebugInput = true;
            drone.debugInput = new Vector4(1f, 0f, 0f, 0f);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            drone.useDebugInput = false;

            Vector3 start = drone.transform.position;
            // a target well off to the side and above, so "steers toward it" is unambiguous
            Vector3 target = start + drone.transform.forward * 40f + drone.transform.right * 60f + Vector3.up * 25f;
            drone.AutopilotTarget = target;

            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate(); // ~2.5s

            Vector3 wantDir = (target - start).normalized;
            Vector3 wentDir = (drone.transform.position - start).normalized;
            float alignment = Vector3.Dot(wantDir, wentDir);
            Assert.Greater(alignment, 0.5f,
                $"autopilot should steer toward AutopilotTarget (alignment={alignment:0.00})");
        }

        [UnityTest]
        public IEnumerator AutopilotTarget_descends_toward_a_target_below()
        {
            // Regression test for a real bug: UpdateAutopilotAim's pitch sign was
            // flipped (aimY = -elevation/pitchRange instead of +elevation/pitchRange),
            // so the drone CLIMBED AWAY from any target below it — i.e. almost
            // every real target, since the drone launches on high ground — instead
            // of diving on it. AutopilotTarget_steers_the_drone_toward_it didn't
            // catch this: its target is mostly off to the side (40 fwd + 60 right
            // vs only 25 up), so yaw alone pulled the alignment dot product over
            // the passing threshold even with pitch commanding the wrong direction.
            // This test isolates pitch by placing the target almost straight ahead
            // and well below, so only a correct descend command can pass it.
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);

            drone.useDebugInput = true;
            drone.debugInput = new Vector4(1f, 0f, 0f, 0f);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            drone.useDebugInput = false;

            float startAlt = drone.transform.position.y;
            Vector3 target = drone.transform.position + drone.transform.forward * 60f + Vector3.down * 60f;
            drone.AutopilotTarget = target;

            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate(); // ~2.5s

            Assert.Less(drone.transform.position.y, startAlt,
                $"autopilot should descend toward a target below it (start alt={startAlt:0.0}, "
                + $"now={drone.transform.position.y:0.0})");
        }

        [UnityTest]
        public IEnumerator AutopilotTarget_makes_progress_past_a_direct_obstacle()
        {
            // Regression test for a real user-reported bug: autopilot aimed
            // straight at the target with zero awareness of anything in
            // between, so a tree/pole directly on the line to the target
            // (common — the map scatters ~2000+ of them) would physically
            // wedge the drone in place, since UpdateAutopilotAim kept
            // commanding "more forward" into the same obstacle every frame.
            // This doesn't require a real tree — any Environment-layer
            // collider directly ahead reproduces it.
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);
            // Driving AutopilotTarget directly, same as the other isolated
            // steering tests above — DiveAssist has to be off or its own
            // FixedUpdate nulls AutopilotTarget straight back out every tick
            // whenever its own (separate) hard-lock gate isn't satisfied,
            // which it never is here since no real lock was acquired.
            drone.GetComponent<DiveAssist>().enabled = false;

            drone.useDebugInput = true;
            drone.debugInput = new Vector4(1f, 0f, 0f, 0f);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
            drone.useDebugInput = false;

            Vector3 start = drone.transform.position;
            Vector3 fwd = drone.transform.forward;
            Vector3 target = start + fwd * 90f;

            // sized like the game's actual scattered trees (BuildFoliagePrefab
            // colliders run roughly 2-8m tall, see IronfieldSetup.cs), not an
            // arbitrary wall — squarely on the direct line to the target
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = start + fwd * 35f + Vector3.up * 3.5f;
            obstacle.transform.localScale = new Vector3(6f, 7f, 3f);
            obstacle.layer = Ironfield.Core.GameLayers.Environment;

            drone.AutopilotTarget = target;

            float startDistToTarget = Vector3.Distance(start, target);
            for (int i = 0; i < 400; i++)
            {
                yield return new WaitForFixedUpdate(); // ~6.7s
                if (drone == null) break;  // clearer failure than a stack trace below
            }

            Assert.IsNotNull(drone,
                "the drone should navigate past the obstacle, not collide hard enough to be destroyed by it");
            float endDistToTarget = Vector3.Distance(drone.transform.position, target);
            Assert.Less(endDistToTarget, startDistToTarget * 0.5f,
                $"autopilot should make real progress past a direct obstacle instead of getting "
                + $"wedged against it (start dist={startDistToTarget:0}, end dist={endDistToTarget:0})");

            Object.Destroy(obstacle);
        }

        [UnityTest]
        public IEnumerator Auto_attack_setting_finishes_a_committed_dive_on_its_own()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            GameSettings.SetAutoAttack(true);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false;

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var targeting = Object.FindAnyObjectByType<Ironfield.Targeting.TargetingSystem>();
            Assert.IsNotNull(targeting);
            var drone = mgr.ActiveDrone;
            Assert.IsNotNull(drone);

            // Engineer "already committed to the dive" directly (hard lock forced,
            // position + velocity placed close and pointed at the target) instead of
            // flying there — acquiring a lock through the camera cone/LOS/dwell-time,
            // and lining up nose + velocity through imperfect proportional steering,
            // is TargetingSystem/flight-model territory already covered by
            // Targeting_sees_the_convoy and the isolated steering test above. This
            // test's job is narrower: given DiveAssist's engagement gate is already
            // satisfied, does GameSettings.AutoAttack correctly hand control to
            // DroneController.AutopilotTarget and finish the job with no further
            // input at all. Position/velocity are plain Rigidbody state, safe to set
            // directly; drone heading (_aim/_heading) is not, so this leaves that to
            // DroneController's own FixedUpdate once autopilot takes over.
            var target = mgr.NearestLiveVehicle(drone.transform.position);
            Assert.IsNotNull(target, "no live vehicle to dive on");
            targeting.DebugForceLockTarget = target;

            // Use the drone's own current forward as the approach vector (rather
            // than an arbitrary direction) so the teleported position/velocity stay
            // consistent with its internal heading/pitch state — DroneController
            // rebuilds rotation from that state every FixedUpdate regardless of
            // transform.position, so an inconsistent approach direction here would
            // fight the flight model back off target before autopilot can converge.
            Vector3 approach = drone.transform.forward;
            var rb = drone.GetComponent<Rigidbody>();
            // rb.position, not drone.transform.position: this is a non-kinematic
            // Rigidbody, so the physics engine is the authority on position and
            // silently overrides a plain Transform set on the next physics sync.
            rb.position = target.AimPoint - approach * 50f;
            rb.linearVelocity = approach * 20f;

            float giveUpAt = Time.time + 10f;
            while (Time.time < giveUpAt)
            {
                if (mgr.ActiveDrone == null) break;
                yield return null; // hands off entirely — no input at all
            }

            Assert.IsTrue(mgr.Killed > 0 || mgr.DronesLeft < 5,
                "auto-attack should have finished the dive (a kill or a spent drone) without further input");
        }
    }
}
