using System.Collections.Generic;
using Ironfield.Combat;
using Ironfield.Drone;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using UnityEngine;

namespace Ironfield.Mission
{
    /// <summary>
    /// Immediate-mode combat HUD for the prototype: briefing, target markers
    /// (on-screen boxes + off-screen edge arrows), lock ring + target health,
    /// hit markers, kill banner, damage vignette, speed/altitude, win/lose.
    /// IMGUI so the scene needs no Canvas; swap for uGUI/TMP in the polish pass.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        public MissionManager mission;
        public TargetingSystem targeting;
        public DroneCameraRig cameraRig;

        GUIStyle _label, _small, _big, _center;
        Texture2D _px;
        Camera _cam;

        float _hitMarker;        // >0 while showing
        bool _hitWasKill;
        float _killBanner;
        string _killText = "";
        float _vignette;         // 0..1 damage flash

        void OnEnable()
        {
            Explosion.DamagedSomething += OnHit;
            VehicleRegistry.AnyDestroyed += OnKill;
            if (mission != null) mission.DroneDamaged += OnDroneDamaged;
        }

        void OnDisable()
        {
            Explosion.DamagedSomething -= OnHit;
            VehicleRegistry.AnyDestroyed -= OnKill;
            if (mission != null) mission.DroneDamaged -= OnDroneDamaged;
        }

