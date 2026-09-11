using Ironfield.Core;
using Ironfield.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ironfield.Mission
{
    /// <summary>
    /// Esc-to-pause overlay for the mission scene: freezes time, frees the OS
    /// cursor, and offers Resume / Controls / Settings / Restart / Main menu.
    /// IMGUI, matching HudController — see SettingsGUI for why. HudController
    /// checks <see cref="IsPaused"/> to stop force-locking the cursor while paused.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public MissionManager mission;

        /// <summary>True while the pause overlay owns input focus.</summary>
        public static bool IsPaused { get; private set; }

        bool _showSettings;
        bool _showControls;
        GUIStyle _title, _btn, _boxTitle, _line;

        void Awake() => GameSettings.Load();

        void OnDisable()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (FirstRunTip.Showing) return;
            if (mission == null || mission.State != MissionState.Active)
            {
                SetPaused(false);
                return;
            }
#if ENABLE_INPUT_SYSTEM
            bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed) SetPaused(!IsPaused);
        }

        void SetPaused(bool p)
        {
            if (IsPaused == p) return;
            IsPaused = p;
            Time.timeScale = p ? 0f : 1f;
            if (mission != null && mission.ActiveDrone != null)
                mission.ActiveDrone.ControlsEnabled = !p;
            if (!p) { _showSettings = false; _showControls = false; }
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = Color.white;
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _boxTitle = new GUIStyle(GUI.skin.box);
            _line = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _line.normal.textColor = new Color(0.9f, 0.94f, 0.9f);
        }

        void OnGUI()
        {
            if (!IsPaused) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            if (_showSettings) { SettingsGUI.Draw(w, h, () => _showSettings = false); return; }
            if (_showControls) { DrawControls(w, h); return; }

            GUI.color = new Color(0f, 0f, 0f, 0.66f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, h * 0.26f, w, 40), "已暂停", _title);

            float bw = 220, bh = 44, gap = bh + 10;
            float bx = w * 0.5f - bw * 0.5f, by = h * 0.38f;
            if (UiSfx.Button(new Rect(bx, by, bw, bh), "继续", _btn)) SetPaused(false);
            if (UiSfx.Button(new Rect(bx, by + gap, bw, bh), "操作说明", _btn)) _showControls = true;
            if (UiSfx.Button(new Rect(bx, by + gap * 2, bw, bh), "设置", _btn)) _showSettings = true;
            if (UiSfx.Button(new Rect(bx, by + gap * 3, bw, bh), "重新开始", _btn))
            { SetPaused(false); SceneFlow.RestartCurrent(); }
            if (UiSfx.Button(new Rect(bx, by + gap * 4, bw, bh), "返回主菜单", _btn))
            { SetPaused(false); SceneFlow.LoadMainMenu(); }
        }

        void DrawControls(float w, float h)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.76f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = 480, ph = 300, px = w * 0.5f - pw * 0.5f, py = h * 0.5f - ph * 0.5f;
            GUI.Box(new Rect(px, py, pw, ph), "操作说明", _boxTitle);
            foreach (var (i, s) in ControlLines())
                GUI.Label(new Rect(px + 22, py + 42 + i * 24, pw - 44, 22), s, _line);

            if (UiSfx.Button(new Rect(px + pw * 0.5f - 60, py + ph - 46, 120, 34), "返回"))
                _showControls = false;
        }

        static readonly string[] MouseKbLines =
        {
            "鼠标移动     瞄准 / 转向 —— 无人机朝你指的方向飞",
            "左键         引爆战斗部",
            "右键(按住)   精瞄(拉近 + 减速瞄准)",
            "W / S        油门 / 刹车        Shift  加速",
            "空格 / Ctrl  升降微调",
            "Q / E        横滚               R      呼回",
            "Esc          暂停 / 继续",
        };
        static readonly string[] GamepadLines =
        {
            "右摇杆       瞄准 / 转向 —— 无人机朝你指的方向飞",
            "A / RB       引爆战斗部",
            "左扳机(按住) 精瞄(拉近 + 减速瞄准)",
            "左摇杆Y      油门 / 刹车        右扳机  加速",
            "RB / LB      升降微调",
            "左摇杆X      横滚               Y       呼回",
            "Esc          暂停 / 继续",
        };

        /// <summary>Mouse+keyboard lines, or gamepad lines if that's what the
        /// player was last actually using (Ironfield.Drone.DroneInput tracks it).</summary>
        internal static System.Collections.Generic.IEnumerable<(int, string)> ControlLines()
        {
            string[] lines = Ironfield.Drone.DroneInput.LastWasGamepad ? GamepadLines : MouseKbLines;
            for (int i = 0; i < lines.Length; i++) yield return (i, lines[i]);
        }
    }
}
