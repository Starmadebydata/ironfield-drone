using Ironfield.Core;
using NUnit.Framework;
using UnityEngine;

namespace Ironfield.Tests
{
    public class CampaignProgressTests
    {
        const string UnlockedKey = "ironfield.campaign.unlocked";

        [SetUp]
        public void ResetProgress()
        {
            // isolate from whatever a prior editor Play session left behind
            PlayerPrefs.DeleteKey(UnlockedKey);
            foreach (var e in MissionCatalog.All)
            {
                PlayerPrefs.DeleteKey("ironfield.campaign.best.score." + e.Id);
                PlayerPrefs.DeleteKey("ironfield.campaign.best.grade." + e.Id);
            }
        }

        [Test]
        public void First_mission_starts_unlocked_only()
        {
            Assert.AreEqual(1, CampaignProgress.UnlockedCount);
            Assert.IsTrue(CampaignProgress.IsUnlocked(0));
            Assert.IsFalse(CampaignProgress.IsUnlocked(1));
        }

        [Test]
        public void Winning_a_mission_unlocks_the_next_one()
        {
            CampaignProgress.ReportResult("m01", won: true, score: 4000, grade: "S");
            Assert.IsTrue(CampaignProgress.IsUnlocked(1));
            Assert.IsFalse(CampaignProgress.IsUnlocked(2));
        }

        [Test]
        public void Losing_does_not_unlock_the_next_one()
        {
            CampaignProgress.ReportResult("m01", won: false, score: 500, grade: "D");
            Assert.IsFalse(CampaignProgress.IsUnlocked(1));
        }

        [Test]
        public void Best_result_keeps_the_higher_score()
        {
            CampaignProgress.ReportResult("m01", won: true, score: 1000, grade: "B");
            CampaignProgress.ReportResult("m01", won: false, score: 300, grade: "F");
            var best = CampaignProgress.BestFor("m01");
            Assert.IsNotNull(best);
            Assert.AreEqual(1000, best.Value.score);
            Assert.AreEqual("B", best.Value.grade);
        }

        [Test]
        public void Catalog_chains_missions_in_order()
        {
            Assert.AreEqual("Mission02", MissionCatalog.NextSceneName("m01"));
            Assert.AreEqual("Mission03", MissionCatalog.NextSceneName("m02"));
            Assert.IsNull(MissionCatalog.NextSceneName("m03"));
        }
    }
}
