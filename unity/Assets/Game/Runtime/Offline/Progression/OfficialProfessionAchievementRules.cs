using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zjcs.Offline.Progression
{
    public sealed class OfficialProfessionAchievement
    {
        public int Index { get; }
        public int Id => (Index << 12) | 10;
        public int RawId { get; }
        public int RequiredLevel { get; }
        public IReadOnlyDictionary<int, long> Reward { get; }
        public string EventId => "profession-achievement:" + Index.ToString(CultureInfo.InvariantCulture);
        internal OfficialProfessionAchievement(int index, int rawId, int level, IReadOnlyDictionary<int, long> reward)
        { Index = index; RawId = rawId; RequiredLevel = level; Reward = reward; }
    }

    public enum OfficialAchievementState { Processing, Completed, Received }
    public sealed class OfficialProfessionAchievementView
    {
        public OfficialAchievementState State { get; }
        public OfficialProfessionAchievement Info { get; }
        internal OfficialProfessionAchievementView(OfficialAchievementState state, OfficialProfessionAchievement info)
        { State = state; Info = info; }
    }

    // AchievementRawInfoParser.GenerateAchievementInfosCount/GetAllAchievementDict,
    // CycleTemplateInfo.GetRawInfoByIndex and AchievementInfo.GetGenerateCycleValue,
    // restricted to the original ProfessionTotalLevel (56), group 10, Level parameter.
    public sealed class OfficialProfessionAchievementRules
    {
        private readonly List<OfficialProfessionAchievement> milestones = new List<OfficialProfessionAchievement>();
        public IReadOnlyList<OfficialProfessionAchievement> Milestones => milestones;
        public OfficialProfessionAchievementRules(IEnumerable<Dictionary<string, object>> rawRows)
        {
            var rows = rawRows.Where(row => Number(row, "Group") == 10 && Number(row, "AchievementType") == 56)
                .OrderBy(row => Number(row, "Id")).ToArray();
            foreach (var row in rows.Where(row => Number(row, "CycleCount") == 0 && string.IsNullOrEmpty(Text(row, "CycleIncrementParams"))))
                Add(row, Level(row, "CompleteParams"));
            var seeds = rows.Where(row => Number(row, "CycleCount") > 0).ToArray();
            if (seeds.Length != 1) throw new FormatException("Original profession achievement cycle seed is missing or ambiguous.");
            var seed = seeds[0];
            var increments = rows.Where(row => !string.IsNullOrEmpty(Text(row, "CycleIncrementParams"))).ToArray();
            if (increments.Length == 0) throw new FormatException("Original profession achievement cycle increments are missing.");
            int required = Level(seed, "CompleteParams");
            Add(seed, required);
            int count = checked(Number(seed, "CycleCount") * increments.Length);
            for (int cycleIndex = 1; cycleIndex <= count; cycleIndex++)
            {
                var row = increments[(cycleIndex - 1) % increments.Length];
                required = checked(required + Level(row, "CycleIncrementParams"));
                Add(row, required);
            }
        }
        private void Add(Dictionary<string, object> row, int level)
        {
            if (level <= 0 || !(row["Reward"] is Dictionary<string, object> rawReward))
                throw new FormatException("Original profession achievement level/reward is invalid.");
            var reward = rawReward.ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture),
                pair => Convert.ToInt64(pair.Value, CultureInfo.InvariantCulture));
            if (reward.Count == 0 || reward.Any(pair => pair.Key <= 0 || pair.Value <= 0))
                throw new FormatException("Original profession achievement reward is invalid.");
            milestones.Add(new OfficialProfessionAchievement(milestones.Count, Number(row, "Id"), level, reward));
        }
        public IReadOnlyList<OfficialProfessionAchievement> CanClaim(int totalLevel, ISet<string> recorded)
            => milestones.Where(row => totalLevel >= row.RequiredLevel && !recorded.Contains(row.EventId)).ToArray();
        public IReadOnlyList<OfficialProfessionAchievement> Visible(int totalLevel, ISet<string> recorded, int showCount)
            => VisibleWithStates(totalLevel, recorded, showCount).Select(row => row.Info).ToArray();
        public IReadOnlyList<OfficialProfessionAchievementView> VisibleWithStates(int totalLevel, ISet<string> recorded,
            int showCount, bool showReceived = false, bool showProcessing = true)
        {
            // AchievementUtils.GetAchievementStateSearchRange / AchievementPanelBase.InitData.
            int gotMax = milestones.Where(row => recorded.Contains(row.EventId)).Select(row => row.Index).DefaultIfEmpty(0).Max();
            int canMax = milestones.Where(row => totalLevel >= row.RequiredLevel).Select(row => row.Index).DefaultIfEmpty(0).Max();
            int start = gotMax > 0 ? Math.Max(0, gotMax - showCount) : 0;
            int end = Math.Min(milestones.Count - 1, canMax + showCount);
            var rows = new List<OfficialProfessionAchievementView>();
            for (int i = start; i <= end; i++)
            {
                OfficialProfessionAchievement info = milestones[i];
                if (recorded.Contains(info.EventId))
                {
                    if (showReceived) rows.Add(new OfficialProfessionAchievementView(OfficialAchievementState.Received, info));
                }
                else if (totalLevel >= info.RequiredLevel)
                    rows.Add(new OfficialProfessionAchievementView(OfficialAchievementState.Completed, info));
                else if (showProcessing)
                    rows.Add(new OfficialProfessionAchievementView(OfficialAchievementState.Processing, info));
            }
            return rows.OrderByDescending(row => row.State == OfficialAchievementState.Completed ? 3
                    : row.State == OfficialAchievementState.Processing ? 2 : 1)
                .ThenBy(row => row.Info.Index).Take(showCount).ToArray();
        }
        private static int Number(Dictionary<string, object> row, string key)
            => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
        private static string Text(Dictionary<string, object> row, string key)
            => row.TryGetValue(key, out object value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        private static int Level(Dictionary<string, object> row, string key)
        {
            string raw = Text(row, key);
            Match match = Regex.Match(raw ?? string.Empty, @"^\s*\{\s*""?Level""?\s*:\s*(\d+)\s*\}\s*$");
            if (!match.Success) throw new FormatException("Unsupported original achievement parameter: " + raw);
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
