using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleReportStatusScore
    {
        // Original FightReportSetting.StatusTypeScore calls this field StageType.
        public int StageType;
        public int Score;
    }

    /// <summary>Original FightSettings report configuration and FightReportUIUtils scoring.
    /// User-confirmed local counters retain successful enemy breaks and separate status types.</summary>
    [Serializable]
    public sealed class FightBattleReportScoreSettings
    {
        public int BreakResilienceScoreFactor;
        public List<FightBattleReportStatusScore> ApplyStatusScore = new List<FightBattleReportStatusScore>();
        public List<int> ReportAllPlayerStages = new List<int>();

        public static FightBattleReportScoreSettings Load(string json, string expectedSha256, string manifest)
        {
            FightOfficialCombatCatalog.VerifyPayloadForDerived("report-score-settings.json", json,
                expectedSha256, manifest);
            Dictionary<string, object> root = FightOfficialCombatCatalog.ParseObject(json, "report settings");
            if (!string.Equals(Convert.ToString(root["manifest"], CultureInfo.InvariantCulture), manifest,
                StringComparison.Ordinal)) throw new FormatException("Report settings manifest differs.");
            Dictionary<string, object> data = FightOfficialCombatCatalog.ParseObject(root["ReportSetting"], "ReportSetting");
            var result = new FightBattleReportScoreSettings
            {
                BreakResilienceScoreFactor = Convert.ToInt32(data["BreakResilienceScoreFactor"], CultureInfo.InvariantCulture)
            };
            foreach (object raw in (IEnumerable)data["ApplyStatusScore"])
            {
                var entry = FightOfficialCombatCatalog.ParseObject(raw, "ApplyStatusScore entry");
                result.ApplyStatusScore.Add(new FightBattleReportStatusScore
                {
                    StageType = Convert.ToInt32(entry["StageType"], CultureInfo.InvariantCulture),
                    Score = Convert.ToInt32(entry["Score"], CultureInfo.InvariantCulture)
                });
            }
            foreach (object raw in (IEnumerable)data["ReportAllPlayerStages"])
                result.ReportAllPlayerStages.Add(Convert.ToInt32(raw, CultureInfo.InvariantCulture));
            return result;
        }

        // Original FightSettings.GetReportApplyStatusScore, with StatusType represented by its ID.
        public int GetReportApplyStatusScore(int statusType)
        {
            foreach (FightBattleReportStatusScore item in ApplyStatusScore)
                if (item.StageType == statusType) return item.Score;
            return 0;
        }

        public bool TryGetApplyStatusScore(FightBattleReportUnit unit, out long score)
        {
            score = 0L;
            if (unit == null || !unit.HasStatusTypeCounts || !unit.HasBreakResilienceCount) return false;
            score = GetApplyStatusScore(unit.BreakResilienceCount, new Dictionary<int, int>
            {
                { 1, unit.BuffApplications },
                { 2, unit.OrdinaryDebuffApplications },
                { 3, unit.AbnormalDebuffApplications }
            });
            return true;
        }

        // Original FightReportUIUtils.GetApplyStatusScore. A missing dictionary returns zero before
        // adding BreakResilience, matching the client's original branch and multiplication order.
        public long GetApplyStatusScore(long breakResilienceValue, Dictionary<int, int> applyStatusCountDict)
        {
            if (applyStatusCountDict == null) return 0L;
            long num = 0L;
            num += breakResilienceValue * BreakResilienceScoreFactor;
            foreach (KeyValuePair<int, int> item in applyStatusCountDict)
            {
                int reportApplyStatusScore = GetReportApplyStatusScore(item.Key);
                num += item.Value * reportApplyStatusScore;
            }
            return num;
        }
    }
}
