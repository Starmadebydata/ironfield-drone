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
            visualRoot.transform.localScale = Vector3.one * 5f;

            // a small nav strobe so the drone is trackable against the ground
            var strobe = new GameObject("NavLight");
            strobe.transform.SetParent(visualRoot.transform, false);
            strobe.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var sl = strobe.AddComponent<Light>();
            sl.type = LightType.Point; sl.color = new Color(1f, 0.3f, 0.2f);
            sl.range = 6f; sl.intensity = 2.5f;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1.2f; rb.useGravity = false;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.3f, 0f);
            col.size = new Vector3(1.8f, 0.7f, 1.8f);

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
            sun.color = new Color(1f, 0.96f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunGo.transform.rotation = Quaternion.Euler(26f, 42f, 0f);   // low-ish afternoon
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.60f, 0.68f, 0.80f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.51f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.86f);
            RenderSettings.fogDensity = 0.0016f;                        // reveal mid-distance
            QualitySettings.shadowDistance = 320f;

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
            ScatterTrees(terrain, waypoints);

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

        static Terrain BuildTerrain()
        {
            var data = new TerrainData
            {
                heightmapResolution = 513,
                size = new Vector3(1024, 90, 1024),
            };
            int res = data.heightmapResolution;
            var h = new float[res, res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)res, ny = y / (float)res;
                float e = Mathf.PerlinNoise(nx * 2.3f + 11f, ny * 2.3f + 7f) * 0.55f
                          + Mathf.PerlinNoise(nx * 6f, ny * 6f) * 0.14f
                          + Mathf.PerlinNoise(nx * 18f, ny * 18f) * 0.04f;
                // gentle rise toward the far (east) edge, dip in the middle plain
                e += (nx - 0.5f) * 0.10f;
                // flatten a broad corridor for the road (diagonal SW->NE)
                float corridor = Mathf.Abs(ny - (0.32f + nx * 0.30f));
                e = Mathf.Lerp(0.30f, e, Mathf.Clamp01(corridor * 5f));
                h[y, x] = Mathf.Clamp01(e) * 0.32f;
            }
            data.SetHeights(0, 0, h);

            AssetDatabase.CreateAsset(data, SettingsDir + "/Mission01_Terrain.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            SetLayerRecursive(go, GameLayers.Environment);
            go.transform.position = new Vector3(-512, 0, -512);
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            return t;                     // textured later by PaintTerrain
        }

        // ------------------------------------------------------------------ //
        // Terrain texturing + vegetation
        // ------------------------------------------------------------------ //
        static Texture2D MakeNoiseTex(string name, Color baseCol, float variance, int size = 64)
        {
            string p = SettingsDir + "/T_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (existing != null) return existing;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = name };
            var rng = new System.Random(name.GetHashCode());
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                float m = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f;
                Color c = baseCol + new Color(1, 1, 1, 0) * (n * variance * 0.4f + m * variance);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, p);
            return tex;
        }

        static void PaintTerrain(Terrain terrain, List<Transform> waypoints)
        {
            var data = terrain.terrainData;

            TerrainLayer L(string n, Color col, float v, float tile)
            {
                string p = SettingsDir + "/TL_" + n + ".terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
                if (tl == null)
                {
                    tl = new TerrainLayer { name = n };
                    AssetDatabase.CreateAsset(tl, p);
                }
                tl.diffuseTexture = MakeNoiseTex(n, col, v);
                tl.tileSize = new Vector2(tile, tile);
                EditorUtility.SetDirty(tl);
                return tl;
            }

            var grass = L("grass", new Color(0.33f, 0.38f, 0.20f), 0.10f, 14f);
            var dry = L("dry", new Color(0.52f, 0.46f, 0.30f), 0.09f, 18f);
            var dirt = L("dirt", new Color(0.40f, 0.32f, 0.22f), 0.07f, 9f);
            data.terrainLayers = new[] { grass, dry, dirt };

            int aw = data.alphamapResolution;
            var maps = new float[aw, aw, 3];
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = data.size;

            for (int y = 0; y < aw; y++)
            for (int x = 0; x < aw; x++)
            {
                float u = x / (float)(aw - 1);
                float vv = y / (float)(aw - 1);
                // alphamap: 2nd index (x) runs along world X, 1st index (y) along world Z
                float wx = tPos.x + u * tSize.x;
                float wz = tPos.z + vv * tSize.z;

                float patch = Mathf.PerlinNoise(wx * 0.006f + 3f, wz * 0.006f + 9f);
                float g = Mathf.Clamp01(1f - patch * 1.3f);
                float d = Mathf.Clamp01(patch * 1.3f - 0.2f);

                float road = RoadMask(new Vector3(wx, 0, wz), waypoints, 9f, 5f);
                float roadv = road;

                float total = g + d + roadv + 1e-4f;
                maps[y, x, 0] = g / total;
                maps[y, x, 1] = d / total;
                maps[y, x, 2] = roadv / total;
            }
            data.SetAlphamaps(0, 0, maps);
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

        static GameObject BuildTreePrefab()
        {
            const string p = PrefabDir + "/Tree.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (existing != null) return existing;

            var go = new GameObject("Tree");
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(go.transform, false);
            trunk.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            trunk.transform.localPosition = new Vector3(0, 3f, 0);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            trunk.GetComponent<MeshRenderer>().sharedMaterial = MakeUnlit(new Color(0.28f, 0.21f, 0.14f), "bark");

            for (int i = 0; i < 2; i++)
            {
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "Canopy" + i;
                canopy.transform.SetParent(go.transform, false);
                float s = 4.5f - i * 1.4f;
                canopy.transform.localScale = new Vector3(s, s * 0.9f, s);
                canopy.transform.localPosition = new Vector3(0, 6f + i * 1.8f, 0);
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.GetComponent<MeshRenderer>().sharedMaterial =
                    MakeUnlit(new Color(0.20f + i * 0.05f, 0.30f + i * 0.04f, 0.14f), "leaf" + i);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void ScatterTrees(Terrain terrain, List<Transform> waypoints)
        {
            var tree = BuildTreePrefab();
            var data = terrain.terrainData;
            data.treePrototypes = new[] { new TreePrototype { prefab = tree } };
            data.RefreshPrototypes();

            var rng = new System.Random(4242);
            var instances = new List<TreeInstance>();
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = data.size;

            for (int i = 0; i < 1400; i++)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();
                Vector3 world = new Vector3(tPos.x + nx * tSize.x, 0, tPos.z + nz * tSize.z);

                if (RoadMask(world, waypoints, 16f, 10f) > 0.05f) continue;          // clear of road
                if (Vector3.Distance(world, new Vector3(40, 0, 0)) < 70f) continue;  // clear of village
                // clumping: skip some to leave clearings
                if (Mathf.PerlinNoise(world.x * 0.02f, world.z * 0.02f) < 0.42f) continue;

                float scale = 0.7f + (float)rng.NextDouble() * 0.9f;
                instances.Add(new TreeInstance
                {
                    position = new Vector3(nx, 0f, nz),
                    prototypeIndex = 0,
                    widthScale = scale,
                    heightScale = scale * (0.9f + (float)rng.NextDouble() * 0.3f),
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }
            data.SetTreeInstances(instances.ToArray(), true);
            terrain.Flush();
        }

        static float SampleHeight(Terrain t, Vector3 world)
            => t.SampleHeight(world) + t.transform.position.y;

        static void ScatterRuins(Terrain terrain)
        {
            var ruins = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRuins);
            var parent = new GameObject("Village").transform;
            var rng = new System.Random(20260910);
            string[] names = { "Ruin_WallLong", "Ruin_WallCorner", "Ruin_RubblePile", "Ruin_HouseShell" };

            // Two rows of buildings lining the road as it passes ~(40,0,0),
            // plus loose rubble. Enough to fly between.
            Vector3 centre = new(40f, 0f, 0f);
            Vector3 along = new Vector3(0.30f, 0f, 1f).normalized;   // road heading here
            Vector3 side = Vector3.Cross(Vector3.up, along);

            for (int i = 0; i < 44; i++)
            {
                float t = (i / 2) * 16f - 176f + (float)rng.NextDouble() * 6f;
                float lane = (i % 2 == 0 ? -1f : 1f) * (14f + (float)rng.NextDouble() * 10f);
                Vector3 c = centre + along * t + side * lane;
                c += new Vector3((float)rng.NextDouble() * 6f, 0, (float)rng.NextDouble() * 6f);
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
                // face roughly toward the road
                piece.transform.rotation = Quaternion.LookRotation(
                    (lane < 0 ? side : -side), Vector3.up)
                    * Quaternion.Euler(0, (float)rng.NextDouble() * 24f - 12f, 0);
                float s = 1f + (float)rng.NextDouble() * 0.6f;
                piece.transform.localScale *= s;
                SetLayerRecursive(piece, GameLayers.Environment);
                if (piece.GetComponentInChildren<Collider>() == null)
                    foreach (var mf in piece.GetComponentsInChildren<MeshFilter>())
                        mf.gameObject.AddComponent<MeshCollider>();
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
