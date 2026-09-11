using Ironfield.Core;
using UnityEngine;

namespace Ironfield.Mission
{
    /// <summary>
    /// Forces a short controls tip in front of a first-time player before the
    /// mission gets going, shown once ever via PlayerPrefs and skippable with a
    /// button. Reachable again any time from the pause menu's "操作说明". IMGUI,
    /// matching HudController.
    ///
    /// Deliberately does NOT freeze Time.timeScale (unlike PauseMenu) — this runs
    /// automatically at mission start, including under automated PlayMode tests
    /// where the "seen it" PlayerPrefs flag is never set, and a frozen timescale
    /// there hangs any test loop keyed on Time.time. Just disables drone controls
    /// instead while the overlay is up.
    /// </summary>
    public class FirstRunTip : MonoBehaviour
    {
        const string SeenKey = "ironfield.seenTutorial";
        public MissionManager mission;

        /// <summary>True while the tip owns input focus (HudController/PauseMenu check this).</summary>
        public static bool Showing { get; private set; }

        GUIStyle _title, _btn, _line;

        void Awake()
        {
            GameSettings.Load();
            Showing = PlayerPrefs.GetInt(SeenKey, 0) == 0;
        }

        void Update()
        {
            if (!Showing) return;
            if (mission != null && mission.ActiveDrone != null)
                mission.ActiveDrone.ControlsEnabled = false;
        }

        void OnDisable() => Showing = false;

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _line = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _line.normal.textColor = new Color(0.9f, 0.94f, 0.9f);
        }

        void OnGUI()
        {
            if (!Showing) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = 520, ph = 300, px = w * 0.5f - pw * 0.5f, py = h * 0.5f - ph * 0.5f;
            GUI.Label(new Rect(px, py, pw, 34), "出击须知", _title);
            foreach (var (i, s) in PauseMenu.ControlLines())
                GUI.Label(new Rect(px, py + 44 + i * 26, pw, 24), s, _line);

            if (GUI.Button(new Rect(px + pw * 0.5f - 70, py + ph - 50, 140, 40), "开始出击", _btn))
                Dismiss();
        }

        void Dismiss()
        {
            Showing = false;
            if (mission != null && mission.ActiveDrone != null)
                mission.ActiveDrone.ControlsEnabled = true;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }
    }
}
