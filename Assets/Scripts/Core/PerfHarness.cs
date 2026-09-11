using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Ironfield.Core
{
    /// <summary>
    /// Real-machine perf smoke test — launch a built Player with -perftest on
    /// the command line and it forces full-throttle debug flight for a fixed
    /// duration, logs rolling FPS to a text file, then quits. A complete no-op
    /// (disables itself in Awake) in a normal play session, so it's safe to
    /// leave wired into every mission scene.
    /// </summary>
    public class PerfHarness : MonoBehaviour
    {
        public Ironfield.Mission.MissionManager mission;
        const float Duration = 20f;
        const float WarmupSeconds = 8f; // let first-appearance shader variants compile before sampling

        bool _active;
        float _elapsed;
        int _frames;
        float _fpsSum;
        readonly List<float> _fpsSamples = new(2000);
        StringBuilder _log;

        void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            _active = System.Array.IndexOf(args, "-perftest") >= 0;
            enabled = _active;
            if (!_active) return;

            // uncapped, so the number reflects real headroom rather than the display's refresh rate
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            _log = new StringBuilder();
            _log.AppendLine($"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} " +
                             $"res={Screen.width}x{Screen.height} " +
                             $"quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
                             $"vsync=off");
        }

        void Update()
        {
            if (mission != null && mission.ActiveDrone != null)
            {
                mission.ActiveDrone.useDebugInput = true;
                mission.ActiveDrone.debugInput = new Vector4(1f, 0f, 0f, 0f); // full forward, level
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < WarmupSeconds) return;

            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _frames++;
            _fpsSum += fps;
            _fpsSamples.Add(fps);

            if (_elapsed >= WarmupSeconds + Duration)
            {
                float avg = _fpsSum / Mathf.Max(1, _frames);
                _fpsSamples.Sort();
                // 1% low reads a lot more like "does it stutter" than a single-frame min,
                // which is easily an unrepresentative one-off hitch (GC, async load, etc.)
                int p1Count = Mathf.Max(1, _fpsSamples.Count / 100);
                float p1Low = 0f;
                for (int i = 0; i < p1Count; i++) p1Low += _fpsSamples[i];
                p1Low /= p1Count;

                _log.AppendLine($"avg_fps={avg:0.0} p1low_fps={p1Low:0.0} "
                               + $"worst_fps={_fpsSamples[0]:0.0} frames={_frames}");
                string path = Path.Combine(Application.persistentDataPath, "perf_log.txt");
                File.WriteAllText(path, _log.ToString());
                Debug.Log("[PerfHarness] wrote " + path);
                Application.Quit();
            }
        }
    }
}
