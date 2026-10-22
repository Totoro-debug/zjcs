using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class CharacterLevelLimitRecord
    {
        public int SubRank;
        public int PlayerLevel;
        public int PlayerBlessLevel;
    }

    [Serializable]
    public sealed class CharacterBlessLevelRecord
    {
        public int Rank;
        public int BlessLevel;
        public long Exp;
    }

    public sealed class CharacterExperienceSnapshot
    {
        public int Level { get; internal set; }
        public int LevelLimit { get; internal set; }
        public int Rank { get; internal set; }
        public int BlessLevel { get; internal set; }
        public long CurrentExperience { get; internal set; }
        public long RequiredExperience { get; internal set; }
        public long TotalExperience { get; internal set; }
        public float Fill { get; internal set; }
        public bool IsStoredExperience { get; internal set; }
        public bool IsBlessing { get; internal set; }
        public bool IsMaxBlessing { get; internal set; }
    }

    public static class OfflineCharacterExperience
    {
        public static int BaseLevelLimit(int subRank, IReadOnlyList<CharacterLevelLimitRecord> limits)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            int result = 0;
            foreach (CharacterLevelLimitRecord row in limits)
                if (row != null && row.SubRank == subRank && row.PlayerBlessLevel == 0)
                    result = Math.Max(result, row.PlayerLevel);
            return result;
        }

        public static CharacterExperienceSnapshot Calculate(long experience, long blessingExperience,
            int subRank, int seasonRank, int seasonBaseLevel, bool blessingOpen, bool storedExperienceOpen,
            bool atSeasonRankLimit, int blessingLevelLimit, IReadOnlyList<long> cumulativeLevelExperience,
            IReadOnlyList<CharacterLevelLimitRecord> limits, IReadOnlyList<CharacterBlessLevelRecord> blessings,
            float sliderMin = 0f, float sliderMax = 1f)
        {
            if (experience < 0 || blessingExperience < 0 || subRank < 0 || seasonRank < 0)
                throw new ArgumentOutOfRangeException("Character progression values cannot be negative.");
            if (cumulativeLevelExperience == null || cumulativeLevelExperience.Count == 0)
                throw new ArgumentException("Official cumulative level experience is required.", nameof(cumulativeLevelExperience));

            int levelLimit = BaseLevelLimit(subRank, limits);
            int level = LevelForExperience(experience, cumulativeLevelExperience, levelLimit);
            bool stored = storedExperienceOpen && experience >= AtLevel(levelLimit, cumulativeLevelExperience)
                && !atSeasonRankLimit;
            int nextLevel = stored
                ? BaseLevelLimit(subRank + 1, limits)
                : Math.Min(level + 1, cumulativeLevelExperience.Count - 1);
            long start = AtLevel(level, cumulativeLevelExperience);
            long end = AtLevel(Math.Max(level + 1, nextLevel), cumulativeLevelExperience);
            long current = Math.Max(0, experience - start);
            long required = Math.Max(1, end - start);
            int blessLevel = 0;
            bool maxBless = false;
            bool blessing = !stored && blessingOpen && seasonBaseLevel >= levelLimit;
            if (blessing)
            {
                blessLevel = BlessLevelForExperience(seasonRank, blessingExperience, blessings);
                maxBless = blessingLevelLimit > 0 && blessLevel >= blessingLevelLimit;
                if (maxBless)
                {
                    current = 0;
                    required = 1;
                }
                else
                {
                    FindBlessExperience(seasonRank, blessLevel, blessings, out long blessStart);
                    FindBlessExperience(seasonRank, blessLevel + 1, blessings, out long blessEnd);
                    current = Math.Max(0, blessingExperience - blessStart);
                    required = Math.Max(1, blessEnd - blessStart);
                }
                level = seasonBaseLevel + blessLevel;
            }

            float ratio = Math.Max(0f, Math.Min(1f, (float)current / required));
            return new CharacterExperienceSnapshot
            {
                Level = level,
                LevelLimit = levelLimit,
                Rank = seasonRank,
                BlessLevel = blessLevel,
                CurrentExperience = current,
                RequiredExperience = required,
                TotalExperience = experience + blessingExperience,
                Fill = sliderMin + (sliderMax - sliderMin) * ratio,
                IsStoredExperience = stored,
                IsBlessing = blessing,
                IsMaxBlessing = maxBless
            };
        }

        public static long ExtendTimedAppearance(long nowSeconds, long previousExpirySeconds,
            int durationHours, int maximumDurationHours, int count, bool canOverlap)
        {
            if (nowSeconds < 0 || previousExpirySeconds < 0 || durationHours < 0
                || maximumDurationHours < 0 || count <= 0)
                throw new ArgumentOutOfRangeException("Timed appearance values are invalid.");
            long duration = checked((long)durationHours * 3600);
            long maximum = checked((long)maximumDurationHours * 3600);
            long start = canOverlap ? Math.Max(nowSeconds, previousExpirySeconds) : nowSeconds;
            long extension = canOverlap ? checked(duration * count) : duration;
            return Math.Min(checked(start + extension), checked(nowSeconds + maximum));
        }

        private static int LevelForExperience(long experience, IReadOnlyList<long> levels, int limit)
        {
            int max = Math.Min(Math.Max(0, limit), levels.Count - 1);
            int low = 0;
            int high = max;
            while (low < high)
            {
                int mid = low + (high - low + 1) / 2;
                if (levels[mid] <= experience) low = mid;
                else high = mid - 1;
            }
            return low;
        }

        private static long AtLevel(int level, IReadOnlyList<long> levels)
        {
            return levels[Math.Max(0, Math.Min(level, levels.Count - 1))];
        }

        private static int BlessLevelForExperience(int rank, long experience,
            IReadOnlyList<CharacterBlessLevelRecord> blessings)
        {
            int result = 0;
            if (blessings == null) return result;
            foreach (CharacterBlessLevelRecord row in blessings)
                if (row != null && row.Rank == rank && row.Exp <= experience)
                    result = Math.Max(result, row.BlessLevel);
            return result;
        }

        private static void FindBlessExperience(int rank, int level,
            IReadOnlyList<CharacterBlessLevelRecord> blessings, out long experience)
        {
            experience = 0;
            if (blessings == null) return;
            foreach (CharacterBlessLevelRecord row in blessings)
                if (row != null && row.Rank == rank && row.BlessLevel == level)
                {
                    experience = row.Exp;
                    return;
                }
        }
    }

    public static class OfficialCharacterProperties
    {
        private static readonly string[] BaseProperties = { "MaxHp", "Attack", "Defence", "Speed" };

        public static Dictionary<int, long> ProcessBattleProperties(IDictionary<int, long> raw,
            IReadOnlyDictionary<string, int> propertyIds)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            if (propertyIds == null) throw new ArgumentNullException(nameof(propertyIds));
            var result = new Dictionary<int, long>(raw);
            HandleBase(result, propertyIds);
            foreach (string name in BaseProperties)
                Increment(result, Id(propertyIds, name), Get(result, Id(propertyIds, name)),
                    Get(result, Id(propertyIds, name + "Percent")), true);
            HandleScale(result, propertyIds);
            return result;
        }

        public static Dictionary<int, long> DisplayProperties(IDictionary<int, long> total,
            IDictionary<int, long> source, IReadOnlyDictionary<string, int> propertyIds)
        {
            if (total == null) throw new ArgumentNullException(nameof(total));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyIds == null) throw new ArgumentNullException(nameof(propertyIds));
            var baseValues = new Dictionary<int, long>(total);
            var result = new Dictionary<int, long>(source);
            foreach (string name in BaseProperties)
            {
                int baseId = Id(propertyIds, "Base" + name);
                Increment(result, baseId, Get(baseValues, baseId), Get(result, Id(propertyIds, "Base" + name + "Rate")), false);
            }
            HandleRate(baseValues, propertyIds);
            HandleBasePercent(baseValues, result, propertyIds);
            TurnBase(result, propertyIds);
            HandleBase(result, propertyIds);
            foreach (string name in BaseProperties)
            {
                int propertyId = Id(propertyIds, name);
                long rate = Get(result, Id(propertyIds, name + "Percent"));
                if (name == "Defence" || name == "Speed") rate = Get(result, Id(propertyIds, "AttackPercent"));
                Increment(result, propertyId, Get(baseValues, propertyId), rate, true);
                if (propertyId != 0) result[propertyId] = Math.Max(1, Get(result, propertyId));
            }
            return result;
        }

        public static void Merge(Dictionary<int, long> target, IDictionary<int, long> addition)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (addition == null) return;
            foreach (KeyValuePair<int, long> entry in addition)
                target[entry.Key] = checked(Get(target, entry.Key) + entry.Value);
        }

        private static void HandleBase(Dictionary<int, long> props, IReadOnlyDictionary<string, int> ids)
        {
            HandleRate(props, ids);
            HandleBasePercent(props, props, ids);
            foreach (string name in new[] { "ElementMaster", "ElementResistance", "KongFuMaster",
                "KongFuResistance", "EffectRate", "EffectDodge" })
            {
                int id = Id(ids, name);
                Increment(props, id, Get(props, id), Get(props, Id(ids, name + "Percent")), false);
            }
            TurnBase(props, ids);
        }

        private static void HandleRate(Dictionary<int, long> props, IReadOnlyDictionary<string, int> ids)
        {
            foreach (string name in BaseProperties)
            {
                int baseId = Id(ids, "Base" + name);
                Increment(props, baseId, Get(props, baseId), Get(props, Id(ids, "Base" + name + "Rate")), false);
            }
        }

        private static void HandleBasePercent(Dictionary<int, long> baseValues, Dictionary<int, long> percentValues,
            IReadOnlyDictionary<string, int> ids)
        {
            foreach (string name in BaseProperties)
            {
                int rateId = Id(ids, "Base" + name + "Percent");
                long rate = Get(percentValues, rateId);
                if (rate == 0) continue;
                Increment(percentValues, Id(ids, name), Get(baseValues, Id(ids, "Base" + name)), rate, false);
                if (rateId != 0) percentValues.Remove(rateId);
            }
        }

        private static void TurnBase(Dictionary<int, long> props, IReadOnlyDictionary<string, int> ids)
        {
            foreach (string name in BaseProperties)
            {
                int baseId = Id(ids, "Base" + name);
                long value = Get(props, baseId);
                if (value == 0) continue;
                props.Remove(baseId);
                int id = Id(ids, name);
                if (id != 0) props[id] = checked(Get(props, id) + value);
            }
        }

        private static void HandleScale(Dictionary<int, long> props, IReadOnlyDictionary<string, int> ids)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed", "EffectDodge" })
            {
                int scaleId = Id(ids, name + "Scale");
                long scale = Get(props, scaleId);
                if (scale == 0) continue;
                int propertyId = Id(ids, name);
                long reference = checked(Get(props, propertyId) + Get(props, Id(ids, "Base" + name)));
                Increment(props, propertyId, reference, scale, true);
                if (scaleId != 0) props.Remove(scaleId);
            }
        }

        private static void Increment(Dictionary<int, long> props, int id, long reference, long rate, bool minimum)
        {
            if (id == 0 || rate == 0) return;
            long addition = (long)((double)reference * ((double)rate / 10000.0));
            long value = checked(Get(props, id) + addition);
            props[id] = minimum ? Math.Max(1, value) : value;
        }

        private static int Id(IReadOnlyDictionary<string, int> ids, string name)
        {
            return ids.TryGetValue(name, out int id) ? id : 0;
        }

        private static long Get(IDictionary<int, long> props, int id)
        {
            return id != 0 && props.TryGetValue(id, out long value) ? value : 0;
        }
    }
}
