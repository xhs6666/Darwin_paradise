using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DarwinPark.Entities;
using DarwinPark.Entities.Ecology;
using DarwinPark.Entities.Stats;
using DarwinPark.Entities.Vitals;
using DarwinPark.Environment;
using DarwinPark.Planet;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarwinPark.EditorTools
{
    // ✦ 新增：实际执行坐标采样、绕水路线与碰撞体入水扣血，覆盖中心位于水面上的漏判。
    public static class GeographyNavigationRegression
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> Passed = new();
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); Passed.Add(message); }
        private static void Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, Private).Invoke(target, args);

        private static PlanetTerrainData Terrain(Func<Vector3, float> height)
        {
            const int n = 48;
            var data = new PlanetTerrainData { Subdivisions = n, Radius = 100, HeightScale = 20, SeaLevel = .5f, FaceHeights = new float[6][] };
            for (int f = 0; f < 6; f++)
            {
                data.FaceHeights[f] = new float[(n + 1) * (n + 1)];
                for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++)
                    data.FaceHeights[f][y * (n + 1) + x] = height(PlanetSeamContinuity.Direction(f, x, y, n));
            }
            // Use production triangles, so water classification cannot silently fall back to height interpolation.
            typeof(PlanetTerrainGenerator).GetMethod("BuildMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { data });
            data.Navigation = new LandNavigationGraph(data, new LandNavigationSettings { longitudeCells = 64, latitudeCells = 32 });
            return data;
        }

