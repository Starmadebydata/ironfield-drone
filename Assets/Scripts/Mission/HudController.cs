using System.Collections.Generic;
using Ironfield.Combat;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Targeting;
using Ironfield.UI;
using Ironfield.Vehicles;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        [Tooltip("Baked procedural blip, set by IronfieldSetup.")]
        public AudioClip hitPingClip;

        GUIStyle _label, _small, _big, _center;
        Texture2D _px;
        Camera _cam;
        AudioSource _sfx;
        Ironfield.Drone.CruiseAssist _cruise;

        float _hitMarker;        // >0 while showing
        bool _hitWasKill;
        float _killBanner;
        string _killText = "";
        float _vignette;         // 0..1 damage flash
        bool _helpHeld;

        void OnEnable()
        {
            Explosion.DamagedSomething += OnHit;
            VehicleRegistry.AnyDestroyed += OnKill;
            if (mission != null) mission.DroneDamaged += OnDroneDamaged;
        }

        void Awake()
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f; // 2D UI feedback, always audible
            _sfx.playOnAwake = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _hitMarker = Mathf.Max(0f, _hitMarker - dt);
            _killBanner = Mathf.Max(0f, _killBanner - dt);
            _vignette = Mathf.Max(0f, _vignette - dt * 1.6f);
#if ENABLE_INPUT_SYSTEM
            _helpHeld = Keyboard.current != null && Keyboard.current.hKey.isPressed;
