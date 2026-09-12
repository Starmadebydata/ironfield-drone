using System.Collections;
using Ironfield.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Sanity check for the P0 menu scaffold: MainMenu loads clean and
    /// wires up its controller.</summary>
    public class MainMenuSmokeTest
    {
        [UnityTest]
        public IEnumerator MainMenu_loads_without_errors()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            var ctrl = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(ctrl, "MainMenuController missing from MainMenu scene");
        }

        /// <summary>The credits panel (P3: ships CC-BY attributions with the
        /// build itself, not just the repo's CREDITS.md) renders without error.
        /// Can't screenshot-verify IMGUI content (see UiScalingTests) — this at
        /// least confirms switching to that panel and drawing a frame of it
        /// doesn't throw.</summary>
        [UnityTest]
        public IEnumerator Credits_panel_opens_and_draws_without_errors()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            var ctrl = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(ctrl);

            ctrl.DebugShowCredits();
            yield return null;
            yield return null;
            // still alive and didn't throw drawing the credits panel for a
            // couple of frames — LogAssert isn't used here since this scene
            // benignly logs an unrelated "no audio listener" warning.
            Assert.IsNotNull(ctrl);
        }
    }
}
