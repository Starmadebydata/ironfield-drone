using System.Collections;
using Ironfield.Core;
using Ironfield.Mission;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Covers the second drone type: GameSettings.SelectedDrone actually
    /// changes which prefab MissionManager spawns, and the Heavy variant really
    /// is tankier (the concrete, checkable half of "slower but tougher").</summary>
    public class DroneSelectionTests
    {
        [TearDown]
        public void ResetSelection() => GameSettings.SetSelectedDrone(0);

        [UnityTest]
        public IEnumerator Heavy_drone_has_more_health_than_light()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);

            GameSettings.SetSelectedDrone(0);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            var mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr);
            Assert.IsNotNull(mgr.ActiveDrone, "light drone should have spawned");
            float lightHealth = mgr.ActiveDrone.GetComponent<Ironfield.Combat.HealthComponent>().maxHealth;

            GameSettings.SetSelectedDrone(1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            mgr = Object.FindAnyObjectByType<MissionManager>();
            Assert.IsNotNull(mgr.ActiveDrone, "heavy drone should have spawned");
            Assert.AreEqual("Drone_Heavy(Clone)", mgr.ActiveDrone.name,
                "SelectedDrone=1 should spawn the heavy prefab, not the light one");
            float heavyHealth = mgr.ActiveDrone.GetComponent<Ironfield.Combat.HealthComponent>().maxHealth;

            Assert.Greater(heavyHealth, lightHealth,
                $"heavy drone should have more HP than light (heavy={heavyHealth}, light={lightHealth})");
        }
    }
}
