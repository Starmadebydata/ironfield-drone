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

        static void Apply()
        {
            AudioListener.volume = MasterVolume;
            int max = Mathf.Max(0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(Mathf.Clamp(QualityLevel, 0, max), true);
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
            PlayerPrefs.Save();
        }

        // Re-Load() on the next domain reload / Play session (editor test isolation).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _loaded = false;
    }
}
