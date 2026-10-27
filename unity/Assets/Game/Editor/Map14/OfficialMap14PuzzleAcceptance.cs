using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.World;
using Zjcs.Offline.World;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14PuzzleAcceptance
    {
        private const string Key = "Zjcs.Map14.PuzzleExecution";
        private static bool started;
        public static void Run()
        {
            OfficialMap14PuzzleTools.ImportAndValidate();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 90);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= ModeChanged; EditorApplication.playModeStateChanged += ModeChanged;
        }
        private static void ModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= ModeChanged;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 puzzle Play Mode timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            try
            {
                Validate(); SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 puzzle Play Mode passed. Rule execution and JSON cold read only; no simulated player input, backpack payment, disk save or rendered puzzle scene.");
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.isPlaying = false;
        }

        public static void Validate()
        {
            var asset = Map14PuzzleAssets.LoadDefault();
            var data = asset.Load();
            var rules = data.Rules.ToDictionary(r => r.PlacementId);
            Require(data.Costs.Select(c => c.TerminalId).Distinct().Count() == 12 && data.Costs.All(c => c.Items.Length == 1
                && c.Items[0].ItemId == 325 && c.Items[0].Amount == 1), "Original 325 x 1 cost configuration");
            Require(rules[8516].TerminalRemoveIds.Count(id => id == 8508) == 2, "Preserve duplicate original cannon target");
            foreach (var terminal in data.Rules.Where(r => r.TerminalPuzzleType == 4))
            {
                var initial = asset.Open().Save();
                foreach (var state in initial.Where(s => terminal.TerminalRemoveIds.Contains(s.PlacementId)))
                { state.GridX += 2; state.OverlapTimes = 3; }
                int unrelated = data.Rules.First(r => !terminal.TerminalRemoveIds.Contains(r.PlacementId)
                    && r.PlacementId != terminal.PlacementId && r.TerminalPuzzleType == 0 && r.LockType == 0).PlacementId;
                initial.Single(s => s.PlacementId == unrelated).Destroyed = true;
                var session = new OfflineMapPuzzleSession(data.Rules, initial);
                var expected = Cold(initial);
                foreach (var state in expected.Where(s => terminal.TerminalRemoveIds.Contains(s.PlacementId))) state.Destroyed = true;
                expected.Single(s => s.PlacementId == terminal.PlacementId).State = 1;
                session.RunStage(terminal.PlacementId, 0);
                Require(Same(session.Save(), expected), "Cannon only destroys listed targets and executes original terminal self-state " + terminal.PlacementId);
                Require(terminal.TerminalRemoveIds.All(id => !session.IsSpawned(id)), "Destroyed obstacles stop occupying the map");
                session = new OfflineMapPuzzleSession(data.Rules, Cold(session.Save()));
                Require(Same(session.Save(), expected), "Cannon JSON cold read");
                session.ResetTerminal(terminal.PlacementId);
                foreach (var state in expected.Where(s => terminal.TerminalRemoveIds.Contains(s.PlacementId))) state.Destroyed = false;
                Require(Same(session.Save(), expected), "Cannon reset only clears configured destroyed flags " + terminal.PlacementId);
                session.ResetTerminal(terminal.PlacementId);
                Require(Same(session.Save(), expected), "Repeated reset is idempotent");
            }
            foreach (var terminal in data.Rules.Where(r => r.TerminalPuzzleType == 6))
            {
                var transports = new List<MapPuzzleTransportRequest>();
                Require(terminal.TerminalLockIds.Length == 1, "Original single step-grid lock");
                var goal = rules[terminal.TerminalLockIds[0]];
                Require(goal.LockType == 7 && goal.EntityClassId == 6533 && goal.SpawnIds.Length == 1
                    && goal.ResetTransferActionId == 22055, "Original step-grid goal binding");
                var session = asset.Open(onTransport: transports.Add);
                int first = goal.LockConditions[0].PlacementId;
                Step(session, rules[first]);
                var counters = goal.LockConditions.ToDictionary(c => c.PlacementId, c => session.OverlapTimes(c.PlacementId));
                var before = session.Save();
                session.RunStage(terminal.PlacementId, 0);
                Require(session.IsUnlocked(goal.PlacementId) && session.State(goal.PlacementId) == 1
                    && session.State(terminal.PlacementId) == 1 && goal.SpawnIds.All(session.IsSpawned), "Auxiliary original spawn/state chain");
                Require(counters.All(p => session.OverlapTimes(p.Key) == p.Value), "Auxiliary does not fabricate plate visits");
                var allowed = goal.SpawnIds.Concat(new[] { goal.PlacementId, terminal.PlacementId }).ToArray();
                Require(before.Where(s => !allowed.Contains(s.PlacementId)).All(s => Same(s, session.Save().Single(v => v.PlacementId == s.PlacementId))),
                    "Auxiliary changes only listed lock, reward spawn and terminal state");
                session = new OfflineMapPuzzleSession(data.Rules, Cold(session.Save()), onTransport: transports.Add);
                int reward = goal.SpawnIds.Single();
                // Stand-in for the host's persisted claimed entity, without granting
                // anything or pretending that a direct state call tests payment.
                session.SynchronizeExternalEntity(reward, 1, false, session.Position(reward));
                var claimed = session.Save().Single(s => s.PlacementId == reward);
                before = session.Save(); session.ResetTerminal(terminal.PlacementId);
                Require(!session.IsUnlocked(goal.PlacementId) && session.State(goal.PlacementId) == goal.InitialState
                    && session.State(terminal.PlacementId) == 1, "Reset only restores goal, preserves terminal state");
                Require(goal.LockConditions.All(c => session.OverlapTimes(c.PlacementId) == 0), "Reset restores this group's plate counters");
                Require(Same(claimed, session.Save().Single(s => s.PlacementId == reward)), "Reset preserves reward and claimed entity state");
                allowed = goal.LockConditions.Select(c => c.PlacementId).Append(goal.PlacementId).ToArray();
                Require(before.Where(s => !allowed.Contains(s.PlacementId)).All(s => Same(s, session.Save().Single(v => v.PlacementId == s.PlacementId))),
                    "Reset preserves every unrelated group and reward");
                session = new OfflineMapPuzzleSession(data.Rules, Cold(session.Save()), onTransport: transports.Add);
                Require(!session.IsUnlocked(goal.PlacementId) && Same(claimed, session.Save().Single(s => s.PlacementId == reward)), "Reset cold read");

                session = asset.Open(onTransport: transports.Add);
                int required = rules[first].OverlapDetector.OverlapTimesIsActivity;
                for (int i = 0; i <= required; i++) Step(session, rules[first]);
                Require(session.OverlapTimes(first) == required + 1 && !session.IsUnlocked(goal.PlacementId), "Exceeding the original required plate count does not complete a lock");
                session.ResetTerminal(terminal.PlacementId);
                transports.Clear();
                foreach (var condition in goal.LockConditions)
                    for (int i = 0; i < rules[condition.PlacementId].OverlapDetector.OverlapTimesIsActivity; i++) Step(session, rules[condition.PlacementId]);
                Require(session.IsUnlocked(goal.PlacementId) && goal.SpawnIds.All(session.IsSpawned)
                    && session.State(terminal.PlacementId) == 1, "Original required plate counts execute the original reward chain");
                int count = session.OverlapTimes(first); Step(session, rules[first]);
                Require(session.OverlapTimes(first) == count, "Original inactive-in-unlock prevents extra plate visits");
                Require(transports.Count == goal.LockConditions.Count(c => rules[c.PlacementId].HasTransport), "Every original transport activation reaches the explicit host boundary: " + terminal.PlacementId + "/" + transports.Count);
                foreach (var request in transports)
                {
                    var source = rules[request.SourceId].Transport;
                    var target = rules[source.TargetLoEntityId];
                    Require(request.TargetLoEntityId == source.TargetLoEntityId && request.ActionId == source.ActionId && request.MapClassId == source.MapClassId
                        && request.Target.Equals(new MapGridPoint(target.GridX + source.Offset.X, target.GridY + source.Offset.Y)), "Original transport target position and action");
                }
            }
            var copy = ScriptableObject.CreateInstance<Map14PuzzleAssets>();
            try
            {
                copy.RulesJson = asset.RulesJson; copy.RulesSha256 = new string('0', 64);
                bool rejected = false; try { copy.Open(); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Reject altered source digest");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            Debug.Log("Map14 puzzle rules passed: all seven cannon scopes/resets and five step-grid manual/auxiliary/reset chains, preserved reward state, independent groups, JSON cold reads and original cost data. Payment, actual claiming and disk persistence remain separate.");
        }
        private static void Step(OfflineMapPuzzleSession session, MapPuzzleRule rule)
        {
            var target = rule.OverlapDetector.TargetPosOffsetThanMe;
            var point = new MapGridPoint(rule.GridX + target.X, rule.GridY + target.Y);
            int direction = rule.OverlapDetector.OverlapDirection.FirstOrDefault();
            MapGridPoint previous;
            switch (direction)
            {
                case 0: previous = new MapGridPoint(point.X, point.Y + 1); break;
                case 1: previous = new MapGridPoint(point.X, point.Y - 1); break;
                case 2: previous = new MapGridPoint(point.X - 1, point.Y); break;
                case 3: previous = new MapGridPoint(point.X + 1, point.Y); break;
                default: throw new InvalidDataException("Unsupported original plate direction");
            }
            session.CharacterMoved(point, previous);
        }
        private static MapPuzzleEntityState[] Cold(MapPuzzleEntityState[] states) =>
            JsonUtility.FromJson<Saved>(JsonUtility.ToJson(new Saved { States = states })).States;
        private static bool Same(MapPuzzleEntityState a, MapPuzzleEntityState b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
        private static bool Same(MapPuzzleEntityState[] a, MapPuzzleEntityState[] b) => JsonUtility.ToJson(new Saved { States = a }) == JsonUtility.ToJson(new Saved { States = b });
        [Serializable] private sealed class Saved { public MapPuzzleEntityState[] States; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    }
}
