using System.Collections.Generic;
using Ironfield.Core;
using Ironfield.Vehicles;
using UnityEngine;

namespace Ironfield.Targeting
{
    /// <summary>
    /// Soft lock-on. Each frame it collects live vehicles inside a forward cone
    /// and range, checks line of sight from the camera, and picks the one
    /// closest to screen centre. Exposes the winner for the HUD and for the
    /// warhead's assisted trigger.
    /// </summary>
    public class TargetingSystem : MonoBehaviour
    {
        [Tooltip("Camera the reticle is drawn from. Defaults to Camera.main.")]
        public Camera viewCamera;
        public float range = 320f;
        [Range(2f, 45f)] public float coneHalfAngle = 22f;
        [Tooltip("Seconds of continuous aim before the lock is 'hard'.")]
        public float lockTime = 0.6f;
        public bool requireLineOfSight = true;

        public Vehicle CurrentTarget { get; private set; }
        public float LockProgress { get; private set; }   // 0..1
        public bool HasHardLock => CurrentTarget != null && LockProgress >= 1f;

        static readonly List<Vehicle> _scratch = new();

        void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) { CurrentTarget = null; LockProgress = 0f; return; }

            Vehicle best = SelectBest(viewCamera, VehicleRegistry.Alive,
                range, coneHalfAngle, requireLineOfSight);

            if (best == CurrentTarget && best != null)
                LockProgress = Mathf.Clamp01(LockProgress + Time.deltaTime / Mathf.Max(0.01f, lockTime));
            else
                LockProgress = 0f;

            CurrentTarget = best;
        }

        /// <summary>
        /// Pure-ish selection: nearest-to-screen-centre live vehicle inside the
        /// forward cone and range, optionally requiring line of sight. Exposed
        /// (and static) so it can be unit tested without play mode.
        /// </summary>
        public static Vehicle SelectBest(Camera cam, IReadOnlyList<Vehicle> candidates,
            float range, float coneHalfAngle, bool requireLineOfSight)
        {
            if (cam == null || candidates == null) return null;

            Vehicle best = null;
            float bestScore = float.MaxValue;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            Vector2 screenCentre = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);

            for (int i = 0; i < candidates.Count; i++)
            {
                var v = candidates[i];
                if (v == null || v.IsDestroyed) continue;

                Vector3 aim = v.AimPoint;
                Vector3 to = aim - camPos;
                float dist = to.magnitude;
                if (dist > range || dist < 1f) continue;
                if (Vector3.Angle(camFwd, to) > coneHalfAngle) continue;

                Vector3 sp = cam.WorldToScreenPoint(aim);
                if (sp.z <= 0f) continue;
                float screenDist = Vector2.Distance(new Vector2(sp.x, sp.y), screenCentre);

                if (requireLineOfSight &&
                    Physics.Raycast(camPos, to.normalized, out var hit, dist * 0.98f,
                                    GameLayers.SightBlockers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<Vehicle>() != v) continue;
                }

                float score = screenDist + dist * 0.05f;
                if (score < bestScore) { bestScore = score; best = v; }
            }
            return best;
        }
    }
}
