using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>
    /// Shared IMGUI settings panel, drawn by both the main menu and the in-mission
    /// pause menu so there is exactly one place that edits Ironfield.Core.GameSettings.
    /// IMGUI (not uGUI/TextMeshPro) to match HudController and avoid depending on a
    /// TextMeshPro essential-resources import, which this headless pipeline can't
    /// drive reliably — see ROADMAP.md P2 for the eventual uGUI pass.
    /// </summary>
    public static class SettingsGUI
    {
        public static void Draw(float w, float h, System.Action onBack)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = 440, ph = 300;
            float px = w * 0.5f - pw * 0.5f, py = h * 0.5f - ph * 0.5f;
            GUI.Box(new Rect(px, py, pw, ph), "设置");

            var title = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            float y = py + 36, rowH = 30;
            float lx = px + 20, lw = 140, sx = px + 168, sw = pw - 208;

            GUI.Label(new Rect(lx, y, lw, rowH), "主音量", title);
            Core.GameSettings.SetMasterVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.MasterVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), "音效音量", title);
            Core.GameSettings.SetSfxVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.SfxVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), "引擎音量", title);
            Core.GameSettings.SetEngineVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.EngineVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), "鼠标灵敏度", title);
            Core.GameSettings.SetAimSensitivity(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.AimSensitivity, 0.3f, 2.5f));
            y += rowH;

            bool inv = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.InvertY, " 反转Y轴");
            if (inv != Core.GameSettings.InvertY) Core.GameSettings.SetInvertY(inv);
            y += rowH + 8;

            string[] names = QualitySettings.names;
            string cur = names.Length > 0 ? names[Mathf.Clamp(Core.GameSettings.QualityLevel, 0, names.Length - 1)] : "-";
            GUI.Label(new Rect(lx, y, lw, rowH), "画质", title);
            if (UiSfx.Button(new Rect(sx, y, sw, rowH), cur))
                Core.GameSettings.SetQualityLevel((Core.GameSettings.QualityLevel + 1) % Mathf.Max(1, names.Length));
            y += rowH + 14;

            if (UiSfx.Button(new Rect(px + pw * 0.5f - 60, y, 120, 34), "返回"))
                onBack?.Invoke();
        }
    }
}
