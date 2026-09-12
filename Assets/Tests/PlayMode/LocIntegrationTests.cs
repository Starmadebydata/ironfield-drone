using System.Collections;
using System.Linq;
using Ironfield.Core;
using Ironfield.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Vehicle.displayName is baked in at build time by IronfieldSetup
    /// as a Loc key string (e.g. "vehicle.tank"), not literal text — this
    /// verifies the baked keys actually exist in Loc's table, since a typo on
    /// either side (the key IronfieldSetup writes vs. the key Loc.cs defines)
    /// would silently show the raw key string in the HUD instead of throwing.</summary>
    public class LocIntegrationTests
    {
        [UnityTest]
        public IEnumerator Every_vehicle_displayName_in_Mission03_resolves_to_real_text()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission03", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;

            var vehicles = VehicleRegistry.Alive.ToList();
            Assert.IsNotEmpty(vehicles, "expected Mission03 to have registered vehicles by now");

            foreach (var v in vehicles)
            {
                string resolved = Loc.Get(v.displayName);
                Assert.AreNotEqual(v.displayName, resolved,
                    $"'{v.displayName}' (on {v.name}) doesn't resolve to a real translation — "
                    + "Loc.Get() is falling back to returning the raw key, meaning it's missing from Loc's table");
            }
        }
    }
}
