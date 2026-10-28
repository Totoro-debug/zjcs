using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfficialMap12Stele
    {
        public int PlacementId, EntityClassId, StageId, Region, Area, X, Y, RegionLevel, RegionSubRank,
            MonsterLevelAdd, PeriodicityGroupId;
        public bool SupportTeam;
        public int[] RuleIds;
        public long[] CombatRatingLevelValues;
    }

    [Serializable] public sealed class OfficialMap12SteleData
    {
        public string manifest;
        public int mapClassId, TeamMaxPlayer, RecommendCombatRatingIndex;
        public OfficialMap12Stele[] Steles;
        public OfficialExploreRewardRule[] RewardRules;
        public int[] EmptyRewardRuleIds;
    }

    public sealed class OfficialMap12SteleCatalog
    {
        public OfficialMap12SteleData Data { get; }
        public OfficialMap12SteleCatalog(string json)
        {
            Data = JsonUtility.FromJson<OfficialMap12SteleData>(json);
            if (Data?.manifest != "871_156598" || Data.mapClassId != 12 || Data.Steles?.Length != 7
                || Data.Steles.Select(v => v.PlacementId).Distinct().Count() != 7
                || Data.RewardRules?.Length != 52 || Data.TeamMaxPlayer != 4
                || Data.EmptyRewardRuleIds == null || !Data.EmptyRewardRuleIds.SequenceEqual(new[] { 1850, 1853 }))
                throw new InvalidDataException("Original Map12 stele rules are incomplete.");
            // JsonUtility materializes JSON null reference fields. Retain the
            // original unconditional rule rather than testing an empty condition.
            var raw = Object(Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(json));
            foreach (var row in Values(raw, "RewardRules").Select(Object)
                .Zip(Data.RewardRules, (source, target) => (source, target)))
                if (row.source["Condition"] == null) row.target.Condition = null;
            if (Data.Steles.Any(v => v.MonsterLevelAdd != 7 || v.RegionLevel <= 0 || v.RegionSubRank <= 0
                || v.CombatRatingLevelValues == null || Data.RecommendCombatRatingIndex < 0
                || Data.RecommendCombatRatingIndex >= v.CombatRatingLevelValues.Length))
                throw new InvalidDataException("Original Map12 stele level/recommendation binding changed.");
            foreach (var rule in Data.RewardRules)
                if (rule.IdList?.Length != 1 || rule.WeightList?.Length != 1 || rule.WeightList[0] <= 0
                    || rule.Percent != 1 || rule.CountList?.Length != 1 || rule.CountList[0] != Math.Floor(rule.CountList[0])
                    || rule.LevelCountId?.Length > 0 || rule.SubRankCountId?.Length > 0
                    || rule.Condition != null && (rule.Condition._type != "SubRankCondition" || rule.Condition.Op != ">="))
                    throw new InvalidDataException("Original Map12 stele requires another reward evaluator.");
        }

        public OfficialMap12Stele Find(int placementId) => Data.Steles.SingleOrDefault(v => v.PlacementId == placementId);
        public static string CompletionTag(int placementId) => "map12:stele:" + placementId;
        public static string WinReceipt(int placementId) => "map12-stele:stage-win:" + placementId;

        // FormulaHandler.GetCombatRatingLevel compares equality as reaching the
        // threshold; preserve the original order and reverse level numbering.
        public static int CombatRatingLevel(long rating, IReadOnlyList<long> thresholds)
        {
            if (thresholds == null || thresholds.Count == 0) return 0;
            int i = 0;
            for (; i < thresholds.Count && rating >= thresholds[i]; i++) { }
            return thresholds.Count - i;
        }

        public IEnumerable<OfficialExploreRewardRule> ApplicableRules(OfficialMap12Stele stele)
        {
            if (stele == null || !ReferenceEquals(Find(stele.PlacementId), stele))
                throw new ArgumentException("A placed stele from this catalog is required.", nameof(stele));
            foreach (int id in stele.RuleIds)
            {
                // RewardRuleInfoParser.Get: ascending condition order, then -1.
                var rule = Data.RewardRules.Where(v => v.Id == id)
                    .OrderBy(v => v.Order == -1 ? int.MaxValue : v.Order)
                    .FirstOrDefault(v => v.Condition == null || stele.RegionSubRank >= v.Condition.SubRank);
                if (rule != null) yield return rule;
                else if (!Data.EmptyRewardRuleIds.Contains(id) && !Data.RewardRules.Any(v => v.Id == id))
                    throw new InvalidDataException("Unbound Map12 stele award: " + id);
            }
        }

        // Exact fixed-entry subset of RewardRuleInfoParser.GetRewardDropInfosByRuleList.
        // The original dialog preview excludes the local settlement-only prop 29.
        public IReadOnlyDictionary<int, long> RewardPreview(OfficialMap12Stele stele)
        {
            var output = new Dictionary<int, long>();
            foreach (var rule in ApplicableRules(stele))
                output[rule.IdList[0]] = checked(output.GetValueOrDefault(rule.IdList[0]) + (long)rule.CountList[0]);
            return output;
        }

        public static OfficialMap12SteleCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("Map12/steles");
            return new OfficialMap12SteleCatalog(asset?.text ?? throw new InvalidDataException("Map12 stele rules are missing."));
        }
    }

    public sealed partial class OfflineWorldExplorationService
    {
        // Persist awards, RNG and the permanent per-player completion together.
        // Losses do not consume the original max-count-one interaction group.
        public ProgressionResult SettleMap12Stele(OfficialMap12SteleCatalog catalog, int placementId, int stageId,
            bool won, long additionRaw, long now, out IReadOnlyDictionary<int, long> awards)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var output = new Dictionary<int, long>();
            awards = output;
            var stele = catalog.Find(placementId);
            if (stele == null || stele.StageId != stageId || now <= 0)
                return Failure("Invalid placed Map12 stele battle.", ProgressionResultStatus.InvalidState);
            if (!won) return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            try { session.Reload(); }
            catch (InvalidDataException error)
            {
                // The shared reader reports both an unreadable locked primary
                // and invalid JSON as Corrupt. Treat this failed read as I/O so
                // the battle retains its finished checkpoint, without writing
                // a fallback profile or consuming the stele interaction.
                throw new IOException("The Map12 stele profile could not be read; settlement remains pending.", error);
            }
            string tag = OfficialMap12SteleCatalog.CompletionTag(placementId);
            if (session.Save.World.CompletedEntityTags.Contains(tag))
                return Failure(string.Empty, ProgressionResultStatus.Unchanged);
            var result = session.ApplyInventoryTransaction("map12-stele:" + placementId, save =>
            {
                foreach (var rule in catalog.ApplicableRules(stele))
                {
                    // All selected rules contain exactly one integral fixed
                    // entry. Keep the confirmed weighted-selection RNG step.
                    int index = Choose(rule.WeightList, ref save.Player.InventoryRandomState);
                    long count = checked((long)Math.Floor(rule.CountList[index] * (1.0 + additionRaw / 10000.0)));
                    if (count > 0) GrantExplore(save.Player, rule.IdList[index], count, now, output);
                }
                save.World.CompletedEntityTags.Add(tag);
                string entityTag = "map12:entity:" + placementId;
                if (!save.World.CompletedEntityTags.Contains(entityTag)) save.World.CompletedEntityTags.Add(entityTag);
                // RecordStageWin's state change belongs to the same profile
                // transaction as the rewards; the map consumer shares this ID.
                string winReceipt = OfficialMap12SteleCatalog.WinReceipt(placementId);
                if (!save.World.AppliedEventIds.Contains(winReceipt))
                {
                    var win = save.World.StageWinCounts.SingleOrDefault(v => v.StageId == stageId);
                    if (win == null)
                    {
                        win = new OfflineStageWinCount { StageId = stageId };
                        save.World.StageWinCounts.Add(win);
                    }
                    win.Count = checked(win.Count + 1);
                    save.World.AppliedEventIds.Add(winReceipt);
                }
            });
            if (!result.IsApplied) output.Clear();
            return result;
        }
    }
}
