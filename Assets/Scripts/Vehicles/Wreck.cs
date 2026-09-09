using Ironfield.Combat;
using UnityEngine;

namespace Ironfield.Vehicles
{
    /// <summary>
    /// On death: hide the intact visual, reveal the wrecked visual, light a
    /// fire/smoke FX, and fling a few debris rigidbodies. Purely cosmetic - the
    /// collider stays so the hulk still blocks and can be flown into.
    /// </summary>
    public class Wreck : MonoBehaviour
    {
        [Tooltip("Root of the intact meshes (turret, hull, wheels...).")]
        public GameObject intactVisual;
        [Tooltip("Root of the burnt/broken meshes. Hidden until death.")]
        public GameObject wreckedVisual;
        public GameObject fireSmokePrefab;
        public Vector3 fireLocalOffset = new Vector3(0f, 1.4f, 0f);
        public Rigidbody[] debris;
        public float debrisImpulse = 6f;
        public AudioClip destroyedSfx;

        bool _done;

        public void Trigger(DamageInfo info)
        {
            if (_done) return;
            _done = true;

            if (intactVisual) intactVisual.SetActive(false);
            if (wreckedVisual) wreckedVisual.SetActive(true);

            if (fireSmokePrefab)
            {
                var fx = Instantiate(fireSmokePrefab, transform);
                fx.transform.localPosition = fireLocalOffset;
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>())
                    ps.Play();
            }

            if (debris != null)
            {
                foreach (var rb in debris)
                {
                    if (!rb) continue;
                    rb.transform.SetParent(null, true);
                    rb.isKinematic = false;
                    rb.AddForce((Random.insideUnitSphere + Vector3.up).normalized
                                * debrisImpulse, ForceMode.VelocityChange);
                    rb.AddTorque(Random.insideUnitSphere * debrisImpulse, ForceMode.VelocityChange);
                    Destroy(rb.gameObject, 12f);
                }
            }

            if (destroyedSfx)
                AudioSource.PlayClipAtPoint(destroyedSfx, transform.position, 1f);
        }
    }
}
