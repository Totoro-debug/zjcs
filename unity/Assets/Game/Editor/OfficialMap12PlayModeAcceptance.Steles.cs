using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        // This checks the real profile transaction and map recovery consumer.
        // It does not substitute for pointer/window/battle Play Mode acceptance.
        public static void RunSteleRuleChecks()
        {
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string repository = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(repository)
                || !Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.Combine(repository, "output/validation"))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(root))
                throw new InvalidDataException("Set the isolated output/validation workspace before stele checks.");
            var slots = new ProfileSlotCatalog(Path.Combine(root, "stele-rule-profiles-" + Guid.NewGuid().ToString("N")));
            var catalog = OfficialMap12SteleCatalog.Load();
            // Explicit expected branches from the selected original table,
            // independently of the runtime reward-preview implementation.
            var boxes = new Dictionary<int, (int item, long count)>
            {
                [1813] = (9203, 1), [2530] = (9203, 1), [3013] = (9219, 2),
                [5519] = (9219, 2), [6406] = (9205, 1), [6614] = (9205, 1), [8320] = (9220, 2)
            };
            foreach (var stele in catalog.Data.Steles)
            {
                string slot = slots.CreateSlot(), path = slots.GetProgressPath(slot);
                var baseline = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                baseline.Player.CharacterCreated = true;
                baseline.Player.CharacterName = "战斗碑验证";
                baseline.Player.InitialWeaponType = 200;
                baseline.Player.Level = 20;
                baseline.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[20];
                baseline.Player.Rank = baseline.Player.SubRank = baseline.Player.SeasonRank = 1;
                baseline.Player.Wardrobe.Profession = 1;
                baseline.Player.UnlockedMapClassIds.AddRange(new[] { 11, 12 });
                File.WriteAllText(path, JsonUtility.ToJson(baseline, true));
                var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                var service = new OfflineWorldExplorationService(session);
                byte[] before = File.ReadAllBytes(path);
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var loss = service.SettleMap12Stele(catalog, stele.PlacementId, stele.StageId, false, 0, now, out var lossAwards);
                SteleRequire(loss.Status == ProgressionResultStatus.Unchanged && lossAwards.Count == 0
                    && before.SequenceEqual(File.ReadAllBytes(path)), "Loss consumed a stele attempt or changed the profile.");
                var invalid = service.SettleMap12Stele(catalog, stele.PlacementId, 102, true, 0, now, out var invalidAwards);
                SteleRequire(invalid.Status == ProgressionResultStatus.InvalidState && invalidAwards.Count == 0
                    && before.SequenceEqual(File.ReadAllBytes(path)), "Wrong stage was accepted as a stele victory.");
                bool rejected = false;
                using (var deny = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    try { service.SettleMap12Stele(catalog, stele.PlacementId, stele.StageId, true, 5000, now, out _); }
                    catch (IOException) { rejected = true; }
                SteleRequire(rejected && before.SequenceEqual(File.ReadAllBytes(path)), "Denied profile read escaped the I/O retry contract.");
                rejected = false;
                string memoryBeforeWrite = JsonUtility.ToJson(session.Save);
                using (var deny = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    try { service.SettleMap12Stele(catalog, stele.PlacementId, stele.StageId, true, 5000, now, out _); }
                    catch (IOException) { rejected = true; }
                SteleRequire(rejected && before.SequenceEqual(File.ReadAllBytes(path))
                    && memoryBeforeWrite == JsonUtility.ToJson(session.Save), "Denied profile write changed inventory, RNG, wins or completion.");
                string worldBefore = JsonUtility.ToJson(session.Save.World);
                var expected = boxes[stele.PlacementId];
                int currency = stele.SupportTeam ? 110 : 2;
                long currencyCount = stele.SupportTeam ? 3 : 120;
                var preview = catalog.RewardPreview(stele);
                SteleRequire(preview.Count == 2 && preview[expected.item] == expected.count && preview[currency] == currencyCount,
                    "Original fixed preview or stele-rank condition differs: " + stele.PlacementId);
                var result = service.SettleMap12Stele(catalog, stele.PlacementId, stele.StageId, true, 5000, now, out var awards);
                SteleRequire(result.IsApplied && awards.Count == 2
                    && awards[expected.item] == (long)Math.Floor(expected.count * 1.5)
                    && awards[currency] == (long)Math.Floor(currencyCount * 1.5), "Confirmed prop-29 settlement differs.");
                SteleRequire(session.Save.World.StageWinCounts.Single(v => v.StageId == stele.StageId).Count == 1
                    && session.Save.World.CompletedEntityTags.Contains(OfficialMap12SteleCatalog.CompletionTag(stele.PlacementId))
                    && session.Save.World.CompletedEntityTags.Contains("map12:entity:" + stele.PlacementId),
                    "Stele victory and completion were not saved with its awards.");
                // A stele is a stage, not an opened chest. Reject accidental
                // reuse of the chest-count overload of the transaction API.
                var oldWorld = JsonUtility.FromJson<OfflineWorldProgress>(worldBefore);
                SteleRequire(oldWorld.OpenChestCounts.Count == session.Save.World.OpenChestCounts.Count
                    && oldWorld.OpenChestCounts.All(v => session.Save.World.OpenChestCounts.Any(
                        current => current.ChestId == v.ChestId && current.Count == v.Count)),
                    "Stele victory changed unrelated chest counters.");
                byte[] committed = File.ReadAllBytes(path);
                session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                var repeat = new OfflineWorldExplorationService(session).SettleMap12Stele(catalog, stele.PlacementId,
                    stele.StageId, true, 5000, now, out var duplicate);
                SteleRequire(repeat.Status == ProgressionResultStatus.Unchanged && duplicate.Count == 0
                    && committed.SequenceEqual(File.ReadAllBytes(path)), "Cold duplicate changed inventory, RNG or win count.");
                var host = new GameObject("Map12SteleRecoveryProbe").AddComponent<OfficialMainlineMapRuntime>();
                try
                {
                    var map = Activator.CreateInstance(typeof(OfficialMainlineMapRuntime).Assembly.GetType(
                        "Zjcs.Presentation.World.Map10LocalSave", true));
                    var mapType = map.GetType();
                    mapType.GetField("slotId").SetValue(map, slot);
                    OfficialMainlineMapInspection.Set(host, "mapClassId", 12);
                    OfficialMainlineMapInspection.Set(host, "progressionSave", session);
                    OfficialMainlineMapInspection.Set(host, "localSave", map);
                    OfficialMainlineMapInspection.Set(host, "map12Steles", catalog);
                    SteleRequire((int)OfficialMainlineMapInspection.Call(host, "GetMap12StelePeriodicityCount", stele.PeriodicityGroupId) == 1,
                        "Native stele count ignored a committed profile before the map-file recovery.");
                    int untouchedGroup = catalog.Data.Steles.First(v => v.PeriodicityGroupId != stele.PeriodicityGroupId).PeriodicityGroupId;
                    SteleRequire((int)OfficialMainlineMapInspection.Call(host, "GetMap12StelePeriodicityCount", untouchedGroup) == 0,
                        "Native stele count completed an unrelated periodicity group.");
                    OfficialMainlineMapInspection.Call(host, "RecoverMap12SteleCompletions");
                    int[] MapValues(string field) => (int[])mapType.GetField(field).GetValue(map);
                    SteleRequire(MapValues("completedEntityPlacements").Contains(stele.PlacementId)
                        && MapValues("completedStages").Contains(stele.StageId)
                        && MapValues("completedPeriodicityGroups").Contains(stele.PeriodicityGroupId), "Profile/map commit gap resurrected a stele attempt.");
                    OfficialMainlineMapInspection.Call(host, "RecoverMap12SteleCompletions");
                    SteleRequire(MapValues("completedPeriodicityGroups").Count(v => v == stele.PeriodicityGroupId) == 1,
                        "Repeated cold recovery duplicated a periodicity group.");
                    SteleRequire((int)OfficialMainlineMapInspection.Call(host, "GetMap12StelePeriodicityCount", stele.PeriodicityGroupId) == 1,
                        "Recovered stele profile/map flags counted one completion twice.");
                }
                finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
            }
            Debug.Log("Map12 stele rule checks passed: seven official award branches, loss/rejection/retry, cold once-only rewards and profile/map recovery. Pointer and real battle acceptance remain separate.");
        }

        private static void SteleRequire(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
        }
    }
}
