using UnityEngine;

namespace Ironfield.Core
{
    /// <summary>PlayerPrefs-backed campaign progress: how many missions are
    /// unlocked, and the best score/grade recorded per mission. Mission 0 is
    /// always unlocked.</summary>
    public static class CampaignProgress
    {
        const string KUnlocked = "ironfield.campaign.unlocked";
        const string KBestScorePrefix = "ironfield.campaign.best.score.";
        const string KBestGradePrefix = "ironfield.campaign.best.grade.";

        /// <summary>Number of mission slots unlocked, counted from the start of MissionCatalog.All.</summary>
        public static int UnlockedCount => Mathf.Max(1, PlayerPrefs.GetInt(KUnlocked, 1));

        public static bool IsUnlocked(int missionIndex) => missionIndex < UnlockedCount;

        /// <summary>Best recorded result for a mission, or null if never completed.</summary>
        public static (int score, string grade)? BestFor(string missionId)
        {
            if (!PlayerPrefs.HasKey(KBestScorePrefix + missionId)) return null;
            int score = PlayerPrefs.GetInt(KBestScorePrefix + missionId, 0);
            string grade = PlayerPrefs.GetString(KBestGradePrefix + missionId, "");
            return (score, grade);
        }

        /// <summary>Call once when a mission ends: records the best score/grade and,
        /// on a win, unlocks the next campaign slot.</summary>
        public static void ReportResult(string missionId, bool won, int score, string grade)
        {
            if (string.IsNullOrEmpty(missionId)) return;

            var best = BestFor(missionId);
            if (best == null || score > best.Value.score)
            {
                PlayerPrefs.SetInt(KBestScorePrefix + missionId, score);
                PlayerPrefs.SetString(KBestGradePrefix + missionId, grade);
            }

            if (won)
            {
                int idx = MissionCatalog.IndexOf(missionId);
                if (idx >= 0)
                {
                    int unlockThrough = Mathf.Min(MissionCatalog.All.Length, idx + 2);
                    if (unlockThrough > UnlockedCount)
                        PlayerPrefs.SetInt(KUnlocked, unlockThrough);
                }
            }
            PlayerPrefs.Save();
        }
    }
}
