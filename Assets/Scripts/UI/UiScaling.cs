using Ironfield.Core;
using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>
    /// Safe per-OnGUI scaling for the IMGUI screens (menus/HUD/pause/first-run
    /// tip). GUI.matrix is a single global — several independent MonoBehaviours
    /// each have their own OnGUI, and any one of them returning early (this
    /// codebase does that a lot, e.g. "if (mission == null) return;") would
    /// leave a scaled matrix in place for whichever OnGUI runs next that frame,
    /// visibly corrupting an unrelated screen. <see cref="Begin"/>/<see cref="End"/>
    /// wrap each OnGUI body in a try/finally instead, so the matrix is always
    /// restored on the way out of that specific call — scaling can never leak
    /// across components regardless of where inside the method it returns.
    /// </summary>
    public static class UiScaling
    {
        /// <summary>Call first thing in OnGUI; always pair with End() in a
        /// finally block, even (especially) around early returns.</summary>
        public static Matrix4x4 Begin()
        {
            var old = GUI.matrix;
            float s = GameSettings.UiScale;
            if (Mathf.Abs(s - 1f) > 0.001f)
            {
                var pivot = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                GUIUtility.ScaleAroundPivot(new Vector2(s, s), pivot);
            }
            return old;
        }

        public static void End(Matrix4x4 old) => GUI.matrix = old;
    }
}
