using UnityEngine;

namespace Ironfield.Core
{
    /// <summary>
    /// PlayerPrefs-backed user settings: audio volumes, mouse sensitivity / Y
    /// invert, quality preset. Static so menus and gameplay code can read/write
    /// it without a scene reference. <see cref="Load"/> pulls persisted values
    /// in (call once at boot, from any entry scene); every setter also applies
    /// and saves immediately.
    /// </summary>
    public static class GameSettings
    {
        const string KMaster = "ironfield.vol.master";
        const string KSfx = "ironfield.vol.sfx";
        const string KEngine = "ironfield.vol.engine";
        const string KSens = "ironfield.aim.sensitivity";
        const string KInvert = "ironfield.aim.inverty";
        const string KQuality = "ironfield.quality";
        const string KColorblind = "ironfield.a11y.colorblind";
        const string KCameraShake = "ironfield.a11y.camerashake";
        const string KAutoAttack = "ironfield.autoattack";
        const string KSelectedDrone = "ironfield.selecteddrone";
        const string KUiScale = "ironfield.a11y.uiscale";
        const string KLanguage = "ironfield.language";
        const string KFullscreen = "ironfield.display.fullscreen";

        public static float MasterVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static float EngineVolume { get; private set; } = 1f;
        /// <summary>Multiplier on DroneController.aimSensitivity (1 = default).</summary>
        public static float AimSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static int QualityLevel { get; private set; }
        /// <summary>Swaps HUD target/lock colours for a blue/orange palette
        /// instead of red/green/yellow (safe for the common red-green forms).</summary>
        public static bool ColorblindMode { get; private set; }
        public static bool CameraShake { get; private set; } = true;
        /// <summary>When on, a committed dive on a hard-locked target (the drone
        /// already roughly pointed at it — see DiveAssist) finishes itself: full
        /// autopilot onto the target plus an automatic warhead trigger in range.
        /// Off by default — manual flying is the core skill of this game; this is
        /// an opt-in convenience/accessibility toggle, not the default experience.</summary>
        public static bool AutoAttack { get; private set; }
        /// <summary>0 = Light (default, always unlocked), 1 = Heavy (unlocked once
        /// Mission02 is — i.e. after winning Mission01, see CampaignProgress).</summary>
        public static int SelectedDrone { get; private set; }
        /// <summary>Scales every IMGUI screen (menus/HUD/pause) around screen
        /// centre — see Ironfield.UI.UiScaling. 1 = default size.</summary>
        public static float UiScale { get; private set; } = 1f;
        /// <summary>UI language — defaults to English regardless of system
        /// locale (the user's explicit call, not auto-detected).</summary>
        public static Language Language { get; private set; } = Language.English;
        /// <summary>Whether the game window runs fullscreen (borderless
        /// FullScreenWindow) or windowed. Defaults to true — the project's own
        /// FullScreenMode default — but the Player build previously shipped
        /// with no in-game way to leave fullscreen (only the OS-level Alt+Enter
        /// shortcut, which most players never discover); this setting exposes
        /// the toggle Settings needs regardless of that default.</summary>
        public static bool Fullscreen { get; private set; } = true;

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            MasterVolume = PlayerPrefs.GetFloat(KMaster, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KSfx, 1f);
            EngineVolume = PlayerPrefs.GetFloat(KEngine, 1f);
            AimSensitivity = PlayerPrefs.GetFloat(KSens, 1f);
            InvertY = PlayerPrefs.GetInt(KInvert, 0) != 0;
            QualityLevel = PlayerPrefs.GetInt(KQuality, QualitySettings.GetQualityLevel());
            ColorblindMode = PlayerPrefs.GetInt(KColorblind, 0) != 0;
            CameraShake = PlayerPrefs.GetInt(KCameraShake, 1) != 0;
            AutoAttack = PlayerPrefs.GetInt(KAutoAttack, 0) != 0;
            SelectedDrone = PlayerPrefs.GetInt(KSelectedDrone, 0);
            UiScale = PlayerPrefs.GetFloat(KUiScale, 1f);
            Language = (Language)PlayerPrefs.GetInt(KLanguage, (int)Language.English);
            Fullscreen = PlayerPrefs.GetInt(KFullscreen, 1) != 0;
            Apply();
        }

        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); Apply(); Save(); }
        public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); Save(); }
        public static void SetEngineVolume(float v) { EngineVolume = Mathf.Clamp01(v); Save(); }
        public static void SetAimSensitivity(float v) { AimSensitivity = Mathf.Clamp(v, 0.3f, 2.5f); Save(); }
        public static void SetInvertY(bool v) { InvertY = v; Save(); }
        public static void SetQualityLevel(int v) { QualityLevel = Mathf.Max(0, v); Apply(); Save(); }
        public static void SetColorblindMode(bool v) { ColorblindMode = v; Save(); }
        public static void SetCameraShake(bool v) { CameraShake = v; Save(); }
        public static void SetAutoAttack(bool v) { AutoAttack = v; Save(); }
        public static void SetSelectedDrone(int v) { SelectedDrone = Mathf.Clamp(v, 0, 1); Save(); }
        public static void SetUiScale(float v) { UiScale = Mathf.Clamp(v, 0.8f, 1.4f); Save(); }
        public static void SetLanguage(Language v) { Language = v; Save(); }
        public static void SetFullscreen(bool v) { Fullscreen = v; Apply(); Save(); }

        static void Apply()
        {
            AudioListener.volume = MasterVolume;
            int max = Mathf.Max(0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(Mathf.Clamp(QualityLevel, 0, max), true);
            // Windowed mode uses the project's own default resolution at a
            // resizable window (FullScreenMode.Windowed ignores resizableWindow
            // in some Unity versions, so set it explicitly too) rather than
            // whatever tiny/huge size the OS last remembered.
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!Fullscreen) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(KMaster, MasterVolume);
            PlayerPrefs.SetFloat(KSfx, SfxVolume);
            PlayerPrefs.SetFloat(KEngine, EngineVolume);
            PlayerPrefs.SetFloat(KSens, AimSensitivity);
            PlayerPrefs.SetInt(KInvert, InvertY ? 1 : 0);
            PlayerPrefs.SetInt(KQuality, QualityLevel);
            PlayerPrefs.SetInt(KColorblind, ColorblindMode ? 1 : 0);
            PlayerPrefs.SetInt(KCameraShake, CameraShake ? 1 : 0);
            PlayerPrefs.SetInt(KAutoAttack, AutoAttack ? 1 : 0);
            PlayerPrefs.SetInt(KSelectedDrone, SelectedDrone);
            PlayerPrefs.SetFloat(KUiScale, UiScale);
            PlayerPrefs.SetInt(KLanguage, (int)Language);
            PlayerPrefs.SetInt(KFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Re-Load() on the next domain reload / Play session (editor test isolation).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _loaded = false;
    }
}
