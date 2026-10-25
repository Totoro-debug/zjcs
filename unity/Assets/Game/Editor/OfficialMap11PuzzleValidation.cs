using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Editor
{
    public static class OfficialMap11PuzzleValidation
    {
        public static void Validate()
        {
            var data = JsonUtility.FromJson<Definitions>(Resources.Load<TextAsset>("Map11/stories").text);
            MapPuzzleRule[] rules = data.puzzleRules;
            Require(rules != null && rules.Count(rule => rule.TerminalLockIds.Length > 0) == 7, "Seven original terminals");
            MapPuzzleRule[] goals = rules.Where(rule => (rule.EntityClassId == 6530 || rule.EntityClassId == 6533)
                && (rule.LockType == 2 || rule.LockType == 7)).ToArray();
            Require(goals.Length == 9, "Nine original puzzle groups");
            MapPuzzleRule dangling = rules.Single(rule => rule.PlacementId == 413);
            Require(dangling.OtherStates[0].StageDatas[0].LoEntId == 423
                && dangling.OfflineIgnoredMissingStateTargets.SequenceEqual(new[] { 423 })
                && !rules.Any(rule => rule.PlacementId == 423), "Preserve original missing target and explicit offline exception");
            MapPuzzleRule[] unexpected = JsonUtility.FromJson<Definitions>(JsonUtility.ToJson(new Definitions { rules = rules })).rules;
            unexpected.Single(rule => rule.PlacementId == 413).OtherStates[0].StageDatas[0].LoEntId = 424;
            bool rejected = false;
            try { new OfflineMapPuzzleSession(unexpected, null); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Do not silently skip other unresolved state targets");
            foreach (MapPuzzleRule terminal in rules.Where(rule => rule.TerminalLockIds.Length > 0))
            {
                var session = new OfflineMapPuzzleSession(rules, null);
                session.RefreshLocks();
                Require(terminal.TerminalLockIds.All(id => !session.IsUnlocked(id)), "Initially locked terminal " + terminal.PlacementId);
                session.RunStage(terminal.PlacementId, 0);
                Require(terminal.TerminalLockIds.All(session.IsUnlocked), "Automatic solve " + terminal.PlacementId);
                Require(session.State(terminal.PlacementId) == 1, "Ordered terminal state result " + terminal.PlacementId);
                foreach (int id in terminal.TerminalLockIds)
                    Require(rules.Single(rule => rule.PlacementId == id).SpawnIds.All(session.IsSpawned), "Unlock spawn results " + id);
                var restored = new OfflineMapPuzzleSession(rules, JsonUtility.FromJson<Saved>(JsonUtility.ToJson(new Saved { states = session.Save() })).states);
                Require(terminal.TerminalLockIds.All(restored.IsUnlocked) && restored.State(terminal.PlacementId) == 1,
                    "Cold puzzle state " + terminal.PlacementId);
            }
            var linked = new OfflineMapPuzzleSession(rules, null);
            linked.RunStage(2302, 0);
            Require(linked.State(2302) == 2 && linked.State(2303) == 2 && linked.State(2304) == 0 && linked.State(2305) == 1,
                "Touch result followed by linked client substage notifications");
            linked.RunStage(2307, 1);
            foreach (int id in new[] { 2302, 2303, 2304, 2305 })
                Require(linked.State(id) == rules.Single(rule => rule.PlacementId == id).InitialState, "Reset original stone state " + id);
            foreach (MapPuzzleRule goal in goals.Where(rule => rule.LockType == 7))
            {
                var session = new OfflineMapPuzzleSession(rules, null);
                foreach (MapPuzzleCondition target in goal.LockConditions)
                {
                    MapPuzzleRule plate = rules.Single(rule => rule.PlacementId == target.PlacementId);
                    var position = new MapGridPoint(plate.GridX, plate.GridY);
                    session.CharacterMoved(position, new MapGridPoint(position.X - 1, position.Y));
                }
                Require(session.IsUnlocked(goal.PlacementId), "Manual plate completion " + goal.PlacementId);
                MapPuzzleRule first = rules.Single(rule => rule.PlacementId == goal.LockConditions[0].PlacementId);
                int count = session.OverlapTimes(first.PlacementId);
                session.CharacterMoved(new MapGridPoint(first.GridX, first.GridY), new MapGridPoint(first.GridX - 1, first.GridY));
                Require(session.OverlapTimes(first.PlacementId) == count, "Inactive detector after unlock " + goal.PlacementId);
            }
            Debug.Log("Map11 puzzle rules validated: nine groups, seven terminals, linked stone transitions, manual step counters, spawn results, reset and cold persistence.");
        }

        [Serializable] private sealed class Saved { public MapPuzzleEntityState[] states; }
        [Serializable] private sealed class Definitions { public MapPuzzleRule[] rules, puzzleRules; }
        private static void Require(bool condition, string name) { if (!condition) throw new InvalidDataException(name); }
    }
}
