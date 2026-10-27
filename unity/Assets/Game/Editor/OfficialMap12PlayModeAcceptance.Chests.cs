using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunRuntimeChests()
        {
            SessionState.SetBool(Key + ".runtimeTerminals", false);
            SessionState.SetBool(Key + ".runtimeSpatial", false);
            SessionState.SetBool(Key + ".runtimeDialogue", false);
            RunCore(false, false, 12, true, verifyChests: true);
        }

        public static void ImportAndRunRuntimeChests()
        {
            try { OfficialMap11UiTools.ImportForMap(12); RunRuntimeChests(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void ChestRequire(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
        }

        private static IEnumerator VerifyRuntimeChestSteps(string root, string slot, Transform canvas, string output)
        {
            var slots = new ProfileSlotCatalog(root);
            string profilePath = slots.GetProgressPath(slot), mapPath = Path.Combine(slots.GetSlotDirectory(slot), "map-12-save.json");
            var catalog = OfficialMap12ChestCatalog.Load();
            VerifyAllChestRewardTransactions(slots, File.ReadAllText(profilePath), catalog);
            // Isolate explicit reward-write rejection from the independent
            // timed exploration cleanup, which also reads this same file.
            OfficialMainlineMapInspection.Set(map12Runtime, "explorationRefreshAt", long.MaxValue);
            foreach (int id in new[] { 301, 3104, 2201, 8110, 8801 })
            {
                // Supply previous region progress and an initial nearby stand;
                // the actual pointer must navigate to the native interaction cell.
                foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                    RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
                RuntimeCall("RefreshEntityActivity");
                OfficialMap12PlacedChest chest = catalog.Find(id);
                var location = new MapGridPoint(chest.X, chest.Y);
                var world = RuntimeField<OfficialWorldMapSession>("session");
                MapGridPoint stand = new[] { new MapGridPoint(chest.X, chest.Y - 1), new MapGridPoint(chest.X - 1, chest.Y),
                    new MapGridPoint(chest.X, chest.Y + 1), new MapGridPoint(chest.X + 1, chest.Y) }.First(world.Movement.IsFit);
                RuntimeCall("SetAvatarPosition", id == 301 || id == 3104 ? location : stand);
                string tag = OfficialMap12ChestCatalog.CompletionTag(id);
                if (id == 301)
                {
                    byte[] before = File.ReadAllBytes(profilePath);
                    using (var deny = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        IEnumerator input = ClickTerminalMapEntity(id);
                        while (input.MoveNext()) yield return input.Current;
                    }
                    ChestRequire(before.SequenceEqual(File.ReadAllBytes(profilePath)), "Denied chest profile commit changed the save.");
                    ChestRequire(map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(id).activeSelf,
                        "Denied chest reward removed the chest.");
                }
                byte[] mapBefore = File.ReadAllBytes(mapPath);
                using (var deny = id == 3104 ? new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.None) : null)
                {
                    IEnumerator input = ClickTerminalMapEntity(id);
                    while (input.MoveNext()) yield return input.Current;
                    float deadline = Time.realtimeSinceStartup + 6f;
                    while (!RuntimeField<OfflineMainlineProgressSession>("progressionSave").Save.World.CompletedEntityTags.Contains(tag)
                        && Time.realtimeSinceStartup < deadline) yield return null;
                    ChestRequire(RuntimeField<OfflineMainlineProgressSession>("progressionSave").Save.World.CompletedEntityTags.Contains(tag),
                        "Actual chest pointer did not award native chest " + id + "; player=" + RuntimeField<MapGridPoint>("currentGrid")
                        + "; target=" + location);
                    ChestRequire(!map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(id).activeSelf,
                        "Awarded chest remained visible: " + id);
                    if (id == 3104)
                    {
                        ChestRequire(RuntimeField<bool>("map12ChestProgressPending"), "Map write failure discarded pending chest progress.");
                        // The profile owns reward+completion; terminate before the
                        // secondary map can retry, then reconstruct it on cold load.
                        RuntimeCall("CloseMap");
                    }
                }
                if (id == 3104) ChestRequire(mapBefore.SequenceEqual(File.ReadAllBytes(mapPath)), "Denied chest map write changed file bytes.");
                if (id == 8110)
                {
                    yield return new WaitForSecondsRealtime(.25f);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "chest-8110-award.png"));
                    yield return new WaitForSecondsRealtime(.3f);
                }
                var committed = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(profilePath));
                long rng = committed.Player.InventoryRandomState;
                string inventory = JsonUtility.ToJson(committed.Player);
                if (id != 3104) RuntimeCall("CloseMap");
                yield return null;
                GameObject avatar = Zjcs.Presentation.CharacterCreation.CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
                map12Runtime = new GameObject("Map12ChestColdLoad").AddComponent<OfficialMainlineMapRuntime>();
                map12Runtime.MapMusicVolume = () => 0f; map12Runtime.MapVoiceVolume = () => 0f;
                map12Runtime.Begin(12, slot, root, canvas, avatar, name, null);
                OfficialMainlineMapInspection.Set(map12Runtime, "explorationRefreshAt", long.MaxValue);
                yield return new WaitForSecondsRealtime(.5f);
                var reloaded = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                ChestRequire(!map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(id).activeSelf,
                    "Cold load resurrected claimed chest: " + id);
                var service = new OfflineWorldExplorationService(reloaded);
                var result = service.CollectMap12Chest(catalog, id, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out var duplicate);
                ChestRequire(result.Status == ProgressionResultStatus.Unchanged && duplicate.Count == 0
                    && reloaded.Save.Player.InventoryRandomState == rng && JsonUtility.ToJson(reloaded.Save.Player) == inventory,
                    "Cold repeat changed rewards/RNG/counters: " + id);
            }
            Debug.Log("Map12 chest checks passed: 83 reward transactions, 5 actual pointer collections, profile rejection and committed-reward/map-failure cold recovery.");
        }

        private static void VerifyAllChestRewardTransactions(ProfileSlotCatalog slots, string source, OfficialMap12ChestCatalog catalog)
        {
            string slot = slots.CreateSlot();
            File.WriteAllText(slots.GetProgressPath(slot), source);
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var service = new OfflineWorldExplorationService(session);
            foreach (var chest in catalog.Data.chests)
            {
                var level = chest.RewardLevel(20, 1, false);
                ChestRequire(level.level == (chest.PlacementId == 8110 ? 70 : chest.RegionLevel)
                    && level.subRank == (chest.PlacementId == 8110 ? 7 : chest.RegionSubRank), "Original chest level/confirmed rank inheritance changed.");
                ChestRequire(chest.RewardLevel(20, 1, true) == (20, 1), "Public chest did not use original player level branch.");
                var result = service.CollectMap12Chest(catalog, chest.PlacementId, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out var awards);
                ChestRequire(result.IsApplied && awards.Count > 0, "Original placed chest reward rules failed: " + chest.PlacementId + "; " + result.Message);
                if (chest.PlacementId == 6401) ChestRequire(awards.TryGetValue(61925, out long count) && count >= 1, "Lo award 782 was not appended.");
                byte[] before = File.ReadAllBytes(slots.GetProgressPath(slot));
                var repeat = service.CollectMap12Chest(catalog, chest.PlacementId, false, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out var duplicate);
                ChestRequire(repeat.Status == ProgressionResultStatus.Unchanged && duplicate.Count == 0
                    && before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "Duplicate chest wrote inventory or RNG.");
            }
            Debug.Log("Map12 all 83 original placed chests have durable, nonduplicate reward receipts including Lo override 782 and absolute-level chest 8110.");
        }
    }
}
