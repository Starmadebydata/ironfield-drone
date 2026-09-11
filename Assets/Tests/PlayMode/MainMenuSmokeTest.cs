using System.Collections;
using Ironfield.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ironfield.Tests
{
    /// <summary>Sanity check for the P0 menu scaffold: MainMenu loads clean and
    /// wires up its controller.</summary>
    public class MainMenuSmokeTest
    {
        [UnityTest]
        public IEnumerator MainMenu_loads_without_errors()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            var ctrl = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsNotNull(ctrl, "MainMenuController missing from MainMenu scene");
        }
    }
}
