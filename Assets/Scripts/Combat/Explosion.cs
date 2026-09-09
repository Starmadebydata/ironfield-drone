using UnityEngine;

namespace Ironfield.Combat
{
    /// <summary>
    /// Self-contained blast: radius damage with linear falloff, a physics push
    /// on loose rigidbodies, and whatever FX/audio are parented under it. Spawn
    /// it and forget it - it despawns itself.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        [Header("Damage")]
        public float damage = 400f;
        public float radius = 6f;
        [Tooltip("Layers that can be hurt / pushed by the blast.")]
        public LayerMask affects = ~0;

        [Header("Physics")]
        public float force = 12f;
        public float upwardBias = 0.4f;

        [Header("Lifetime")]
        public float autoDestroyAfter = 4f;

        GameObject _instigator;

        public void Fire(GameObject instigator)
        {
            _instigator = instigator;
            Detonate();
        }

        void Start()
        {
            // Also works if simply instantiated without calling Fire().
            if (_instigator == null) Detonate();
            Destroy(gameObject, autoDestroyAfter);
        }

        void Detonate()
        {
            var hits = Physics.OverlapSphere(transform.position, radius, affects,
                                             QueryTriggerInteraction.Ignore);
            foreach (var col in hits)
            {
                float dist = Vector3.Distance(transform.position, col.ClosestPoint(transform.position));
                float t = 1f - Mathf.Clamp01(dist / radius);
                if (t <= 0f) continue;

                var health = col.GetComponentInParent<HealthComponent>();
                if (health != null && !health.IsDead)
                {
                    var info = DamageInfo.Explosive(damage * t, transform.position,
                                                    _instigator != null ? _instigator : gameObject);
                    health.ApplyDamage(info);
                }

                var rb = col.attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(force * 100f * t, transform.position,
                                         radius, upwardBias, ForceMode.Impulse);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
