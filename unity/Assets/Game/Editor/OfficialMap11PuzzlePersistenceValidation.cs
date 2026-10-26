using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap11PuzzlePersistenceValidation
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Type Runtime = typeof(OfficialMainlineMapRuntime);
        private static readonly Type Entity = Runtime.GetNestedType("Map10RuntimeEntity", BindingFlags.NonPublic);

        public static void Run()
        {
            try
            {
                OfficialMap11PuzzleValidation.Validate();
                Validate();
                ProjectTools.Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Validate()
        {
            string root = Path.Combine(Path.GetTempPath(), "zjcs-map11-persistence-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var initial = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            initial.Player.CharacterCreated = true;
            initial.Player.CharacterName = "机关存档验证";
            initial.Player.InitialWeaponType = 200;
            initial.Player.Wardrobe.Profession = 1;
            initial.Player.UnlockedMapClassIds.Add(11);
            OfflineInventoryOperations.Grant(initial.Player, 325, 1, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(initial));
            OfficialMainlineMapRuntime runtime = null;
            try
            {
                runtime = Open(root, slot);
                WriteIntent(runtime, "interrupted-before-payment");
                Release(ref runtime);
                runtime = Open(root, slot);
                Require(Count(runtime) == 1 && !Puzzle(runtime).IsUnlocked(2306), "Unpaid interrupted solve must not unlock or consume");
                Require(!HasIntent(runtime), "Unpaid intent cleared on cold load");

                WriteIntent(runtime, "interrupted-after-payment");
                var save = (OfflineMainlineProgressSession)Get(runtime, "progressionSave");
                Require(save.ApplyInventoryTransaction("interrupted-after-payment", progress =>
                {
                    Require(OfflineInventoryOperations.Consume(progress.Player, 325, 1,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied, "Payment fixture");
                }).Status == ProgressionResultStatus.Applied, "Durable payment receipt");
                Release(ref runtime);
                runtime = Open(root, slot);
                Require(Count(runtime) == 0 && Puzzle(runtime).IsUnlocked(2306) && Puzzle(runtime).State(2307) == 1,
                    "Paid interruption must recover original solve without a second deduction");
                Require(!HasIntent(runtime), "Paid intent cleared after map commit");
                Release(ref runtime);
                runtime = Open(root, slot);
                Require(Count(runtime) == 0 && Puzzle(runtime).IsUnlocked(2306) && !HasIntent(runtime),
                    "Completed solve remains stable on repeated cold loads");
                save = (OfflineMainlineProgressSession)Get(runtime, "progressionSave");
                Require(save.Save.Player.AppliedInventoryEventIds.Count(value => value == "interrupted-after-payment") == 1,
                    "Exactly one inventory payment receipt");
                OfficialMap11StoryBranchValidation.Validate(runtime);
                Release(ref runtime);
                runtime = Open(root, slot);
                OfficialMap11StoryBranchValidation.ValidateColdLoad(runtime);
                Debug.Log("Map11 puzzle persistence validated: interruption before payment cancels; after payment recovers; repeated cold loads preserve one payment and one unlock.");
            }
            finally
            {
                Release(ref runtime);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(root);
                if (!target.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(target).StartsWith("zjcs-map11-persistence-", StringComparison.Ordinal))
                    throw new InvalidDataException("Validation cleanup must remain inside its isolated temporary directory.");
                Directory.Delete(target, true);
            }
        }

        private static OfficialMainlineMapRuntime Open(string root, string slot)
        {
            var host = new GameObject("Map11PersistenceValidation");
            host.SetActive(false);
            OfficialMainlineMapRuntime runtime = host.AddComponent<OfficialMainlineMapRuntime>();
            Set(runtime, "closed", true);
            Set(runtime, "mapClassId", 11);
            Set(runtime, "slotId", slot);
            Set(runtime, "persistentDataPath", root);
            Set(runtime, "mapSavePath", Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "map-11-save.json"));
            Call(runtime, "LoadOfficialData");
            Call(runtime, "InitializeMap");
            Call(runtime, "LoadLocalProgress");
            var map = (OfficialWorldMapSession)Get(runtime, "session");
            IDictionary entities = (IDictionary)Get(runtime, "mapEntities");
            foreach (MapPuzzleRule rule in (MapPuzzleRule[])Get(runtime, "puzzleRules"))
            {
                object entity = JsonUtility.FromJson("{\"PlacementId\":" + rule.PlacementId + ",\"EntityClassId\":"
                    + rule.EntityClassId + ",\"State\":" + rule.InitialState + ",\"StageIds\":[]}", Entity);
                entities.Add(rule.PlacementId, entity);
            }
            Set(runtime, "storyContextEntity", entities[2307]);
            Call(runtime, "InitializePuzzles");
            return runtime;
        }

        private static void WriteIntent(OfficialMainlineMapRuntime runtime, string receipt)
        {
            object local = Get(runtime, "localSave");
            FieldInfo operation = local.GetType().GetField("pendingPuzzleOperation");
            operation.SetValue(local, JsonUtility.FromJson("{\"placementId\":2307,\"subStateIndex\":0,\"inventoryReceipt\":\""
                + receipt + "\"}", operation.FieldType));
            local.GetType().GetField("hasPendingPuzzleOperation").SetValue(local, true);
            Call(runtime, "SaveLocalProgress");
        }
        private static bool HasIntent(OfficialMainlineMapRuntime runtime)
        {
            object local = Get(runtime, "localSave");
            return (bool)local.GetType().GetField("hasPendingPuzzleOperation").GetValue(local);
        }
        private static long Count(OfficialMainlineMapRuntime runtime) =>
            ((OfflineMainlineProgressSession)Get(runtime, "progressionSave")).Save.Player.Inventory.FirstOrDefault(value => value.ItemId == 325)?.Count ?? 0;
        private static OfflineMapPuzzleSession Puzzle(OfficialMainlineMapRuntime runtime) => (OfflineMapPuzzleSession)Get(runtime, "puzzles");
        private static void Release(ref OfficialMainlineMapRuntime runtime)
        {
            if (runtime != null) UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            runtime = null;
        }
        private static void Set(object target, string name, object value) => OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)target, name, value);
        private static object Get(object target, string name) => OfficialMainlineMapInspection.Get((OfficialMainlineMapRuntime)target, name);
        private static void Call(object target, string name) => OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)target, name, null);
        private static void Require(bool value, string name) { if (!value) throw new InvalidDataException(name); }
    }
}
