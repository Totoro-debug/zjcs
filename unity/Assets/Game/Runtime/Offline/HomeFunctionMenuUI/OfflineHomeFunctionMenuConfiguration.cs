using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Offline.HomeUI;
using Rules = Zjcs.Official.HomeFunctionMenuUI.HomeMenuConfigurationRules;

namespace Zjcs.Offline.HomeFunctionMenuUI
{
    // Materializes selected binary-table fields for the migrated original parsers.
    // Configuration entries do not create activity records or authorize an activity.
    public sealed class OfflineHomeFunctionMenuConfiguration
    {
        private readonly Rules rules;
        private readonly Dictionary<(ActivityEntryType, int), HomeMenuActivityRecommendInfo> recommendations;
        private readonly Dictionary<ActivityEntryType, IReadOnlyDictionary<int, HomeMenuActivityEntryInfo>> entries;
        private readonly Dictionary<string, HomeMenuWikiInfo> wiki = new Dictionary<string, HomeMenuWikiInfo>();
        public IReadOnlyCollection<int> DisplayServerTimeRegionList { get; }

        public OfflineHomeFunctionMenuConfiguration(OfficialCurrentHomeRuleAssetCatalog configuration = null)
        {
            configuration = configuration ? configuration :
                Resources.Load<OfficialCurrentHomeRuleAssetCatalog>("Home/OfflineHomeFunctionMenuConfiguration");
            if (!configuration) throw new InvalidOperationException("Selected function menu configuration is unavailable.");
            var wikiRows = configuration.Rows("wiki").Select(row => new Rules.WikiInfo {
                Type = (string)row["Type"], Region = (Rules.RegionCodeEnum)Number(row, "Region"),
                LimitConditionId = Number(row, "LimitConditionId"), URL = (string)row["URL"] }).ToArray();
            var recommendRows = configuration.Rows("activity_recommend").Select(row => new Rules.ActivityRecommendInfo {
                ActivityEntryType = (ActivityEntryType)Number(row, "ActivityEntryType"), Id = Number(row, "Id"),
                Order = Number(row, "Order"), ShowInMap = (bool)row["ShowInMap"] }).ToArray();
            var entryRows = configuration.Rows("activity_entry").Select(row => new Rules.ActivityEntryInfo {
                ActivityType = (ActivityEntryType)Number(row, "ActivityType"), Id = Number(row, "Id"),
                Order = Number(row, "Order"), ActivityCategoryType = (Rules.ActivityCategoryType)Number(row, "ActivityCategoryType"),
                EntryPrefabPath = (string)row["EntryPrefabPath"], IsShow = (bool)row["IsShow"] }).ToArray();
            rules = new Rules(wikiRows, recommendRows, entryRows);
            recommendations = recommendRows.ToDictionary(row => (row.ActivityEntryType, row.Id),
                row => new HomeMenuActivityRecommendInfo { ActivityEntryType = row.ActivityEntryType,
                    Id = row.Id, Order = row.Order, ShowInMap = row.ShowInMap });
            entries = entryRows.Select(row => row.ActivityType).Distinct().ToDictionary(type => type,
                type => (IReadOnlyDictionary<int, HomeMenuActivityEntryInfo>)rules.GetEntries(type).ToDictionary(
                    pair => pair.Key, pair => new HomeMenuActivityEntryInfo { EntryPrefabPath = pair.Value.EntryPrefabPath }));
            foreach (string type in wikiRows.Select(row => row.Type).Distinct())
            {
                var row = rules.GetWikiInfo(type);
                if (row != null) wiki.Add(type, new HomeMenuWikiInfo { LimitConditionId = row.LimitConditionId });
            }
            var regions = (List<object>)configuration.Record("game_settings")["DisplayServerTimeRegionList"];
            DisplayServerTimeRegionList = regions == null ? null : Array.AsReadOnly(regions.Select(Convert.ToInt32).ToArray());
        }

        private static int Number(Dictionary<string, object> row, string field) => Convert.ToInt32(row[field]);
        public HomeMenuWikiInfo GetWikiInfo(string type) => wiki.TryGetValue(type, out var row) ? row : null;
        public string GetWikiUrl(string type) => rules.GetURL(type);
        public string GetWikiTitleKey(string type) => rules.GetTitleKey(type);
        public HomeMenuActivityRecommendInfo GetRecommend(ActivityEntryType type, int id)
            => recommendations.TryGetValue((type, id), out var row) ? row : null;
        public IReadOnlyDictionary<int, HomeMenuActivityEntryInfo> GetEntries(ActivityEntryType type)
            => entries.TryGetValue(type, out var group) ? group : null;
    }
}
