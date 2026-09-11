using System.Collections;
using System.Linq;
using Ironfield.Mission;
using Ironfield.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Sanity for the P1 mission set: Mission02/03 load clean, wire a
    /// MissionManager with the right id, and carry the enemy-diversity features
    /// (flak emplacements, a high-value target) their MissionBuildConfig asked for.</summary>
    public class CampaignSmokeTests
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
        public IEnumerator Mission02_has_a_flak_emplacement()
        {
            yield return LoadAndSettle("Mission02");

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr, "MissionManager missing from Mission02");
            Assert.AreEqual("m02", mgr.missionId);
            Assert.IsNotNull(mgr.ActiveDrone, "no drone spawned");

            int flakCount = SceneManager.GetActiveScene().GetRootGameObjects()
                .Count(go => go.name == "FlakPosition");
            Assert.AreEqual(1, flakCount, "Mission02 should have exactly one flak emplacement");
        }

        [UnityTest]
        public IEnumerator Mission03_has_two_flak_and_a_high_value_target()
        {
            yield return LoadAndSettle("Mission03");

            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr, "MissionManager missing from Mission03");
            Assert.AreEqual("m03", mgr.missionId);

            int flakCount = SceneManager.GetActiveScene().GetRootGameObjects()
                .Count(go => go.name == "FlakPosition");
            Assert.AreEqual(2, flakCount, "Mission03 should have two flak emplacements");

            bool anyHighValue = VehicleRegistry.Alive.Any(v => v.highValue);
            Assert.IsTrue(anyHighValue, "Mission03 should have one high-value convoy vehicle");
        }
    }
}
