using Ironfield.Core;
using UnityEngine;

namespace Ironfield.UI
{
    /// <summary>
    /// One persistent click sound for every IMGUI button in the game (menus,
    /// pause, end panel). Lives on a DontDestroyOnLoad object seeded in
    /// MainMenu.unity, so it survives every scene load after that. <see cref="Button"/>
    /// is a drop-in replacement for GUI.Button that also plays the click.
    /// </summary>
    public class UiSfx : MonoBehaviour
    {
        public static UiSfx Instance { get; private set; }
        public AudioClip clickClip;
        AudioSource _src;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;
            _src.playOnAwake = false;
        }

        public static void Click()
        {
            if (Instance == null || Instance.clickClip == null || Instance._src == null) return;
            Instance._src.PlayOneShot(Instance.clickClip, GameSettings.SfxVolume);
        }

        /// <summary>GUI.Button, but it plays the shared click sound on press.</summary>
        public static bool Button(Rect r, string text, GUIStyle style = null)
        {
            bool pressed = style != null ? GUI.Button(r, text, style) : GUI.Button(r, text);
            if (pressed) Click();
            return pressed;
        }
    }
}
