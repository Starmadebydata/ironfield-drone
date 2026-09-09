using System.Collections.Generic;
using Ironfield.Combat;
using Ironfield.Targeting;
using Ironfield.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace Ironfield.Tests
{
    public class TargetingSystemTests
    {
        readonly List<GameObject> _spawned = new();

        Camera _cam;

        [SetUp]
        public void SetUp()
        {
            var camGo = new GameObject("cam");
            _spawned.Add(camGo);
            _cam = camGo.AddComponent<Camera>();
            camGo.transform.position = Vector3.zero;
            camGo.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        Vehicle MakeVehicleAt(Vector3 pos, string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = pos;
            go.AddComponent<HealthComponent>();
            var aim = new GameObject(name + "_aim");
            aim.transform.SetParent(go.transform, false);
            var v = go.AddComponent<Vehicle>();
            v.aimPoint = aim.transform;   // avoids renderer-bounds fallback
            return v;
        }

        [Test]
        public void Picks_the_vehicle_closest_to_screen_centre()
        {
            var centre = MakeVehicleAt(new Vector3(0f, 0f, 60f), "centre");
            var offAxisSmall = MakeVehicleAt(new Vector3(6f, 0f, 60f), "slightlyOff");
            var list = new List<Vehicle> { offAxisSmall, centre };

            var best = TargetingSystem.SelectBest(_cam, list, 320f, 25f, requireLineOfSight: false);

            Assert.AreSame(centre, best);
        }

        [Test]
        public void Ignores_vehicles_outside_the_cone()
        {
            var wideOff = MakeVehicleAt(new Vector3(80f, 0f, 40f), "wideOff"); // ~63 deg
            var list = new List<Vehicle> { wideOff };

            var best = TargetingSystem.SelectBest(_cam, list, 320f, 20f, requireLineOfSight: false);

            Assert.IsNull(best);
        }

        [Test]
        public void Ignores_vehicles_behind_the_camera_and_out_of_range()
        {
            var behind = MakeVehicleAt(new Vector3(0f, 0f, -50f), "behind");
            var tooFar = MakeVehicleAt(new Vector3(0f, 0f, 900f), "tooFar");
            var list = new List<Vehicle> { behind, tooFar };

            var best = TargetingSystem.SelectBest(_cam, list, 320f, 25f, requireLineOfSight: false);

            Assert.IsNull(best);
        }

        [Test]
        public void Skips_destroyed_vehicles()
        {
            var live = MakeVehicleAt(new Vector3(2f, 0f, 70f), "live");
            var dead = MakeVehicleAt(new Vector3(0f, 0f, 60f), "dead");
            dead.GetComponent<HealthComponent>().Kill();

            var list = new List<Vehicle> { dead, live };
            var best = TargetingSystem.SelectBest(_cam, list, 320f, 25f, requireLineOfSight: false);

            Assert.AreSame(live, best);
        }
    }
}
