using System.Collections.Generic;
using System.IO;
using Ironfield.Combat;
using Ironfield.Core;
using Ironfield.Drone;
using Ironfield.Fx;
using Ironfield.Mission;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using UnityEditor;
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
        const string ArtTank = "Assets/Art/Vehicles/Tank.fbx";
        const string ArtIFV = "Assets/Art/Vehicles/IFV.fbx";
        const string ArtTruck = "Assets/Art/Vehicles/Truck.fbx";
        const string ArtRuins = "Assets/Art/Ruins/RuinsKit.fbx";

        const string PrefabDir = "Assets/Prefabs";
        const string SettingsDir = "Assets/Settings";
        const string ScenesDir = "Assets/Scenes";
        const string ScenePath = ScenesDir + "/Mission01.unity";

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

            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(ScenesDir);

            ConfigureModelImport(ArtDrone, 1f);
            ConfigureModelImport(ArtTank, 1f);
            ConfigureModelImport(ArtIFV, 1f);
            ConfigureModelImport(ArtTruck, 1f);
            ConfigureModelImport(ArtRuins, 1f);
            AssetDatabase.Refresh();

            var tuning = CreateTuning();
            BuildExplosionPrefab();
            BuildFireSmokePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildDronePrefab(tuning);
            BuildVehiclePrefab("Tank", ArtTank, VehicleClass.Tank, 900f, 60f, 3.6f, 7.6f, 2.7f);
            BuildVehiclePrefab("IFV", ArtIFV, VehicleClass.IFV, 420f, 25f, 3.2f, 6.2f, 2.9f);
            BuildVehiclePrefab("Truck", ArtTruck, VehicleClass.Truck, 160f, 0f, 2.7f, 8.2f, 3.3f);

            // Asset ops above can reimport the prefabs and invalidate in-memory
            // references, so reload everything fresh from disk before wiring the scene.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Ironfield] Build complete. Open " + ScenePath);
        }

        /// <summary>
        /// Graphics-batchmode smoke shot: open Mission01, frame the convoy from
        /// the launch ridge, render one PNG next to the project. Lets a headless
        /// run eyeball model orientation / scale without opening the editor.
        ///   Unity -batchmode -quit -executeMethod Ironfield.EditorTools.IronfieldSetup.Screenshot
        /// </summary>
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

            // drone shot: spawn one at the launch point and frame it chase-cam style
            var dronePf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone.prefab");
            if (dronePf != null && launch != null)
            {
                var d = (GameObject)PrefabUtility.InstantiatePrefab(dronePf);
                d.transform.position = launch.transform.position;
                d.transform.rotation = launch.transform.rotation;
                Vector3 back = -launch.transform.forward;
                Vector3 eye3 = d.transform.position + back * 6f + Vector3.up * 2.3f;
                Shot(cam, eye3, d.transform.position + d.transform.forward * 3f, "Ironfield_smoke_drone.png");
                Object.DestroyImmediate(d);
            }
            _ = scene;
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
            t.maxHealth = 30f;
            t.warheadDamage = 650f;
            t.warheadRadius = 5.5f;
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
            audio.clip = MakeBoomClip();

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

        static DroneController BuildDronePrefab(DroneTuning tuning)
        {
            var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Explosion.prefab")
                ?.GetComponent<Explosion>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDrone);
            GameObject root = model != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(model)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            // unpack so the imported children are plain objects we can reparent
            if (model != null)
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            root.name = "Drone";
            root.tag = GameTags.Drone;
            SetLayerRecursive(root, GameLayers.Drone);

            // The real blockout is ~0.35 m — a speck from the chase cam. Move the
            // whole imported visual under a scaled child so it reads; the root
            // collider stays a sane ~1.8 m box.
            var existingKids = new List<Transform>();
            foreach (Transform child in root.transform) existingKids.Add(child);
            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            foreach (var k in existingKids) k.SetParent(visualRoot.transform, true);

            // Scale the imported visual to an explicit motor-to-motor size,
            // measured from renderer bounds so it's independent of the FBX scale.
            const float targetSpan = 3.8f;   // metres, prop tip to prop tip
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
            Debug.Log($"[Ironfield] drone visual span={span:0.00}m  ->  scale x{mult:0.0}  (meshes={mfs.Length})");

            // a small nav strobe so the drone is trackable against the ground
            var strobe = new GameObject("NavLight");
            strobe.transform.SetParent(visualRoot.transform, false);
            strobe.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var sl = strobe.AddComponent<Light>();
            sl.type = LightType.Point; sl.color = new Color(1f, 0.3f, 0.2f);
            sl.range = 9f; sl.intensity = 3f;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.2f; rb.useGravity = false;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(3.4f, 1.3f, 3.4f);

            var health = root.AddComponent<HealthComponent>();
            health.maxHealth = tuning.maxHealth; health.armor = 0f;

            var ctrl = root.AddComponent<DroneController>();
            ctrl.tuning = tuning;

            var props = new List<Transform>();
            foreach (var tr in root.GetComponentsInChildren<Transform>())
                if (tr.name.StartsWith("Drone_Prop")) props.Add(tr);
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

            var prefab = SavePrefab(root, PrefabDir + "/Drone.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DroneController>();
        }

        static Vehicle BuildVehiclePrefab(string name, string fbx, VehicleClass cls,
            float hp, float armor, float width, float length, float height)
        {
            var fireSmoke = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/FireSmoke.prefab");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(name);
            root.tag = GameTags.Vehicle;
            SetLayerRecursive(root, GameLayers.Vehicle);

            GameObject intact = null, wreck = null;
            if (model != null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);
                inst.name = "Model";
                foreach (Transform child in inst.transform)
                {
                    if (child.name.EndsWith("_Intact")) intact = child.gameObject;
                    else if (child.name.EndsWith("_Wreck")) wreck = child.gameObject;
                }
            }
            if (intact == null)
            {
                intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
                intact.transform.SetParent(root.transform, false);
                intact.transform.localScale = new Vector3(width, height, length);
                intact.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            }
            if (wreck != null) wreck.SetActive(false);

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
            wk.destroyedSfx = MakeBoomClip();

            if (cls != VehicleClass.Truck)
            {
                var turret = root.AddComponent<VehicleTurret>();
                turret.enabled = false; // opt-in return fire
                turret.range = cls == VehicleClass.Tank ? 200f : 160f;
                turret.damagePerHit = cls == VehicleClass.Tank ? 8f : 5f;
                turret.muzzle = aim.transform;
                turret.tracerPrefab = MakeTracerPrefab();
            }

            var prefab = SavePrefab(root, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Vehicle>();
        }

        // ----------------------------------------------------------------- //
        // Scene
        // ----------------------------------------------------------------- //
        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- lighting / atmosphere (Built-in) ---------------------
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.83f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunGo.transform.rotation = Quaternion.Euler(46f, 32f, 0f);   // mid-afternoon, above the frame
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.63f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.47f, 0.47f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.16f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.77f, 0.83f);
            RenderSettings.fogDensity = 0.0018f;                        // reveal mid-distance, haze the far ridge
            QualitySettings.shadowDistance = 360f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadows = ShadowQuality.All;

            // --- terrain ---------------------------------------------
            var terrain = BuildTerrain();

            // --- road + convoy path --------------------------------
            var pathParent = new GameObject("ConvoyPath").transform;
            var waypoints = new List<Transform>();
            Vector3[] pts =
            {
                new(-300, 0, -190), new(-180, 0, -120), new(-70, 0, -60),
                new(40, 0, 0), new(150, 0, 60), new(300, 0, 170), new(430, 0, 300),
            };
            for (int i = 0; i < pts.Length; i++)
            {
                var wp = new GameObject($"WP_{i}").transform;
                wp.SetParent(pathParent);
                Vector3 p = pts[i];
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
            cam.fieldOfView = 62f;
            cam.farClipPlane = 1400f;
            cam.nearClipPlane = 0.08f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<DroneCameraRig>();
            camGo.transform.position = lp + new Vector3(0, 3, -8);

            // --- convoy -------------------------------------------
            // Load prefab refs *here*, after every AssetDatabase.CreateAsset in
            // this method (terrain, ground mat) has run, so they can't be
            // invalidated by a mid-build reimport.
            var tankPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Tank.prefab");
            var ifvPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/IFV.prefab");
            var truckPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Truck.prefab");

            var convoyParent = new GameObject("Convoy").transform;
            GameObject[] order = { tankPf, ifvPf, truckPf, tankPf, ifvPf, truckPf };
            Vehicle prevAhead = null;
            for (int i = 0; i < order.Length; i++)
            {
                Vector3 pos = Vector3.Lerp(waypoints[0].position, waypoints[1].position, 0.15f)
                              - (waypoints[1].position - waypoints[0].position).normalized * (i * 14f);
                pos.y = SampleHeight(terrain, pos);
                var vgo = (GameObject)PrefabUtility.InstantiatePrefab(order[i]);
                var vinst = vgo.GetComponent<Vehicle>();
                vinst.transform.SetParent(convoyParent);
                vinst.transform.position = pos;
                vinst.transform.rotation = Quaternion.LookRotation(
                    (waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
                var ai = vinst.GetComponent<VehicleConvoyAI>();
                ai.waypoints = waypoints.ToArray();
                ai.vehicleAhead = prevAhead;
                prevAhead = vinst;
            }

            // --- village ruins ----------------------------------
            ScatterRuins(terrain);

            // --- managers --------------------------------------
            var mgrGo = new GameObject("MissionManager");
            var mgr = mgrGo.AddComponent<MissionManager>();
            mgr.dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/Drone.prefab")
                .GetComponent<DroneController>();
            mgr.launchPoint = launch.transform;
            mgr.cameraRig = rig;
            mgr.droneStock = 5;

            var targeting = mgrGo.AddComponent<TargetingSystem>();
            targeting.viewCamera = cam;
            mgr.targeting = targeting;

            var hud = mgrGo.AddComponent<HudController>();
            hud.mission = mgr;
            hud.targeting = targeting;
            hud.cameraRig = rig;

            rig.Bind(null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var list = new List<EditorBuildSettingsScene>
            {
                new(ScenePath, true),
            };
            EditorBuildSettings.scenes = list.ToArray();
        }

        static readonly Vector3 TerrainOrigin = new(-512, 0, -512);
        static readonly Vector3 LaunchHillCentre = new(-200, 0, 100);

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
                heightmapResolution = 513,
                size = new Vector3(1024, 110, 1024),
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

                float e = 0.30f
                          + Fbm(nx * 2.2f + 11f, ny * 2.2f + 7f, 3) * 0.9f
                          + Fbm(nx * 7f, ny * 7f, 3) * 0.18f
                          + (nx - 0.5f) * 0.12f;                    // rise to the east

                // broad flattened corridor for the road (SW -> NE diagonal)
                float corridor = Mathf.Abs(ny - (0.32f + nx * 0.30f));
                e = Mathf.Lerp(0.29f, e, Mathf.Clamp01(corridor * 4.5f));

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
                Color c = Color.Lerp(a, b, Mathf.Clamp01(0.5f + n * 1.6f + (blotch - 0.5f) * 0.6f));
                c += new Color(1f, 1f, 1f, 0f) * (n * 0.18f + g * grain);
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
            data.alphamapResolution = 512;
            data.baseMapResolution = 1024;

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

            var grass = L("grass", new Color(0.24f, 0.32f, 0.13f), new Color(0.37f, 0.43f, 0.22f), 0.05f, 8f);
            var dry = L("dry", new Color(0.46f, 0.43f, 0.26f), new Color(0.60f, 0.55f, 0.36f), 0.05f, 10f);
            var dirt = L("dirt", new Color(0.30f, 0.24f, 0.17f), new Color(0.44f, 0.36f, 0.26f), 0.06f, 6f);
            var gravel = L("gravel", new Color(0.27f, 0.25f, 0.22f), new Color(0.44f, 0.42f, 0.39f), 0.10f, 3.5f);
            var rock = L("rock", new Color(0.20f, 0.19f, 0.18f), new Color(0.40f, 0.39f, 0.37f), 0.09f, 12f);
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

                float wGrass = Mathf.Clamp01(0.62f - macro * 1.7f) * (1f - slope01);
                float wDry = Mathf.Clamp01(0.5f + macro * 1.7f) * (1f - slope01);
                float wDirt = Mathf.Clamp01((meso - 0.62f) * 4f) * (1f - slope01) * 0.7f;
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
            data.SetDetailResolution(1024, 16);
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

            int treeN = 0, rockN = 0;
            for (int i = 0; i < 12000 && (treeN < 1000 || rockN < 190); i++)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                Vector3 world = new Vector3(tPos.x + nx * tSize.x, 0, tPos.z + nz * tSize.z);

                if (RoadMask(world, waypoints, 14f, 9f) > 0.05f) continue;
                if (Vector3.Distance(world, new Vector3(40, 0, 0)) < 62f) continue;
                float steep = terrain.terrainData.GetSteepness(nx, nz);

                float woods = Fbm(world.x * 0.010f + 5f, world.z * 0.010f + 2f, 3);
                world.y = SampleHeight(terrain, world);

                if (steep > 24f && rockN < 260)
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

                if (woods < 0.06f && treeN >= 400) continue;    // keep some open fields
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

            StaticBatchingUtility.Combine(trees.gameObject);
            StaticBatchingUtility.Combine(scatter.gameObject);
        }

        static float SampleHeight(Terrain t, Vector3 world)
            => t.SampleHeight(world) + t.transform.position.y;

        static void ScatterRuins(Terrain terrain)
        {
            var ruins = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRuins);
            var parent = new GameObject("Village").transform;
            var rng = new System.Random(20260910);
            string[] names = { "Ruin_WallLong", "Ruin_WallCorner", "Ruin_RubblePile", "Ruin_HouseShell" };
            var concrete = MakeUnlit(new Color(0.52f, 0.50f, 0.46f), "concrete");
            var concreteDmg = MakeUnlit(new Color(0.40f, 0.37f, 0.33f), "concrete_dmg");
            var scorch = MakeUnlit(new Color(0.08f, 0.07f, 0.06f), "scorch");
            var sand = MakeUnlit(new Color(0.42f, 0.37f, 0.24f), "sandbag");
            var carDark = MakeUnlit(new Color(0.06f, 0.06f, 0.06f), "burntcar");

            Vector3 centre = new(40f, 0f, 0f);
            Vector3 along = new Vector3(0.30f, 0f, 1f).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along);

            // --- buildings in two rows along the road -----------------
            for (int i = 0; i < 40; i++)
            {
                float t = (i / 2) * 18f - 162f + (float)rng.NextDouble() * 7f;
                float lane = (i % 2 == 0 ? -1f : 1f) * (15f + (float)rng.NextDouble() * 12f);
                Vector3 c = centre + along * t + side * lane
                            + new Vector3((float)rng.NextDouble() * 5f, 0, (float)rng.NextDouble() * 5f);
                c.y = SampleHeight(terrain, c);

                GameObject piece;
                if (ruins != null)
                {
                    piece = (GameObject)PrefabUtility.InstantiatePrefab(ruins);
                    string want = names[rng.Next(names.Length)];
                    foreach (Transform ch in piece.transform)
                        ch.gameObject.SetActive(ch.name == want);
                }
                else
                {
                    piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    piece.transform.localScale = new Vector3(6, 3, 0.4f);
                }
                piece.transform.SetParent(parent);
                piece.transform.position = c;
                piece.transform.rotation = Quaternion.LookRotation(lane < 0 ? side : -side, Vector3.up)
                    * Quaternion.Euler(0, (float)rng.NextDouble() * 26f - 13f, 0);
                piece.transform.localScale *= 1.2f + (float)rng.NextDouble() * 0.8f;

                var mat = rng.NextDouble() < 0.3 ? scorch : (rng.NextDouble() < 0.5 ? concreteDmg : concrete);
                foreach (var r in piece.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterial = mat;

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

            StaticBatchingUtility.Combine(parent.gameObject);
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

        static Material MakeUnlit(Color c, string name)
        {
            if (_matCache.TryGetValue(name, out var cached) && cached != null) return cached;
            string p = SettingsDir + "/M_" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                existing = new Material(shader) { name = name, color = c };
                AssetDatabase.CreateAsset(existing, p);
            }
            _matCache[name] = existing;
            return existing;
        }

        static LineRenderer MakeTracerPrefab()
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            lr.widthMultiplier = 0.06f;
            lr.material = MakeUnlit(new Color(1f, 0.8f, 0.3f), "Tracer");
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
            rend.material = MakeUnlit(Color.white, name + "_ps");
        }

        const string AudioDir = "Assets/Audio";

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
