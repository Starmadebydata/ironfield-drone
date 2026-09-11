using System.Collections;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Targeting;
using Ironfield.UI;
using Ironfield.Vehicles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfield.Mission
{
    /// <summary>
    /// Scripted playthrough for recording a demo video: menu navigation, a
    /// mission with one manual kill and one auto-attack kill (each a real,
    /// full flight — nothing teleported or faked), a pause-menu detour, then
    /// a quick wrap-up of the rest of the column so the video reaches a real
    /// win screen without needing several more minutes of flight time, and a
    /// trip back to the main menu. No-op unless launched with -demo. Drives
    /// gameplay through the same debug hooks used by tests/PerfHarness, so it
    /// exercises real code paths rather than a separate scripted fake.
    /// Uses WaitForSecondsRealtime throughout — PauseMenu sets Time.timeScale
    /// to 0 while paused, and a scaled wait would hang exactly like the
    /// FirstRunTip/timescale bug this project already hit once.
    /// </summary>
    public class DemoRunner : MonoBehaviour
    {
        static bool s_active;

        void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-demo") < 0 || s_active) { Destroy(gameObject); return; }
            s_active = true;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(RunMainMenu());
        }

        IEnumerator RunMainMenu()
        {
            for (int i = 0; i < 3; i++) yield return null;
            var menu = FindAnyObjectByType<MainMenuController>();
            yield return new WaitForSecondsRealtime(2.5f);
            if (menu != null) menu.DebugShowMissions();
            yield return new WaitForSecondsRealtime(2.5f);
            SceneFlow.LoadMissionByName("Mission01");
            yield return RunMission();
        }

        IEnumerator RunMission()
        {
            for (int i = 0; i < 10; i++) yield return null;
            var mgr = FindAnyObjectByType<MissionManager>();
            var targeting = FindAnyObjectByType<TargetingSystem>();
            var pause = FindAnyObjectByType<PauseMenu>();
            if (mgr == null) { Debug.LogWarning("[DemoRunner] no MissionManager, aborting"); yield break; }

            var tip = FindAnyObjectByType<FirstRunTip>();
            if (tip != null && FirstRunTip.Showing)
            {
                yield return new WaitForSecondsRealtime(2f);
                tip.DebugDismiss();
            }
            yield return new WaitForSecondsRealtime(0.5f);

            GameSettings.SetAutoAttack(false);
            yield return FlyAndKill(mgr, targeting, manual: true);

            if (pause != null)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                pause.DebugSetPaused(true);
                yield return new WaitForSecondsRealtime(3f);
                pause.DebugSetPaused(false);
            }

            GameSettings.SetAutoAttack(true);
            yield return FlyAndKill(mgr, targeting, manual: false);

            // wrap up the rest of the column directly (same death path a warhead
            // hit takes — HealthComponent.Kill still triggers Wreck/fire/sfx/HUD
            // kill banner — just without several more minutes of flight time)
            // so the demo reaches a genuine win screen.
            while (mgr.State == MissionState.Active && VehicleRegistry.Alive.Count > 0)
            {
                var v = VehicleRegistry.Alive[0];
                if (v != null && v.Health != null) v.Health.Kill();
                yield return new WaitForSecondsRealtime(0.6f);
            }

            float waitEnd = Time.unscaledTime + 8f;
            while (mgr.State == MissionState.Active && Time.unscaledTime < waitEnd) yield return null;
            yield return new WaitForSecondsRealtime(4f); // linger on the end screen

            SceneFlow.LoadMainMenu();
            yield return new WaitForSecondsRealtime(3f);
            Debug.Log("[DemoRunner] demo complete");
        }

        /// <summary>Steers toward the nearest live vehicle until a kill lands, by
        /// driving DroneController.AutopilotTarget directly — the same mechanism
        /// DiveAssist hands the drone once a real player commits to a dive, and
        /// the one AutopilotTarget_steers_the_drone_toward_it already proves
        /// converges reliably. A hand-rolled duplicate of that steering math
        /// here (an earlier version of this method) turned out not to converge
        /// reliably from the ~250-300m launch-to-convoy distance — reusing the
        /// tested flight code instead of re-deriving it avoids that class of bug
        /// entirely. manual: also fires the warhead by hand once in range,
        /// rather than relying only on a physical collision.</summary>
        IEnumerator FlyAndKill(MissionManager mgr, TargetingSystem targeting, bool manual)
        {
            int killsBefore = mgr.Killed;
            float startTime = Time.unscaledTime;
            float giveUp = startTime + 60f;
            float nextLog = startTime;

            // Commit to ONE target for the whole approach instead of re-querying
            // "nearest" every frame — with several convoy vehicles at similar
            // distances, re-picking every frame can flip between two of them
            // and never converge on either.
            Vehicle target = null;
            var firstDrone = mgr.ActiveDrone;
            if (firstDrone != null) target = mgr.NearestLiveVehicle(firstDrone.transform.position);
            if (target == null) yield break;

            while (Time.unscaledTime < giveUp && mgr.State == MissionState.Active)
            {
                var d = mgr.ActiveDrone;
                if (d == null) { yield return null; continue; } // waiting out the respawn delay
                if (target == null || target.IsDestroyed)
                {
                    target = mgr.NearestLiveVehicle(d.transform.position);
                    if (target == null) yield break; // nothing left to dive on
                }

                var diveAssist = d.GetComponent<DiveAssist>();
                if (diveAssist != null) diveAssist.enabled = false;

                d.useDebugInput = false;
                d.AutopilotTarget = target.AimPoint;
                float dist = Vector3.Distance(d.transform.position, target.AimPoint);
                if (manual && dist < 6.5f) d.RequestFire();

                if (Time.unscaledTime >= nextLog)
                {
                    nextLog = Time.unscaledTime + 5f;
                    Debug.Log($"[DemoRunner] FlyAndKill(manual={manual}) t={Time.unscaledTime - startTime:0.0} "
                        + $"dist={dist:0.0} speed={d.Speed:0.0} alt={d.transform.position.y:0.0}");
                }

                if (mgr.Killed > killsBefore) yield break;
                yield return null;
            }
            if (mgr.Killed == killsBefore)
                Debug.LogWarning($"[DemoRunner] FlyAndKill(manual={manual}) gave up without a kill");
        }
    }
}
