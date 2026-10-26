using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfflineWorldExplorationProgress
    {
        public int TotalCount, DailyCount, NextSerial = 1;
        public string Day = string.Empty;
        public List<OfflineExploreRecord> Records = new List<OfflineExploreRecord>();
        public List<OfflineExploreGroupCount> GroupCounts = new List<OfflineExploreGroupCount>();
        public List<OfflineExploreEntity> Entities = new List<OfflineExploreEntity>();
        public List<OfflineExploreHistory> History = new List<OfflineExploreHistory>();
    }
    [Serializable] public sealed class OfflineExploreRecord
    {
        public string Key;
        public int Index, HitCount, KeepMissCount, FireGuaranteedCount;
    }
    [Serializable] public sealed class OfflineExploreGroupCount { public int Group, Count; }
    [Serializable] public sealed class OfflineExploreExtra
    {
        public string Kind;
        public int ItemId, Count, ActivityType, ActivityId;
        public bool ExpendRandomItem;
        public int[] StageIds = Array.Empty<int>(), ActivityIds = Array.Empty<int>();
    }
    [Serializable] public sealed class OfflineExploreEntity
    {
        public int Serial, MapClassId, Area, Region, X, Y, Group, Variant, EntityClassId, State, Level, SubRank;
        public long CreatedAt, ExpiresAt;
        public bool Completed, RemoveOnComplete;
        public OfflineExploreLostGoddessAward LostGoddessAward;
        public List<OfflineExploreExtra> Extra = new List<OfflineExploreExtra>();
        public int PlacementId => checked(1000000000 + Serial);
        public bool Active(long now) => (!Completed || !RemoveOnComplete) && (ExpiresAt == 0 || ExpiresAt > now);
    }
    [Serializable] public sealed class OfflineExploreHistory
    {
        public string EventId;
        public int MapClassId, Area, Region, X, Y, TokenItemId, ExploreGroup, EntityGroup, Variant, Count;
        public long CreatedAt;
        public bool Guaranteed;
        public int[] EntitySerials;
    }
    public readonly struct OfflineExplorePoint
    {
        public readonly int X, Y;
        public OfflineExplorePoint(int x, int y) { X = x; Y = y; }
    }
    public sealed class OfflineExploreContext
    {
        public int MapClassId, MapGroupId, Area, Region, X, Y, Level, SubRank, RegionLevel, RegionSubRank, RegionCode;
        public int[] DropItems;
        public bool MapCanExplore, RegionForbidsExplore, IsAlive;
        public Func<int, int, int, bool> IsActivityOpen;
        public Func<OfflineMainlineSave, int, bool> HasOrUsedItem;
        public Func<OfficialExploreEntityGroup, OfflineMainlineSave, bool> IsGroupCandidate;
        public Func<int, int, IReadOnlyList<OfflineExploreEntity>, OfflineExplorePoint?> FindSpawn;
        public Func<int, int, OfflineMainlineSave, IReadOnlyList<OfflineItemStack>> ResolveRandomItem;
    }
    public sealed class OfflineExploreResult
    {
        public readonly List<OfflineExploreEntity> Entities = new List<OfflineExploreEntity>();
        public readonly List<OfflineExploreHistory> History = new List<OfflineExploreHistory>();
        public bool GuideRewardGranted;
    }

    /// <summary>Local OperPlayerWorldExplore adapter. Costs, original weighted
    /// selection/recorder, generated EC data and first-success reward commit once.</summary>
    public sealed partial class OfflineWorldExplorationService
    {
        private readonly OfflineMainlineProgressSession session;
        public OfficialWorldExplorationCatalog Catalog { get; }
        private OfficialWorldExplorationData Data => Catalog.Data;
        public OfflineWorldExplorationService(OfflineMainlineProgressSession session, OfficialWorldExplorationCatalog catalog = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            Catalog = catalog ?? OfficialWorldExplorationCatalog.Load();
        }
        public int DailyCount(long now) => session.Save.World.Exploration.Day == Day(now) ? session.Save.World.Exploration.DailyCount : 0;
        public int MaxTimes(long now) => Math.Max(0, (int)Math.Min(Math.Min(session.InventoryCount(Data.settings.FoodId),
            Data.settings.WorldExploreMaxTimes), Data.dailyLimit - DailyCount(now)));
        public bool FunctionOpen(int type, OfflineExploreContext context, OfflineMainlineSave save = null) =>
            Data.functionLimits.Where(v => v.Type == type && v.Key0 == 0 && v.Key1 == 0)
                .All(v => IsMet(v.Condition, context, save ?? session.Save));
        public bool CanExplore(OfflineExploreContext context, OfflineMainlineSave save = null)
        {
            save ??= session.Save;
            return context != null && context.MapClassId > 0 && context.Area >= 0 && context.Region >= 0 && context.MapCanExplore
                && !context.RegionForbidsExplore && FunctionOpen(8, context, save)
                && save.World.ActivatedGoddessRegions.Any(v => v.MapClassId == context.MapClassId && v.Region == context.Region);
        }
        public IReadOnlyList<OfficialExploreEntityGroup> Candidates(int exploreGroup, OfflineExploreContext context, OfflineMainlineSave save = null)
        {
            save ??= session.Save;
            var outer = Data.exploreGroups.Single(v => v.Id == exploreGroup);
            var values = new List<OfficialExploreEntityGroup>();
            foreach (int id in outer.EntityGroups)
            {
                int limit = Data.limits.SingleOrDefault(v => v.MapEntityGroupId == id)?.Limit ?? 0;
                if (limit > 0 && (save.World.Exploration.GroupCounts.SingleOrDefault(v => v.Group == id)?.Count ?? 0) >= limit) continue;
                var group = Data.entityGroups.Where(v => v.Group == id).OrderByDescending(v => v.MatchOrder)
                    .FirstOrDefault(v => IsMet(v.FilterCondition, context, save));
                if (group != null && (context.IsGroupCandidate == null || context.IsGroupCandidate(group, save))) values.Add(group);
            }
            return values;
        }
        public ProgressionResult Explore(OfflineExploreContext context, int times, int tokenItemId, string requestId,
            long now, out OfflineExploreResult result)
        {
            session.Reload();
            var output = new OfflineExploreResult();
            result = output;
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 100 || now <= 0 || context?.FindSpawn == null)
                return Failure("Invalid local exploration request.", ProgressionResultStatus.InvalidState);
            string receipt = "world-explore:" + requestId;
            if (session.Save.Player.AppliedInventoryEventIds.Contains(receipt))
                return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            if (!CanExplore(context) || !context.IsAlive || times <= 0 || times > Data.settings.WorldExploreMaxTimes
                || times > Data.dailyLimit - DailyCount(now)) return Failure("2119");
            var token = Data.tokenGroups.SingleOrDefault(v => v.ItemId == tokenItemId);
            if (token == null || tokenItemId != 0 && (times != 1 || session.Save.World.Exploration.TotalCount == 0
                || !FunctionOpen(51, context))) return Failure("explore_use_token_in_invalid_area_tip");
            long foodCost = checked((long)Data.settings.ExploreFoodCost * times);
            long tokenCost = tokenItemId == 0 ? 0 : checked((long)Data.settings.ExploreFoodTokenCost * times);
            if (session.InventoryCount(Data.settings.FoodId) < foodCost
                || tokenItemId != 0 && session.InventoryCount(tokenItemId) < tokenCost) return Failure("1153");
            ProgressionResult transaction = session.ApplyInventoryTransaction(receipt, save =>
            {
                var progress = save.World.Exploration;
                Prune(progress, now);
                Spend(save.Player, Data.settings.FoodId, foodCost, now);
                if (tokenCost > 0) Spend(save.Player, tokenItemId, tokenCost, now);
                if (progress.Day != Day(now)) { progress.Day = Day(now); progress.DailyCount = 0; }
                for (int iteration = 0; iteration < times; iteration++)
                {
                    bool first = progress.TotalCount == 0;
                    var available = Enumerable.Range(0, token.ExploreGroups.Length)
                        .Where(i => Candidates(token.ExploreGroups[i], context, save).Count > 0).ToArray();
                    if (available.Length == 0) throw new InvalidOperationException("explore_use_token_in_invalid_area_tip");
                    int index;
                    bool guaranteed = false;
                    OfficialExploreEntityGroup group;
                    if (first)
                    {
                        group = Data.entityGroups.Single(v => v.Group == 51);
                        index = Array.IndexOf(token.ExploreGroups, 3);
                        if (index < 0) throw new InvalidDataException("Native first exploration guide group is unavailable.");
                    }
                    else
                    {
                        index = HardGuaranteed(token, available, progress);
                        guaranteed = index >= 0;
                        if (index < 0) index = available[Choose(available.Select(i => token.ExploreWeights[i]).ToArray(), ref save.Player.InventoryRandomState)];
                        var candidates = Candidates(token.ExploreGroups[index], context, save);
                        var outer = Data.exploreGroups.Single(v => v.Id == token.ExploreGroups[index]);
                        group = candidates[Choose(candidates.Select(v => outer.Weights[Array.IndexOf(outer.EntityGroups, v.Group)]).ToArray(), ref save.Player.InventoryRandomState)];
                    }
                    if (group.RefreshPrecent != 10000 || group.LevelMin != 0 || group.LevelMax != 0)
                        throw new InvalidDataException("Unconverted exploration spawn percentage/level range.");
                    int count = group.ExploreCounts[Choose(group.ExploreCountWeights, ref save.Player.InventoryRandomState)];
                    if (group.LastMapRefreshOnly) progress.Entities.RemoveAll(v => v.MapClassId == context.MapClassId && v.Group == group.Group);
                    var serials = new List<int>();
                    for (int unit = 0; unit < count; unit++)
                    {
                        int entityClass = group.Entitys[Choose(group.Weights, ref save.Player.InventoryRandomState)];
                        int distance = group.RefreshDistance > 0 ? group.RefreshDistance : Data.settings.ExploreEntityMaxDist;
                        var point = context.FindSpawn(entityClass, distance, progress.Entities.Where(v => v.Active(now)).ToArray());
                        if (!point.HasValue) throw new InvalidOperationException("WORLD_EXPLORE_POS_NOT_ENOUGH");
                        var parameters = Catalog.Parameters(entityClass);
                        // EntityExtendParameterInfoParser.GetPublicEntityLevelAndSubRank;
                        // WorldEntityLevelComponent preserves Explore-source levels.
                        int level = parameters.LevelTypeInPublicMap == 1 ? context.RegionLevel : context.Level;
                        int subRank = parameters.LevelTypeInPublicMap == 1 ? context.RegionSubRank : context.SubRank;
                        if (parameters.LevelTypeInPublicMap == 3)
                            level = Data.playerMapLevels.SingleOrDefault(v => v.Level == context.Level && v.SubRank == context.SubRank)?.MapingLevel ?? context.Level;
                        if (parameters.LevelTypeInPublicMap == 0) { level = 0; subRank = 0; }
                        var entity = new OfflineExploreEntity
                        {
                            Serial = progress.NextSerial++, MapClassId = context.MapClassId, Area = context.Area, Region = context.Region,
                            X = point.Value.X, Y = point.Value.Y, Group = group.Group, Variant = group.Id, EntityClassId = entityClass,
                            State = parameters.PublicDefaultState, Level = level, SubRank = subRank, CreatedAt = now,
                            RemoveOnComplete = Convert.ToBoolean(Object(Catalog.Components(entityClass)["WorldStageComponent"])["DestroyOnEnd"]),
                            ExpiresAt = parameters.DurationWhenExplore > 0 ? checked(now + parameters.DurationWhenExplore) : 0
                        };
                        ResolveExtra(group, context, save, entity);
                        progress.Entities.Add(entity); output.Entities.Add(entity); serials.Add(entity.Serial);
                    }
                    var total = progress.GroupCounts.SingleOrDefault(v => v.Group == group.Group);
                    if (total == null) progress.GroupCounts.Add(total = new OfflineExploreGroupCount { Group = group.Group });
                    total.Count = checked(total.Count + count);
                    Record(token, index, guaranteed, progress);
                    var history = new OfflineExploreHistory { EventId = receipt, MapClassId = context.MapClassId, Area = context.Area,
                        Region = context.Region, X = context.X, Y = context.Y, TokenItemId = tokenItemId,
                        ExploreGroup = token.ExploreGroups[index], EntityGroup = group.Group, Variant = group.Id, Count = count,
                        CreatedAt = now, Guaranteed = guaranteed, EntitySerials = serials.ToArray() };
                    progress.History.Add(history); output.History.Add(history);
                    progress.TotalCount++; progress.DailyCount++;
                    if (first && !save.Player.AppliedInventoryEventIds.Contains("fixed_award:5"))
                    {
                        foreach (var item in Data.guideReward.Items)
                            if (!OfflineInventoryOperations.Grant(save.Player, item.ItemId, item.Count, now).IsApplied)
                                throw new InvalidOperationException("Native exploration guide reward could not be granted.");
                        save.Player.AppliedInventoryEventIds.Add("fixed_award:5");
                        output.GuideRewardGranted = true;
                    }
                }
            });
            if (!transaction.IsApplied)
            {
                output.Entities.Clear(); output.History.Clear(); output.GuideRewardGranted = false;
                if (transaction.Message == "WORLD_EXPLORE_POS_NOT_ENOUGH"
                    || transaction.Message == "explore_use_token_in_invalid_area_tip") return Failure(transaction.Message);
            }
            return transaction;
        }

        private void ResolveExtra(OfficialExploreEntityGroup group, OfflineExploreContext context, OfflineMainlineSave save, OfflineExploreEntity entity)
        {
            foreach (var value in Catalog.ExtraData(group.Id))
            {
                var source = Object(value);
                string kind = Text(source, "_type");
                var extra = new OfflineExploreExtra { Kind = kind };
                switch (kind)
                {
                    case "MapEntityGroupInfoExtraActivityData":
                        extra.ActivityType = Int(source, "ActivityType"); extra.ActivityId = Int(source, "ActivityId"); break;
                    case "MapEntityGroupInfoExtraAwardItemData":
                        int[] ids = Ints(source, "AwardItemIds"), counts = Ints(source, "AwardItemCounts");
                        extra.ItemId = ids[Choose(Ints(source, "AwardItemIdsWeights"), ref save.Player.InventoryRandomState)];
                        extra.Count = counts[Choose(Ints(source, "AwardItemCountWeights"), ref save.Player.InventoryRandomState)];
                        extra.ExpendRandomItem = Convert.ToBoolean(source["IsExpendRandomItem"], CultureInfo.InvariantCulture);
                        if (extra.ExpendRandomItem)
                        {
                            var resolved = context.ResolveRandomItem?.Invoke(extra.ItemId, extra.Count, save)
                                ?? throw new InvalidDataException("Native exploration random-item selector is unbound.");
                            if (resolved.Count == 0) throw new InvalidOperationException("Native exploration item selector has no candidates.");
                            foreach (var item in resolved) entity.Extra.Add(new OfflineExploreExtra { Kind = kind, ItemId = item.ItemId, Count = checked((int)item.Count) });
                            continue;
                        }
                        break;
                    case "MapEntityGroupInfoExtraStageIdsData":
                        var choices = Values(source, "StageIdsList");
                        extra.StageIds = ((List<object>)choices[Choose(Ints(source, "StageIdWights"), ref save.Player.InventoryRandomState)])
                            .Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)).ToArray(); break;
                    case "MapEntityGroupInfoExtraSpawnArrestMonsterData":
                        var activities = Values(source, "ActivityIds");
                        extra.ActivityIds = ((List<object>)activities[Choose(Ints(source, "ActivityWights"), ref save.Player.InventoryRandomState)])
                            .Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)).ToArray(); break;
                    default: throw new InvalidDataException("Unconverted original exploration extra data: " + kind);
                }
                entity.Extra.Add(extra);
            }
        }

        private int HardGuaranteed(OfficialExploreToken token, int[] available, OfflineWorldExplorationProgress progress)
        {
            var entries = available.Where(i => token.ExploreMininums[i] > 0)
                .OrderByDescending(i => token.ExplorePriorities[i]).ThenBy(i => i).ToArray();
            int guaranteedCount = token.ExploreMininums.Count(v => v > 0);
            for (int horizon = 1; horizon <= guaranteedCount; horizon++)
            {
                int nearest = int.MaxValue, selected = -1, matches = 0;
                foreach (int index in entries)
                {
                    int misses = progress.Records.SingleOrDefault(v => v.Key == token.RecordKey && v.Index == token.RecordIndex(index))?.KeepMissCount ?? 0;
                    int left = token.ExploreMininums[index] - misses;
                    if (left > horizon) continue;
                    if (left < nearest) { nearest = left; selected = index; }
                    if (++matches >= horizon) return selected;
                }
            }
            return -1;
        }
        private static void Record(OfficialExploreToken token, int hitIndex, bool guaranteed, OfflineWorldExplorationProgress progress)
        {
            // PlayerExploreRecordDataWrap.RecordResult: hit debt subtracts its
            // threshold and adds this miss score; it is deliberately not reset.
            for (int index = 0; index < token.ExploreGroups.Length; index++)
            {
                int minimum = token.ExploreMininums[index];
                if (minimum <= 0) continue;
                int recordIndex = token.RecordIndex(index);
                var record = progress.Records.SingleOrDefault(v => v.Key == token.RecordKey && v.Index == recordIndex);
                bool hit = hitIndex == index;
                if (record == null)
                {
                    progress.Records.Add(new OfflineExploreRecord { Key = token.RecordKey, Index = recordIndex,
                        HitCount = hit ? 1 : 0, KeepMissCount = hit ? 0 : 1, FireGuaranteedCount = hit && guaranteed ? 1 : 0 });
                    continue;
                }
                if (hit)
                {
                    record.HitCount++;
                    record.KeepMissCount = Math.Max(record.KeepMissCount - minimum + 1, 0);
                    if (guaranteed) record.FireGuaranteedCount++;
                }
                else record.KeepMissCount++;
            }
        }
        public bool IsMet(OfficialExploreCondition condition, OfflineExploreContext context, OfflineMainlineSave save, int depth = 0)
        {
            if (condition == null) return true;
            if (depth > 20) throw new InvalidDataException("Exploration condition recursion exceeded its source limit.");
            switch (condition._type)
            {
                case "NoneCondition": case "AlwaysCondition": return true;
                case "CompositeAndCondition": return condition.Ids.All(id => IsMet(Data.conditions.Single(v => v.ClassId == id).Condition, context, save, depth + 1));
                case "CompositeOrCondition": return condition.Ids.Any(id => IsMet(Data.conditions.Single(v => v.ClassId == id).Condition, context, save, depth + 1));
                case "MapLocationCondition": return context.MapClassId == condition.MapClassId;
                case "LevelCondition": return Compare(context.Level, condition.Level, condition.Op);
                case "SubRankCondition": return Compare(context.SubRank, condition.SubRank, condition.Op);
                case "MapGroupLocationCondition": return context.MapGroupId == condition.MapGroupId;
                case "MapUnlockCondition": return save.Player.UnlockedMapClassIds.Contains(condition.MapClassId);
                case "PlayerProfessionCondition": return save.Player.Wardrobe.Profession == condition.Profession;
                case "PlayerProfessionFactionCondition":
                    int profession = save.Player.Wardrobe.Profession;
                    for (int limit = 0; limit < Data.professions.Length && profession != 0; limit++)
                    {
                        if (profession == condition.Profession) return true;
                        profession = Data.professions.SingleOrDefault(v => v.Profession == profession)?.PrePro ?? 0;
                    }
                    return false;
                case "RegionCodeCondition": return condition.RegionCodes.Contains(context.RegionCode) != condition.Not;
                case "ServerRankUnlockDaysCondition": return true; // Confirmed shared offline opening policy.
                case "HasItemCondition": return Compare(save.Player.Inventory.Where(v => v.ItemId == condition.ClassId).Sum(v => v.Count), condition.Count, condition.Op);
                case "HasOrUsedItemCondition": return save.Player.Inventory.Any(v => v.ItemId == condition.ClassId && v.Count > 0)
                    || context.HasOrUsedItem?.Invoke(save, condition.ClassId) == true;
                case "AnyMapGoddessActivedCondition": return save.World.ActivatedGoddessRegions.Count > 0;
                case "MapGoddessCondition": return save.World.ActivatedGoddessRegions.Count(v => v.MapClassId == condition.MapClassId) >= condition.Count;
                case "IsActivityOpenCondition": return context.IsActivityOpen?.Invoke(condition.ActivityType, condition.Id, condition.AheadTime) == true;
                default: throw new InvalidDataException("Unconverted original exploration condition: " + condition._type);
            }
        }
        private static bool Compare(long actual, long expected, string op)
        {
            switch (op) { case ">=": return actual >= expected; case ">": return actual > expected; case "<=": return actual <= expected;
                case "<": return actual < expected; case "=": case "==": return actual == expected; case "!=": return actual != expected;
                default: throw new InvalidDataException("Unknown original exploration comparison: " + op); }
        }
        private void Prune(OfflineWorldExplorationProgress progress, long now)
        {
            progress.Entities.RemoveAll(v => !v.Active(now));
            long oldest = now - checked((long)Data.settings.ExploreHistoryRecordDays * 86400);
            progress.History.RemoveAll(v => v.CreatedAt < oldest);
        }
        public ProgressionResult Cleanup(long now)
        {
            session.Reload();
            var progress = session.Save.World.Exploration;
            long oldest = now - checked((long)Data.settings.ExploreHistoryRecordDays * 86400);
            if (!progress.Entities.Any(v => !v.Active(now)) && !progress.History.Any(v => v.CreatedAt < oldest))
                return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            return session.ApplyInventoryTransaction("world-explore-clean:" + now, save => Prune(save.World.Exploration, now));
        }
        private static void Spend(OfflinePlayerProgress player, int item, long count, long now)
        {
            if (!OfflineInventoryOperations.Consume(player, item, count, now).IsApplied)
                throw new InvalidOperationException("1153");
        }
        private string Day(long now) => DateTimeOffset.FromUnixTimeSeconds(now).LocalDateTime
            .Subtract(DateTime.ParseExact(Data.settings.DailyResetTime, "(HH:mm:ss)", CultureInfo.InvariantCulture).TimeOfDay)
            .ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        private static ProgressionResult Failure(string message, ProgressionResultStatus status = ProgressionResultStatus.NotReady) =>
            new ProgressionResult { Status = status, Message = message };
        private static int Choose(int[] weights, ref long state)
        {
            if (weights == null || weights.Length == 0 || weights.Any(v => v < 0) || weights.Sum() <= 0)
                throw new InvalidDataException("Original exploration weights are invalid.");
            if (state == 0) state = 88172645463325252L;
            ulong value = unchecked((ulong)state);
            value ^= value << 13; value ^= value >> 7; value ^= value << 17;
            value &= long.MaxValue;
            if (value == 0) value = 88172645463325252UL;
            state = (long)value;
            long roll = (long)((value >> 10) * (1.0 / 9007199254740992.0) * weights.Sum());
            for (int index = 0; index < weights.Length; index++) if ((roll -= weights[index]) < 0) return index;
            return weights.Length - 1;
        }
    }
}
