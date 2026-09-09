using Ironfield.Drone;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using UnityEngine;

namespace Ironfield.Mission
{
    /// <summary>
    /// Immediate-mode HUD for the prototype: reticle + lock box, drone/target
    /// counters, speed readout, and win/lose panels. Deliberately IMGUI so the
    /// scene needs no Canvas wiring; swap for uGUI/TMP in the polish pass.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public MissionManager mission;
        public TargetingSystem targeting;
        public DroneCameraRig cameraRig;

        GUIStyle _label, _big, _center;
        Texture2D _px;

        void Awake()
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _label.normal.textColor = new Color(0.85f, 0.95f, 0.85f);
            _big = new GUIStyle(_label) { fontSize = 34 };
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        }

        void OnGUI()
        {
            EnsureStyles();
            if (mission == null) return;

            float w = Screen.width, h = Screen.height;

            // --- counters ------------------------------------------------
            GUI.Label(new Rect(24, 20, 480, 30),
                $"COLUMN  {mission.VehiclesTotal - mission.VehiclesLeft}/{mission.VehiclesTotal} destroyed", _label);
            GUI.Label(new Rect(24, 48, 480, 30),
                $"DRONES  {mission.DronesLeft}", _label);

            var drone = mission.ActiveDrone;
            if (drone != null)
                GUI.Label(new Rect(24, h - 44, 320, 30), $"{drone.Speed * 3.6f:0} km/h", _label);

            // --- reticle ----------------------------------------------------
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            DrawCross(c, 14f, new Color(1f, 1f, 1f, 0.5f));

            var tgt = targeting != null ? targeting.CurrentTarget : null;
            if (tgt != null && cameraRig != null)
            {
                var cam = cameraRig.GetComponent<Camera>();
                Vector3 sp = cam.WorldToScreenPoint(tgt.AimPoint);
                if (sp.z > 0f)
                {
                    Vector2 p = new Vector2(sp.x, h - sp.y);
                    float lock01 = targeting.LockProgress;
                    Color col = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.25f, 0.15f), lock01);
                    float box = Mathf.Lerp(46f, 26f, lock01);
                    DrawBox(p, box, col, targeting.HasHardLock ? 3f : 1.5f);
                    GUI.Label(new Rect(p.x + box, p.y - box, 200, 22),
                        $"{tgt.displayName}  {Vector3.Distance(cam.transform.position, tgt.AimPoint):0} m", _label);
                    if (targeting.HasHardLock)
                        GUI.Label(new Rect(p.x - 30, p.y + box + 2, 120, 22), "LOCK", _label);
                }
            }

            // --- end panels ----------------------------------------------
            if (mission.State == MissionState.Won || mission.State == MissionState.Lost)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(0, 0, w, h), _px);
                GUI.color = Color.white;

                string title = mission.State == MissionState.Won
                    ? "COLUMN DESTROYED" : "DRONES EXPENDED";
                GUI.Label(new Rect(0, h * 0.36f, w, 50), title, _center2(_big));
                GUI.Label(new Rect(0, h * 0.36f + 54, w, 30),
                    mission.State == MissionState.Won
                        ? "All armour on the road knocked out."
                        : $"{mission.VehiclesLeft} vehicle(s) broke through.", _center);

                if (GUI.Button(new Rect(w * 0.5f - 110, h * 0.5f + 20, 100, 38), "Restart"))
                    mission.Restart();
                if (GUI.Button(new Rect(w * 0.5f + 10, h * 0.5f + 20, 100, 38), "Quit"))
                    mission.Quit();
            }
        }

        GUIStyle _center2(GUIStyle s)
        {
            s.alignment = TextAnchor.MiddleCenter;
            return s;
        }

        void DrawCross(Vector2 c, float size, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - size, c.y - 1, size * 2, 2), _px);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - size, 2, size * 2), _px);
            GUI.color = Color.white;
        }

        void DrawBox(Vector2 c, float half, Color col, float t)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - half, c.y - half, half * 2, t), _px);
            GUI.DrawTexture(new Rect(c.x - half, c.y + half - t, half * 2, t), _px);
            GUI.DrawTexture(new Rect(c.x - half, c.y - half, t, half * 2), _px);
            GUI.DrawTexture(new Rect(c.x + half - t, c.y - half, t, half * 2), _px);
            GUI.color = Color.white;
        }
    }
}
