using Ironfield.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Ironfield.Tests
{
    public class HealthComponentTests
    {
        HealthComponent Make(float hp, float armor, float pierce = 0.8f)
        {
            var go = new GameObject("h");
            var h = go.AddComponent<HealthComponent>();
            h.maxHealth = hp;
            h.armor = armor;
            h.explosiveArmorPierce = pierce;
            // Awake ran on AddComponent; force a known start.
            h.ResetHealth();
            return h;
        }

        [Test]
        public void Kinetic_hit_is_reduced_by_full_armor()
        {
            var h = Make(100f, 20f);
            float dealt = h.ApplyDamage(new DamageInfo(30f, DamageType.Kinetic, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(10f, dealt, 0.001f);
            Assert.AreEqual(90f, h.Current, 0.001f);
        }

        [Test]
        public void Every_hit_gets_at_least_one_through()
        {
            var h = Make(100f, 500f);
            float dealt = h.ApplyDamage(new DamageInfo(5f, DamageType.Kinetic, Vector3.zero, Vector3.forward, null));
            Assert.AreEqual(1f, dealt, 0.001f);
        }

        [Test]
        public void Explosive_pierces_most_of_the_armor()
        {
            var h = Make(1000f, 60f, 0.8f);
            // effective armor = 60 * 0.2 = 12  ->  400 - 12 = 388
            float dealt = h.ApplyDamage(DamageInfo.Explosive(400f, Vector3.zero, null));
            Assert.AreEqual(388f, dealt, 0.001f);
        }

        [Test]
        public void Death_fires_once_and_latches()
        {
            var h = Make(50f, 0f);
            int deaths = 0;
            h.Died += _ => deaths++;
            h.ApplyDamage(DamageInfo.Explosive(80f, Vector3.zero, null));
            h.ApplyDamage(DamageInfo.Explosive(80f, Vector3.zero, null));
            Assert.IsTrue(h.IsDead);
            Assert.AreEqual(1, deaths);
            Assert.AreEqual(0f, h.Current);
        }

        [Test]
        public void Normalized_tracks_the_fraction_remaining()
        {
            var h = Make(200f, 0f);
            h.ApplyDamage(DamageInfo.Explosive(50f, Vector3.zero, null));
            Assert.AreEqual(0.75f, h.Normalized, 0.001f);
        }
    }
}
