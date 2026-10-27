using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static class OfficialMap12PuzzleValidation
    {
        public static void Run()
        {
            try
            {
                Validate();
                OfficialMap11PuzzleValidation.Validate();
                ProjectTools.Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Validate()
        {
            var definitions = JsonUtility.FromJson<Definitions>(Resources.Load<TextAsset>("Map12/stories").text);
            var map = JsonUtility.FromJson<OfficialMapDefinition>(File.ReadAllText("Assets/Official/World/Map/12/map12.json"));
            MapPuzzleRule[] rules = definitions.puzzleRules;
            var plans = JsonUtility.FromJson<PlansFile>(File.ReadAllText("../scripts/testdata/map12-push-plans.json"));
            foreach (Input input in plans.Inputs)
            {
                using (SHA256 hash = SHA256.Create())
                    Require(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("../" + input.Path)))
                        .Replace("-", "").ToLowerInvariant() == input.Sha256, "Test plan input hash: " + input.Path);
            }
            Require(rules.Length == 2591 && plans.Plans.Length == 4, "Original mountain placements and push-box groups");
            ValidateInteractionRangeOverrides(rules);
            var collision = new OfflineMapPuzzleSession(rules, null);
            // 4605 and 4604 occupy consecutive original cells. A box cannot enter another box.
            MapPuzzleRule adjacentA = rules.Single(r => r.PlacementId == 4605);
            MapPuzzleRule adjacentB = rules.Single(r => r.PlacementId == 4604);
            Require(Math.Abs(adjacentA.GridX - adjacentB.GridX) + Math.Abs(adjacentA.GridY - adjacentB.GridY) == 1, "Original collision fixture adjacency");
            Require(!collision.TryPush(adjacentA.PlacementId, new MapGridPoint(
                    2 * adjacentA.GridX - adjacentB.GridX, 2 * adjacentA.GridY - adjacentB.GridY), map.Navigation, out _), "Reject occupied box target");
            MapPuzzleRule bounded = rules.Single(r => r.PlacementId == 1407);
            MapPuzzleEntityState[] edgeState = collision.Save();
            MapPuzzleEntityState edgeBox = edgeState.Single(s => s.PlacementId == bounded.PlacementId);
            edgeBox.GridX = bounded.MovableRect.MinGrid.X;
            collision = new OfflineMapPuzzleSession(rules, edgeState);
            Require(!collision.TryPush(bounded.PlacementId, new MapGridPoint(edgeBox.GridX + 1, edgeBox.GridY),
                map.Navigation, out _), "Reject target outside original inclusive MovableRect");
            int pushes = 0;
            foreach (Plan plan in plans.Plans)
            {
                var session = new OfflineMapPuzzleSession(rules, null);
                session.RefreshLocks();
                Require(!session.IsUnlocked(plan.LockId), "Initially locked " + plan.LockId);
                MapPuzzleRule goal = rules.Single(r => r.PlacementId == plan.LockId);
                MapPuzzleRule terminal = rules.Single(r => r.PlacementId == plan.TerminalId);
                var player = new MapGridPoint(terminal.GridX, terminal.GridY);
                foreach (Action action in plan.Actions)
                {
                    var beforePush = new MapGridPoint(action.PlayerX, action.PlayerY);
                    OfficialMapGroundRules ground = session.CreateGround(map.Navigation, map.CharacterMover.MoveMask);
                    var walk = OfficialMapPathfinder.FindPath(ground, new OfficialMapPathRequest
                    {
                        Start = player, End = beforePush, PreciseTarget = true,
                        BodyOffsets = new[] { new MapGridPoint(0, 0) }
                    });
                    Require(player.Equals(beforePush) || walk != null && walk.Last().Equals(beforePush), "Walk to box " + plan.LockId);
                    player = beforePush;
                    var old = new MapGridPoint(action.BoxX, action.BoxY);
                    int id = goal.PushBoxIds.Single(box => session.Position(box).Equals(old));
                    string snapshot = JsonUtility.ToJson(new Saved { States = session.Save() });
                    Require(!session.TryPush(id, new MapGridPoint(old.X + 2, old.Y), map.Navigation, out _), "Reject distant push");
                    Require(snapshot == JsonUtility.ToJson(new Saved { States = session.Save() }), "Rejected push changes no state");
                    Require(session.TryPush(id, player, map.Navigation, out MapGridPoint follow) && follow.Equals(old), "Push and follow " + id);
                    Require(session.Position(id).Equals(new MapGridPoint(old.X + old.X - player.X, old.Y + old.Y - player.Y)), "Original next box cell");
                    player = follow;
                    session = new OfflineMapPuzzleSession(rules, JsonUtility.FromJson<Saved>(
                        JsonUtility.ToJson(new Saved { States = session.Save() })).States);
                    session.RefreshLocks();
                    pushes++;
                }
                Require(session.IsUnlocked(plan.LockId) && session.State(plan.LockId) == 1, "Manual completion " + plan.LockId);
                Require(goal.SpawnIds.Length > 0 && goal.SpawnIds.All(session.IsSpawned), "Original unlock spawn results");
                MapPuzzleEntityState[] detached = session.Save();
                detached.Single(s => s.PlacementId == goal.PushBoxIds[0]).GridX++;
                var latched = new OfflineMapPuzzleSession(rules, detached);
                latched.RefreshLocks();
                Require(latched.IsUnlocked(plan.LockId) && latched.State(plan.LockId) == 1, "Completed lock remains open");
                Require(!latched.Position(goal.PushBoxIds[0]).Equals(session.Position(goal.PushBoxIds[0])), "Save snapshots are detached");

                var auto = new OfflineMapPuzzleSession(rules, null);
                auto.RunStage(plan.TerminalId, 0);
                Require(auto.IsUnlocked(plan.LockId) && goal.SpawnIds.All(auto.IsSpawned), "Confirmed automatic unlock");
                Require(goal.PushBoxIds.All(id => auto.Position(id).Equals(new MapGridPoint(
                    rules.Single(r => r.PlacementId == id).GridX, rules.Single(r => r.PlacementId == id).GridY))), "Auto does not move boxes");

                var partial = new OfflineMapPuzzleSession(rules, null);
                Action first = plan.Actions[0];
                int firstBox = goal.PushBoxIds.Single(id => partial.Position(id).Equals(new MapGridPoint(first.BoxX, first.BoxY)));
                Require(partial.TryPush(firstBox, new MapGridPoint(first.PlayerX, first.PlayerY), map.Navigation, out _), "Initial push before reset");
                partial.RunStage(plan.TerminalId, 1);
                Require(goal.PushBoxIds.All(id => partial.Position(id).Equals(new MapGridPoint(
                    rules.Single(r => r.PlacementId == id).GridX, rules.Single(r => r.PlacementId == id).GridY))), "Reset original box positions");
                Require(!partial.IsUnlocked(plan.LockId), "Reset does not complete an unsolved lock");
            }
            foreach (MapPuzzleRule terminal in rules.Where(r => r.TerminalPuzzleType == 3))
            {
                var auto = new OfflineMapPuzzleSession(rules, null);
                auto.RunStage(terminal.PlacementId, 0);
                Require(terminal.TerminalRemoveIds.All(id => !auto.IsSpawned(id)), "Huarong removes original list only");
                Require(terminal.TerminalBoxIds.Except(terminal.TerminalRemoveIds).All(auto.IsSpawned), "Huarong preserves unlisted box");
                MapPuzzleEntityState[] changed = new OfflineMapPuzzleSession(rules, null).Save();
                foreach (var state in changed.Where(s => terminal.TerminalRemoveIds.Concat(terminal.TerminalBoxIds).Contains(s.PlacementId)))
                { state.GridX++; state.Spawned = false; }
                var reset = new OfflineMapPuzzleSession(rules, changed);
                reset.RunStage(terminal.PlacementId, 1);
                Require(terminal.TerminalRemoveIds.Concat(terminal.TerminalBoxIds).All(id => reset.IsSpawned(id)
                    && reset.Position(id).Equals(new MapGridPoint(rules.Single(r => r.PlacementId == id).GridX,
                        rules.Single(r => r.PlacementId == id).GridY))), "Huarong reset restores the union of both original lists");
            }
            Debug.Log("Map12 puzzle rules passed: four original full push paths, " + pushes
                + " pushes, walking reachability, follow-through, per-push cold reload, latched unlock/spawn, automatic unlock, six resets and detached saves. UI/payment/player input remain separate.");
        }

        private static void ValidateInteractionRangeOverrides(MapPuzzleRule[] rules)
        {
            var configured = rules.Where(rule => rule.InteractiveRangeOperations?.Length > 0).ToArray();
            Require(configured.Select(rule => rule.EntityClassId).Distinct().OrderBy(value => value)
                .SequenceEqual(new[] { 5013, 5014, 5015, 6660 }), "Original interaction range override templates");
            foreach (MapPuzzleRule rule in configured)
            {
                for (int state = 0; state < rule.InteractiveRangeOperations.Length; state++)
                {
                    MapPuzzleEntityState[] saved = { new MapPuzzleEntityState
                    {
                        PlacementId = rule.PlacementId, State = state, HasPosition = true,
                        GridX = rule.GridX, GridY = rule.GridY
                    } };
                    var session = new OfflineMapPuzzleSession(rules, saved);
                    MapPuzzleInteractiveRangeOperation operation = rule.InteractiveRangeOperations[state];
                    OfficialStageInteractionOffset[] expected = operation.IsRevertGroundType
                        ? rule.Stage.InteractiveRange.ToArray()
                        : rule.InteractiveRangeInfos[operation.DataIndex].InteractiveRange;
                    Require(session.InteractionRange(rule.PlacementId).SequenceEqual(expected),
                        "Original interaction range state " + rule.EntityClassId + "/" + state);
                }
            }
        }

        [Serializable] private sealed class Definitions { public MapPuzzleRule[] puzzleRules; }
        [Serializable] private sealed class Saved { public MapPuzzleEntityState[] States; }
        [Serializable] private sealed class Input { public string Path, Sha256; }
        [Serializable] private sealed class PlansFile { public Input[] Inputs; public Plan[] Plans; }
        [Serializable] private sealed class Plan { public int LockId, TerminalId; public Action[] Actions; }
        [Serializable] private sealed class Action { public int BoxX, BoxY, PlayerX, PlayerY; }
        private static void Require(bool value, string name) { if (!value) throw new InvalidDataException(name); }
    }
}
