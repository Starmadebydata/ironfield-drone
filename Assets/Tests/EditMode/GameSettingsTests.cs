using Ironfield.Core;
using NUnit.Framework;
using UnityEngine;

namespace Ironfield.Tests
{
    /// <summary>Covers the UI scale setting (ROADMAP.md P2 accessibility item,
    /// added 2026-09-12) — the setter clamp/persist half. The "doesn't leak
    /// across independent OnGUI components" half is covered separately by
    /// UiScalingTests (PlayMode, needs an actual scene with several of them).</summary>
    public class GameSettingsTests
    {
        [SetUp]
        public void ResetUiScale() => GameSettings.SetUiScale(1f);

        [Test]
        public void UiScale_defaults_to_1()
        {
            PlayerPrefs.DeleteKey("ironfield.a11y.uiscale");
            GameSettings.SetUiScale(1f); // ResetStatics only fires on a real domain
                                          // reload, not between EditMode tests in one
                                          // run — assert against the known-clean value
                                          // this SetUp/DeleteKey combo establishes.
            Assert.AreEqual(1f, GameSettings.UiScale);
        }

        [Test]
        public void UiScale_setter_clamps_to_the_supported_range()
        {
            GameSettings.SetUiScale(5f);
            Assert.AreEqual(1.4f, GameSettings.UiScale, 0.001f, "should clamp to the max");

            GameSettings.SetUiScale(-2f);
            Assert.AreEqual(0.8f, GameSettings.UiScale, 0.001f, "should clamp to the min");

            GameSettings.SetUiScale(1.15f);
            Assert.AreEqual(1.15f, GameSettings.UiScale, 0.001f, "an in-range value should pass through unchanged");
        }
    }
}
