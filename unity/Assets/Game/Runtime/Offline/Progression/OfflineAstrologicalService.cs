using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    /// <summary>Fixed-version star-contract costs and local, receipt-protected infusion.</summary>
    public sealed class OfflineAstrologicalService
    {
        private const int CostItemId = 61;
        private const int PreviewLockedTagCount = 10;
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfficialCharacterCatalog catalog;
        private readonly long[] cumulativeCosts;
        private readonly Dictionary<int, long>[] levelAttributes;
        private readonly Dictionary<int, int> seasonByRank;
        private readonly int minimumSeason;
        private readonly float baseChargeRate;
        private readonly ChargeStage[] chargeStages;
        private double pendingAmount;
        private float chargeStartTime;
        private float currentChargeRate;
        private int nextChargeStage;

        private sealed class ChargeStage
        {
            public float HoldSeconds;
            public float Multiplier;
        }

        public OfflineAstrologicalService(OfflineMainlineProgressSession profile,
            OfficialCharacterCatalog catalog)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.catalog = catalog;

            Dictionary<string, object>[] levels = catalog.Rows("astrological_level")
                .OrderBy(row => Integer(row, "Level")).ToArray();
            if (levels.Length != 520)
                throw new FormatException("The pinned astrological level table must contain 520 rows.");
            cumulativeCosts = new long[levels.Length];
            levelAttributes = new Dictionary<int, long>[levels.Length];
            long total = 0;
            for (int index = 0; index < levels.Length; index++)
            {
                Dictionary<string, object> row = levels[index];
                if (Integer(row, "Level") != index + 1)
                    throw new FormatException("The pinned astrological level table is not contiguous.");
                long amount = Long(row, "MaterialAmount");
                if (amount <= 0) throw new FormatException("Astrological level costs must be positive.");
                total = checked(total + amount);
                cumulativeCosts[index] = total;
                levelAttributes[index] = IntegerLongMap(Value(row, "Attribuate"));
            }
            if (total != 221080L)
                throw new FormatException("The pinned astrological level total does not match the fixed version.");

            Dictionary<string, object>[] seasons = catalog.Rows("astrological_season_config")
                .OrderBy(row => Integer(row, "Season")).ToArray();
            if (seasons.Length != 7)
                throw new FormatException("The pinned astrological season table must contain seven rows.");
            seasonByRank = new Dictionary<int, int>();
            minimumSeason = int.MaxValue;
            foreach (Dictionary<string, object> season in seasons)
            {
                int seasonId = Integer(season, "Season");
                int rank = Integer(season, "Rank");
                seasonByRank.Add(rank, seasonId);
                minimumSeason = Math.Min(minimumSeason, seasonId);
            }

            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault(row =>
                row.ContainsKey("AstrologicalUpgradeCostItemId"));
            if (settings == null || Integer(settings, "AstrologicalUpgradeCostItemId") != CostItemId)
                throw new FormatException("The pinned astrological material setting is unavailable.");
            baseChargeRate = Float(settings, "ChargeRate");
            chargeStages = List(settings, "ChargeRateStages").Select(value =>
            {
                Dictionary<string, object> row = AsObject(value, "astrological charge stage");
                return new ChargeStage
                {
                    HoldSeconds = Float(row, "HoldSeconds"),
                    Multiplier = Float(row, "Multiplier")
                };
            }).OrderBy(value => value.HoldSeconds).ToArray();
            if (baseChargeRate <= 0f || chargeStages.Any(value => value.HoldSeconds < 0f || value.Multiplier <= 0f))
                throw new FormatException("The pinned astrological charge rates are invalid.");
        }

        public long CurrentCost => Math.Max(0, profile.Save.Player.CharacterProperties.AstrologicalCost);
        public long MaterialCount => profile.InventoryCount(CostItemId);
        public long MaximumCost => cumulativeCosts[cumulativeCosts.Length - 1];
        public long PreviewCost => Math.Min(MaximumCost,
            CurrentCost + (long)Math.Round(pendingAmount));
        public long PreviewMaterialCount => Math.Max(0, MaterialCount - (long)Math.Round(pendingAmount));
        public bool IsCharging { get; private set; }
        public int LevelCount => cumulativeCosts.Length;
        public int MinimumSeason => minimumSeason;

        public int SeasonForRank(int rank)
        {
            return seasonByRank.TryGetValue(rank, out int season) ? season : -1;
        }

        public int CurrentLevel => LevelForCost(CurrentCost);

        public int LevelForCost(long cost)
        {
            long clamped = Math.Max(0, Math.Min(MaximumCost, cost));
            int level = 0;
            for (int index = 0; index < cumulativeCosts.Length; index++)
            {
                if (clamped < cumulativeCosts[index]) break;
                level = index + 1;
            }
            return level;
        }

        public long TotalCostForLevel(int level)
        {
            if (level <= 0) return 0;
            return cumulativeCosts[Math.Min(level, cumulativeCosts.Length) - 1];
        }

        public long MaterialCostForLevel(int level)
        {
            if (level <= 0 || level > cumulativeCosts.Length) return 0;
            long previous = level == 1 ? 0 : cumulativeCosts[level - 2];
            return cumulativeCosts[level - 1] - previous;
        }

        public long MaximumVisibleCost(long currentCost)
        {
            long previewCost = Math.Max(0, Math.Min(MaximumCost, currentCost));
            int visibleLockedTags = 0;
            int level = 0;
            for (; level < cumulativeCosts.Length; level++)
            {
                if (visibleLockedTags >= PreviewLockedTagCount) break;
                if (previewCost < cumulativeCosts[level]) visibleLockedTags++;
            }
            return TotalCostForLevel(level);
        }

        public float CostProcess(long cost)
        {
            long current = Math.Max(0, Math.Min(MaximumCost, cost));
            int index = cumulativeCosts.Length - 1;
            for (int candidate = index; candidate >= 1; candidate--)
            {
                long upper = cumulativeCosts[candidate];
                long lower = cumulativeCosts[candidate - 1];
                if ((upper - lower) * 0.5d + lower <= current) break;
                index = candidate - 1;
            }

            long threshold = cumulativeCosts[index];
            float progress;
            if (current >= threshold)
            {
                if (index >= cumulativeCosts.Length - 1) return 1f;
                long next = cumulativeCosts[index + 1];
                progress = (index + 1f + (float)(current - threshold) / (next - threshold))
                    / cumulativeCosts.Length;
            }
            else if (index > 0)
            {
                long previous = cumulativeCosts[index - 1];
                progress = (index + (float)(current - previous) / (threshold - previous))
                    / cumulativeCosts.Length;
            }
            else
                progress = (float)current / threshold / cumulativeCosts.Length;
            return Math.Min(progress, 1f);
        }

        public IReadOnlyDictionary<int, long> LevelAttributes(int level)
        {
            return level <= 0 || level > levelAttributes.Length
                ? new Dictionary<int, long>() : levelAttributes[level - 1];
        }

        public OfficialPropDefinition Property(int propType) => catalog.Props.Get(propType);

        public IReadOnlyDictionary<int, long> AttributesAtCost(long cost)
        {
            long clamped = Math.Max(0, Math.Min(MaximumCost, cost));
            var result = new Dictionary<int, long>();
            for (int index = 0; index < cumulativeCosts.Length; index++)
            {
                if (cumulativeCosts[index] > clamped) break;
                foreach (KeyValuePair<int, long> property in levelAttributes[index])
                    result[property.Key] = result.TryGetValue(property.Key, out long current)
                        ? checked(current + property.Value) : property.Value;
            }
            return result;
        }

        public bool BeginCharge(float now)
        {
            if (IsCharging) return true;
            if (CurrentCost >= MaximumCost || MaterialCount <= 0) return false;
            pendingAmount = 0d;
            chargeStartTime = now;
            currentChargeRate = baseChargeRate;
            nextChargeStage = 0;
            IsCharging = true;
            return true;
        }

        public bool StepCharge(float now, float dampingProcess)
        {
            if (!IsCharging) return false;
            long available = MaterialCount;
            long cost = CurrentCost;
            if (pendingAmount >= available || cost + pendingAmount >= MaximumCost)
            {
                return false;
            }

            long lower = 0;
            long upper = cumulativeCosts[0];
            double effectiveCost = cost + pendingAmount;
            for (int index = cumulativeCosts.Length - 1; index >= 0; index--)
            {
                if (effectiveCost < cumulativeCosts[index]) continue;
                lower = cumulativeCosts[index];
                upper = index + 1 < cumulativeCosts.Length ? cumulativeCosts[index + 1] : lower;
                break;
            }

            float elapsed = Math.Max(0f, now - chargeStartTime);
            while (nextChargeStage < chargeStages.Length
                && elapsed >= chargeStages[nextChargeStage].HoldSeconds)
            {
                currentChargeRate = baseChargeRate * chargeStages[nextChargeStage].Multiplier;
                nextChargeStage++;
            }

            double increment = (float)(upper - lower) * currentChargeRate * Math.Max(0f, Math.Min(1f, dampingProcess));
            pendingAmount = Math.Max(1d, Math.Min(pendingAmount + increment, available));
            pendingAmount = Math.Min(pendingAmount + cost, MaximumCost) - cost;
            return true;
        }

        public ProgressionResult EndCharge(string receipt)
        {
            bool wasCharging = IsCharging;
            IsCharging = false;
            long amount = (long)Math.Round(pendingAmount);
            pendingAmount = 0d;
            if (!wasCharging || amount <= 0)
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };

            return profile.ApplyInventoryTransaction(receipt, save =>
            {
                OfflineCharacterPropertyProgress properties = save.Player.CharacterProperties;
                OfflineCharacterPropertyProgress.Normalize(properties);
                List<OfflineItemStack> inventory = save.Player.Inventory;
                OfflineItemStack material = inventory.FirstOrDefault(value => value.ItemId == CostItemId);
                if (material == null || material.Count < amount)
                    throw new InvalidOperationException("原初之星数量不足。");
                if (properties.AstrologicalCost > MaximumCost - amount)
                    throw new InvalidOperationException("星命契约已达到固定版上限。");
                material.Count -= amount;
                if (material.Count == 0) inventory.Remove(material);
                properties.AstrologicalCost += amount;
            });
        }

        private static object Value(Dictionary<string, object> row, string key) =>
            row != null && row.TryGetValue(key, out object value) ? value : null;

        private static int Integer(Dictionary<string, object> row, string key) =>
            Convert.ToInt32(Value(row, key), CultureInfo.InvariantCulture);

        private static long Long(Dictionary<string, object> row, string key) =>
            Convert.ToInt64(Value(row, key), CultureInfo.InvariantCulture);

        private static float Float(Dictionary<string, object> row, string key) =>
            Convert.ToSingle(Value(row, key), CultureInfo.InvariantCulture);

        private static List<object> List(Dictionary<string, object> row, string key) =>
            Value(row, key) as List<object> ?? new List<object>();

        private static Dictionary<string, object> AsObject(object value, string name) =>
            value as Dictionary<string, object> ?? throw new FormatException("Invalid " + name + ".");

        private static Dictionary<int, long> IntegerLongMap(object value)
        {
            var result = new Dictionary<int, long>();
            if (!(value is Dictionary<string, object> entries)) return result;
            foreach (KeyValuePair<string, object> entry in entries)
                result.Add(int.Parse(entry.Key, CultureInfo.InvariantCulture),
                    Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture));
            return result;
        }
    }
}
