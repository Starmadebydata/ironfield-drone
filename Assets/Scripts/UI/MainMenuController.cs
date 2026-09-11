using Ironfield.Core;
using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>Title screen: start the mission, open settings, or quit. IMGUI —
    /// see SettingsGUI for why.</summary>
    public class MainMenuController : MonoBehaviour
    {
        bool _showSettings;
        GUIStyle _title, _subtitle, _btn, _footer;

        void Awake() => GameSettings.Load();

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _title.normal.textColor = new Color(0.88f, 0.95f, 0.87f);
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _subtitle.normal.textColor = new Color(0.75f, 0.8f, 0.75f);
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _footer = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _footer.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            if (_showSettings)
            {
                SettingsGUI.Draw(w, h, () => _showSettings = false);
                return;
            }

            GUI.Label(new Rect(0, h * 0.16f, w, 60), "IRONFIELD", _title);
            GUI.Label(new Rect(0, h * 0.16f + 56, w, 22),
                "无人机突袭 · 一片虚构的东欧战线", _subtitle);

            float bw = 240, bh = 48, bx = w * 0.5f - bw * 0.5f, by = h * 0.46f, gap = bh + 14;
            if (GUI.Button(new Rect(bx, by, bw, bh), "开始任务", _btn))
                SceneFlow.LoadMission();
            if (GUI.Button(new Rect(bx, by + gap, bw, bh), "设置", _btn))
                _showSettings = true;
            if (GUI.Button(new Rect(bx, by + gap * 2, bw, bh), "退出", _btn))
                SceneFlow.Quit();

            GUI.Label(new Rect(14, h - 20, 640, 18),
                "prototype v0.1 — MIT licensed, third-party model credits in CREDITS.md", _footer);
        }
    }
}