#if UNITY_EDITOR
        public static void RunBatch()
        {
            try { Validate(); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        [MenuItem("Tools/Darwin Park/Validate Geography and Navigation")]
#endif
        public static void Validate()
        {
            Passed.Clear();
            var owner = new GameObject("GeographyRegressionPlanet");
            var subject = new GameObject("GeographyRegressionEntity");
            var planetField = typeof(EcologyQueries).GetField("_planet", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPlanet = planetField.GetValue(null);
            bool previousReady = DarwinPark.Core.WorldRuntimeGate.Ready;
            try
            {
                DarwinPark.Core.WorldRuntimeGate.Ready = true;
                var planet = owner.AddComponent<PlanetTerrainMeshBuilder>(); planet.enabled = false;
                planetField.SetValue(null, planet);
                var data = Terrain(d => Mathf.Abs(d.x) < .14f && Mathf.Abs(d.y) < .14f && d.z > .9f ? .3f : .7f);
                typeof(PlanetTerrainMeshBuilder).GetProperty("Data").SetValue(planet, data);
                var a = new Vector3(-.35f, 0, 1).normalized;
                var b = new Vector3(.35f, 0, 1).normalized;
                var graph = data.Navigation;
                Check(!graph.Segment(a, b) && graph.Connected(a, b), "Land remains connected around water; a straight crossing is rejected");
                Check(graph.Closest(Vector3.forward) < 0, "A water destination cannot be snapped onto a land route");
                LandPathScheduler.Configure(planet);
                var route = new LandRoutePlan();
                var current = a * 114; var target = b * 114;
                var scheduler = planet.GetComponent<LandPathScheduler>();
                for (int step = 0; step < 600 && Vector3.Distance(current, target) > 2; step++)
                {
                    Call(scheduler, "LateUpdate");
                    if (!route.Resolve(planet, current, target, out var waypoint)) continue;
                    current = Vector3.RotateTowards(current.normalized, waypoint.normalized, .02f, 0).normalized * 114;
                    Check(graph.Dry(current.normalized), "Route step stays on dry ground");
                }
                Check(Vector3.Distance(current, target) <= 2, "Budgeted A* reaches the destination around water");
                // ★ 修复（防Bug回归）：浅水目标不能沿用靠近岸边的已有路径。
                Check(!route.Resolve(planet, current, Vector3.forward * 110, out _), "An existing dry path cannot accept a wet target");
                var shallow = Terrain(d => d.z > .997f ? .499f : .7f);
                Check(shallow.Navigation.Closest(Vector3.forward) < 0,
                    "A shallow seabed is not walkable even when less than one simulation unit deep");
                Check(LandRoutePlan.SafeVelocity(planet, Vector3.forward * 110, Vector3.right * 20) == Vector3.zero, "Water cannot supply land propulsion");
                var islands = Terrain(d => Mathf.Abs(d.y) < .2f ? .3f : .7f);
                Check(!islands.Navigation.Connected(Vector3.up, Vector3.down), "Separate continents cannot be connected through the ocean");

                var collider = subject.AddComponent<SphereCollider>(); collider.radius = 1;
                var stats = subject.AddComponent<EntityStats>(); Call(stats, "Awake");
                var hazard = subject.AddComponent<EntityEnvironmentHazard>(); hazard.enabled = false; Call(hazard, "Awake");
                typeof(EntityEnvironmentHazard).GetField("_planetBuilder", Private).SetValue(hazard, planet);
                var config = new EnvironmentConfig();
                typeof(EntityEnvironmentHazard).GetField("_config", Private).SetValue(hazard, config);
                subject.transform.position = Vector3.forward * 110.5f; Physics.SyncTransforms();
                Check(!hazard.IsSubmerged && hazard.IsInWater, "Body touching water is detected even with its center above the surface");
                stats.TryGet("health", out float hp); Call(hazard, "UpdateDrowning", 1f); stats.TryGet("health", out float damaged);
                Check(hazard.IsDrowning && hp - damaged >= config.drowningHealthDamagePerSecond * config.minimumDrowningSeverity - .001f, "Contact with water applies configured minimum drowning damage");
                subject.transform.position = Vector3.forward * 120; Physics.SyncTransforms();
                Call(hazard, "UpdateDrowning", 1f); stats.TryGet("health", out float airborne);
                Check(!hazard.IsDrowning && airborne == damaged, "Flying above water does not cause drowning");
                subject.transform.position = a * 114.5f; Physics.SyncTransforms();
                Call(hazard, "UpdateDrowning", 1f); stats.TryGet("health", out float dry);
                Check(!hazard.IsDrowning && dry == damaged, "Dry land does not apply drowning damage");

                var genes = subject.GetComponent<EntityGeneController>();
                Check(EntityGeography.TryRead(genes, out var before) && !before.IsWater, "Entity coordinates expose dry geography");
                subject.transform.position = Vector3.forward * 110.5f;
                Check(EntityGeography.TryRead(genes, out var after) && after.IsWater, "Movement invalidates cached geography in the same frame");
                owner.transform.SetPositionAndRotation(new Vector3(30, -20, 15), Quaternion.Euler(15, 67, 12));
                owner.transform.localScale = Vector3.one * 2;
                subject.transform.position = owner.transform.TransformPoint(Vector3.forward * 109);
                Check(EntityGeography.TryRead(genes, out var transformed) && (transformed.LocalPosition - Vector3.forward * 109).magnitude < .001f, "Translated, rotated and scaled planets preserve local coordinates");
                Check(planet.IsSubmerged(subject.transform.position) && Mathf.Abs(planet.LocalWaterRadius(subject.transform.position) - 220) < .01f, "Scaled water and immersion agree");
                typeof(PlanetTerrainMeshBuilder).GetProperty("Data").SetValue(planet, Terrain(_ => .8f));
                Check(EntityGeography.TryRead(genes, out var rebuilt) && !rebuilt.IsWater, "Terrain replacement invalidates cached geography without movement");
                var seamA = PlanetCoordinate.AtLocal(data, PlanetTerrainSampler.DirectionFromUV(.00001f, .5f) * 114);
                var seamB = PlanetCoordinate.AtLocal(data, PlanetTerrainSampler.DirectionFromUV(.99999f, .5f) * 114);
                Check(Mathf.Abs(seamA.WaterDepth - seamB.WaterDepth) < .001f, "Longitude seam samples consistent geography");
                owner.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);owner.transform.localScale=Vector3.one;
                GlobalCoordinateRegression.Validate(planet,Passed);
                Directory.CreateDirectory("Artifacts/GeographyNavigationReview");
                File.WriteAllLines("Artifacts/GeographyNavigationReview/result.txt", Passed);
                Debug.Log($"[GeographyNavigation] PASS: {Passed.Count} assertions");
            }
            finally
            {
                planetField.SetValue(null, previousPlanet);
                DarwinPark.Core.WorldRuntimeGate.Ready = previousReady;
                Object.DestroyImmediate(subject); Object.DestroyImmediate(owner);
            }
        }
    }
}
