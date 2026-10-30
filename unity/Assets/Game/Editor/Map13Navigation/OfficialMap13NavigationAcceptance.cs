using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13Navigation;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World;
using Zjcs.Offline.World.Map13Navigation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map13Navigation
{
    // Runtime data/algorithm verification. No user saves, simulated UI, player
    // visuals or terminal transaction claims are made by this test.
    public static class OfficialMap13NavigationAcceptance
    {
        private const string Key = "Zjcs.Map13.NavigationAcceptance";
        private static bool started;
        public static void Run()
        {
            OfficialMap13NavigationTools.RestoreAndValidate();
            StartRuntimeAcceptance();
        }
        public static void RunRuntimeOnly()
        {
            StartRuntimeAcceptance();
        }
        private static void StartRuntimeAcceptance()
        {
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Changed; EditorApplication.playModeStateChanged += Changed;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Map13 navigation verification timed out.");
                EditorApplication.isPlaying = false; return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            new GameObject("Map13 original navigation verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private sealed class Runner : MonoBehaviour { }
        private static IEnumerator Verify()
        {
            yield return null;
            try
            {
                VerifyOriginalAStar();
                VerifyPackagedMap();
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map13 navigation Play Mode passed: original AStar branches, actual navigation and water masks, platform ground checks/rollback, carried paths, map-save JSON roundtrip and cold state reconstruction. Input/UI interaction remains pending.");
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.isPlaying = false;
        }
        private sealed class TestGround : IAStarMovableHandler
        {
            public Func<Pos2d, bool> Fit = p => Math.Abs(p.X) < 5 && Math.Abs(p.Y) < 5;
            public Func<Pos2d, Pos2d, bool> Move = (a, b) => true;
            public bool IsFit(Pos2d p, IAStarMoveCustomContext c) => Fit(p);
            public bool CanMoveToNext(Pos2d a, Pos2d b) => Move(a, b);
        }
        private static FindPathArgContext.Builder Builder(Pos2d start, Pos2d end) =>
            new FindPathArgContext.Builder(start, end).WithBodyRanges(new List<Pos2d> { Pos2d.Zero })
                .WithCustomContext(new DefaultAStarMoveCustomContext(65)).WithPreciseTarget(true);
        private static void VerifyOriginalAStar()
        {
            var ground = new TestGround();
            var path = AStarUtils.FindPath(ground, Builder(Pos2d.Zero, new Pos2d(-1, -1)).Build());
            Require(path.SequenceEqual(new[] { Pos2d.Zero, Pos2d.Down, new Pos2d(-1, -1) }), "Original cardinal/tie order changed.");
            path = AStarUtils.FindPath(ground, Builder(Pos2d.Zero, new Pos2d(-3, 0)).WithMaxStep(3).Build());
            Require(path.SequenceEqual(new[] { Pos2d.Zero, Pos2d.Left, new Pos2d(-2, 0) }), "Original max-step closest fallback changed.");
            Require(AStarUtils.FindPath(ground, Builder(Pos2d.Zero, Pos2d.Zero).Build()).Count == 0, "Start=end no longer returns empty.");
            ground.Fit = p => false;
            path = AStarUtils.FindPath(ground, Builder(Pos2d.Zero, new Pos2d(4, 0))
                .WithFirstStepPosList(new List<Pos2d> { new Pos2d(4, 0) }).Build());
            Require(path.SequenceEqual(new[] { Pos2d.Zero, new Pos2d(4, 0) }), "Original precomputed first-step branch changed.");
            ground = new TestGround();
            path = AStarUtils.FindPath(ground, Builder(Pos2d.Zero, new Pos2d(4, 0)).WithCheckEndDel(p => p == Pos2d.Right).Build());
            Require(path.Last() == Pos2d.Right, "Original custom end predicate changed.");
            ground.Move = (a, b) => false;
            Require(AStarUtils.FindPath(ground, Builder(Pos2d.Zero, Pos2d.Left).Build()).Count == 0, "Cross-block fallback changed.");
            bool rejected = false;
            try { new FindPathArgContext.Builder(Pos2d.Zero, Pos2d.Up).Build(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Original builder accepted missing body/context.");
        }
        private static string Snapshot(OfflineMap13NavigationSession session) => JsonUtility.ToJson(new Saved { States = session.Water.Save() });
        [Serializable] private sealed class Saved { public Map13WaterState[] States; }
        private static void VerifyPackagedMap()
        {
            var assets = Map13NavigationAssets.LoadDefault();
            var session = assets.Open();
            VerifyMapSaveRoundTrip(assets, session);
            VerifyTerminalRoadOperations(session, JsonUtility.FromJson<Map13WaterDefinition>(assets.WaterJson.text));
            var definition = session.Definition;
            Require(definition.Entities.Select(p => p.Id).Distinct().Count() == 2690
                && definition.EntityComponents.Length == 2690 && definition.Configs.Length == 889
                && definition.MaxRegionProgress.Length == 106, "Map/config placement binding scope differs.");
            var nav = definition.Navigation;
            int cells = 0, standable = 0;
            var ground = session.CreateGround(65);
            var water = JsonUtility.FromJson<Map13WaterDefinition>(assets.WaterJson.text);
            var bodies = new Dictionary<MapGridPoint, List<Map13WaterBody>>();
            foreach (var rule in water.Rules)
            {
                var state = session.Water.State(rule.PlacementId);
                if (!state.Spawned) continue;
                foreach (var body in session.Water.Body(rule.PlacementId))
                {
                    var point = state.Grid + body.Pos;
                    if (!bodies.TryGetValue(point, out var list)) bodies.Add(point, list = new List<Map13WaterBody>());
                    list.Add(body);
                }
            }
            for (int y = 0; y < nav.Height; y++) for (int x = 0; x < nav.Width; x++)
            {
                var point = new MapGridPoint(x, y);
                Require(OfficialMapNavigationView.TryGetCell(nav, x, y, out _, out int type), "Missing original grid.");
                int mask = 1 << type, replacement = 0;
                if (bodies.TryGetValue(point, out var overlays)) foreach (var body in overlays)
                {
                    if (body.OverrideGroundType) replacement |= 1 << body.GroundType;
                    else mask |= 1 << body.GroundType;
                }
                Require(ground.IsFit(point) == ((~65 & (replacement > 0 ? replacement : mask)) == 0), "Original water occupancy mask differs.");
                var world = OfficialMapNavigationView.GridToWorld(nav, x, y);
                OfficialMapNavigationView.WorldToGrid(nav, world, out int rx, out int ry);
                Require(rx == x && ry == y, "Original grid/world conversion differs.");
                if (ground.IsFit(point)) standable++;
                cells++;
            }
            Require(cells == 57600 && standable > 0 && !ground.IsFit(new MapGridPoint(-1, 0)), "Grid scope/boundary differs.");
            var corner = new OfficialMapVector3 { X = nav.Origin.X - 0.75f, Y = nav.Origin.Y - 0.75f };
            OfficialMapNavigationView.WorldToGrid(nav, corner, out int cx, out int cy);
            Require(cx == 0 && cy == 0, "Original negative fractional truncation was changed to floor.");

            int routes = 0, moved = 0, obstructed = 0;
            foreach (var platform in water.Rules.Where(rule => rule.Routes.Length > 0))
            {
                session = assets.Open();
                var trigger = water.Rules.First(rule => rule.TriggerEntities.Contains(platform.PlacementId));
                var actor = trigger.OriginalGrid + trigger.InteractiveRange[0];
                string before = Snapshot(session);
                bool accepted = session.TouchWater(trigger.PlacementId, actor, Array.Empty<Map13WaterOccupant>());
                if (accepted) moved++; else { obstructed++; Require(Snapshot(session) == before, "Actual blocked route changed state."); }
                session = assets.Open();
                before = Snapshot(session);
                Action<OfficialMapGroundRules, int> block = (g, id) =>
                {
                    Require(id == platform.PlacementId, "World occupancy received wrong moving entity exclusion.");
                    foreach (var segment in platform.Routes) foreach (var point in segment.OffsetPath)
                        foreach (var body in platform.Body) g.AddExtraBlocked(platform.OriginalGrid + point + body.Pos);
                };
                Require(!session.TouchWater(trigger.PlacementId, actor, Array.Empty<Map13WaterOccupant>(), block)
                    && Snapshot(session) == before && session.Water.Movements.Count == 0, "Host obstruction did not roll back atomically.");
                // Explicit world occupancy fixture makes every route traversable;
                // verifies adapter masks and excludes the moving boat itself.
                var isolatedStates = session.Water.Save();
                foreach (var state in isolatedStates)
                    state.Spawned = state.PlacementId == platform.PlacementId || state.PlacementId == trigger.PlacementId;
                session = assets.Open(isolatedStates);
                Action<OfficialMapGroundRules, int> route = (g, id) =>
                {
                    long key = 1;
                    foreach (var segment in platform.Routes) foreach (var point in segment.OffsetPath)
                        foreach (var body in platform.Body) g.AddOverlay(key++, platform.OriginalGrid + point + body.Pos, 6, true);
                };
                for (int step = 0; step < platform.Routes.Length * 2; step++)
                {
                    var offset = platform.Body.Last().Pos;
                    var passenger = new Map13WaterOccupant { PlacementId = -3001, EntityClassId = 3001,
                        HasGridMover = true, Grid = session.Water.State(platform.PlacementId).Grid + offset };
                    Require(session.TouchWater(trigger.PlacementId, actor, new[] { passenger }, route), $"Configured route fixture could not move: platform {platform.PlacementId}, step {step}.");
                    var boat = session.Water.Movements.Single(v => v.PlacementId == platform.PlacementId);
                    var carried = session.Water.Movements.Single(v => v.PlacementId == -3001);
                    Require(carried.Path.SequenceEqual(boat.Path.Select(p => p + offset)), "Passenger offset/path differs.");
                    string snapshot = Snapshot(session);
                    session = assets.Open(JsonUtility.FromJson<Saved>(snapshot).States);
                    Require(Snapshot(session) == snapshot, "Cold reconstruction changed route direction/state.");
                }
                Require(session.Water.State(platform.PlacementId).Grid.Equals(platform.OriginalGrid), "Route did not return to original anchor.");
                routes++;
            }
            Require(routes == 10, "Original platform count differs.");
            Debug.Log($"Map13 navigation fixture: {cells} cells / {standable} standable with water; first original routes: {moved} moved, {obstructed} blocked; all ten controlled full round trips and host-obstruction rollbacks passed.");

            // The adapter must use the selected map's limit, even if a caller
            // passes a larger value. Ordinary spawned entities remain explicit.
            MapGridPoint start = default, end = default; bool found = false;
            for (int y = 1; y < 239 && !found; y++) for (int x = 1; x < 237 && !found; x++)
                if (ground.IsFit(new MapGridPoint(x, y)) && ground.IsFit(new MapGridPoint(x + 1, y)) && ground.IsFit(new MapGridPoint(x + 2, y)))
                { start = new MapGridPoint(x, y); end = new MapGridPoint(x + 2, y); found = true; }
            Require(found, "No actual terrain path sample found.");
            session = assets.Open();
            var request = new OfficialMapPathRequest { Start = start, End = end, PreciseTarget = true, MaxSteps = 10000,
                BodyOffsets = new[] { new MapGridPoint(0, 0) } };
            var actualPath = session.FindPlayerPath(request);
            Require(actualPath.Count == 3 && actualPath.Last().Equals(end), "Actual packaged player navigation failed.");
            Action<OfficialMapGroundRules, int> extra = (g, id) =>
            { g.AddCrossBlocked(start, start + new MapGridPoint(1, 0)); g.AddExtraBlocked(end); };
            var blockedPath = session.FindPlayerPath(request, extra);
            Require(blockedPath != null && (blockedPath.Count == 0 || !blockedPath.Last().Equals(end)), "Host cross/extra blocks were ignored.");
            var bad = UnityEngine.Object.Instantiate(assets);
            bad.MoversSha256 = "changed";
            bool rejected = false; try { bad.Open(); } catch (System.IO.InvalidDataException) { rejected = true; }
            UnityEngine.Object.Destroy(bad);
            Require(rejected, "Changed navigation payload hash was silently accepted.");
        }
        private static void VerifyMapSaveRoundTrip(Map13NavigationAssets assets, OfflineMap13NavigationSession session)
        {
            Type saveType = typeof(OfficialMainlineMapRuntime).Assembly.GetType(
                "Zjcs.Presentation.World.Map10LocalSave", true);
            FieldInfo waterField = saveType.GetField("map13WaterStates");
            Require(waterField != null, "Map13 water state is missing from the formal map save.");
            object save = Activator.CreateInstance(saveType);
            Map13WaterState[] source = session.Water.Save();
            waterField.SetValue(save, source);
            object coldSave = JsonUtility.FromJson(JsonUtility.ToJson(save), saveType);
            Map13WaterState[] restored = (Map13WaterState[])waterField.GetValue(coldSave);
            Require(restored != null && restored.Length == source.Length
                && restored.Zip(source, (left, right) => left.PlacementId == right.PlacementId
                    && left.State == right.State && left.Grid.Equals(right.Grid)
                    && left.Spawned == right.Spawned && left.NextSegment == right.NextSegment
                    && left.Returning == right.Returning && left.GroundOperation == right.GroundOperation).All(value => value),
                "Map13 water state changed during formal map-save JSON serialization.");
            OfflineMap13NavigationSession coldSession = assets.Open(restored);
            Require(JsonUtility.ToJson(new Saved { States = coldSession.Water.Save() })
                == JsonUtility.ToJson(new Saved { States = source }),
                "Map13 cold navigation session changed persisted water state.");
        }
        private static void VerifyTerminalRoadOperations(OfflineMap13NavigationSession session, Map13WaterDefinition definition)
        {
            foreach (Map13WaterTerminal terminal in definition.Terminals)
            {
                session = new OfflineMap13NavigationSession(session.Definition, definition,
                    JsonUtility.FromJson<Map13NavigationAssets.MoverData>(Map13NavigationAssets.LoadDefault().MoversJson.text));
                Dictionary<int, Map13WaterState> before = session.Water.Save().ToDictionary(value => value.PlacementId);
                session.Water.OpenPaidTerminalRoads(terminal.PlacementId);
                foreach (int id in terminal.ExtendRoadLoEntityIds)
                    Require(session.Water.State(id).Spawned, "Paid terminal did not open its configured road entity: " + terminal.PlacementId + "/" + id);
                session.Water.ResetTerminal(terminal.PlacementId);
                foreach (int id in terminal.NeedResetLoEntityIds)
                {
                    Map13WaterState reset = session.Water.State(id);
                    Map13WaterRule rule = definition.Rules.Single(value => value.PlacementId == id);
                    Require(reset.Grid.Equals(rule.OriginalGrid) && reset.Spawned == rule.InitiallySpawned
                        && reset.State == rule.InitialState && reset.NextSegment == 0 && !reset.Returning
                        && reset.GroundOperation == before[id].GroundOperation,
                        "Terminal reset did not restore configured initial water state: " + terminal.PlacementId + "/" + id);
                }
                foreach (int id in terminal.ExtendRoadLoEntityIds.Except(terminal.NeedResetLoEntityIds))
                    Require(session.Water.State(id).Spawned, "Terminal reset closed a road outside its reset list: " + terminal.PlacementId + "/" + id);
                Require(before.Count == session.Water.Save().Length, "Terminal operation changed the water state set.");
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
