using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Progression
{
    public static class OfflineWorldExplorationValidation
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "zjcs-explore-" + Guid.NewGuid().ToString("N"));
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slot = slots.CreateSlot();
                var session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
                var catalog = OfficialWorldExplorationCatalog.Load();
                var service = new OfflineWorldExplorationService(session, catalog);
                Require(session.InitializeMap(11, new[] { 11 }).IsApplied, "Map region fixture");
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var allowed = new HashSet<int> { 51 };
                var context = new OfflineExploreContext { MapClassId = 11, Area = 11, Region = 11, X = 15, Y = 20,
                    Level = 31, SubRank = 1, RegionLevel = 20, RegionSubRank = 1, MapCanExplore = true, IsAlive = true,
                    IsActivityOpen = (type, id, ahead) => true, IsGroupCandidate = (group, save) => allowed.Contains(group.Group),
                    FindSpawn = (entity, distance, existing) => new OfflineExplorePoint(15 + existing.Count, 20),
                    ResolveRandomItem = (item, count, save) => new[] { new OfflineItemStack { ItemId = 1, Count = count } } };
                Require(!service.CanExplore(context), "Native function gate rejects a new player");
                var fixture = session.ApplyInventoryTransaction("fixture", save =>
                {
                    save.Player.Level = 31; save.Player.SubRank = 1;
                    save.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = 11, Region = 11 });
                    foreach (int item in new[] { 115, 2011 })
                        Require(OfflineInventoryOperations.Grant(save.Player, item, 1000, now).IsApplied, "Fixture item " + item);
                    Require(OfflineInventoryOperations.Grant(save.Player, 4016, 1, now).IsApplied, "Function item");
                });
                Require(fixture.IsApplied, "Isolated exploration fixture: " + fixture.Message);
                Require(service.CanExplore(context) && service.MaxTimes(now) == 10, "Native unlock and ten-times bound");
                Require(service.Explore(context, 1, 2011, "premature-token", now, out _).Status == ProgressionResultStatus.NotReady,
                    "First guide cannot consume a token");
                Require(service.Explore(context, 1, 0, "first", now, out var first).IsApplied
                    && first.Entities.Single().EntityClassId == 8201 && first.GuideRewardGranted
                    && session.InventoryCount(115) == 999 && session.InventoryCount(56) == 1,
                    "First success spends one fruit, creates native guide chest and grants fixed award five once");
                Require(first.Entities[0].Level == 31 && first.Entities[0].SubRank == 1
                    && first.Entities[0].ExpiresAt == now + 300, "Explore source snapshots native level, rank and five-minute lifetime");
                string after = JsonUtility.ToJson(session.Save);
                Require(service.Explore(context, 1, 0, "first", now, out var duplicate).Status == ProgressionResultStatus.Unchanged
                    && duplicate.Entities.Count == 0 && JsonUtility.ToJson(session.Save) == after, "A replayed request cannot reroll or duplicate awards");
                session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
                service = new OfflineWorldExplorationService(session, catalog);
                Require(session.Save.World.Exploration.Entities.Single().ExpiresAt == now + 300, "Cold reload restores generated entities");
                int serial = session.Save.World.Exploration.Entities.Single().Serial;
                after = JsonUtility.ToJson(session.Save);
                var originalRewards = catalog.Data.rewardRules.Select(v => v.IdList.ToArray()).ToArray();
                foreach (var rule in catalog.Data.rewardRules) rule.IdList = rule.IdList.Select(_ => 0).ToArray();
                Require(service.CollectEntity(serial, context, now, out var rejected).Status == ProgressionResultStatus.InvalidState
                    && rejected.Count == 0 && JsonUtility.ToJson(session.Save) == after,
                    "Failed reward settlement restores inventory, entity, receipt and random state");
                for (int i = 0; i < originalRewards.Length; i++) catalog.Data.rewardRules[i].IdList = originalRewards[i];
                var collected = service.CollectEntity(serial, context, now, out var chestAwards);
                Require(collected.IsApplied && chestAwards.Count > 0
                    && session.Save.World.Exploration.Entities.Single().Completed,
                    "Guide chest skips approved empty reward references and settles remaining native rules: " + collected.Status + " " + collected.Message);
                after = JsonUtility.ToJson(session.Save);
                Require(service.CollectEntity(serial, context, now, out var repeatedAwards).Status == ProgressionResultStatus.Unchanged
                    && repeatedAwards.Count == 0 && JsonUtility.ToJson(session.Save) == after, "Claiming a chest twice never duplicates its awards");
                allowed.Clear(); allowed.Add(52);
                int calls = 0;
                context.FindSpawn = (entity, distance, existing) => ++calls == 2 ? (OfflineExplorePoint?)null : new OfflineExplorePoint(16, 20);
                after = JsonUtility.ToJson(session.Save);
                Require(service.Explore(context, 2, 0, "rollback", now, out var failed).Status == ProgressionResultStatus.NotReady
                    && failed.Entities.Count == 0 && failed.History.Count == 0 && JsonUtility.ToJson(session.Save) == after,
                    "No free spawn cell rejects the request and rolls back the entire batch, cost, RNG, pity, replacements and outward result");
                context.FindSpawn = (entity, distance, existing) => new OfflineExplorePoint(16 + existing.Count, 20);
                Require(service.Explore(context, 2, 0, "two", now, out var two).IsApplied
                    && two.History.Count == 2 && session.Save.World.Exploration.Entities.Count(v => v.Group == 52) == 1
                    && session.InventoryCount(56) == 1 && session.InventoryCount(115) == 997,
                    "LastMapRefreshOnly replaces its own group and leaves the global guide award claimed");
                allowed.Clear(); allowed.UnionWith(new[] { 52, 53, 65 });
                Require(session.ApplyInventoryTransaction("pity-fixture", save =>
                {
                    var records = save.World.Exploration.Records;
                    records.Single(v => v.Key == "0" && v.Index == 3).KeepMissCount = 109;
                    records.Single(v => v.Key == "0" && v.Index == 4).KeepMissCount = 1999;
                    records.Single(v => v.Key == "0" && v.Index == 5).KeepMissCount = 19999;
                }).IsApplied, "Native competing guarantees fixture");
                Require(service.Explore(context, 1, 0, "pity", now, out var pity).IsApplied
                    && pity.History.Single().Guaranteed && pity.History[0].ExploreGroup == 6,
                    "Simultaneous native guarantees follow priority and nearest remaining count");
                var hit = session.Save.World.Exploration.Records.Single(v => v.Key == "0" && v.Index == 5);
                Require(hit.KeepMissCount == 0 && hit.FireGuaranteedCount == 1
                    && session.Save.World.Exploration.Records.Single(v => v.Key == "0" && v.Index == 4).KeepMissCount == 2000,
                    "Native recorder updates hit debt and every other guaranteed group");
                allowed.Clear(); allowed.Add(54);
                Require(service.Explore(context, 1, 0, "coop", now, out var coop).IsApplied
                    && coop.Entities.Single().EntityClassId == 7050 && coop.Entities[0].Level == 20,
                    "Cooperative chest retains native region level and activity candidate");
                int coopSerial = coop.Entities.Single().Serial;
                Require(service.CollectEntity(coopSerial, context, now, out var coopAwards).IsApplied && coopAwards.Count > 0
                    && session.Save.World.Exploration.Entities.Single(v => v.Serial == coopSerial).Active(now),
                    "Confirmed solo cooperative opening retains the original opened non-destroying chest until expiry");
                Require(session.ApplyInventoryTransaction("coop-cap", save =>
                    save.World.Exploration.GroupCounts.Single(v => v.Group == 54).Count = 20).IsApplied, "Lifetime cap fixture");
                Require(service.Candidates(7, context).Count == 0, "Cooperative chest cumulative produced limit is twenty");
                context.IsActivityOpen = (type, id, ahead) => false;
                Require(service.Candidates(7, context).Count == 0, "Closed native activity contributes no cooperative candidate");
                Require(service.Cleanup(now + 300).IsApplied && session.Save.World.Exploration.Entities.Count == 0
                    && session.Save.World.Exploration.GroupCounts.Single(v => v.Group == 54).Count == 20,
                    "Offline expiry removes entities while retaining lifetime produced counts");
                Require(service.Cleanup(now + 181L * 86400).IsApplied && session.Save.World.Exploration.History.Count == 0,
                    "History expires after the original 180 days");
                VerifyLostGoddess(session, service, catalog, now);
                var old = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(session.Save));
                old.World.Exploration = null;
                old.Player.AppliedInventoryEventIds.Remove("fixed_award:5");
                old.Player.AppliedInventoryEventIds.Add("map11:" + slot + ":fixed-reward:5:0");
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(old));
                session = OfflineMainlineProgressSession.Open(slots, slot, OfficialMainlineTaskCatalog.LoadDefault());
                Require(session.Save.World.Exploration.TotalCount == 0 && session.Save.Player.AppliedInventoryEventIds.Contains("fixed_award:5")
                    && session.InventoryCount(56) == 1, "Legacy story award migrates to the global receipt without granting again");
                Debug.Log("World exploration validation passed: native gates, guide, atomic batch rollback, saved levels, expiry, competing guarantees, lifetime cap, legacy receipt, lost goddess level/skill/property transactions, cap filtering, replay/cold read and 08:00 daily boundary.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        private static void VerifyLostGoddess(OfflineMainlineProgressSession session, OfflineWorldExplorationService service,
            OfficialWorldExplorationCatalog catalog, long now)
        {
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var character = assets.Load();
            var skills = assets.LoadSkillRules();
            var definition = skills.Skills.First(v => v.MaximumRank > 1);
            var group = catalog.Data.entityGroups.First(v => v.Entitys.Contains(8364));
            var settings = character.Rows("game_settings").Single();
            var weights = OfficialWorldExplorationCatalog.Object(settings["LostGoddnessAwardWeights"]);
            var originalWeights = weights.ToDictionary(v => v.Key, v => v.Value);
            try
            {
                foreach (int type in new[] { 2, 3, 4 })
                {
                    foreach (string key in weights.Keys.ToArray()) weights[key] = key == type.ToString() ? 100 : 0;
                    int serial = session.Save.World.Exploration.NextSerial;
                    Require(session.ApplyInventoryTransaction("lost-fixture:" + serial, save =>
                    {
                        save.Player.Level = 31;
                        save.Player.SubRank = character.LevelLimits.Where(v => v.PlayerLevel > 32).Min(v => v.SubRank);
                        save.Player.Experience = character.CumulativeLevelExperience[31] + 17;
                        save.Player.Skills.Skills.Clear();
                        save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill { ClassId = definition.ClassId, Rank = 1 });
                        save.World.Exploration.Entities.Add(new OfflineExploreEntity { Serial = save.World.Exploration.NextSerial++,
                            MapClassId = 11, Area = 11, Region = 11, X = 20, Y = 20, Group = group.Group, Variant = group.Id,
                            EntityClassId = 8364, State = catalog.Parameters(8364).PublicDefaultState, CreatedAt = now, ExpiresAt = now + 300 });
                    }).IsApplied, "Lost goddess isolated fixture");
                    var transaction = service.AwardLostGoddess(serial, 11, now, character, skills, out var award);
                    Require(transaction.IsApplied && award.Type == type
                        && session.Save.World.Exploration.Entities.Single(v => v.Serial == serial).Completed,
                        "Lost goddess award and native completion save atomically: " + transaction.Message);
                    if (type == 2) Require(award.TargetRank == 2
                        && session.Save.Player.Skills.Skills.Single().Rank == 2, "An owned skill upgrades one rank without consuming materials");
                    if (type == 3) Require(session.Save.Player.Level == 32
                        && session.Save.Player.Experience == character.CumulativeLevelExperience[32] + 17,
                        "The character gains one level and retains within-level experience");
                    if (type == 4) Require(award.Properties.Length > 0 && award.Properties.All(v => v.PropId == 410 && v.Level == 1)
                        && session.Save.Player.CharacterProperties.ExtraProperties.Count(v => v.SourceId == "world-explore-lost-goddess:" + serial) == award.Properties.Length,
                        "Original weighted property group is permanently recorded once");
                    string after = JsonUtility.ToJson(session.Save);
                    Require(service.AwardLostGoddess(serial, 11, now, character, skills, out _).Status == ProgressionResultStatus.Unchanged
                        && JsonUtility.ToJson(session.Save) == after, "A repeated lost goddess callback cannot reroll or duplicate awards");
                    session.Reload();
                    Require(session.Save.World.Exploration.Entities.Single(v => v.Serial == serial).LostGoddessAward.Type == type,
                        "Lost goddess response survives a cold JSON read");
                }
                foreach (var value in originalWeights) weights[value.Key] = value.Value;
                int capped = session.Save.World.Exploration.NextSerial;
                Require(session.ApplyInventoryTransaction("lost-capped:" + capped, save =>
                {
                    save.Player.Level = OfflineCharacterExperience.BaseLevelLimit(save.Player.SubRank, character.LevelLimits);
                    save.Player.Experience = character.CumulativeLevelExperience[save.Player.Level];
                    save.Player.Skills.Skills.Single().Rank = definition.MaximumRank;
                    save.World.Exploration.Entities.Add(new OfflineExploreEntity { Serial = save.World.Exploration.NextSerial++,
                        MapClassId = 11, Area = 11, Region = 11, X = 20, Y = 20, Group = group.Group, Variant = group.Id,
                        EntityClassId = 8364, State = catalog.Parameters(8364).PublicDefaultState, CreatedAt = now, ExpiresAt = now + 300 });
                }).IsApplied, "Lost goddess capped fixture");
                Require(service.AwardLostGoddess(capped, 11, now, character, skills, out var fallback).IsApplied && fallback.Type == 4,
                    "Capped level and skill categories are removed before weighted selection");
                var reset = DateTime.Now.Date.AddHours(8);
                long boundary = new DateTimeOffset(reset).ToUnixTimeSeconds();
                Require(session.ApplyInventoryTransaction("explore-daily-boundary", save =>
                {
                    save.World.Exploration.Day = reset.AddDays(-1).ToString("yyyyMMdd");
                    save.World.Exploration.DailyCount = 1;
                }).IsApplied && service.DailyCount(boundary - 1) == 1 && service.DailyCount(boundary) == 0,
                    "Native local 08:00 daily reset preserves the previous day until its exact boundary");
            }
            finally { foreach (var value in originalWeights) weights[value.Key] = value.Value; }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
