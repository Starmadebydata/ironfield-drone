using System;
using UnityEngine;

namespace Ironfield.Combat
{
    /// <summary>
    /// Shared hit points for the drone and every vehicle. Optional flat
    /// <see cref="armor"/> is subtracted from each hit (min 1 damage gets
    /// through) so light guns barely scratch a tank but a warhead still kills.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float armor = 0f;
        [Tooltip("Explosive damage ignores this fraction of armor (shaped charge).")]
        [Range(0f, 1f)] public float explosiveArmorPierce = 0.8f;

        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Normalized => Mathf.Clamp01(Current / Mathf.Max(1f, maxHealth));

        /// <summary>(finalDamage, info)</summary>
        public event Action<float, DamageInfo> Damaged;
        /// <summary>(info that caused death)</summary>
        public event Action<DamageInfo> Died;

        void Awake() => Current = maxHealth;

        public void ResetHealth()
        {
            Current = maxHealth;
            IsDead = false;
        }

        public float ApplyDamage(in DamageInfo info)
        {
            if (IsDead) return 0f;

            float effectiveArmor = armor;
            if (info.Type == DamageType.Explosive)
                effectiveArmor *= (1f - explosiveArmorPierce);

            float final = Mathf.Max(1f, info.Amount - effectiveArmor);
            Current = Mathf.Max(0f, Current - final);
            Damaged?.Invoke(final, info);

            if (Current <= 0f && !IsDead)
            {
                IsDead = true;
                Died?.Invoke(info);
            }
            return final;
        }

        public void Kill(GameObject source = null)
            => ApplyDamage(new DamageInfo(maxHealth + armor + 1f, DamageType.Explosive,
                                          transform.position, Vector3.zero, source));
    }
}
