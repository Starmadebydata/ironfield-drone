namespace Ironfield.Core
{
    /// <summary>One campaign slot: identity, display name, and the scene that
    /// holds it. Fixed at compile time — this project's scenes are pre-baked by
    /// IronfieldSetup, not generated at runtime, so there is nothing to load
    /// from data here.</summary>
    public readonly struct MissionEntry
    {
        public readonly string Id;
        /// <summary>A Loc key, not the literal display text — call
        /// Loc.Get(DisplayName) at draw time, not GUI.Label(DisplayName, ...)
        /// directly, or a language switch won't reach this string.</summary>
        public readonly string DisplayName;
        public readonly string SceneName;

        public MissionEntry(string id, string displayName, string sceneName)
        {
            Id = id; DisplayName = displayName; SceneName = sceneName;
        }
    }

    /// <summary>The fixed campaign order. Keep in sync with the scenes
    /// IronfieldSetup.Run() builds (Assets/Editor/IronfieldSetup.cs).</summary>
    public static class MissionCatalog
    {
        public static readonly MissionEntry[] All =
        {
            new("m01", "mission.m01.name", "Mission01"),
            new("m02", "mission.m02.name", "Mission02"),
            new("m03", "mission.m03.name", "Mission03"),
        };

        public static int IndexOf(string missionId)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == missionId) return i;
            return -1;
        }

        /// <summary>Scene name of the mission after this one, or null if this was the last.</summary>
        public static string NextSceneName(string currentMissionId)
        {
            int i = IndexOf(currentMissionId);
            if (i < 0 || i + 1 >= All.Length) return null;
            return All[i + 1].SceneName;
        }
    }
}
