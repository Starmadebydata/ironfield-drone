using System.Collections;
using System.IO;
using UnityEngine;

namespace Ironfield.Core
{
    /// <summary>
    /// In-game frame recorder for producing demo/marketing videos without any
    /// OS-level screen capture: reads straight from the game's own rendered
    /// frame each tick and writes it to disk as a JPG. No macOS Screen
    /// Recording permission needed, and nothing outside the game window can
    /// ever end up in a frame — this only ever sees what the game itself
    /// renders. Gated on -record; a complete no-op otherwise. Stitch the
    /// output frames into a video afterward with ffmpeg (not part of the
    /// build — this only produces the frames).
    /// </summary>
    public class DemoRecorder : MonoBehaviour
    {
        const float Fps = 15f;

        bool _active;
        int _frame;
        string _dir;

        void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            _active = System.Array.IndexOf(args, "-record") >= 0;
            if (!_active) { enabled = false; return; }

            _dir = Path.Combine(Application.persistentDataPath, "frames");
            Directory.CreateDirectory(_dir);
            DontDestroyOnLoad(gameObject);
            Debug.Log("[DemoRecorder] writing frames to " + _dir);
            StartCoroutine(CaptureLoop());
        }

        IEnumerator CaptureLoop()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] jpg = tex.EncodeToJPG(85);
                Destroy(tex);
                File.WriteAllBytes(Path.Combine(_dir, $"frame_{_frame:D5}.jpg"), jpg);
                _frame++;
                yield return new WaitForSecondsRealtime(1f / Fps);
            }
        }
    }
}
