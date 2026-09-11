using Ironfield.Combat;
using Ironfield.Core;
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
        [Tooltip("When there is no separate wreck mesh, char the intact meshes in place instead.")]
        public bool charInPlace = true;

        static Material s_charred;
        bool _done;

        public void Trigger(DamageInfo info)
        {
            if (_done) return;
            _done = true;

            if (wreckedVisual)
            {
                if (intactVisual) intactVisual.SetActive(false);
                wreckedVisual.SetActive(true);
            }
            else if (charInPlace && intactVisual)
            {
                if (s_charred == null)
                    s_charred = new Material(Shader.Find("Standard"))
                    {
                        color = new Color(0.05f, 0.045f, 0.04f)
                    };
                foreach (var r in intactVisual.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = s_charred;
                    r.sharedMaterials = mats;
                }
                // slump it a bit
                intactVisual.transform.localRotation *= Quaternion.Euler(
                    Random.Range(-4f, 4f), 0f, Random.Range(-6f, 6f));
            }

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
                AudioSource.PlayClipAtPoint(destroyedSfx, transform.position, GameSettings.SfxVolume);
        }
    }
}