#endif
            // hide + lock the OS cursor while flying so mouse drives the drone —
            // unless a modal menu (pause / first-run tip) needs the cursor free.
            bool blocked = PauseMenu.IsPaused || FirstRunTip.Showing;
            bool flying = mission != null && mission.State == MissionState.Active && !blocked;
            Cursor.lockState = flying ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !flying;
        }

        void OnDisable()
        {
            Explosion.DamagedSomething -= OnHit;
            VehicleRegistry.AnyDestroyed -= OnKill;
            if (mission != null) mission.DroneDamaged -= OnDroneDamaged;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnHit(Vector3 pos, bool vehicle)
        {
            _hitMarker = 0.25f;
            _hitWasKill = false;
            PlayPing(1f);
        }

        void OnKill(Vehicle v)
        {
            _killBanner = 2.2f;
            _killText = v != null && v.optional
                ? Loc.Get("hud.bonus_destroyed", mission.BonusKilled, mission.BonusTotal)
                : Loc.Get("hud.target_destroyed",
                    mission.VehiclesTotal - mission.VehiclesLeft, mission.VehiclesTotal);
            _hitMarker = 0.3f;
            _hitWasKill = true;
            PlayPing(1.4f);
        }

        void PlayPing(float pitch)
        {
            if (hitPingClip == null || _sfx == null) return;
            _sfx.pitch = pitch;
            _sfx.PlayOneShot(hitPingClip, GameSettings.SfxVolume);
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
            // See Ironfield.UI.UiScaling — guarantees the scale matrix is
            // restored even though DrawGUI below returns early in several
            // places, so it can never leak into another component's OnGUI.
            var m = Ironfield.UI.UiScaling.Begin();
            try { DrawGUI(); }
            finally { Ironfield.UI.UiScaling.End(m); }
        }

        void DrawGUI()
        {
            EnsureStyles();
            if (mission == null) return;
            if (_cam == null) _cam = cameraRig ? cameraRig.GetComponent<Camera>() : Camera.main;

            float w = Screen.width, h = Screen.height;
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);

            DrawVignette(w, h);

            // --- top-left status --------------------------------------
            GUI.Label(new Rect(24, 18, 520, 24),
                Loc.Get("hud.column", mission.Killed, mission.VehiclesTotal), _label);
            string droneType = GameSettings.SelectedDrone switch
            {
                1 => Loc.Get("drone.heavy.short"),
                2 => Loc.Get("drone.recon.short"),
                _ => Loc.Get("drone.light.short"),
            };
            GUI.Label(new Rect(24, 42, 520, 24), Loc.Get("hud.drones", mission.DronesLeft, droneType), _label);
            float statusY = 66f;
            if (mission.BonusTotal > 0)
            {
                var bonus = new GUIStyle(_label) { fontSize = 12 };
                bonus.normal.textColor = new Color(0.75f, 0.85f, 1f);
                GUI.Label(new Rect(24, statusY, 520, 20),
                    Loc.Get("hud.bonus", mission.BonusKilled, mission.BonusTotal), bonus);
                statusY += 20f;
            }
            if (mission.Escaped > 0)
            {
                var warn = new GUIStyle(_label);
                warn.normal.textColor = new Color(1f, 0.5f, 0.25f);
                GUI.Label(new Rect(24, statusY, 520, 24), Loc.Get("hud.broke_through", mission.Escaped), warn);
            }

            var drone = mission.ActiveDrone;
            if (drone != null)
            {
                GUI.Label(new Rect(24, h - 66, 320, 22), $"{drone.Speed * 3.6f:0} km/h", _label);
                GUI.Label(new Rect(24, h - 44, 320, 22), Loc.Get("hud.alt", Mathf.RoundToInt(drone.transform.position.y)), _small);

                if (_cruise == null || _cruise.gameObject != drone.gameObject)
                    _cruise = drone.GetComponent<Ironfield.Drone.CruiseAssist>();
                if (_cruise != null && _cruise.Engaged)
                {
                    var cs = new GUIStyle(_label) { fontSize = 12 };
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 4f);
                    cs.normal.textColor = new Color(0.5f, 0.85f, 1f, pulse);
                    GUI.Label(new Rect(24, h - 88, 320, 20), Loc.Get("hud.cruise_on"), cs);
                }

                if (drone.IsNearBoundary)
                {
                    var bw = new GUIStyle(_center) { fontSize = 20, fontStyle = FontStyle.Bold };
                    float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
                    bw.normal.textColor = new Color(1f, 0.55f, 0.2f, pulse);
                    GUI.Label(new Rect(0, h * 0.10f, w, 30), Loc.Get("hud.boundary_warning"), bw);
                }
            }

            // --- control legend (bottom-right; fades out, hold H to re-show) --
            bool showHelp = mission.TimeActive < 14f || _helpHeld;
            if (showHelp && mission.State == MissionState.Active)
            {
                float fade = _helpHeld ? 1f
                    : Mathf.Clamp01(Mathf.Min(mission.TimeActive, 14f - mission.TimeActive) / 2f);
                var hs = new GUIStyle(_small);
                var hc = hs.normal.textColor; hc.a = fade; hs.normal.textColor = hc;
                string[] keyKeys = DroneInput.LastWasGamepad
                    ? new[]
                    {
                        "ctrl.pad.aim", "ctrl.pad.fire", "ctrl.pad.precision", "ctrl.pad.throttle",
                        "ctrl.pad.trim", "ctrl.pad.roll", "ctrl.pad.cruise", "ctrl.hint_hold_h",
                    }
                    : new[]
                    {
                        "ctrl.mkb.aim", "ctrl.mkb.fire", "ctrl.mkb.precision", "ctrl.mkb.throttle",
                        "ctrl.mkb.trim", "ctrl.mkb.roll", "ctrl.mkb.cruise", "ctrl.hint_hold_h",
                    };
                for (int i = 0; i < keyKeys.Length; i++)
                    GUI.Label(new Rect(w - 260, h - 24 - (keyKeys.Length - i) * 16, 250, 16), Loc.Get(keyKeys[i]), hs);
            }

            // --- briefing (first seconds) ----------------------------
            if (mission.State == MissionState.Active && mission.TimeActive < 6f)
            {
                float a = Mathf.Clamp01(Mathf.Min(mission.TimeActive, 6f - mission.TimeActive) / 1.2f);
                var col = _center.normal.textColor; col.a = a;
                var s = new GUIStyle(_center); s.normal.textColor = col;
                GUI.Label(new Rect(0, h * 0.16f, w, 26), Loc.Get("hud.briefing.line1"), s);
                GUI.Label(new Rect(0, h * 0.16f + 26, w, 22), Loc.Get("hud.briefing.line2"), s);
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
                Color col = _hitWasKill ? KillFlashColor() : Color.white;
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

        // Blue/orange reads clearly for red-green colour blindness; the default
        // red/yellow pair does not. GameSettings.ColorblindMode picks between them.
        static Color NearestTargetColor() => GameSettings.ColorblindMode
            ? new Color(1f, 0.55f, 0.05f) : new Color(1f, 0.35f, 0.2f);
        static Color OtherTargetColor() => GameSettings.ColorblindMode
            ? new Color(0.35f, 0.65f, 1f, 0.85f) : new Color(1f, 0.8f, 0.25f, 0.8f);
        static Color LockColor(float lockProgress) => GameSettings.ColorblindMode
            ? Color.Lerp(new Color(0.35f, 0.65f, 1f), new Color(1f, 0.55f, 0.05f), lockProgress)
            : Color.Lerp(new Color(1f, 0.8f, 0.25f), new Color(1f, 0.25f, 0.15f), lockProgress);
        static Color KillFlashColor() => GameSettings.ColorblindMode
            ? new Color(1f, 0.55f, 0.05f) : new Color(1f, 0.3f, 0.2f);

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
                Color col = isNearest ? NearestTargetColor() : OtherTargetColor();

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
            Color col = LockColor(lp);
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

            GUI.Label(new Rect(p.x + box + 4, p.y - box, 220, 20), Loc.Get(tgt.displayName), _small);
            bool auto = mission.ActiveDrone != null && mission.ActiveDrone.AutopilotEngaged;
            if (auto)
            {
                var autoStyle = new GUIStyle(_label);
                autoStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
                GUI.Label(new Rect(p.x - 42, p.y + box + 2, 140, 18), Loc.Get("hud.auto_attack"), autoStyle);
            }
            else if (targeting.HasHardLock)
                GUI.Label(new Rect(p.x - 18, p.y + box + 2, 80, 18), Loc.Get("hud.lock"), _label);
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
            float w = Screen.width, h = Screen.height;
            float ring = Mathf.Min(w, h) * 0.16f;   // reticle travel radius

            // fixed centre pip + deadzone ring (where the nose points now)
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            DrawCircle(c, ring, 1f);
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(c.x - 3, c.y - 1, 6, 2), _px);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - 3, 2, 6), _px);

            // moving mouse-aim reticle
            var drone = mission != null ? mission.ActiveDrone : null;
            if (drone != null)
            {
                Vector2 a = drone.AimReticle;               // -1..1
                Vector2 p = new Vector2(c.x + a.x * ring, c.y - a.y * ring);
                Color col = drone.Precision ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 1f, 0.8f);
                GUI.color = col;
                float s = drone.Precision ? 9f : 12f;
                GUI.DrawTexture(new Rect(p.x - s, p.y - 1.5f, s - 3, 3), _px);
                GUI.DrawTexture(new Rect(p.x + 3, p.y - 1.5f, s - 3, 3), _px);
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y - s, 3, s - 3), _px);
                GUI.DrawTexture(new Rect(p.x - 1.5f, p.y + 3, 3, s - 3), _px);
            }
            GUI.color = Color.white;
        }

        void DrawCircle(Vector2 c, float r, float thick)
        {
            const int seg = 40;
            Vector2 prev = c + new Vector2(r, 0);
            for (int i = 1; i <= seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                Vector2 cur = c + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
                DrawLine(prev, cur, thick);
                prev = cur;
            }
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
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0, 0, w, h), _px);
            GUI.color = Color.white;

            bool won = mission.State == MissionState.Won;
            GUI.Label(new Rect(0, h * 0.24f, w, 44), Loc.Get(won ? "end.won" : "end.lost"), _big);

            int t = Mathf.RoundToInt(mission.TimeActive);
            var lineList = new List<string>
            {
                Loc.Get("end.destroyed", mission.Killed, mission.VehiclesTotal),
                Loc.Get("end.broke_through", mission.Escaped),
            };
            if (mission.BonusTotal > 0)
                lineList.Add(Loc.Get("end.bonus_targets", mission.BonusKilled, mission.BonusTotal));
            lineList.Add(Loc.Get("end.drones_used", mission.droneStock - mission.DronesLeft, mission.droneStock));
            lineList.Add(Loc.Get("end.time", t / 60, $"{t % 60:00}"));
            lineList.Add("");
            lineList.Add(Loc.Get("end.score_grade", mission.Score, mission.Grade));
            string[] lines = lineList.ToArray();
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(0, h * 0.24f + 58 + i * 24, w, 22), lines[i], _center);

            string next = won ? MissionCatalog.NextSceneName(mission.missionId) : null;
            if (next != null)
            {
                if (UiSfx.Button(new Rect(w * 0.5f - 100, h * 0.60f, 200, 42), Loc.Get("end.next_mission")))
                    SceneFlow.LoadMissionByName(next);
            }

            float row2 = won && next != null ? h * 0.60f + 52f : h * 0.62f;
            if (UiSfx.Button(new Rect(w * 0.5f - 172, row2, 104, 40), Loc.Get("end.restart")))
                SceneFlow.RestartCurrent();
            if (UiSfx.Button(new Rect(w * 0.5f - 60, row2, 120, 40), Loc.Get("end.main_menu")))
                SceneFlow.LoadMainMenu();
            if (UiSfx.Button(new Rect(w * 0.5f + 68, row2, 104, 40), Loc.Get("end.quit")))
                SceneFlow.Quit();
        }
    }
}
