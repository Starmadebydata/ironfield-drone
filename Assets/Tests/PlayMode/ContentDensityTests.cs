using System.Collections;
using System.Linq;
using Ironfield.Combat;
using Ironfield.Mission;
using Ironfield.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Covers the 2026-09-12 content-density pass: optional side
    /// objectives that don't gate mission completion, and the new SPAAG
    /// vehicle type mixed into the convoy.</summary>
    public class ContentDensityTests
    {
        static IEnumerator LoadAndSettle(string scene)
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            foreach (var tt in Object.FindObjectsByType<VehicleTurret>(FindObjectsSortMode.None))
                tt.enabled = false; // deterministic: no return fire mid-test
        }

        [UnityTest]
        public IEnumerator Bonus_targets_are_registered_but_excluded_from_the_mandatory_total()
        {
            yield return LoadAndSettle("Mission01");

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr);

            // Mission01's MissionBuildConfig has a 6-vehicle convoy and 2 bonus
            // RadioOutposts — VehiclesTotal (what you must clear to win) should
            // only count the convoy; BonusTotal should see the outposts.
            Assert.AreEqual(6, mgr.VehiclesTotal, "mandatory total should be convoy-only");
            Assert.AreEqual(2, mgr.BonusTotal, "Mission01 should have its 2 bonus targets registered");
            Assert.Greater(VehicleRegistry.TotalRegistered, mgr.VehiclesTotal,
                "the registry's raw total should include the optional targets too");
        }

        [UnityTest]
        public IEnumerator Destroying_a_bonus_target_does_not_end_the_mission()
        {
            yield return LoadAndSettle("Mission01");

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            var outpost = VehicleRegistry.Alive.FirstOrDefault(v => v.optional);
            Assert.IsNotNull(outpost, "expected at least one optional bonus target alive");

            int killedBefore = mgr.Killed;
            outpost.Health.ApplyDamage(new DamageInfo(9999f, DamageType.Explosive,
                outpost.transform.position, Vector3.up, null));
            yield return null;

            Assert.AreEqual(MissionState.Active, mgr.State,
                "killing only an optional target must not end the mission");
            Assert.AreEqual(killedBefore, mgr.Killed, "optional kills shouldn't count toward Killed");
            Assert.AreEqual(1, mgr.BonusKilled);
        }

        [UnityTest]
        public IEnumerator Mission02_convoy_includes_a_faster_firing_SPAAG()
        {
            yield return LoadAndSettle("Mission02");

            var spaag = VehicleRegistry.Alive.FirstOrDefault(v => v.vehicleClass == VehicleClass.SPAAG);
            Assert.IsNotNull(spaag, "Mission02's convoy should include a SPAAG");

            var turret = spaag.GetComponent<Ironfield.Vehicles.VehicleTurret>();
            Assert.IsNotNull(turret, "SPAAG should have return fire like other combat vehicles");
            Assert.Less(turret.fireInterval, 0.11f,
                "SPAAG should fire noticeably faster than an IFV (0.11s) to feel like dedicated AA");
        }
    }
}
