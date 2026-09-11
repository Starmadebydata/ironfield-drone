using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ironfield.Combat;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Fx;
using Ironfield.Mission;
using Ironfield.Targeting;
using Ironfield.UI;
using Ironfield.Vehicles;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfield.EditorTools
{
    /// <summary>
    /// Phase B of headless setup. Configures FBX imports, builds the drone /
    /// vehicle / explosion / ruins prefabs, creates the DroneTuning asset, and
    /// assembles Assets/Scenes/Mission01.unity end to end.
    ///
    ///   Unity -batchmode -quit -executeMethod Ironfield.EditorTools.IronfieldSetup.Run
    ///
    /// Re-runnable: it deletes and rebuilds the prefabs and the scene each time.
    /// Prototype renders on the Built-in pipeline; URP is installed for later.
    /// </summary>
    public static class IronfieldSetup
    {
        const string ArtDrone = "Assets/Art/Drone/Drone.fbx";
        const string ArtDroneExt = "Assets/Art/External/Drone_FPV.fbx";   // CC-BY, NateGazzard (from .glb)
        const string ArtTank = "Assets/Art/Vehicles/Tank.fbx";
        const string ArtIFV = "Assets/Art/Vehicles/IFV.fbx";
        const string ArtTruck = "Assets/Art/Vehicles/Truck.fbx";
        const string ArtRuins = "Assets/Art/Ruins/RuinsKit.fbx";

        // CC0 / CC-BY packs converted from .glb via tools/blender/convert_gltf.py — see CREDITS.md
        const string ExtDir = "Assets/Art/External/";
        static readonly string[] ExtModels =
        {
            "tank.fbx", "ifv.fbx", "truck.fbx",
            "tree_broadleaf.fbx", "tree_conifer.fbx", "tree_dead.fbx",
            "bld_house.fbx", "bld_block.fbx",
        };

        const string PrefabDir = "Assets/Prefabs";
        const string SettingsDir = "Assets/Settings";
        const string ScenesDir = "Assets/Scenes";
        const string ScenePath = ScenesDir + "/Mission01.unity";
        const string MainMenuScenePath = ScenesDir + "/MainMenu.unity";

        /// <summary>Per-mission tuning for BuildScene — everything that makes the
        /// three campaign scenes different without duplicating the ~150 lines of
        /// terrain/road/lighting setup they share. See Ironfield.Core.MissionCatalog
        /// for the matching runtime id/scene list.</summary>
        struct MissionBuildConfig
        {
            public string scenePath;
            public string missionId;
            public int droneStock;
            /// <summary>Convoy composition, lead vehicle first.</summary>
            public VehicleClass[] convoy;
            /// <summary>Mark the last convoy vehicle as the high-value bonus target.</summary>
            public bool highValueTarget;
            /// <summary>Multiplies every convoy member's VehicleConvoyAI.speed.</summary>
            public float convoySpeedMul;
            /// <summary>How many static flak emplacements to place along the road.</summary>
            public int flakCount;
        }

        static readonly MissionBuildConfig[] Missions =
        {
            new()
            {
                scenePath = ScenePath, missionId = "m01", droneStock = 5,
                convoy = new[]
                {
                    VehicleClass.Tank, VehicleClass.IFV, VehicleClass.Truck,
                    VehicleClass.Tank, VehicleClass.IFV, VehicleClass.Truck,
                },
                highValueTarget = false, convoySpeedMul = 1.0f, flakCount = 0,
            },
            new()
            {
                scenePath = ScenesDir + "/Mission02.unity", missionId = "m02", droneStock = 5,
                convoy = new[]
                {
                    VehicleClass.Tank, VehicleClass.Tank, VehicleClass.IFV, VehicleClass.IFV,
                    VehicleClass.Truck, VehicleClass.Truck, VehicleClass.Truck,
                },
                highValueTarget = false, convoySpeedMul = 1.15f, flakCount = 1,
            },
            new()
            {
                scenePath = ScenesDir + "/Mission03.unity", missionId = "m03", droneStock = 6,
                convoy = new[]
                {
                    VehicleClass.Tank, VehicleClass.Tank, VehicleClass.IFV, VehicleClass.IFV,
                    VehicleClass.IFV, VehicleClass.Truck, VehicleClass.Truck, VehicleClass.Truck,
                },
                highValueTarget = true, convoySpeedMul = 1.3f, flakCount = 2,
            },
        };

        static readonly string[] WantTags = { "Drone", "Vehicle", "LaunchPoint" };
        // index -> name; 6..9 are the first free user layer slots
        static readonly (int idx, string name)[] WantLayers =
        {
            (6, "Drone"), (7, "Vehicle"), (8, "Environment"), (9, "Projectile"),
        };

        [MenuItem("Ironfield/0. Ensure Tags & Layers")]
        public static void EnsureTagsAndLayers()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[Ironfield] Could not load TagManager.asset");
                return;
            }
            var so = new SerializedObject(asset[0]);

            var tags = so.FindProperty("tags");
            foreach (var t in WantTags)
            {
                bool has = false;
                for (int i = 0; i < tags.arraySize; i++)
                    if (tags.GetArrayElementAtIndex(i).stringValue == t) { has = true; break; }
                if (!has)
                {
                    tags.arraySize++;
                    tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = t;
                }
            }

            var layers = so.FindProperty("layers");
            foreach (var (idx, name) in WantLayers)
            {
                if (idx >= layers.arraySize) continue;
                var slot = layers.GetArrayElementAtIndex(idx);
                if (string.IsNullOrEmpty(slot.stringValue) || slot.stringValue == name)
                    slot.stringValue = name;
                else
                    Debug.LogWarning($"[Ironfield] Layer {idx} already '{slot.stringValue}', wanted '{name}'.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Ironfield] Tags & layers ensured.");
        }

        [MenuItem("Ironfield/2. Build Game (prefabs + scene)")]
        public static void Run()
        {
            EnsureTagsAndLayers();

            // wipe generated prefabs/materials so stale ones can't shadow rebuilds
            if (AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.DeleteAsset(PrefabDir);
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { SettingsDir }))
            {
                var mp = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(mp).StartsWith("M_ext_")) AssetDatabase.DeleteAsset(mp);
            }
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(ScenesDir);
            AssetDatabase.Refresh();

            ConfigureModelImport(ArtDrone, 1f);
            ConfigureModelImport(ArtDroneExt, 1f);
            ConfigureModelImport(ArtTank, 1f);
            ConfigureModelImport(ArtIFV, 1f);
            ConfigureModelImport(ArtTruck, 1f);
            ConfigureModelImport(ArtRuins, 1f);
            foreach (var m in ExtModels) ConfigureModelImport(ExtDir + m, 1f);
            AssetDatabase.Refresh();

            var tuning = CreateTuning();
            var tuningHeavy = CreateHeavyTuning();
            BuildExplosionPrefab();
            BuildFireSmokePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildDronePrefab(tuning, DroneBuildConfig.Light);
            BuildDronePrefab(tuningHeavy, DroneBuildConfig.Heavy);
            BuildVehiclePrefab("Tank", ArtTank, VehicleClass.Tank, 900f, 60f, 3.6f, 7.6f, 2.7f);
            BuildVehiclePrefab("IFV", ArtIFV, VehicleClass.IFV, 420f, 25f, 3.2f, 6.2f, 2.9f);
            BuildVehiclePrefab("Truck", ArtTruck, VehicleClass.Truck, 160f, 0f, 2.7f, 8.2f, 3.3f);

            // Asset ops above can reimport the prefabs and invalidate in-memory
            // references, so reload everything fresh from disk before wiring the scene.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var cfg in Missions) BuildScene(cfg);
            BuildMainMenuScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // MainMenu first so a real build boots there, not straight into a mission.
            var scenes = new List<EditorBuildSettingsScene> { new(MainMenuScenePath, true) };
            foreach (var cfg in Missions) scenes.Add(new EditorBuildSettingsScene(cfg.scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[Ironfield] Build complete. Open " + MainMenuScenePath);
        }

        /// <summary>
        /// Graphics-batchmode smoke shot: open Mission01, frame the convoy from
        /// the launch ridge, render one PNG next to the project. Lets a headless
        /// run eyeball model orientation / scale without opening the editor.
        ///   Unity -batchmode -quit -executeMethod Ironfield.EditorTools.IronfieldSetup.Screenshot
        /// </summary>
        /// <summary>
        /// Builds a Development Player (this machine's own platform is the only
        /// one supported without extra platform modules) so real perf numbers
        /// can be measured outside editor overhead — see PerfHarness.cs and
        /// ROADMAP.md P2 item 2. Launch the result with -perftest.
        ///   Unity -batchmode -quit -executeMethod Ironfield.EditorTools.IronfieldSetup.BuildDevPlayer
        /// </summary>
        [MenuItem("Ironfield/9. Build Dev Player (perf)")]
        public static void BuildDevPlayer()
        {
            string outDir = "Builds/DevPerf";
            Directory.CreateDirectory(outDir);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir + "/Ironfield.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development,
            });
            Debug.Log($"[Ironfield] Dev player build: {report.summary.result}, "
                    + $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, "
                    + $"{report.summary.totalSize / 1024 / 1024} MB -> {outDir}/Ironfield.app");
        }

        public static void Screenshot()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var convoy = GameObject.Find("Convoy");
            var launch = GameObject.Find("LaunchPoint");
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Ironfield] No main camera in scene.");
                return;
            }

            RenderSettings.fogDensity = 0.0009f; // clear the air for the smoke shots

            Vector3 convoyCentre = Vector3.zero;
            Transform first = null;
            if (convoy != null && convoy.transform.childCount > 0)
            {
                first = convoy.transform.GetChild(0);
                Bounds b = new Bounds(first.position, Vector3.one);
                foreach (Transform c in convoy.transform) b.Encapsulate(c.position);
                convoyCentre = b.center;
            }

            // wide shot from the launch ridge
            Vector3 eye = launch != null ? launch.transform.position
                                         : convoyCentre + new Vector3(-60, 40, -60);
            Shot(cam, eye, convoyCentre, "Ironfield_smoke_wide.png");

            // close shot on the lead vehicle
            if (first != null)
            {
                Vector3 close = first.position + first.right * 14f + Vector3.up * 5f - first.forward * 4f;
                Shot(cam, close, first.position + Vector3.up * 1.5f, "Ironfield_smoke_close.png");
            }

            // village shot
            var vg = GameObject.Find("Village");
            if (vg != null)
            {
                Vector3 vc = new Vector3(40f, 0f, 0f);
                var t = Terrain.activeTerrain;
                if (t != null) vc.y = t.SampleHeight(vc) + t.transform.position.y;
                Vector3 veye = vc + new Vector3(-55f, 26f, -55f);
                Shot(cam, veye, vc + Vector3.up * 4f, "Ironfield_smoke_village.png");
            }

            // drone shot: spawn one at the launch point and frame it chase-cam style
            var dronePf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone.prefab");
            if (dronePf != null && launch != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dronePf);
                d.transform.position = launch.transform.position;
                d.transform.rotation = launch.transform.rotation;
                // spin the rotor pivots 30 deg so a still frame shows the sweep plane
                var dc = d.GetComponent<Ironfield.Drone.DroneController>();
                if (dc != null && dc.propSpinners != null)
                    foreach (var p in dc.propSpinners) if (p) p.Rotate(Vector3.up, 30f, Space.World);
                Vector3 back = -launch.transform.forward;
                Vector3 eye3 = d.transform.position + back * 6f + Vector3.up * 2.3f;
                Shot(cam, eye3, d.transform.position + d.transform.forward * 3f, "Ironfield_smoke_drone.png");
                // top-down: a real quad shows 4 flat discs, a broken one shows 4 thin lines
                Shot(cam, d.transform.position + Vector3.up * 7f, d.transform.position,
                     "Ironfield_smoke_drone_top.png");
                Object.DestroyImmediate(d);
            }

            // heavy drone shot: same framing, side-by-side comparable with the shot above
            var dronePfHeavy = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone_Heavy.prefab");
            if (dronePfHeavy != null && launch != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dronePfHeavy);
                d.transform.position = launch.transform.position;
                d.transform.rotation = launch.transform.rotation;
                var dc = d.GetComponent<Ironfield.Drone.DroneController>();
                if (dc != null && dc.propSpinners != null)
                    foreach (var p in dc.propSpinners) if (p) p.Rotate(Vector3.up, 30f, Space.World);
                Vector3 back = -launch.transform.forward;
                Vector3 eye3 = d.transform.position + back * 6f + Vector3.up * 2.3f;
                Shot(cam, eye3, d.transform.position + d.transform.forward * 3f, "Ironfield_smoke_drone_heavy.png");
                Object.DestroyImmediate(d);
            }
            _ = scene;

            // Mission03: eyeball the flak emplacement + high-value beacon
            var m3 = EditorSceneManager.OpenScene(ScenesDir + "/Mission03.unity", OpenSceneMode.Single);
            var cam3 = Camera.main;
            var flak = GameObject.Find("FlakPosition");
            if (cam3 != null && flak != null)
            {
                RenderSettings.fogDensity = 0.0009f;
                Vector3 feye = flak.transform.position + new Vector3(-16, 9, -16);
                Shot(cam3, feye, flak.transform.position + Vector3.up * 2f, "Ironfield_smoke_flak.png");
            }
            var hqFlag = GameObject.Find("HQFlag");
            if (cam3 != null && hqFlag != null)
            {
                Vector3 heye = hqFlag.transform.position + new Vector3(-10, 4, -10);
                Shot(cam3, heye, hqFlag.transform.position, "Ironfield_smoke_hq.png");
            }
            _ = m3;
        }

        static void Shot(Camera cam, Vector3 eye, Vector3 lookAt, string file)
        {
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - eye, Vector3.up);
            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            string outPath = Path.GetFullPath(file);
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[Ironfield] Wrote " + outPath);
        }

        static T LoadPrefabComponent<T>(string path) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { Debug.LogError("[Ironfield] Prefab missing: " + path); return null; }
            return go.GetComponent<T>();
        }

        // ----------------------------------------------------------------- //
        // Imports
        // ----------------------------------------------------------------- //
        static void ConfigureModelImport(string path, float scale)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null)
            {
                Debug.LogError("[Ironfield] Missing model: " + path + " (run tools/build_assets.sh)");
                return;
            }
            mi.globalScale = scale;
            mi.useFileScale = true;
            try { mi.bakeAxisConversion = true; } catch { /* not on all versions */ }
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.SaveAndReimport();
        }

        // ----------------------------------------------------------------- //
        // External CC0/CC-BY model loading
        // ----------------------------------------------------------------- //
        public enum FitAxis { XZ, Y }

        /// <summary>
        /// Instantiate an imported FBX, unpack it, scale it so its largest
        /// footprint (XZ) or height (Y) equals <paramref name="targetSize"/>
        /// metres, drop it so its base sits at local y=0, and yaw it. Returns
        /// the instance (unparented) or null if the model is missing.
        /// </summary>
        static Dictionary<string, Dictionary<string, Color>> _palettes;
        static Color _skyColor = new(0.70f, 0.76f, 0.83f);

        static void LoadPalettes()
        {
            if (_palettes != null) return;
            _palettes = new();
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ExtDir + "palettes.txt");
            if (ta == null) return;
            // flat format: model|material|r,g,b   (one per line)
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var p = line.Split('|');
                if (p.Length != 3) continue;
                var rgb = p[2].Split(',');
                if (rgb.Length != 3) continue;
                if (!_palettes.TryGetValue(p[0], out var map))
                    _palettes[p[0]] = map = new Dictionary<string, Color>();
                map[p[1]] = new Color(
                    float.Parse(rgb[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(rgb[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(rgb[2], System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        static Color Militarize(Color c)
        {
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            if (lum < 0.06f) return c;                       // keep near-blacks (tracks, tyres)
            var olive = new Color(0.29f, 0.31f, 0.19f) * Mathf.Clamp01(lum * 1.6f + 0.10f);
            return Color.Lerp(c, olive, 0.80f);              // strongly de-toy the tank
        }

        static void ApplyPalette(GameObject go, string modelName, bool militarize)
        {
            LoadPalettes();
            if (!_palettes.TryGetValue(modelName, out var map)) return;
            bool isTree = modelName.StartsWith("tree_");
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = r.sharedMaterials;
                var outMats = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) { outMats[i] = mats[i]; continue; }
                    string key = mats[i].name.Replace(" (Instance)", "");
                    int dot = key.IndexOf('.');
                    string baseKey = dot > 0 ? key[..dot] : key;
                    Color col = map.TryGetValue(key, out var c1) ? c1
                              : map.TryGetValue(baseKey, out var c2) ? c2
                              : mats[i].color;
                    if (militarize) col = Militarize(col);
                    // the tree palettes are near-black; lift them into daylight
                    if (isTree && col.maxColorComponent < 0.35f)
                        col = new Color(col.r, col.g, col.b) * (0.35f / Mathf.Max(0.02f, col.maxColorComponent));
                    outMats[i] = MakeStandard($"ext_{modelName}_{key}", col, 0.12f, 0f);
                }
                r.sharedMaterials = outMats;
            }
        }

        static GameObject LoadExternalModel(string path, float targetSize,
            FitAxis fit = FitAxis.XZ, float yawDeg = 0f, bool militarize = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            // keep the importer's own axis-correction transform on `go`; only
            // move it to the origin so bounds math below is in a clean frame.
            go.transform.position = Vector3.zero;

            // rebuild materials from the source .glb base colours (the glb->fbx
            // hop mangles them), optionally pushed toward olive-drab.
            ApplyPalette(go, Path.GetFileNameWithoutExtension(path), militarize);

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Object.DestroyImmediate(go); return null; }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            float measure = fit == FitAxis.Y ? Mathf.Max(b.size.y, 0.01f)
                                             : Mathf.Max(b.size.x, b.size.z, 0.01f);
            float k = targetSize / measure;

            // wrap in a clean root so scale + base-align + yaw compose cleanly
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.transform.SetParent(root.transform, true);
            root.transform.localScale = Vector3.one * k;

            // recompute bounds after scaling to find the base
            rends = root.GetComponentsInChildren<Renderer>();
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            return root;
        }

        // ----------------------------------------------------------------- //
        // Assets / prefabs
        // ----------------------------------------------------------------- //
        static DroneTuning CreateTuning()
        {
            string p = SettingsDir + "/DroneTuning.asset";
            var t = AssetDatabase.LoadAssetAtPath<DroneTuning>(p);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<DroneTuning>();
                AssetDatabase.CreateAsset(t, p);
            }
            // keep the flight feel in code so a rebuild re-applies it
            t.maxSpeed = 22f;
            t.boostMaxSpeed = 40f;
            t.yawRate = 110f;
            t.climbAccel = 12f;
            t.gravity = 9.81f;
            t.linearDrag = 1.7f;
            t.angularDamp = 7f;
            t.maxHealth = 55f;               // survive a burst or two of return fire
            t.warheadDamage = 650f;
            t.warheadRadius = 5.5f;
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>Second drone type — slower and sluggish to turn, but far
        /// tougher and hits much harder. A real tradeoff, not a strict upgrade:
        /// the Light drone stays the better pick when speed/agility matter (most
        /// dives, evading return fire); Heavy earns its keep against flak-heavy
        /// missions and one-shotting armoured high-value targets.</summary>
        static DroneTuning CreateHeavyTuning()
        {
            string p = SettingsDir + "/DroneTuning_Heavy.asset";
            var t = AssetDatabase.LoadAssetAtPath<DroneTuning>(p);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<DroneTuning>();
                AssetDatabase.CreateAsset(t, p);
            }
            t.maxSpeed = 17f;                // -23% vs Light: sluggish
            t.boostMaxSpeed = 30f;           // -25%
            t.yawRate = 85f;                 // -23%: turns slower too
            t.climbAccel = 10f;
            t.gravity = 9.81f;
            t.linearDrag = 2.0f;             // heavier, slower to respond to throttle changes
            t.angularDamp = 8f;
            t.maxHealth = 95f;               // +73%: shrugs off return fire that would kill a Light
            t.warheadDamage = 900f;          // +38%
            t.warheadRadius = 6.5f;          // +18%: more forgiving near-miss radius
            EditorUtility.SetDirty(t);
            return t;
        }

        static Explosion BuildExplosionPrefab()
        {
            var go = new GameObject("Explosion");
            var ex = go.AddComponent<Explosion>();
            ex.damage = 650f; ex.radius = 5.5f; ex.force = 14f; ex.autoDestroyAfter = 4f;

            var light = new GameObject("FlashLight");
            light.transform.SetParent(go.transform, false);
            var l = light.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.7f, 0.35f);
            l.range = 22f; l.intensity = 8f;
            light.AddComponent<FlashFade>();

            AddParticles(go.transform, "Blast", new Color(1f, 0.55f, 0.2f), 60, 9f, 2.2f, 1.1f);
            AddParticles(go.transform, "Smoke", new Color(0.15f, 0.15f, 0.15f), 30, 3f, 3.5f, 2.5f);

            var audio = go.AddComponent<AudioSource>();
            audio.spatialBlend = 1f; audio.minDistance = 8f; audio.maxDistance = 260f;
            audio.playOnAwake = true;
            audio.clip = ExplosionClip();

            var prefab = SavePrefab(go, PrefabDir + "/Explosion.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Explosion>();
        }

        static ParticleSystem BuildFireSmokePrefab()
        {
            var go = new GameObject("FireSmoke");
            AddParticles(go.transform, "Fire", new Color(1f, 0.45f, 0.12f), 24, 2.2f, 2.4f, 2.0f);
            AddParticles(go.transform, "Column", new Color(0.1f, 0.1f, 0.1f), 16, 1.6f, 6f, 3.5f);
            var ps = go.GetComponentInChildren<ParticleSystem>();
            var prefab = SavePrefab(go, PrefabDir + "/FireSmoke.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponentInChildren<ParticleSystem>();
        }

        /// <summary>Visual identity for one drone variant — same imported FBX,
        /// different size/tint so Light and Heavy read as distinct at a glance
        /// (and their generated materials don't collide, since MakeStandard
        /// caches by name).</summary>
        struct DroneBuildConfig
        {
            public string prefabName;
            public float sizeMultiplier;
            public Color airframeColor;
            public Color rotorColor;

            public static readonly DroneBuildConfig Light = new()
            {
                prefabName = "Drone",
                sizeMultiplier = 1f,
                airframeColor = new Color(0.055f, 0.055f, 0.065f),   // dark carbon
                rotorColor = new Color(0.16f, 0.16f, 0.18f),          // gunmetal
            };
            // Bulkier silhouette + an armoured olive tint (same treatment as
            // Militarize on the tank) so it visually reads as "heavier armour",
            // matching the tuning: slower, tankier, hits harder.
            public static readonly DroneBuildConfig Heavy = new()
            {
                prefabName = "Drone_Heavy",
                sizeMultiplier = 1.22f,
                airframeColor = Militarize(new Color(0.10f, 0.11f, 0.08f)),
                rotorColor = new Color(0.14f, 0.15f, 0.13f),
            };
        }

        static DroneController BuildDronePrefab(DroneTuning tuning, DroneBuildConfig cfg)
        {
            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Explosion.prefab")
                ?.GetComponent<Explosion>();

            // Prefer the real CC-BY FPV model; fall back to the Blender blockout.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDroneExt)
                        ?? AssetDatabase.LoadAssetAtPath<GameObject>(ArtDrone);
            GameObject root = model != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(model)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (model != null)
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            root.name = cfg.prefabName;
            root.tag = GameTags.Drone;
            SetLayerRecursive(root, GameLayers.Drone);

            // Move the imported visual under a scaled child so it reads from the
            // chase cam; the root collider stays a sane box.
            var existingKids = new List<Transform>();
            foreach (Transform child in root.transform) existingKids.Add(child);
            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            foreach (var k in existingKids) k.SetParent(visualRoot.transform, true);

            float targetSpan = 3.0f * cfg.sizeMultiplier;   // metres, motor to motor
            var mfs = visualRoot.GetComponentsInChildren<MeshFilter>();
            float span = 0f;
            foreach (var mf in mfs)
                if (mf.sharedMesh != null)
                {
                    var s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
                    span = Mathf.Max(span, s.x, s.z);
                }
            float mult = span > 0.001f ? targetSpan / span : 10f;
            visualRoot.transform.localScale = Vector3.one * mult;
            Debug.Log($"[Ironfield] {cfg.prefabName} visual span={span:0.00}m  ->  scale x{mult:0.00}  (meshes={mfs.Length})");

            // --- retint: the imported palette texture doesn't survive the
            //     glb->fbx hop, so give the airframe a proper dark-carbon look
            //     (or, for the Heavy variant, an armoured olive one).
            var carbon = MakeStandard(cfg.prefabName + "_carbon", cfg.airframeColor, 0.4f, 0.15f);
            var gunmetal = MakeStandard(cfg.prefabName + "_gunmetal", cfg.rotorColor, 0.55f, 0.6f);
            foreach (var r in visualRoot.GetComponentsInChildren<MeshRenderer>())
            {
                string rn = r.name.ToLowerInvariant();
                r.sharedMaterial = (rn.Contains("rotor") || rn.Contains("prop")) ? gunmetal : carbon;
            }

            // --- spin pivots for the rotors --------------------------------
            var props = new List<Transform>();
            foreach (var tr in visualRoot.GetComponentsInChildren<Transform>())
            {
                string n = tr.name.ToLowerInvariant();
                if (!(n.Contains("rotor") || n.Contains("prop") || n.Contains("blade") || n.Contains("fan")))
                    continue;
                var mr = tr.GetComponent<Renderer>();
                if (mr == null) continue;
                var pivot = new GameObject(tr.name + "_Spin");
                pivot.transform.SetParent(tr.parent, false);
                // spin axis is TRUE vertical through the rotor's centre — a real
                // quad's rotors sweep the horizontal plane. World-aligned pivot
                // so DroneController can just spin it about world up.
                pivot.transform.position = new Vector3(mr.bounds.center.x, mr.bounds.center.y, mr.bounds.center.z);
                pivot.transform.rotation = Quaternion.identity;
                tr.SetParent(pivot.transform, true);
                props.Add(pivot.transform);
            }

            // --- underslung warhead (visual only) ----------------------
            {
                var whVis = new GameObject("Warhead");
                whVis.transform.SetParent(visualRoot.transform, true);
                var b = visualRoot.GetComponentInChildren<Renderer>().bounds;
                whVis.transform.position = new Vector3(b.center.x, b.min.y - 0.05f, b.center.z);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                body.name = "WH_Body";
                body.transform.SetParent(whVis.transform, false);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.transform.localScale = new Vector3(0.18f, 0.28f, 0.18f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(new Color(0.14f, 0.14f, 0.15f), "wh_body");
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.name = "WH_Tip";
                tip.transform.SetParent(whVis.transform, false);
                tip.transform.localPosition = new Vector3(0f, 0f, 0.28f);
                tip.transform.localScale = new Vector3(0.19f, 0.19f, 0.24f);
                Object.DestroyImmediate(tip.GetComponent<Collider>());
                tip.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(new Color(0.75f, 0.22f, 0.10f), "wh_tip");
                SetLayerRecursive(whVis, GameLayers.Drone);
            }

            // a small nav strobe so the drone reads against the ground
            var strobe = new GameObject("NavLight");
            strobe.transform.SetParent(visualRoot.transform, false);
            strobe.transform.localPosition = Vector3.zero;
            var sl = strobe.AddComponent<Light>();
            sl.type = LightType.Point; sl.color = new Color(1f, 0.25f, 0.15f);
            sl.range = 10f; sl.intensity = 2.4f;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.2f; rb.useGravity = false;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.2f, 0f);
            col.size = new Vector3(2.8f, 1.0f, 2.8f);

            var health = root.AddComponent<HealthComponent>();
            health.maxHealth = tuning.maxHealth; health.armor = 0f;

            var ctrl = root.AddComponent<DroneController>();
            ctrl.tuning = tuning;
            ctrl.propSpinners = props.ToArray();

            var wh = root.AddComponent<DroneWarhead>();
            wh.explosionPrefab = explosion;

            var trail = new GameObject("MotorAudio");
            trail.transform.SetParent(root.transform, false);
            var a = trail.AddComponent<AudioSource>();
            a.loop = true; a.playOnAwake = true; a.spatialBlend = 1f;
            a.minDistance = 3f; a.maxDistance = 90f; a.volume = 0.5f;
            a.clip = MakeMotorClip();
            trail.AddComponent<MotorPitch>();

            var prefab = SavePrefab(root, PrefabDir + "/" + cfg.prefabName + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DroneController>();
        }

        static Vehicle BuildVehiclePrefab(string name, string fbx, VehicleClass cls,
            float hp, float armor, float width, float length, float height)
        {
            var fireSmoke = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/FireSmoke.prefab");
            var root = new GameObject(name);
            root.tag = GameTags.Vehicle;
            SetLayerRecursive(root, GameLayers.Vehicle);

            // Prefer the CC-BY external model; the yaw values line each model's
            // nose up with +Z (found by eye from the smoke shot).
            (string ext, float yaw) = cls switch
            {
                VehicleClass.Tank  => (ExtDir + "tank.fbx", 0f),
                VehicleClass.IFV   => (ExtDir + "ifv.fbx", 0f),
                _                  => (ExtDir + "truck.fbx", 0f),
            };

            // the tank model ships toy-coloured; militarise it. IFV / truck
            // palettes are already olive so leave them faithful.
            bool milit = cls == VehicleClass.Tank;
            GameObject intact = LoadExternalModel(ext, length, FitAxis.XZ, yaw, milit);
            GameObject wreck = null;
            if (intact != null)
            {
                intact.name = "Model";
                intact.transform.SetParent(root.transform, false);
                // recover real footprint from the fitted model
                var rr = intact.GetComponentsInChildren<Renderer>();
                Bounds bb = rr[0].bounds;
                for (int i = 1; i < rr.Length; i++) bb.Encapsulate(rr[i].bounds);
                width = Mathf.Max(1f, bb.size.x);
                height = Mathf.Max(1f, bb.size.y);
                length = Mathf.Max(1f, bb.size.z);
            }
            else
            {
                intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
                intact.name = "Model";
                intact.transform.SetParent(root.transform, false);
                intact.transform.localScale = new Vector3(width, height, length);
                intact.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            }

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height, length);
            col.center = new Vector3(0f, height * 0.5f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = cls == VehicleClass.Tank ? 45000f : cls == VehicleClass.IFV ? 18000f : 9000f;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var health = root.AddComponent<HealthComponent>();
            health.maxHealth = hp; health.armor = armor; health.explosiveArmorPierce = 0.85f;

            var v = root.AddComponent<Vehicle>();
            v.vehicleClass = cls;
            v.displayName = cls switch
            {
                VehicleClass.Tank => "Main battle tank",
                VehicleClass.IFV => "Infantry fighting vehicle",
                _ => "Supply truck",
            };
            var aim = new GameObject("AimPoint");
            aim.transform.SetParent(root.transform, false);
            aim.transform.localPosition = new Vector3(0f, height * 0.6f, 0f);
            v.aimPoint = aim.transform;

            var ai = root.AddComponent<VehicleConvoyAI>();
            ai.speed = cls == VehicleClass.Truck ? 8f : 6f;

            var wk = root.AddComponent<Wreck>();
            wk.intactVisual = intact;
            wk.wreckedVisual = wreck;
            wk.fireSmokePrefab = fireSmoke;
            wk.fireLocalOffset = new Vector3(0f, height * 0.5f, 0f);
            wk.destroyedSfx = ExplosionClip();

            if (cls != VehicleClass.Truck)
            {
                var turret = root.AddComponent<VehicleTurret>();
                turret.enabled = true;                       // convoy shoots back
                turret.range = cls == VehicleClass.Tank ? 190f : 150f;
                turret.fireInterval = cls == VehicleClass.Tank ? 0.16f : 0.11f;
                turret.burst = cls == VehicleClass.Tank ? 3 : 5;
                turret.burstPause = cls == VehicleClass.Tank ? 1.7f : 1.3f;
                turret.spreadDeg = cls == VehicleClass.Tank ? 3.2f : 2.4f;
                turret.damagePerHit = cls == VehicleClass.Tank ? 7f : 4.5f;
                var muz = new GameObject("Muzzle");
                muz.transform.SetParent(root.transform, false);
                muz.transform.localPosition = new Vector3(0f, height * 0.7f, length * 0.25f);
                turret.muzzle = muz.transform;
                turret.tracerPrefab = MakeTracerPrefab();
            }

            var prefab = SavePrefab(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Vehicle>();
        }

        // ----------------------------------------------------------------- //
        // Scene
        // ----------------------------------------------------------------- //
        static void BuildScene(MissionBuildConfig cfg)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- lighting / atmosphere (Built-in) : hazy overcast afternoon ---
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.5f;                           // clear afternoon: real contrast
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.5f;
            sunGo.transform.rotation = Quaternion.Euler(48f, 34f, 0f);
            RenderSettings.sun = sun;

            // cool skylight fill so shadow sides aren't dead black
            var fillGo = new GameObject("SkyFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.45f, 0.53f, 0.68f);
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(-28f, 210f, 0f);

            var skyCol = new Color(0.53f, 0.66f, 0.83f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.49f, 0.60f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.38f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.14f, 0.11f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.72f, 0.82f);
            RenderSettings.fogDensity = 0.0010f;            // clear mid-field, haze only the far ridge
            QualitySettings.shadowDistance = 420f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.antiAliasing = 4;
            QualitySettings.pixelLightCount = 4;
            _skyColor = skyCol;

            // --- terrain ---------------------------------------------
            var terrain = BuildTerrain();

            // --- road + convoy path --------------------------------
            var pathParent = new GameObject("ConvoyPath").transform;
            var waypoints = new List<Transform>();
            for (int i = 0; i < RoadPoints.Length; i++)
            {
                var wp = new GameObject($"WP_{i}").transform;
                wp.SetParent(pathParent);
                Vector3 p = RoadPoints[i];
                p.y = SampleHeight(terrain, p) ;
                wp.position = p;
                waypoints.Add(wp);
            }

            // paint ground + road + scatter vegetation now that the path is known
            PaintTerrain(terrain, waypoints);
            BuildRoadMesh(terrain, waypoints);
            ScatterVegetation(terrain, waypoints);

            // --- launch ridge --------------------------------------
            var launch = new GameObject("LaunchPoint");
            launch.tag = GameTags.LaunchPoint;
            Vector3 lp = new(-200, 0, 100);
            lp.y = SampleHeight(terrain, lp) + 22f;
            launch.transform.position = lp;
            launch.transform.rotation = Quaternion.LookRotation(
                new Vector3(0, -8, -30) - lp, Vector3.up);

            // --- camera --------------------------------------------
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.farClipPlane = 1600f;
            cam.nearClipPlane = 0.08f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _skyColor;
            camGo.AddComponent<AudioListener>();
            var post = camGo.AddComponent<Ironfield.Fx.CameraPost>();
            post.exposure = 1.0f;
            post.contrast = 1.11f;
            post.saturation = 1.14f;
            post.vignette = 0.26f;
            var rig = camGo.AddComponent<DroneCameraRig>();
            camGo.transform.position = lp + new Vector3(0, 3, -8);

            // --- convoy -------------------------------------------
            // Load prefab refs *here*, after every AssetDatabase.CreateAsset in
            // this method (terrain, ground mat) has run, so they can't be
            // invalidated by a mid-build reimport.
            var tankPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Tank.prefab");
            var ifvPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/IFV.prefab");
            var truckPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Truck.prefab");

            GameObject PrefabFor(VehicleClass c) => c switch
            {
                VehicleClass.Tank => tankPf,
                VehicleClass.IFV => ifvPf,
                _ => truckPf,
            };

            var convoyParent = new GameObject("Convoy").transform;
            Vehicle prevAhead = null;
            for (int i = 0; i < cfg.convoy.Length; i++)
            {
                Vector3 pos = Vector3.Lerp(waypoints[0].position, waypoints[1].position, 0.15f)
                              - (waypoints[1].position - waypoints[0].position).normalized * (i * 14f);
                pos.y = SampleHeight(terrain, pos);
                var vgo = (GameObject)PrefabUtility.InstantiatePrefab(PrefabFor(cfg.convoy[i]));
                var vinst = vgo.GetComponent<Vehicle>();
                vinst.transform.SetParent(convoyParent);
                vinst.transform.position = pos;
                vinst.transform.rotation = Quaternion.LookRotation(
                    (waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
                var ai = vinst.GetComponent<VehicleConvoyAI>();
                ai.waypoints = waypoints.ToArray();
                ai.vehicleAhead = prevAhead;
                ai.speed *= cfg.convoySpeedMul;
                prevAhead = vinst;

                bool isHighValue = cfg.highValueTarget && i == cfg.convoy.Length - 1;
                if (isHighValue)
                {
                    vinst.highValue = true;
                    vinst.displayName = "HQ COMMAND VEHICLE";
                    MarkHighValue(vinst.transform);
                }
            }

            // --- static flak emplacements (Mission02+) ---------
            for (int f = 0; f < cfg.flakCount; f++)
            {
                float t = 0.35f + f * 0.28f; // spaced out along the road
                Vector3 along = Vector3.Lerp(waypoints[1].position, waypoints[3].position, t);
                Vector3 side = Vector3.Cross(Vector3.up,
                    (waypoints[3].position - waypoints[1].position).normalized);
                Vector3 pos = along + side * (f % 2 == 0 ? 34f : -34f);
                pos.y = SampleHeight(terrain, pos);
                BuildFlakPosition(pos, convoyParent.position - pos);
            }

            // --- village ruins + battlefield dressing ----------
            ScatterRuins(terrain);
            ScenePropsPass(terrain, waypoints);
            BuildAtmosphereDust();

            // --- managers --------------------------------------
            var mgrGo = new GameObject("MissionManager");
            var mgr = mgrGo.AddComponent<MissionManager>();
            mgr.dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone.prefab")
                .GetComponent<DroneController>();
            mgr.dronePrefabHeavy = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone_Heavy.prefab")
                ?.GetComponent<DroneController>();
            mgr.launchPoint = launch.transform;
            mgr.cameraRig = rig;
            mgr.droneStock = cfg.droneStock;
            mgr.missionId = cfg.missionId;

            var targeting = mgrGo.AddComponent<TargetingSystem>();
            targeting.viewCamera = cam;
            mgr.targeting = targeting;

            var hud = mgrGo.AddComponent<HudController>();
            hud.mission = mgr;
            hud.targeting = targeting;
            hud.cameraRig = rig;
            hud.hitPingClip = HitPingClip();

            var pause = mgrGo.AddComponent<PauseMenu>();
            pause.mission = mgr;
            var tip = mgrGo.AddComponent<FirstRunTip>();
            tip.mission = mgr;
            mgrGo.AddComponent<PerfHarness>().mission = mgr; // inert unless launched with -perftest

            rig.Bind(null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, cfg.scenePath);
        }

        /// <summary>Small red beacon light + flag over the high-value convoy
        /// vehicle so it reads as distinct at a glance and from the HUD lock.</summary>
        static void MarkHighValue(Transform vehicle)
        {
            var beaconGo = new GameObject("HQBeacon");
            beaconGo.transform.SetParent(vehicle, false);
            beaconGo.transform.localPosition = Vector3.up * 4.6f;
            var light = beaconGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.2f, 0.15f);
            light.range = 14f;
            light.intensity = 3f;

            // pole + flag well clear of the tallest vehicle roof (truck ~3.3 m)
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "HQPole";
            pole.transform.SetParent(vehicle, false);
            pole.transform.localPosition = Vector3.up * 3.6f;
            pole.transform.localScale = new Vector3(0.05f, 1f, 0.05f);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.GetComponent<MeshRenderer>().sharedMaterial =
                MakeStandard("hq_pole", new Color(0.1f, 0.1f, 0.1f), 0.2f, 0.3f);

            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.name = "HQFlag";
            flag.transform.SetParent(vehicle, false);
            flag.transform.localPosition = Vector3.up * 4.6f + Vector3.right * 0.3f;
            flag.transform.localScale = new Vector3(0.6f, 0.4f, 0.04f);
            Object.DestroyImmediate(flag.GetComponent<Collider>());
            flag.GetComponent<MeshRenderer>().sharedMaterial =
                MakeStandard("hq_flag", new Color(0.75f, 0.08f, 0.06f), 0.15f, 0f);
            SetLayerRecursive(flag, GameLayers.Vehicle);
        }

        /// <summary>Stationary flak position: a standalone VehicleTurret (no Vehicle/
        /// HealthComponent, so it can't be destroyed for score — it's terrain to route
        /// around, not an objective) on a short pole with a simple gun mount, tuned
        /// slower/wider than vehicle return fire so it reads as "AA" rather than "tank".</summary>
        static void BuildFlakPosition(Vector3 pos, Vector3 facing)
        {
            var root = new GameObject("FlakPosition");
            root.transform.position = pos;
            root.transform.rotation = facing.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(new Vector3(facing.x, 0, facing.z).normalized, Vector3.up)
                : Quaternion.identity;

            var baseMat = MakeStandard("flak_base", new Color(0.24f, 0.23f, 0.19f), 0.1f, 0.1f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.5f, 1.1f, 0.5f);
            pole.transform.localPosition = Vector3.up * 1.1f;
            pole.GetComponent<MeshRenderer>().sharedMaterial = baseMat;

            var yaw = new GameObject("Yaw").transform;
            yaw.SetParent(root.transform, false);
            yaw.localPosition = Vector3.up * 2.2f;

            var pitch = new GameObject("Pitch").transform;
            pitch.SetParent(yaw, false);

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(pitch, false);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.16f, 1.3f, 0.16f);
            barrel.transform.localPosition = new Vector3(0, 0, 1.1f);
            barrel.GetComponent<MeshRenderer>().sharedMaterial =
                MakeStandard("flak_gunmetal", new Color(0.16f, 0.16f, 0.17f), 0.35f, 0.6f);
            Object.DestroyImmediate(barrel.GetComponent<Collider>());

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pitch, false);
            muzzle.localPosition = new Vector3(0, 0, 2.4f);

            var turret = root.AddComponent<VehicleTurret>();
            turret.yaw = yaw; turret.pitch = pitch; turret.muzzle = muzzle;
            turret.tracerPrefab = MakeTracerPrefab();
            turret.range = 140f;
            turret.traverseDeg = 140f;
            turret.fireInterval = 0.32f;
            turret.burst = 3;
            turret.burstPause = 2.2f;
            turret.spreadDeg = 4.5f;
            turret.damagePerHit = 5f;

            // sandbag ring for a battlefield read, matches ScenePropsPass's palette
            var sand = MakeUnlit(new Color(0.42f, 0.37f, 0.24f), "sandbag");
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bag.transform.SetParent(root.transform, false);
                bag.transform.localPosition = new Vector3(Mathf.Sin(a) * 2f, 0.35f, Mathf.Cos(a) * 2f - 1.2f);
                bag.transform.localScale = new Vector3(1.1f, 0.6f, 0.7f);
                bag.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
                Object.DestroyImmediate(bag.GetComponent<Collider>());
                bag.GetComponent<MeshRenderer>().sharedMaterial = sand;
            }

            root.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 3f);
            SetLayerRecursive(root, GameLayers.Environment);
        }

        /// <summary>Title screen: Start / Settings / Quit. Its own tiny scene so a
        /// build boots there instead of straight into Mission01.</summary>
        static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("MainCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.10f);
            cam.tag = "MainCamera";

            var menuGo = new GameObject("MainMenu");
            menuGo.AddComponent<MainMenuController>();

            // DontDestroyOnLoad in UiSfx.Awake() carries this into every later
            // scene, so every IMGUI button in the game gets the same click.
            var uiSfxGo = new GameObject("UiSfx");
            uiSfxGo.AddComponent<UiSfx>().clickClip = UiClickClip();

            // inert unless launched with -demo; DontDestroyOnLoad carries it
            // through the whole scripted playthrough (see DemoRunner.cs)
            var demoGo = new GameObject("DemoRunner");
            demoGo.AddComponent<Ironfield.Mission.DemoRunner>();
            demoGo.AddComponent<DemoRecorder>(); // inert unless launched with -record

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory(ScenesDir);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        // Map was 1024x1024 through 2026-09-11; a player flying in any direction
        // away from the road hit open terrain edge in under 20s at cruise speed
        // (worse with boost) with nothing to see on the way there. Doubled to
        // 2048x2048 (4x the area) — the road/village/launch point keep their
        // existing absolute coordinates, so this just adds open flyable margin
        // on every side rather than requiring every hardcoded prop position in
        // this file to be rescaled. See DroneBoundary for what stops a player
        // who still flies past *that* edge.
        static readonly Vector3 TerrainOrigin = new(-1024, 0, -1024);
        static readonly Vector3 LaunchHillCentre = new(-200, 0, 100);
        // Road polyline in world space — used both to lay out the convoy path
        // (BuildScene) and, via distance-to-polyline, to flatten a corridor for
        // it while sculpting the heightmap (BuildTerrain runs before the actual
        // waypoint Transforms exist, so it needs its own copy of the route).
        static readonly Vector3[] RoadPoints =
        {
            new(-300, 0, -190), new(-180, 0, -120), new(-70, 0, -60),
            new(40, 0, 0), new(150, 0, 60), new(300, 0, 170), new(430, 0, 300),
        };

        static float Fbm(float x, float y, int oct, float lac = 2.03f, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, frq = 1f;
            for (int i = 0; i < oct; i++)
            {
                sum += (Mathf.PerlinNoise(x * frq, y * frq) - 0.5f) * amp;
                frq *= lac; amp *= gain;
            }
            return sum;
        }

        static Terrain BuildTerrain()
        {
            var data = new TerrainData
            {
                // 1025 keeps per-metre detail on the now-2048m-wide map the same
                // as the old 513-sample 1024m map (both ~2m/sample).
                heightmapResolution = 1025,
                size = new Vector3(2048, 110, 2048),
            };
            int res = data.heightmapResolution;
            var h = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)res, ny = y / (float)res;
                // world XZ of this heightmap sample
                float wx = TerrainOrigin.x + nx * data.size.x;
                float wz = TerrainOrigin.z + ny * data.size.z;

                // Noise frequency is defined in world metres (not a fraction of
                // terrain size) so hill wavelength stays constant regardless of
                // how big the terrain is — matches the ~465m/~146m wavelengths
                // the old 1024m map had (2.2/1024 and 7/1024 cycles per metre).
                float e = 0.30f
                          + Fbm(wx * 0.002148f + 11f, wz * 0.002148f + 7f, 3) * 0.9f
                          + Fbm(wx * 0.006836f, wz * 0.006836f, 3) * 0.18f
                          + (nx - 0.5f) * 0.12f;                    // rise to the east

                // broad flattened corridor for the road: real distance to the
                // actual route polyline (world metres), not a normalized-space
                // line guess — stays correct no matter how big the terrain is.
                float corridorDist = DistanceToPolylineXZ(new Vector3(wx, 0, wz), RoadPoints);
                e = Mathf.Lerp(0.29f, e, Mathf.Clamp01(corridorDist / 180f));

                // raise a real ridge at the launch point
                float dHill = new Vector2(wx - LaunchHillCentre.x, wz - LaunchHillCentre.z).magnitude;
                e += Mathf.Exp(-(dHill * dHill) / (2f * 70f * 70f)) * 0.14f;

                h[y, x] = Mathf.Clamp01(e) * 0.34f;
            }
            data.SetHeights(0, 0, h);

            AssetDatabase.CreateAsset(data, SettingsDir + "/Mission01_Terrain.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            SetLayerRecursive(go, GameLayers.Environment);
            go.transform.position = TerrainOrigin;
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            t.detailObjectDistance = 160f;
            t.detailObjectDensity = 1f;
            t.heightmapPixelError = 3f;
            t.treeDistance = 1400f;
            return t;                     // textured later by PaintTerrain
        }

        // ------------------------------------------------------------------ //
        // Terrain texturing + vegetation
        // ------------------------------------------------------------------ //
        // ---- procedural ground / foliage textures --------------------------
        static Texture2D MakeGroundTex(string name, Color a, Color b, float grain, int size = 256)
        {
            string p = SettingsDir + "/T_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (existing != null) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name };
            var rng = new System.Random(name.GetHashCode());
            float ox = (float)rng.NextDouble() * 60f, oy = (float)rng.NextDouble() * 60f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(u * 4f + ox, v * 4f + oy, 5, 2.17f, 0.55f);
                float blotch = Mathf.PerlinNoise(u * 7f + ox, v * 7f + oy);
                float g = (float)rng.NextDouble() - 0.5f;
                Color c = Color.Lerp(a, b, Mathf.Clamp01(0.5f + n * 1.4f + (blotch - 0.5f) * 0.55f));
                c += new Color(1f, 1f, 1f, 0f) * (n * 0.10f + g * grain * 0.7f);
                c *= 0.82f;   // ground reads darker once it's lit
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, p);
            return tex;
        }

        static Texture2D MakeGrassBladeTex()
        {
            const string p = SettingsDir + "/T_grassblade.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (existing != null) return existing;
            int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "grassblade" };
            var px = new Color[s * s];
            var rng = new System.Random(7);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            for (int blade = 0; blade < 7; blade++)
            {
                float bx = 4 + (float)rng.NextDouble() * (s - 8);
                float lean = ((float)rng.NextDouble() - 0.5f) * 10f;
                float hgt = s * (0.5f + (float)rng.NextDouble() * 0.45f);
                var col = Color.Lerp(new Color(0.32f, 0.44f, 0.18f), new Color(0.5f, 0.55f, 0.25f),
                                     (float)rng.NextDouble());
                for (float t = 0; t < hgt; t += 0.5f)
                {
                    int yy = s - 1 - Mathf.RoundToInt(t);
                    int xx = Mathf.RoundToInt(bx + lean * (t / hgt));
                    float w = Mathf.Lerp(1.6f, 0.3f, t / hgt);
                    for (int dx = -Mathf.CeilToInt(w); dx <= Mathf.CeilToInt(w); dx++)
                    {
                        int ix = xx + dx;
                        if (ix < 0 || ix >= s || yy < 0 || yy >= s) continue;
                        px[yy * s + ix] = new Color(col.r, col.g, col.b, 1f);
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, p);
            return tex;
        }

        static void PaintTerrain(Terrain terrain, List<Transform> waypoints)
        {
            var data = terrain.terrainData;
            // doubled alongside the terrain footprint to hold the same
            // metres-per-sample texture detail as before.
            data.alphamapResolution = 1024;
            data.baseMapResolution = 2048;

            TerrainLayer L(string n, Color a, Color b, float grain, float tile)
            {
                string p = SettingsDir + "/TL_" + n + ".terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
                if (tl == null) { tl = new TerrainLayer { name = n }; AssetDatabase.CreateAsset(tl, p); }
                tl.diffuseTexture = MakeGroundTex(n, a, b, grain);
                tl.tileSize = new Vector2(tile, tile);
                tl.specular = Color.black;
                tl.metallic = 0f; tl.smoothness = 0.02f;
                EditorUtility.SetDirty(tl);
                return tl;
            }

            var grass = L("grass", new Color(0.17f, 0.24f, 0.09f), new Color(0.28f, 0.34f, 0.16f), 0.05f, 8f);
            var dry = L("dry", new Color(0.34f, 0.32f, 0.19f), new Color(0.47f, 0.43f, 0.27f), 0.05f, 10f);
            var dirt = L("dirt", new Color(0.22f, 0.17f, 0.12f), new Color(0.34f, 0.27f, 0.19f), 0.06f, 6f);
            var gravel = L("gravel", new Color(0.20f, 0.19f, 0.16f), new Color(0.34f, 0.32f, 0.29f), 0.10f, 3.5f);
            var rock = L("rock", new Color(0.16f, 0.15f, 0.14f), new Color(0.32f, 0.31f, 0.29f), 0.09f, 12f);
            var burn = L("burn", new Color(0.05f, 0.045f, 0.04f), new Color(0.16f, 0.14f, 0.12f), 0.05f, 5f);
            data.terrainLayers = new[] { grass, dry, dirt, gravel, rock, burn };

            // burn scars: village centre + a few strikes near the road
            var scars = new List<(Vector3 c, float r)>
            {
                (new Vector3(40, 0, 0), 34f), (new Vector3(70, 0, 40), 12f),
                (new Vector3(10, 0, -30), 10f), (new Vector3(-90, 0, -60), 9f),
                (new Vector3(150, 0, 60), 11f),
            };

            int aw = data.alphamapResolution;
            var maps = new float[aw, aw, 6];
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = data.size;

            for (int y = 0; y < aw; y++)
            for (int x = 0; x < aw; x++)
            {
                float u = x / (float)(aw - 1);
                float vv = y / (float)(aw - 1);
                float wx = tPos.x + u * tSize.x;
                float wz = tPos.z + vv * tSize.z;

                float steep = data.GetSteepness(u, vv);            // degrees
                float slope01 = Mathf.Clamp01((steep - 20f) / 26f);
                float hz01 = data.GetInterpolatedHeight(u, vv) / tSize.y;

                float macro = Fbm(wx * 0.0045f + 2f, wz * 0.0045f + 6f, 3);
                float meso = Mathf.PerlinNoise(wx * 0.02f + 4f, wz * 0.02f + 1f);

                // grass-dominant meadow with dry patches, not the other way round
                float wGrass = (1.15f + macro * 1.3f) * (1f - slope01);
                float wDry = Mathf.Clamp01(0.30f + macro * 1.9f) * (1f - slope01) * 0.8f;
                float wDirt = Mathf.Clamp01((meso - 0.66f) * 4f) * (1f - slope01) * 0.6f;
                // thin shoulder only — the crisp road surface is the ribbon mesh
                float wRoad = RoadMask(new Vector3(wx, 0, wz), waypoints, 6.5f, 2.5f) * 4f;
                float wRock = slope01 * 3.5f + Mathf.Clamp01(hz01 - 0.62f) * 2f;

                float wBurn = 0f;
                foreach (var (c, r) in scars)
                {
                    float dd = new Vector2(wx - c.x, wz - c.z).magnitude;
                    wBurn += Mathf.Clamp01(1f - dd / r) * (0.6f + meso * 0.8f);
                }
                wBurn *= 4f;

                float tot = wGrass + wDry + wDirt + wRoad + wRock + wBurn + 1e-4f;
                maps[y, x, 0] = wGrass / tot;
                maps[y, x, 1] = wDry / tot;
                maps[y, x, 2] = wDirt / tot;
                maps[y, x, 3] = wRoad / tot;
                maps[y, x, 4] = wRock / tot;
                maps[y, x, 5] = wBurn / tot;
            }
            data.SetAlphamaps(0, 0, maps);

            // --- detail grass -------------------------------------------
            var dp = new DetailPrototype
            {
                prototypeTexture = MakeGrassBladeTex(),
                renderMode = DetailRenderMode.GrassBillboard,
                healthyColor = new Color(0.42f, 0.5f, 0.24f),
                dryColor = new Color(0.55f, 0.5f, 0.3f),
                minWidth = 0.7f, maxWidth = 1.6f, minHeight = 0.5f, maxHeight = 1.3f,
                noiseSpread = 0.3f, useInstancing = false,
            };
            data.detailPrototypes = new[] { dp };
            data.SetDetailResolution(2048, 16);  // doubled with the terrain footprint
            int dw = data.detailWidth;
            var dm = new int[dw, dw];
            for (int y = 0; y < dw; y++)
            for (int x = 0; x < dw; x++)
            {
                float u = x / (float)(dw - 1);
                float vv = y / (float)(dw - 1);
                float wx = tPos.x + u * tSize.x;
                float wz = tPos.z + vv * tSize.z;
                if (RoadMask(new Vector3(wx, 0, wz), waypoints, 7f, 4f) > 0.1f) continue;
                if (data.GetSteepness(u, vv) > 26f) continue;
                float macro = Fbm(wx * 0.0045f + 2f, wz * 0.0045f + 6f, 3);
                float lush = Mathf.Clamp01(0.62f - macro * 1.7f);
                float fine = Mathf.PerlinNoise(wx * 0.09f, wz * 0.09f);
                dm[y, x] = Mathf.RoundToInt(Mathf.Clamp01(lush * 1.4f) * fine * 9f);
            }
            data.SetDetailLayer(0, 0, 0, dm);
        }

        /// <summary>1 on the road centre, fading to 0 at (halfWidth+feather).</summary>
        /// <summary>Min distance from a world XZ point to a polyline given as raw
        /// points (used by BuildTerrain, before the waypoint Transforms exist —
        /// see RoadMask for the Transform-based equivalent used everywhere else).</summary>
        static float DistanceToPolylineXZ(Vector3 world, Vector3[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                a.y = b.y = world.y = 0f;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(world, a + ab * t));
            }
            return best;
        }

        static float RoadMask(Vector3 world, List<Transform> wps, float halfWidth, float feather)
        {
            float best = float.MaxValue;
            for (int i = 0; i < wps.Count - 1; i++)
            {
                Vector3 a = wps[i].position, b = wps[i + 1].position;
                a.y = b.y = world.y = 0f;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(world - a, ab) / Mathf.Max(0.01f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(world, a + ab * t));
            }
            return Mathf.Clamp01(1f - Mathf.InverseLerp(halfWidth, halfWidth + feather, best));
        }

        // ---- road ribbon mesh --------------------------------------------
        static List<Vector3> ResamplePath(List<Transform> wps, float step)
        {
            var raw = new List<Vector3>();
            foreach (var w in wps) raw.Add(w.position);
            var outp = new List<Vector3>();
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector3 p0 = raw[Mathf.Max(0, i - 1)];
                Vector3 p1 = raw[i];
                Vector3 p2 = raw[i + 1];
                Vector3 p3 = raw[Mathf.Min(raw.Count - 1, i + 2)];
                float segLen = Vector3.Distance(p1, p2);
                int n = Mathf.Max(2, Mathf.CeilToInt(segLen / step));
                for (int s = 0; s < n; s++)
                {
                    float t = s / (float)n;
                    outp.Add(CatmullRom(p0, p1, p2, p3, t));
                }
            }
            outp.Add(raw[^1]);
            return outp;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static void BuildRoadMesh(Terrain terrain, List<Transform> waypoints)
        {
            var pts = ResamplePath(waypoints, 7f);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            const float halfW = 5.4f;
            float run = 0f;

            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 fwd = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 l = pts[i] - side * halfW;
                Vector3 r = pts[i] + side * halfW;
                l.y = SampleHeight(terrain, l) + 0.07f;
                r.y = SampleHeight(terrain, r) + 0.07f;
                verts.Add(l); verts.Add(r);
                uvs.Add(new Vector2(0f, run * 0.09f));
                uvs.Add(new Vector2(1f, run * 0.09f));
                if (i > 0)
                {
                    int b = (i - 1) * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
                if (i < pts.Count - 1) run += Vector3.Distance(pts[i], pts[i + 1]);
            }

            var mesh = new Mesh { name = "RoadMesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, SettingsDir + "/RoadMesh.asset");

            var go = new GameObject("Road");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var roadMat = new Material(Shader.Find("Standard")) { name = "RoadRibbon" };
            roadMat.mainTexture = MakeGroundTex("roadsurf", new Color(0.22f, 0.20f, 0.17f),
                                                new Color(0.36f, 0.33f, 0.29f), 0.10f);
            roadMat.mainTextureScale = new Vector2(2f, 1f);   // v already runs in metres*0.09
            roadMat.SetFloat("_Glossiness", 0.05f);
            AssetDatabase.CreateAsset(roadMat, SettingsDir + "/RoadRibbon.mat");
            mr.sharedMaterial = roadMat;
            SetLayerRecursive(go, GameLayers.Environment);
        }

        // ---- vegetation & props ---------------------------------------
        enum Foliage { Broadleaf, Conifer, Bush, Dead }

        static GameObject BuildFoliagePrefab(Foliage kind)
        {
            string p = PrefabDir + "/Foliage_" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (existing != null) return existing;

            // Prefer the CC0/CC-BY tree models; only the bush stays procedural.
            string extPath = kind switch
            {
                Foliage.Broadleaf => ExtDir + "tree_broadleaf.fbx",
                Foliage.Conifer   => ExtDir + "tree_conifer.fbx",
                Foliage.Dead      => ExtDir + "tree_dead.fbx",
                _                 => null,
            };
            float extH = kind == Foliage.Conifer ? 9f : kind == Foliage.Dead ? 8f : 8f;
            if (extPath != null)
            {
                var ext = LoadExternalModel(extPath, extH, FitAxis.Y);
                if (ext != null)
                {
                    ext.name = kind.ToString();
                    var pf = PrefabUtility.SaveAsPrefabAsset(ext, p);
                    Object.DestroyImmediate(ext);
                    return pf;
                }
            }

            var go = new GameObject(kind.ToString());
            void Trunk(float rad, float hgt, string mat)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                c.name = "Trunk";
                c.transform.SetParent(go.transform, false);
                c.transform.localScale = new Vector3(rad, hgt * 0.5f, rad);
                c.transform.localPosition = new Vector3(0, hgt * 0.5f, 0);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(
                    mat == "bark" ? new Color(0.24f, 0.18f, 0.12f) : new Color(0.32f, 0.27f, 0.2f), mat);
            }
            void Blob(string n, float y, float s, Color col, float squash = 0.85f)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = n;
                b.transform.SetParent(go.transform, false);
                b.transform.localScale = new Vector3(s, s * squash, s);
                b.transform.localPosition = new Vector3(0, y, 0);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(col, n + "_" + kind);
            }

            switch (kind)
            {
                case Foliage.Broadleaf:
                    Trunk(0.42f, 5.2f, "bark");
                    Blob("Canopy0", 5.8f, 4.9f, new Color(0.19f, 0.30f, 0.13f));
                    Blob("Canopy1", 7.2f, 3.4f, new Color(0.24f, 0.34f, 0.15f));
                    break;
                case Foliage.Conifer:
                    Trunk(0.34f, 3.0f, "bark");
                    Blob("Tier0", 4.0f, 4.3f, new Color(0.14f, 0.24f, 0.13f), 1.5f);
                    Blob("Tier1", 6.6f, 3.2f, new Color(0.16f, 0.27f, 0.14f), 1.6f);
                    Blob("Tier2", 8.8f, 2.0f, new Color(0.18f, 0.29f, 0.15f), 1.7f);
                    break;
                case Foliage.Bush:
                    Blob("B0", 0.9f, 2.3f, new Color(0.22f, 0.31f, 0.15f));
                    Blob("B1", 1.5f, 1.7f, new Color(0.25f, 0.34f, 0.17f));
                    Blob("B2", 1.1f, 1.6f, new Color(0.20f, 0.29f, 0.14f));
                    break;
                case Foliage.Dead:
                    Trunk(0.36f, 4.6f, "deadwood");
                    var fork = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fork.name = "Branch";
                    fork.transform.SetParent(go.transform, false);
                    fork.transform.localScale = new Vector3(0.18f, 1.4f, 0.18f);
                    fork.transform.localPosition = new Vector3(0.5f, 4.2f, 0);
                    fork.transform.localRotation = Quaternion.Euler(0, 0, 48f);
                    Object.DestroyImmediate(fork.GetComponent<Collider>());
                    fork.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(new Color(0.3f, 0.26f, 0.2f), "deadwood");
                    break;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildRockPrefab()
        {
            const string p = PrefabDir + "/Rock.prefab";
            var ex = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (ex != null) return ex;
            var go = new GameObject("Rock");
            var rng = new System.Random(99);
            for (int i = 0; i < 3; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Chunk" + i;
                c.transform.SetParent(go.transform, false);
                float s = 1.4f + (float)rng.NextDouble() * 1.6f;
                c.transform.localScale = new Vector3(s, s * 0.7f, s * (0.8f + (float)rng.NextDouble() * 0.5f));
                c.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.4f,
                    s * 0.3f, ((float)rng.NextDouble() - 0.5f) * 1.4f);
                c.transform.localRotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 40f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
                Object.DestroyImmediate(c.GetComponent<Collider>());
                c.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(
                    new Color(0.32f, 0.31f, 0.30f) * (0.8f + (float)rng.NextDouble() * 0.3f), "rockmat");
            }
            go.AddComponent<BoxCollider>().size = new Vector3(2.4f, 1.4f, 2.4f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void ScatterVegetation(Terrain terrain, List<Transform> waypoints)
        {
            var broad = BuildFoliagePrefab(Foliage.Broadleaf);
            var conif = BuildFoliagePrefab(Foliage.Conifer);
            var bush = BuildFoliagePrefab(Foliage.Bush);
            var dead = BuildFoliagePrefab(Foliage.Dead);
            var rock = BuildRockPrefab();

            var trees = new GameObject("Trees").transform;
            var scatter = new GameObject("Scatter").transform;
            var rng = new System.Random(4242);
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;

            // Targets scaled ~2.4x alongside the terrain's 4x area increase (not
            // the full 4x — the old map was already fairly dense near the road;
            // scaling density with distance-from-road already thins it out
            // further away, so a flat 4x would mostly pile more trees near the
            // village without doing much for the empty far terrain).
            int treeN = 0, rockN = 0;
            for (int i = 0; i < 30000 && (treeN < 2400 || rockN < 460); i++)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                Vector3 world = new Vector3(tPos.x + nx * tSize.x, 0, tPos.z + nz * tSize.z);

                if (RoadMask(world, waypoints, 14f, 9f) > 0.05f) continue;
                if (Vector3.Distance(world, new Vector3(40, 0, 0)) < 62f) continue;
                float steep = terrain.terrainData.GetSteepness(nx, nz);

                float woods = Fbm(world.x * 0.010f + 5f, world.z * 0.010f + 2f, 3);
                world.y = SampleHeight(terrain, world);

                if (steep > 24f && rockN < 620)
                {
                    var rk = (GameObject)PrefabUtility.InstantiatePrefab(rock);
                    rk.transform.SetParent(scatter);
                    rk.transform.position = world;
                    float rs = 0.6f + (float)rng.NextDouble() * 1.3f;
                    rk.transform.localScale = Vector3.one * rs;
                    rk.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SetLayerRecursive(rk, GameLayers.Environment);
                    rockN++;
                    continue;
                }

                if (woods < 0.06f && treeN >= 960) continue;    // keep some open fields
                GameObject src;
                double roll = rng.NextDouble();
                if (woods > 0.16f) src = roll < 0.35 ? conif : broad;
                else if (roll < 0.5) src = bush;
                else if (roll < 0.62) src = dead;
                else src = broad;

                var t = (GameObject)PrefabUtility.InstantiatePrefab(src);
                t.transform.SetParent(trees);
                t.transform.position = world;
                float s = 0.8f + (float)rng.NextDouble() * 1.0f;
                t.transform.localScale = new Vector3(s, s * (0.85f + (float)rng.NextDouble() * 0.4f), s);
                t.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                SetLayerRecursive(t, GameLayers.Environment);
                if (src != bush)
                {
                    var rr2 = t.GetComponentsInChildren<Renderer>();
                    Bounds tb = rr2[0].bounds;
                    for (int k = 1; k < rr2.Length; k++) tb.Encapsulate(rr2[k].bounds);
                    var cc = t.AddComponent<CapsuleCollider>();
                    cc.radius = 0.5f;
                    cc.height = Mathf.Max(2f, tb.size.y / Mathf.Max(0.01f, t.transform.lossyScale.y));
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
                }
                treeN++;
            }

            // hedgerows along a couple of field boundaries
            var hedgeLines = new (Vector3 a, Vector3 b)[]
            {
                (new Vector3(-160, 0, 120), new Vector3(120, 0, 210)),
                (new Vector3(60, 0, -140), new Vector3(300, 0, -40)),
            };
            foreach (var (a, b) in hedgeLines)
            {
                int n = Mathf.RoundToInt(Vector3.Distance(a, b) / 4.5f);
                for (int i = 0; i <= n; i++)
                {
                    Vector3 w = Vector3.Lerp(a, b, i / (float)n)
                                + new Vector3((float)rng.NextDouble() * 3f, 0, (float)rng.NextDouble() * 3f);
                    if (RoadMask(w, waypoints, 10f, 6f) > 0.1f) continue;
                    w.y = SampleHeight(terrain, w);
                    var hb = (GameObject)PrefabUtility.InstantiatePrefab(bush);
                    hb.transform.SetParent(trees);
                    hb.transform.position = w;
                    float s = 1.1f + (float)rng.NextDouble() * 0.7f;
                    hb.transform.localScale = new Vector3(s, s * 1.3f, s);
                    hb.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                    SetLayerRecursive(hb, GameLayers.Environment);
                }
            }

            // NOTE: deliberately NOT using StaticBatchingUtility.Combine here — it bakes
            // a new, disk-less combined Mesh per batch, which gets serialized inline into
            // the scene file (this is what blew Mission01.unity up to >1 GB). Materials
            // are GPU-instanced instead (see MakeStandard/MakeUnlit), which batches draw
            // calls at runtime without embedding geometry in the scene.
        }

        static float SampleHeight(Terrain t, Vector3 world)
            => t.SampleHeight(world) + t.transform.position.y;

        static void BuildAtmosphereDust()
        {
            var go = new GameObject("AtmosphereDust");
            go.transform.position = new Vector3(60f, 40f, 20f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 26f;
            main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new Color(0.85f, 0.83f, 0.78f, 0.10f);
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.004f;
            var em = ps.emission; em.rateOverTime = 34f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(620f, 70f, 520f);
            var noise = ps.noise;
            noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f),
                              new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = MakeFx(Color.white, "dust");
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void DestroyIfPresent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }

        // ---- battlefield dressing: poles+wires, wrecks, debris, dust --------
        static void ScenePropsPass(Terrain terrain, List<Transform> waypoints)
        {
            var parent = new GameObject("BattlefieldProps").transform;
            var rng = new System.Random(555);
            var wood = MakeUnlit(new Color(0.20f, 0.16f, 0.11f), "pole_wood");
            var charred = MakeUnlit(new Color(0.05f, 0.045f, 0.04f), "wreck_char");
            var rubble = MakeUnlit(new Color(0.28f, 0.26f, 0.23f), "rubble");
            var pts = ResamplePath(waypoints, 42f);

            // --- utility poles along the road with a sagging wire ---------
            var poleTops = new List<Vector3>();
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector3 fwd = (pts[i + 1] - pts[i - 1]).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, fwd);
                Vector3 baseP = pts[i] + side * 9f;
                baseP.y = SampleHeight(terrain, baseP);

                var pole = new GameObject("Pole").transform;
                pole.SetParent(parent);
                pole.position = baseP;
                pole.rotation = Quaternion.LookRotation(fwd, Vector3.up)
                                * Quaternion.Euler((float)rng.NextDouble() * 4f - 2f, 0, (float)rng.NextDouble() * 4f - 2f);
                var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shaft.transform.SetParent(pole, false);
                shaft.transform.localScale = new Vector3(0.25f, 8.5f, 0.25f);
                shaft.transform.localPosition = new Vector3(0, 4.25f, 0);
                shaft.GetComponent<MeshRenderer>().sharedMaterial = wood;
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arm.transform.SetParent(pole, false);
                arm.transform.localScale = new Vector3(2.4f, 0.18f, 0.18f);
                arm.transform.localPosition = new Vector3(0, 8f, 0);
                arm.GetComponent<MeshRenderer>().sharedMaterial = wood;
                SetLayerRecursive(pole.gameObject, GameLayers.Environment);
                poleTops.Add(baseP + Vector3.up * 8f);
            }
            if (poleTops.Count > 1)
            {
                var wireGo = new GameObject("Wires");
                wireGo.transform.SetParent(parent);
                var lr = wireGo.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.05f;
                lr.material = MakeUnlit(new Color(0.05f, 0.05f, 0.05f), "wire");
                lr.positionCount = poleTops.Count;
                for (int i = 0; i < poleTops.Count; i++)
                    lr.SetPosition(i, poleTops[i] + Vector3.down * (i % 2 == 0 ? 0f : 0.6f)); // slight sag
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // --- knocked-out vehicle hulks beside the road --------------
            string[] hulkSrc = { "Tank.prefab", "IFV.prefab", "Truck.prefab" };
            for (int i = 0; i < 4; i++)
            {
                float t = 0.15f + (float)rng.NextDouble() * 0.7f;
                int seg = Mathf.Clamp(Mathf.FloorToInt(t * (waypoints.Count - 1)), 0, waypoints.Count - 2);
                Vector3 a = waypoints[seg].position, b = waypoints[seg + 1].position;
                Vector3 dir = (b - a).normalized;
                Vector3 sidev = Vector3.Cross(Vector3.up, dir);
                Vector3 p = Vector3.Lerp(a, b, (float)rng.NextDouble())
                            + sidev * (rng.NextDouble() < 0.5 ? -1f : 1f) * (7f + (float)rng.NextDouble() * 5f);
                p.y = SampleHeight(terrain, p);

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + hulkSrc[rng.Next(hulkSrc.Length)]);
                if (src == null) continue;
                var hulk = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(hulk, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // strip gameplay so a hulk is scenery, not a live target
                // (dependency order: dependents before their RequireComponent)
                DestroyIfPresent<VehicleConvoyAI>(hulk);
                DestroyIfPresent<VehicleTurret>(hulk);
                DestroyIfPresent<Wreck>(hulk);
                DestroyIfPresent<Vehicle>(hulk);
                DestroyIfPresent<HealthComponent>(hulk);
                DestroyIfPresent<Rigidbody>(hulk);
                hulk.name = "Hulk";
                hulk.transform.SetParent(parent);
                hulk.transform.position = p;
                hulk.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 12f - 6f,
                    (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 18f - 9f);
                foreach (var r in hulk.GetComponentsInChildren<MeshRenderer>())
                {
                    var ms = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < ms.Length; k++) ms[k] = charred;
                    r.sharedMaterials = ms;
                }
                SetLayerRecursive(hulk, GameLayers.Environment);

                // scorch + smoke plume on the hulk
                var fs = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/FireSmoke.prefab");
                if (fs != null && rng.NextDouble() < 0.6)
                {
                    var plume = (GameObject)PrefabUtility.InstantiatePrefab(fs);
                    plume.transform.SetParent(hulk.transform);
                    plume.transform.localPosition = Vector3.up * 2f;
                }
            }

            // --- rubble clusters near the village ----------------------
            for (int i = 0; i < 30; i++)
            {
                Vector3 c = new(40f + (float)rng.NextDouble() * 200f - 100f, 0,
                                (float)rng.NextDouble() * 180f - 90f);
                c.y = SampleHeight(terrain, c);
                var pile = new GameObject("Rubble").transform;
                pile.SetParent(parent);
                pile.position = c;
                int chunks = 3 + rng.Next(4);
                for (int k = 0; k < chunks; k++)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.transform.SetParent(pile, false);
                    float s = 0.4f + (float)rng.NextDouble() * 0.9f;
                    b.transform.localScale = new Vector3(s, s * 0.6f, s * (0.7f + (float)rng.NextDouble() * 0.6f));
                    b.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 2f, s * 0.3f,
                        ((float)rng.NextDouble() - 0.5f) * 2f);
                    b.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 40f,
                        (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 40f);
                    Object.DestroyImmediate(b.GetComponent<Collider>());
                    b.GetComponent<MeshRenderer>().sharedMaterial = rng.NextDouble() < 0.4 ? charred : rubble;
                }
                SetLayerRecursive(pile.gameObject, GameLayers.Environment);
            }

            // --- worn tyre ruts along the road -----------------------
            var rut = MakeUnlit(new Color(0.13f, 0.11f, 0.09f), "rut");
            var ruts = new GameObject("Ruts").transform;
            ruts.SetParent(parent);
            var rpts = ResamplePath(waypoints, 9f);
            for (int i = 0; i < rpts.Count - 1; i++)
            {
                Vector3 a = rpts[i], b = rpts[i + 1];
                Vector3 dir = (b - a); float len = dir.magnitude; dir /= Mathf.Max(0.01f, len);
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                foreach (float off in new[] { -1.7f, 1.7f })
                {
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(seg.GetComponent<Collider>());
                    seg.transform.SetParent(ruts);
                    Vector3 mid = (a + b) * 0.5f + side * off;
                    mid.y = SampleHeight(terrain, mid) + 0.09f;
                    seg.transform.position = mid;
                    seg.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                    seg.transform.localScale = new Vector3(0.55f, 0.04f, len * 1.05f);
                    seg.GetComponent<MeshRenderer>().sharedMaterial = rut;
                    seg.GetComponent<MeshRenderer>().shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            SetLayerRecursive(ruts.gameObject, GameLayers.Environment);
        }

        static void ScatterRuins(Terrain terrain)
        {
            var parent = new GameObject("Village").transform;
            var rng = new System.Random(20260910);
            var scorch = MakeUnlit(new Color(0.08f, 0.07f, 0.06f), "scorch");
            var sand = MakeUnlit(new Color(0.42f, 0.37f, 0.24f), "sandbag");
            var carDark = MakeUnlit(new Color(0.06f, 0.06f, 0.06f), "burntcar");

            // CC0/CC-BY building prefabs, sized once
            GameObject BuildingPrefab(string file, float widthM)
            {
                string pp = PrefabDir + "/" + Path.GetFileNameWithoutExtension(file) + ".prefab";
                var ex = AssetDatabase.LoadAssetAtPath<GameObject>(pp);
                if (ex != null) return ex;
                var m = LoadExternalModel(ExtDir + file, widthM, FitAxis.XZ);
                if (m == null) return null;
                var pf = PrefabUtility.SaveAsPrefabAsset(m, pp);
                Object.DestroyImmediate(m);
                return pf;
            }
            var hHouse = BuildingPrefab("bld_house.fbx", 12f);
            var hBlock = BuildingPrefab("bld_block.fbx", 22f);
            // mostly small houses, the odd apartment block
            var houses = new[] { hHouse, hHouse, hHouse, hBlock };
            bool haveModels = System.Array.Exists(houses, h => h != null);

            Vector3 centre = new(40f, 0f, 0f);
            Vector3 along = new Vector3(0.30f, 0f, 1f).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);

            // --- buildings in two rows along the road -----------------
            for (int i = 0; i < 22; i++)
            {
                float t = (i / 2) * 34f - 170f + (float)rng.NextDouble() * 10f;
                float lane = (i % 2 == 0 ? -1f : 1f) * (24f + (float)rng.NextDouble() * 16f);
                Vector3 c = centre + along * t + side * lane
                            + new Vector3((float)rng.NextDouble() * 5f, 0, (float)rng.NextDouble() * 5f);
                c.y = SampleHeight(terrain, c);

                GameObject piece;
                if (haveModels)
                {
                    var src = houses[rng.Next(houses.Length)] ?? hHouse;
                    piece = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    piece.transform.localScale *= 0.8f + (float)rng.NextDouble() * 0.45f;
                }
                else
                {
                    piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.transform.localScale = new Vector3(8, 6, 8);
                }
                piece.transform.SetParent(parent);
                piece.transform.position = c;
                piece.transform.rotation = Quaternion.LookRotation(lane < 0 ? side : -side, Vector3.up)
                    * Quaternion.Euler(0, (float)rng.NextDouble() * 30f - 15f, 0);

                // war damage: char ~35% of them, tilt a few
                if (rng.NextDouble() < 0.35)
                    foreach (var r in piece.GetComponentsInChildren<MeshRenderer>())
                        r.sharedMaterial = scorch;
                if (rng.NextDouble() < 0.25)
                    piece.transform.rotation *= Quaternion.Euler(rng.Next(-5, 6), 0, rng.Next(-8, 9));

                SetLayerRecursive(piece, GameLayers.Environment);
                if (piece.GetComponentInChildren<Collider>() == null)
                    foreach (var mf in piece.GetComponentsInChildren<MeshFilter>())
                        mf.gameObject.AddComponent<MeshCollider>();
            }

            // --- craters (dark ring of low rubble) --------------------
            for (int i = 0; i < 7; i++)
            {
                Vector3 c = centre + along * ((float)rng.NextDouble() * 260f - 130f)
                                   + side * ((float)rng.NextDouble() * 60f - 30f);
                c.y = SampleHeight(terrain, c);
                var crater = new GameObject("Crater").transform;
                crater.SetParent(parent);
                crater.position = c;
                int chunks = 8;
                for (int k = 0; k < chunks; k++)
                {
                    float a = k / (float)chunks * Mathf.PI * 2f;
                    var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    rim.transform.SetParent(crater);
                    float rad = 2.6f + (float)rng.NextDouble() * 1.2f;
                    rim.transform.localPosition = new Vector3(Mathf.Cos(a) * rad, 0.2f, Mathf.Sin(a) * rad);
                    rim.transform.localScale = new Vector3(1.4f, 0.5f, 1.4f);
                    rim.transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg, (float)rng.NextDouble() * 30f);
                    Object.DestroyImmediate(rim.GetComponent<Collider>());
                    rim.GetComponent<MeshRenderer>().sharedMaterial = scorch;
                }
                SetLayerRecursive(crater.gameObject, GameLayers.Environment);
            }

            // --- sandbag emplacements near the road ------------------
            for (int e = 0; e < 4; e++)
            {
                Vector3 c = centre + along * ((float)rng.NextDouble() * 200f - 100f)
                                   + side * (e % 2 == 0 ? 9f : -9f);
                c.y = SampleHeight(terrain, c);
                var wall = new GameObject("Sandbags").transform;
                wall.SetParent(parent);
                wall.position = c;
                wall.rotation = Quaternion.LookRotation(along, Vector3.up);
                for (int r = 0; r < 3; r++)
                for (int b = 0; b < 6; b++)
                {
                    var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bag.transform.SetParent(wall);
                    bag.transform.localPosition = new Vector3((b - 2.5f) * 0.8f + (r % 2) * 0.4f, 0.35f + r * 0.55f, 0);
                    bag.transform.localScale = new Vector3(0.8f, 0.5f, 1.1f);
                    Object.DestroyImmediate(bag.GetComponent<Collider>());
                    bag.GetComponent<MeshRenderer>().sharedMaterial = sand;
                }
                wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(5.5f, 2f, 1.2f);
                wall.GetComponent<BoxCollider>().center = new Vector3(0, 1f, 0);
                SetLayerRecursive(wall.gameObject, GameLayers.Environment);
            }

            // --- a couple of burnt-out civilian cars ----------------
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = centre + along * ((float)rng.NextDouble() * 220f - 110f)
                                   + side * ((float)rng.NextDouble() * 20f - 10f);
                c.y = SampleHeight(terrain, c);
                var car = new GameObject("BurntCar").transform;
                car.SetParent(parent);
                car.position = c;
                car.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, rng.NextDouble() < 0.3 ? 18f : 0f);
                var bodyc = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bodyc.transform.SetParent(car);
                bodyc.transform.localScale = new Vector3(1.8f, 0.9f, 4.2f);
                bodyc.transform.localPosition = new Vector3(0, 0.7f, 0);
                bodyc.GetComponent<MeshRenderer>().sharedMaterial = carDark;
                Object.DestroyImmediate(bodyc.GetComponent<Collider>());
                var cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cabin.transform.SetParent(car);
                cabin.transform.localScale = new Vector3(1.6f, 0.8f, 2f);
                cabin.transform.localPosition = new Vector3(0, 1.4f, -0.2f);
                cabin.GetComponent<MeshRenderer>().sharedMaterial = carDark;
                Object.DestroyImmediate(cabin.GetComponent<Collider>());
                for (int wI = 0; wI < 4; wI++)
                {
                    var wh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    wh.transform.SetParent(car);
                    wh.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
                    wh.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                    wh.transform.localPosition = new Vector3(wI < 2 ? -0.9f : 0.9f, 0.35f, wI % 2 == 0 ? 1.4f : -1.4f);
                    wh.GetComponent<MeshRenderer>().sharedMaterial = carDark;
                    Object.DestroyImmediate(wh.GetComponent<Collider>());
                }
                car.gameObject.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 4.4f);
                car.GetComponent<BoxCollider>().center = new Vector3(0, 1f, 0);
                SetLayerRecursive(car.gameObject, GameLayers.Environment);
            }
        }

        // ----------------------------------------------------------------- //
        // small helpers
        // ----------------------------------------------------------------- //
        static Material MakeGroundMaterial()
        {
            const string p = SettingsDir + "/Ground.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing != null) return existing;
            var m = new Material(Shader.Find("Standard")) { name = "Ground" };
            m.color = new Color(0.34f, 0.33f, 0.24f);
            m.SetFloat("_Glossiness", 0.05f);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static readonly Dictionary<string, Material> _matCache = new();

        /// <summary>Matte LIT material — props catch the sun and read as solid form.</summary>
        static Material MakeUnlit(Color c, string name)
        {
            if (_matCache.TryGetValue(name, out var cached) && cached != null) return cached;
            string p = SettingsDir + "/M_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing == null)
            {
                existing = new Material(Shader.Find("Standard")) { name = name, color = c };
                existing.SetFloat("_Glossiness", 0.06f);
                existing.SetFloat("_Metallic", 0f);
                AssetDatabase.CreateAsset(existing, p);
            }
            existing.enableInstancing = true; // GPU-batch draw calls instead of baking combined meshes into the scene
            _matCache[name] = existing;
            return existing;
        }

        /// <summary>Genuinely unlit FX material (tracers, particle quads).</summary>
        static Material MakeFx(Color c, string name)
        {
            if (_matCache.TryGetValue("fx_" + name, out var cached) && cached != null) return cached;
            string p = SettingsDir + "/M_fx_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                existing = new Material(shader) { name = "fx_" + name, color = c };
                AssetDatabase.CreateAsset(existing, p);
            }
            _matCache["fx_" + name] = existing;
            return existing;
        }

        static Material MakeStandard(string name, Color c, float smoothness, float metallic)
        {
            if (_matCache.TryGetValue(name, out var cached) && cached != null) return cached;
            string p = SettingsDir + "/M_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing == null)
            {
                existing = new Material(Shader.Find("Standard")) { name = name, color = c };
                existing.SetFloat("_Glossiness", smoothness);
                existing.SetFloat("_Metallic", metallic);
                AssetDatabase.CreateAsset(existing, p);
            }
            existing.enableInstancing = true; // GPU-batch draw calls instead of baking combined meshes into the scene
            _matCache[name] = existing;
            return existing;
        }

        static LineRenderer MakeTracerPrefab()
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.widthMultiplier = 0.06f;
            lr.material = MakeFx(new Color(1f, 0.8f, 0.3f), "tracer");
            lr.numCapVertices = 0;
            var prefab = SavePrefab(go, PrefabDir + "/Tracer.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<LineRenderer>();
        }

        static void AddParticles(Transform parent, string name, Color color, int burst,
            float speed, float size, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f; main.loop = false;
            main.startLifetime = life; main.startSpeed = speed;
            main.startSize = size; main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.4f, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.material = MakeFx(Color.white, name + "_ps");
        }

        const string AudioDir = "Assets/Audio";
        const string AudioExtDir = "Assets/Audio/External/";

        // Real CC0 recordings from Kenney (https://kenney.nl) — see CREDITS.md.
        // Fall back to the synthesised clip if a file ever goes missing so a
        // rebuild can't hard-fail on a stale/partial Assets/Audio/External.
        static AudioClip LoadExternalClip(string fileName) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(AudioExtDir + fileName);
        static AudioClip ExplosionClip() => LoadExternalClip("explosion_boom.ogg") ?? MakeBoomClip();
        static AudioClip HitPingClip() => LoadExternalClip("hit_ping.ogg") ?? MakePingClip();
        static AudioClip UiClickClip() => LoadExternalClip("ui_click.ogg");

        static AudioClip MakeBoomClip() => LoadOrWriteWav("boom", () =>
        {
            int sr = 44100, n = (int)(sr * 1.4f);
            var data = new float[n];
            var rnd = new System.Random(1);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Exp(-i / (float)sr * 4.5f);
                float white = (float)(rnd.NextDouble() * 2 - 1);
                lp = Mathf.Lerp(lp, white, 0.08f);          // low-passed rumble
                data[i] = Mathf.Clamp((lp * 1.6f + white * 0.3f) * env * 0.9f, -1f, 1f);
            }
            return (data, sr);
        });

        static AudioClip MakeMotorClip() => LoadOrWriteWav("motor", () =>
        {
            int sr = 44100, n = sr;                          // 1 s loop
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                data[i] = (Mathf.Sin(t * 2f * Mathf.PI * 180f) * 0.4f
                           + Mathf.Sin(t * 2f * Mathf.PI * 322f) * 0.22f
                           + Mathf.Sin(t * 2f * Mathf.PI * 61f) * 0.15f) * 0.5f;
            }
            return (data, sr);
        });

        static AudioClip MakePingClip() => LoadOrWriteWav("ping", () =>
        {
            int sr = 44100, n = (int)(sr * 0.14f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 32f);
                data[i] = Mathf.Sin(t * 2f * Mathf.PI * 1400f) * env * 0.7f;
            }
            return (data, sr);
        });

        static AudioClip LoadOrWriteWav(string name, System.Func<(float[] data, int sr)> gen)
        {
            Directory.CreateDirectory(AudioDir);
            string assetPath = AudioDir + "/" + name + ".wav";
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) == null)
            {
                var (data, sr) = gen();
                File.WriteAllBytes(assetPath, EncodeWav16(data, sr));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        static byte[] EncodeWav16(float[] samples, int sampleRate)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);            // PCM
            w.Write((short)1);            // mono
            w.Write(sampleRate);
            w.Write(sampleRate * 2);      // byte rate
            w.Write((short)2);            // block align
            w.Write((short)16);           // bits
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            foreach (var s in samples)
                w.Write((short)(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            return ms.ToArray();
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }
    }
}
