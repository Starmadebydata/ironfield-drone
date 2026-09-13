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

            float pw = 440, ph = 504;
            float px = w * 0.5f - pw * 0.5f, py = h * 0.5f - ph * 0.5f;
            GUI.Box(new Rect(px, py, pw, ph), Core.Loc.Get("menu.settings"));

            var title = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            float y = py + 36, rowH = 30;
            float lx = px + 20, lw = 140, sx = px + 168, sw = pw - 208;

            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.master_volume"), title);
            Core.GameSettings.SetMasterVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.MasterVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.sfx_volume"), title);
            Core.GameSettings.SetSfxVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.SfxVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.engine_volume"), title);
            Core.GameSettings.SetEngineVolume(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.EngineVolume, 0f, 1f));
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.mouse_sensitivity"), title);
            Core.GameSettings.SetAimSensitivity(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.AimSensitivity, 0.3f, 2.5f));
            y += rowH;

            bool inv = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.InvertY, Core.Loc.Get("settings.invert_y"));
            if (inv != Core.GameSettings.InvertY) Core.GameSettings.SetInvertY(inv);
            y += rowH;

            bool cb = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.ColorblindMode,
                Core.Loc.Get("settings.colorblind"));
            if (cb != Core.GameSettings.ColorblindMode) Core.GameSettings.SetColorblindMode(cb);
            y += rowH;

            bool shake = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.CameraShake, Core.Loc.Get("settings.camera_shake"));
            if (shake != Core.GameSettings.CameraShake) Core.GameSettings.SetCameraShake(shake);
            y += rowH;

            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.ui_scale"), title);
            Core.GameSettings.SetUiScale(
                GUI.HorizontalSlider(new Rect(sx, y + 10, sw, rowH), Core.GameSettings.UiScale, 0.8f, 1.4f));
            y += rowH;

            bool auto = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.AutoAttack,
                Core.Loc.Get("settings.auto_attack"));
            if (auto != Core.GameSettings.AutoAttack) Core.GameSettings.SetAutoAttack(auto);
            y += rowH + 8;

            string[] names = QualitySettings.names;
            string cur = names.Length > 0 ? names[Mathf.Clamp(Core.GameSettings.QualityLevel, 0, names.Length - 1)] : "-";
            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.quality"), title);
            if (UiSfx.Button(new Rect(sx, y, sw, rowH), cur))
                Core.GameSettings.SetQualityLevel((Core.GameSettings.QualityLevel + 1) % Mathf.Max(1, names.Length));
            y += rowH;

            // Language names are each language's own endonym (not run through
            // Loc.Get) so a player can always find their language regardless
            // of what the UI is currently showing.
            GUI.Label(new Rect(lx, y, lw, rowH), Core.Loc.Get("settings.language"), title);
            int langCount = System.Enum.GetValues(typeof(Core.Language)).Length;
            if (UiSfx.Button(new Rect(sx, y, sw, rowH), Core.Loc.LanguageNames[(int)Core.GameSettings.Language]))
                Core.GameSettings.SetLanguage((Core.Language)(((int)Core.GameSettings.Language + 1) % langCount));
            y += rowH;

            bool full = GUI.Toggle(new Rect(lx, y, pw - 40, rowH), Core.GameSettings.Fullscreen,
                Core.Loc.Get("settings.fullscreen"));
            if (full != Core.GameSettings.Fullscreen) Core.GameSettings.SetFullscreen(full);
            y += rowH + 14;

            if (UiSfx.Button(new Rect(px + pw * 0.5f - 60, y, 120, 34), Core.Loc.Get("common.back")))
                onBack?.Invoke();
        }
    }
}
