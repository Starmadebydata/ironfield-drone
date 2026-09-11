using System.Collections;
using Ironfield.Mission;
using Ironfield.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>
    /// Regression coverage for the "no dangling state across a scene load"
    /// class of bug (ROADMAP.md P2 item 5) — this project already hit one real
    /// instance of it: FirstRunTip freezing Time.timeScale broke every
    /// Time.time-keyed test loop until it was fixed to not touch timescale.
    /// </summary>
    public class StabilityTests
    {
        [UnityTest]
        public IEnumerator Loading_a_mission_always_starts_unpaused_at_normal_timescale()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            yield return null;

            Assert.AreEqual(1f, Time.timeScale, "a fresh mission load must not inherit a frozen timescale");
            Assert.IsFalse(PauseMenu.IsPaused, "a fresh mission load must not inherit a stuck pause state");
        }

        [UnityTest]
        public IEnumerator Repeated_mission_loads_do_not_accumulate_duplicate_managers()
        {
            PlayerPrefs.SetInt("ironfield.seenTutorial", 1);
            for (int i = 0; i < 3; i++)
            {
                yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
                yield return null;
            }

            Assert.AreEqual(1, Object.FindObjectsByType<MissionManager>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<HudController>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1, Object.FindObjectsByType<PauseMenu>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator UiSfx_singleton_survives_scene_loads_without_duplicating()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            var first = UiSfx.Instance;
            Assert.IsNotNull(first, "UiSfx should be seeded by MainMenu.unity");

            yield return SceneManager.LoadSceneAsync("Mission01", LoadSceneMode.Single);
            yield return null;
            Assert.AreSame(first, UiSfx.Instance, "UiSfx must survive a mission load via DontDestroyOnLoad");

            // Going back through MainMenu creates a second UiSfx GameObject — it
            // must self-destroy in Awake() rather than replace the original.
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            Assert.AreSame(first, UiSfx.Instance, "re-entering MainMenu must not duplicate the UiSfx singleton");
        }
    }
}
