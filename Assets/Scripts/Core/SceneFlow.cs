using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfield.Core
{
    /// <summary>Scene-transition helper shared by the main menu, pause menu, and
    /// mission end panel. Always resets Time.timeScale so a paused/frozen state
    /// can never leak across a scene load.</summary>
    public static class SceneFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string MissionScene = "Mission01";

        public static void LoadMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenuScene);
        }

        public static void LoadMission()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MissionScene);
        }

        public static void RestartCurrent()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