        void Awake()
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _hitMarker = Mathf.Max(0f, _hitMarker - dt);
            _killBanner = Mathf.Max(0f, _killBanner - dt);
            _vignette = Mathf.Max(0f, _vignette - dt * 1.6f);
        }

        void OnHit(Vector3 pos, bool vehicle)
        {
            _hitMarker = 0.25f;
            _hitWasKill = false;
        }

        void OnKill(Vehicle v)
        {
            _killBanner = 2.2f;
            _killText = "TARGET DESTROYED  " +
                        $"{mission.VehiclesTotal - mission.VehiclesLeft}/{mission.VehiclesTotal}";
            _hitMarker = 0.3f;
            _hitWasKill = true;
        }

        void OnDroneDamaged(float amt) => _vignette = Mathf.Clamp01(_vignette + amt * 0.05f + 0.25f);

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _label.normal.textColor = new Color(0.86f, 0.95f, 0.86f);
            _small = new GUIStyle(_label) { fontSize = 11, fontStyle = FontStyle.Normal };
            _big = new GUIStyle(_label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            _center = new GUIStyle(_label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            EnsureStyles();
            if (mission == null) return;
            if (_cam == null) _cam = cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;

            float w = Screen.width, h = Screen.height;
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);

            DrawVignette(w, h);

            // --- top-left status --------------------------------------
            GUI.Label(new Rect(24, 18, 520, 24),
                $"COLUMN   {mission.VehiclesTotal - mission.VehiclesLeft}/{mission.VehiclesTotal} destroyed", _label);
            GUI.Label(new Rect(24, 42, 520, 24), $"DRONES   {mission.DronesLeft}", _label);

            var drone = mission.ActiveDrone;
            if (drone != null)
            {
                GUI.Label(new Rect(24, h - 66, 320, 22), $"{drone.Speed * 3.6f:0} km/h", _label);
                GUI.Label(new Rect(24, h - 44, 320, 22), $"ALT {drone.transform.position.y:0} m", _small);
            }

            // --- briefing (first seconds) ----------------------------
            if (mission.State == MissionState.Active && mission.TimeActive < 6f)
            {
                float a = Mathf.Clamp01(Mathf.Min(mission.TimeActive, 6f - mission.TimeActive) / 1.2f);
                var col = _center.normal.textColor; col.a = a;
                var s = new GUIStyle(_center); s.normal.textColor = col;
                GUI.Label(new Rect(0, h * 0.16f, w, 26),
                    "RECON  ·  enemy armour column on the road", s);
                GUI.Label(new Rect(0, h * 0.16f + 26, w, 22),
                    "Fly in and destroy all vehicles before your drones run out", s);
            }

            // --- target markers -------------------------------------
            if (_cam != null)
                DrawTargetMarkers(w, h);

            // --- reticle + lock ------------------------------------
            DrawReticle(c);
            DrawLock(c, w, h);

            // --- hit marker --------------------------------------
            if (_hitMarker > 0f)
            {
                float s = _hitMarker / 0.3f;
                Color col = _hitWasKill ? new Color(1f, 0.3f, 0.2f) : Color.white;
                col.a = Mathf.Clamp01(s);
                DrawHitMarker(c, Mathf.Lerp(10f, 20f, 1f - s), col);
            }

            // --- kill banner ------------------------------------
            if (_killBanner > 0f)
            {
                float a = Mathf.Clamp01(_killBanner / 0.5f);
                var col = new Color(1f, 0.85f, 0.3f, a);
                var s = new GUIStyle(_center) { fontSize = 24 };
                s.normal.textColor = col;
                GUI.Label(new Rect(0, h * 0.30f, w, 30), _killText, s);
            }

            // --- end panels ------------------------------------
            if (mission.State == MissionState.Won || mission.State == MissionState.Lost)
                DrawEndPanel(w, h);
        }

        void DrawTargetMarkers(float w, float h)
        {
            var nearest = targeting != null ? targeting.CurrentTarget : null;
            Vector3 camPos = _cam.transform.position;

            foreach (var v in VehicleRegistry.Alive)
            {
                if (v == null || v.IsDestroyed) continue;
                Vector3 wp = v.AimPoint;
                Vector3 sp = _cam.WorldToScreenPoint(wp);
                float dist = Vector3.Distance(camPos, wp);
                bool onScreen = sp.z > 0f && sp.x > 0 && sp.x < w && sp.y > 0 && sp.y < h;
                bool isNearest = v == nearest;
                Color col = isNearest ? new Color(1f, 0.35f, 0.2f)
                                      : new Color(1f, 0.8f, 0.25f, 0.8f);

                if (onScreen)
                {
                    Vector2 p = new Vector2(sp.x, h - sp.y);
                    float box = Mathf.Clamp(1400f / Mathf.Max(20f, dist), 10f, 40f);
                    DrawBox(p, box, col, isNearest ? 2.5f : 1.5f);
                    GUI.Label(new Rect(p.x + box + 3, p.y - 8, 160, 18), $"{dist:0} m", _small);
                }
                else
                {
                    // clamp an arrow to the screen edge pointing at the target
                    Vector3 dir = sp.z < 0f ? -sp : sp;
                    Vector2 v2 = new Vector2(dir.x - w * 0.5f, (h - dir.y) - h * 0.5f);
                    if (v2.sqrMagnitude < 1f) v2 = Vector2.up;
                    v2.Normalize();
                    Vector2 edge = new Vector2(w * 0.5f, h * 0.5f) + v2 * Mathf.Min(w, h) * 0.42f;
                    DrawArrow(edge, v2, col);
                }
            }
        }

        void DrawLock(Vector2 c, float w, float h)
        {
            var tgt = targeting != null ? targeting.CurrentTarget : null;
            if (tgt == null || _cam == null) return;
            Vector3 sp = _cam.WorldToScreenPoint(tgt.AimPoint);
            if (sp.z <= 0f) return;
            Vector2 p = new Vector2(sp.x, h - sp.y);

            float lp = targeting.LockProgress;
            Color col = Color.Lerp(new Color(1f, 0.8f, 0.25f), new Color(1f, 0.25f, 0.15f), lp);
            float box = Mathf.Lerp(44f, 24f, lp);
            DrawBracket(p, box, col, targeting.HasHardLock ? 3f : 2f);

            // target health bar
            var hpc = tgt.Health;
            if (hpc != null)
            {
                float frac = hpc.Normalized;
                Rect bar = new Rect(p.x - box, p.y - box - 10, box * 2, 5);
                GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(bar, _px);
                GUI.color = Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.35f), frac);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), _px);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(p.x + box + 4, p.y - box, 220, 20), tgt.displayName, _small);
            if (targeting.HasHardLock)
                GUI.Label(new Rect(p.x - 18, p.y + box + 2, 80, 18), "LOCK", _label);
        }

        // ---- primitives -------------------------------------------------
        void DrawVignette(float w, float h)
        {
            if (_vignette <= 0.01f) return;
            float a = _vignette * 0.4f;
            GUI.color = new Color(0.8f, 0.05f, 0.03f, a);
            GUI.DrawTexture(new Rect(0, 0, w, 70), _px);
            GUI.DrawTexture(new Rect(0, h - 70, w, 70), _px);
            GUI.DrawTexture(new Rect(0, 0, 70, h), _px);
            GUI.DrawTexture(new Rect(w - 70, 0, 70, h), _px);
            GUI.color = Color.white;
        }

        void DrawReticle(Vector2 c)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(c.x - 12, c.y - 1, 9, 2), _px);
            GUI.DrawTexture(new Rect(c.x + 3, c.y - 1, 9, 2), _px);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - 12, 2, 9), _px);
            GUI.DrawTexture(new Rect(c.x - 1, c.y + 3, 2, 9), _px);
            GUI.color = Color.white;
        }

        void DrawHitMarker(Vector2 c, float r, Color col)
        {
            GUI.color = col;
            for (int s = -1; s <= 1; s += 2)
            {
                DrawLine(new Vector2(c.x + s * r, c.y - r), new Vector2(c.x + s * r * 0.4f, c.y - r * 0.4f), 2f);
                DrawLine(new Vector2(c.x + s * r, c.y + r), new Vector2(c.x + s * r * 0.4f, c.y + r * 0.4f), 2f);
            }
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

        void DrawBracket(Vector2 c, float half, Color col, float t)
        {
            GUI.color = col;
            float len = half * 0.5f;
            // four corner brackets
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sy in new[] { -1f, 1f })
            {
                Vector2 corner = new Vector2(c.x + sx * half, c.y + sy * half);
                GUI.DrawTexture(new Rect(corner.x - (sx < 0 ? 0 : len), corner.y - (sy < 0 ? 0 : t), len, t), _px);
                GUI.DrawTexture(new Rect(corner.x - (sx < 0 ? 0 : t), corner.y - (sy < 0 ? 0 : len), t, len), _px);
            }
            GUI.color = Color.white;
        }

        void DrawArrow(Vector2 pos, Vector2 dir, Color col)
        {
            GUI.color = col;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, pos);
            GUI.DrawTexture(new Rect(pos.x - 8, pos.y - 6, 16, 12), _px);
            GUI.matrix = m;
            GUI.color = Color.white;
        }

        void DrawLine(Vector2 a, Vector2 b, float width)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), _px);
            GUI.matrix = m;
        }

        void DrawEndPanel(float w, float h)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _px);
            GUI.color = Color.white;

            bool won = mission.State == MissionState.Won;
            GUI.Label(new Rect(0, h * 0.34f, w, 44), won ? "COLUMN DESTROYED" : "DRONES EXPENDED", _big);
            GUI.Label(new Rect(0, h * 0.34f + 48, w, 24),
                won ? "Every vehicle on the road knocked out."
                    : $"{mission.VehiclesLeft} vehicle(s) broke through.", _center);

            if (GUI.Button(new Rect(w * 0.5f - 112, h * 0.52f, 104, 40), "Restart"))
                mission.Restart();
            if (GUI.Button(new Rect(w * 0.5f + 8, h * 0.52f, 104, 40), "Quit"))
                mission.Quit();
        }
    }
}
