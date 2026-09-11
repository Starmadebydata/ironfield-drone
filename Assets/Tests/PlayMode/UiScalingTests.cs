using System.Collections;
using Ironfield.Core;
using Ironfield.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Covers the UI scale fix (ROADMAP.md P2, added 2026-09-12): four
    /// independent MonoBehaviours (HudController/PauseMenu/FirstRunTip/
    /// MainMenuController) each own an OnGUI and share the single global
    /// GUI.matrix. Before this, there was no scale option at all specifically
    /// because a naive GUIUtility.ScaleAroundPivot call could leak a scaled
    /// matrix into whichever OnGUI Unity dispatches next that frame — this
    /// tests that UiScaling.Begin()/End() (which every OnGUI now wraps its body
    /// in via try/finally) actually holds that contract.</summary>
    public class UiScalingTests
    {
        [TearDown]
        public void ResetUiScale() => GameSettings.SetUiScale(1f);

        [UnityTest]
        public IEnumerator Begin_applies_a_real_scale_and_End_restores_the_exact_prior_matrix()
        {
            yield return null;
            GameSettings.SetUiScale(1.3f);
            var before = GUI.matrix;

            var captured = UiScaling.Begin();
            Assert.AreNotEqual(before, GUI.matrix, "Begin() should actually change GUI.matrix when UiScale != 1");

            UiScaling.End(captured);
            Assert.AreEqual(before, GUI.matrix,
                "End() must restore the exact matrix Begin() captured, not just reset to identity");
        }

        [UnityTest]
        public IEnumerator Independent_BeginEnd_pairs_never_leak_a_scaled_matrix_to_the_next_caller()
        {
            // Simulates what a frame with several independent OnGUI owners
            // actually does: each wraps its own draw call in Begin()/End(),
            // one after another, all sharing the same global GUI.matrix.
            yield return null;
            GameSettings.SetUiScale(1.25f);
            var outerBefore = GUI.matrix;

            var m1 = UiScaling.Begin();
            UiScaling.End(m1);
            Assert.AreEqual(outerBefore, GUI.matrix,
                "first caller's End() must leave the matrix exactly as it found it");

            var m2 = UiScaling.Begin();
            UiScaling.End(m2);
            Assert.AreEqual(outerBefore, GUI.matrix,
                "second caller must not have inherited a scaled matrix from the first");
        }

        [UnityTest]
        public IEnumerator UiScale_of_1_leaves_the_matrix_untouched()
        {
            yield return null;
            GameSettings.SetUiScale(1f);
            var before = GUI.matrix;

            var m = UiScaling.Begin();
            Assert.AreEqual(before, GUI.matrix, "no-op scale shouldn't touch the matrix at all");
            UiScaling.End(m);
        }
    }
}
