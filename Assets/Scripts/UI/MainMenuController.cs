using Ironfield.Core;
using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>Title screen: pick a mission, open settings, or quit. IMGUI —
    /// see SettingsGUI for why.</summary>
    public class MainMenuController : MonoBehaviour
    {
        enum Panel { Title, Missions, Settings, Credits }
        Panel _panel;

        GUIStyle _title, _subtitle, _btn, _footer, _missionName, _missionSub, _sectionTitle, _creditsLine;

        void Awake()
        {
            GameSettings.Load();
            // -perftest jumps straight into a mission so PerfHarness can measure
            // real gameplay instead of the menu.
            var args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "-perftest") >= 0)
                SceneFlow.LoadMissionByName(PerfTestScene(args));
        }

        static string PerfTestScene(string[] args)
        {
            int i = System.Array.IndexOf(args, "-perftest-scene");
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "Mission03";
        }

        /// <summary>Demo/test hook: jump straight to the mission-select panel.</summary>
        public void DebugShowMissions() => _panel = Panel.Missions;
        /// <summary>Test hook: jump straight to the credits panel.</summary>
        public void DebugShowCredits() => _panel = Panel.Credits;

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
            _creditsLine = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _creditsLine.normal.textColor = new Color(0.82f, 0.85f, 0.82f);
        }

        void OnGUI()
        {
            // See Ironfield.UI.UiScaling.
            var m = UiScaling.Begin();
            try { DrawGUI(); }
            finally { UiScaling.End(m); }
        }

        void DrawGUI()
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
                case Panel.Credits:
                    DrawCredits(w, h);
                    return;
            }

            GUI.Label(new Rect(0, h * 0.16f, w, 60), "IRONFIELD", _title);
            GUI.Label(new Rect(0, h * 0.16f + 56, w, 22),
                "无人机突袭 · 一片虚构的东欧战线", _subtitle);

            float bw = 240, bh = 48, bx = w * 0.5f - bw * 0.5f, by = h * 0.42f, gap = bh + 14;
            if (UiSfx.Button(new Rect(bx, by, bw, bh), "开始任务", _btn))
                _panel = Panel.Missions;
            if (UiSfx.Button(new Rect(bx, by + gap, bw, bh), "设置", _btn))
                _panel = Panel.Settings;
            if (UiSfx.Button(new Rect(bx, by + gap * 2, bw, bh), "制作人员", _btn))
                _panel = Panel.Credits;
            if (UiSfx.Button(new Rect(bx, by + gap * 3, bw, bh), "退出", _btn))
                SceneFlow.Quit();

            GUI.Label(new Rect(14, h - 20, 640, 18),
                "prototype v0.1 — MIT licensed, third-party model credits in CREDITS.md", _footer);
        }

        /// <summary>Ships the CC-BY attributions with the build itself (not
        /// just the repo's CREDITS.md, which a downloaded build doesn't carry)
        /// — required before distributing anywhere. CC0 assets are credited
        /// too, matching this project's convention, even though their license
        /// doesn't require it.</summary>
        static readonly (string line, string license)[] CreditsEntries =
        {
            ("无人机模型 \"Drone\" — NateGazzard (poly.pizza)", "CC-BY 3.0"),
            ("坦克模型 \"Tank\" — Nico _ (poly.pizza)", "CC-BY 3.0"),
            ("步兵战车模型 \"Super Tank\" — Zsky (poly.pizza)", "CC-BY 3.0"),
            ("卡车模型 — Alex Safayan (poly.pizza)", "CC-BY 3.0"),
            ("针叶树 / 枯树模型 — Danni Bittman (poly.pizza)", "CC-BY 3.0"),
            ("阔叶树模型 \"Common Tree\" — Quaternius (poly.pizza)", "CC0"),
            ("建筑模型 — Kenney / Quaternius (poly.pizza)", "CC0"),
            ("音效 — Kenney.nl (Sci-fi / Impact / Interface Sounds)", "CC0"),
        };

        void DrawCredits(float w, float h)
        {
            GUI.Label(new Rect(0, h * 0.08f, w, 34), "制作人员 · Credits", _sectionTitle);
            GUI.Label(new Rect(0, h * 0.08f + 36, w, 20),
                "第三方素材完整清单见仓库 CREDITS.md — Full attributions in CREDITS.md", _missionSub);

            float y = h * 0.08f + 68, cw = 620, cx = w * 0.5f - cw * 0.5f;
            for (int i = 0; i < CreditsEntries.Length; i++)
            {
                var (line, license) = CreditsEntries[i];
                GUI.Label(new Rect(cx, y, cw - 90, 22), line, _creditsLine);
                GUI.Label(new Rect(cx + cw - 90, y, 90, 22), license, _missionSub);
                y += 26;
            }

            GUI.Label(new Rect(cx, y + 10, cw, 20),
                "全部地形/道路/植被散布/UI/游戏代码为本项目原创。", _missionSub);

            if (UiSfx.Button(new Rect(w * 0.5f - 60, h - 70, 120, 34), "返回"))
                _panel = Panel.Title;
        }

        void DrawMissions(float w, float h)
        {
            GUI.Label(new Rect(0, h * 0.08f, w, 34), "选择任务", _sectionTitle);

            float droneY = h * 0.08f + 42;
            float droneH = DrawDronePicker(w, droneY);

            var entries = MissionCatalog.All;
            float cw = 420, ch = 72, gap = 14;
            float cx = w * 0.5f - cw * 0.5f;
            float cy = droneY + droneH + 22;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                bool unlocked = CampaignProgress.IsUnlocked(i);
                var best = CampaignProgress.BestFor(e.Id);
                Rect r = new Rect(cx, cy + i * (ch + gap), cw, ch);

                GUI.enabled = unlocked;
                if (UiSfx.Button(r, string.Empty))
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
            if (UiSfx.Button(new Rect(w * 0.5f - 60, backY, 120, 34), "返回"))
                _panel = Panel.Title;
        }

        /// <summary>Light/Heavy drone picker, drawn above the mission list since
        /// the choice applies campaign-wide, not per mission. Returns the row's
        /// height so the caller can lay out what comes after it.</summary>
        float DrawDronePicker(float w, float y)
        {
            const float dw = 210, dh = 54, dgap = 16;
            float startX = w * 0.5f - (dw * 2 + dgap) * 0.5f;
            bool heavyUnlocked = CampaignProgress.IsUnlocked(1);

            DrawDroneOption(new Rect(startX, y, dw, dh), 0,
                "轻型无人机", "标准 · 敏捷", true);
            DrawDroneOption(new Rect(startX + dw + dgap, y, dw, dh), 1,
                "重型无人机", heavyUnlocked ? "重装甲 · 高伤害 · 较慢" : "打通任务01解锁", heavyUnlocked);

            return dh;
        }

        void DrawDroneOption(Rect r, int index, string name, string sub, bool unlocked)
        {
            bool selected = GameSettings.SelectedDrone == index;
            GUI.enabled = unlocked;
            if (UiSfx.Button(r, string.Empty, _btn) && unlocked)
                GameSettings.SetSelectedDrone(index);
            GUI.enabled = true;

            string label = (selected ? "✓ " : "") + (unlocked ? name : name + "  🔒");
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 20), label, _missionName);
            GUI.Label(new Rect(r.x + 12, r.y + 28, r.width - 24, 20), sub, _missionSub);
        }
    }
}
