using Ironfield.Core;
using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>Title screen: pick a mission, open settings, or quit. IMGUI —
    /// see SettingsGUI for why.</summary>
    public class MainMenuController : MonoBehaviour
    {
        enum Panel { Title, Missions, Settings }
        Panel _panel;

        GUIStyle _title, _subtitle, _btn, _footer, _missionName, _missionSub, _sectionTitle;

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
            _sectionTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _sectionTitle.normal.textColor = Color.white;
            _missionName = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _missionName.normal.textColor = Color.white;
            _missionSub = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _missionSub.normal.textColor = new Color(0.7f, 0.75f, 0.7f);
        }

        void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            switch (_panel)
            {
                case Panel.Settings:
                    SettingsGUI.Draw(w, h, () => _panel = Panel.Title);
                    return;
                case Panel.Missions:
                    DrawMissions(w, h);
                    return;
            }

            GUI.Label(new Rect(0, h * 0.16f, w, 60), "IRONFIELD", _title);
            GUI.Label(new Rect(0, h * 0.16f + 56, w, 22),
                "无人机突袭 · 一片虚构的东欧战线", _subtitle);

            float bw = 240, bh = 48, bx = w * 0.5f - bw * 0.5f, by = h * 0.46f, gap = bh + 14;
            if (GUI.Button(new Rect(bx, by, bw, bh), "开始任务", _btn))
                _panel = Panel.Missions;
            if (GUI.Button(new Rect(bx, by + gap, bw, bh), "设置", _btn))
                _panel = Panel.Settings;
            if (GUI.Button(new Rect(bx, by + gap * 2, bw, bh), "退出", _btn))
                SceneFlow.Quit();

            GUI.Label(new Rect(14, h - 20, 640, 18),
                "prototype v0.1 — MIT licensed, third-party model credits in CREDITS.md", _footer);
        }

        void DrawMissions(float w, float h)
        {
            GUI.Label(new Rect(0, h * 0.14f, w, 34), "选择任务", _sectionTitle);

            var entries = MissionCatalog.All;
            float cw = 420, ch = 72, gap = 14;
            float cx = w * 0.5f - cw * 0.5f;
            float cy = h * 0.14f + 50;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool unlocked = CampaignProgress.IsUnlocked(i);
                var best = CampaignProgress.BestFor(e.Id);
                Rect r = new Rect(cx, cy + i * (ch + gap), cw, ch);

                GUI.enabled = unlocked;
                if (GUI.Button(r, string.Empty))
                    SceneFlow.LoadMissionByName(e.SceneName);
                GUI.enabled = true;

                string name = unlocked ? e.DisplayName : $"{e.DisplayName}  🔒";
                GUI.Label(new Rect(r.x + 18, r.y + 10, r.width - 36, 24), name, _missionName);
                string sub = !unlocked ? "先打通上一关解锁"
                    : best != null ? $"最佳评分 {best.Value.score}    评级 {best.Value.grade}"
                    : "尚未通关";
                GUI.Label(new Rect(r.x + 18, r.y + 36, r.width - 36, 20), sub, _missionSub);
            }

            float backY = cy + entries.Length * (ch + gap) + 8;
            if (GUI.Button(new Rect(w * 0.5f - 60, backY, 120, 34), "返回"))
                _panel = Panel.Title;
        }
    }
}
