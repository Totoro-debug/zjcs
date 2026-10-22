using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    /// <summary>Original <c>EPropHandleType</c> chunk identity used by the property pipeline.</summary>
    public static class CharacterPropHandleType
    {
        public const int BattleBasic = 0;
        public const int NoBattleBasic = 1;
        public const int Skill = 2;
        public const int Equip = 3;
        public const int EquipResonance = 4;
        public const int Treasure = 5;
        public const int TreasureSuit = 6;
        public const int Gem = 7;
        public const int Cooking = 8;
        public const int Extra = 9;
        public const int NpcFriendshipLevel = 10;
        public const int Pet = 11;
        public const int LostGoddess = 12;
        public const int PeriodIcCar = 13;
        public const int Legion = 14;
        public const int EquipCabinet = 15;
        public const int Astrological = 16;
        public const int Appearance = 17;
        public const int NpcTalentTree = 18;
    }

    /// <summary>Original <c>PropDetailCategory</c> identity of a detailed property source.</summary>
    public static class CharacterPropDetailCategory
    {
        public const int CharacterBase = 0;
        public const int Profession = 1;
        public const int Equip = 2;
        public const int Skill = 3;
        public const int Treasure = 4;
        public const int Gem = 5;
        public const int Cooking = 6;
        public const int Extra = 7;
        public const int Npc = 8;
        public const int Pet = 9;
        public const int Legion = 10;
        public const int Appearance = 12;
        public const int Astrological = 13;
    }

    /// <summary>
    /// Supplied by a subsystem that owns one original property chunk (equipment, skills, ...).
    /// </summary>
    public interface IOfflineCharacterPropSource
    {
        int HandleType { get; }
        IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player);
        long CalculateCombatRating(OfflinePlayerProgress player, IReadOnlyDictionary<int, long> props,
            CharacterPropScoreContext context);
    }

    /// <summary>Inputs shared by chunk combat-rating calculations.</summary>
    public sealed class CharacterPropScoreContext
    {
        public int Level;
        public int SubRank;
        public OfficialPropCatalog Props;
        public CharacterPropBasicRef BasicRef;

        public long Common(IReadOnlyDictionary<int, long> props)
        {
            return CharacterPropFormula.CombatRatingCommon(Level, SubRank, props, Props, BasicRef, true);
        }
    }

    /// <summary>
    /// Original <c>CommonCombatRatingRefData</c>: pure base properties, rate-formatted base
    /// properties and non-base reference properties of the current level and sub-rank.
    /// </summary>
    public sealed class CharacterPropBasicRef
    {
        public Dictionary<int, long> PureBaseProps { get; } = new Dictionary<int, long>();
        public Dictionary<int, long> RateFormateBaseProps { get; } = new Dictionary<int, long>();
        public Dictionary<int, long> RefNonBaseProps { get; } = new Dictionary<int, long>();

        public static CharacterPropBasicRef Build(int level, int subRank, OfficialPropCatalog props,
            Func<int, int, IReadOnlyDictionary<int, long>> levelProps)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            if (levelProps == null) throw new ArgumentNullException(nameof(levelProps));
            var result = new CharacterPropBasicRef();
            if (CharacterPropFormula.CalcCombatRatingRefProps(level, subRank, props, levelProps,
                    result.RateFormateBaseProps, true))
            {
                CharacterPropFormula.RetrieveProps(result.RateFormateBaseProps,
                    props.ByCombatRatingSort(OfficialCombatRatingSort.Base), result.PureBaseProps);
                CharacterPropFormula.HandlePropRate(result.RateFormateBaseProps, props);
            }
            CharacterPropFormula.CalcCombatRatingRefProps(level, subRank, props, levelProps,
                result.RefNonBaseProps, false);
            return result;
        }

        public void Update(int level, int subRank, Dictionary<int, long> baseProps, OfficialPropCatalog props,
            Func<int, int, IReadOnlyDictionary<int, long>> levelProps)
        {
            if (baseProps == null) throw new ArgumentNullException(nameof(baseProps));
            PureBaseProps.Clear();
            RateFormateBaseProps.Clear();
            RefNonBaseProps.Clear();
            IReadOnlyList<int> baseTypes = props.ByCombatRatingSort(OfficialCombatRatingSort.Base);
            CharacterPropFormula.RetrieveProps(baseProps, baseTypes, PureBaseProps);
            CharacterPropFormula.HandlePropRate(baseProps, props);
            CharacterPropFormula.Merge(RateFormateBaseProps, baseProps);
            CharacterPropFormula.CalcCombatRatingRefProps(level, subRank, props, levelProps, RefNonBaseProps, false);
        }
    }

    /// <summary>
    /// Faithful port of the original <c>BattleFormulaHandler</c> steps used outside of combat.
    /// </summary>
    public static class CharacterPropFormula
    {
        private static readonly int[] CoreProperties = new int[4];

        public static void Configure(OfficialPropCatalog props)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            CoreProperties[0] = props.Id("MaxHp");
            CoreProperties[1] = props.Id("Attack");
            CoreProperties[2] = props.Id("Defence");
            CoreProperties[3] = props.Id("Speed");
        }

        public static void Merge(Dictionary<int, long> target, IEnumerable<KeyValuePair<int, long>> addition)
        {
            if (target == null) return;
            if (addition == null) return;
            foreach (KeyValuePair<int, long> entry in addition)
                target[entry.Key] = target.TryGetValue(entry.Key, out long value)
                    ? value + entry.Value : entry.Value;
        }

        public static void RetrieveProps(IEnumerable<KeyValuePair<int, long>> source, IReadOnlyList<int> wanted,
            Dictionary<int, long> result)
        {
            result.Clear();
            if (source == null || wanted == null) return;
            foreach (KeyValuePair<int, long> entry in source)
                if (wanted.Contains(entry.Key)) result.Add(entry.Key, entry.Value);
        }

        public static long Raw(IReadOnlyDictionary<int, long> props, int propType)
        {
            return propType != 0 && props != null && props.TryGetValue(propType, out long value) ? value : 0L;
        }

        private static long RawMutable(IDictionary<int, long> props, int propType)
        {
            return propType != 0 && props != null && props.TryGetValue(propType, out long value) ? value : 0L;
        }

        public static double Value(IReadOnlyDictionary<int, long> props, int propType, OfficialPropCatalog catalog)
        {
            OfficialPropDefinition definition = catalog.Get(propType);
            long value = Raw(props, propType);
            return definition != null && definition.Float ? value / 10000.0 : value;
        }

        /// <summary>Original <c>HandlePropRate</c>: applies Base*Rate to the raw base properties.</summary>
        public static void HandlePropRate(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int baseId = catalog.Id("Base" + name);
                int rateId = catalog.Id("Base" + name + "Rate");
                long rate = RawMutable(props, rateId);
                if (rate == 0) continue;
                long current = RawMutable(props, baseId);
                props[baseId] = current + (long)(current * (rate / 10000.0));
            }
        }

        /// <summary>Original <c>HandleBasePercentProps(Dictionary)</c>.</summary>
        public static void HandleBasePercentProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int baseId = catalog.Id("Base" + name);
                long percent = Raw(props, catalog.Id("Base" + name + "Percent"));
                if (percent == 0) continue;
                long value = RawMutable(props, baseId);
                int propertyId = catalog.Id(name);
                props[propertyId] = RawMutable(props, propertyId)
                    + (long)(value * (percent / 10000.0));
            }
        }

        /// <summary>Original <c>HandleOtherBasePercentProps</c>.</summary>
        public static void HandleOtherBasePercentProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "ElementMaster", "ElementResistance", "KongFuMaster",
                "KongFuResistance", "EffectRate", "EffectDodge" })
            {
                int percentId = catalog.Id(name + "Percent");
                long percent = Raw(props, percentId);
                if (percent == 0) continue;
                int id = catalog.Id(name);
                long value = Raw(props, id);
                props[id] = value + (long)(value * (percent / 10000.0));
            }
        }

        /// <summary>Original <c>TurnBaseProps</c>.</summary>
        public static void TurnBaseProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int baseId = catalog.Id("Base" + name);
                long value = RawMutable(props, baseId);
                if (value == 0) continue;
                props.Remove(baseId);
                int id = catalog.Id(name);
                props[id] = Raw(props, id) + value;
            }
        }

        /// <summary>Original <c>HandleBaseProps</c>.</summary>
        public static void HandleBaseProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            HandlePropRate(props, catalog);
            HandleBasePercentProps(props, catalog);
            HandleOtherBasePercentProps(props, catalog);
            TurnBaseProps(props, catalog);
        }

        /// <summary>Original <c>HandlePercentProps</c>.</summary>
        public static void HandlePercentProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int percentId = catalog.Id(name + "Percent");
                long percent = Raw(props, percentId);
                if (percent == 0) continue;
                int id = catalog.Id(name);
                long value = Raw(props, id);
                value += (long)(value * (percent / 10000.0));
                props[id] = Math.Max(1L, value);
            }
        }

        /// <summary>Original <c>HandleScaleProps</c>.</summary>
        public static void HandleScaleProps(Dictionary<int, long> props, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed", "EffectDodge" })
            {
                int scaleId = catalog.Id(name + "Scale");
                long scale = Raw(props, scaleId);
                if (scale == 0) continue;
                int id = catalog.Id(name);
                long value = Raw(props, id);
                long reference = value + Raw(props, catalog.Id("Base" + name));
                value += (long)((reference) * (scale / 10000.0));
                props[id] = Math.Max(1L, value);
            }
        }

        /// <summary>Original <c>ProcessBattleProp</c>.</summary>
        public static Dictionary<int, long> ProcessBattleProps(IDictionary<int, long> source,
            OfficialPropCatalog catalog)
        {
            var result = source == null ? new Dictionary<int, long>() : new Dictionary<int, long>(source);
            HandleBaseProps(result, catalog);
            HandlePercentProps(result, catalog);
            HandleScaleProps(result, catalog);
            return result;
        }

        /// <summary>Original <c>HandleRawBaseRate</c>: base rates merge into the displayed source.</summary>
        public static void HandleRawBaseRate(IReadOnlyDictionary<int, long> total, Dictionary<int, long> source,
            OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int rateId = catalog.Id("Base" + name + "Rate");
                long rate = Raw(source, rateId);
                if (rate == 0) continue;
                int baseId = catalog.Id("Base" + name);
                long reference = Raw(total, baseId);
                source[baseId] = Raw(source, baseId) + (long)(reference * (rate / 10000.0));
            }
        }

        /// <summary>Original <c>HandleBasePercentProps(total, percent)</c>.</summary>
        public static void HandleSystemBasePercentProps(IReadOnlyDictionary<int, long> total,
            Dictionary<int, long> source, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int percentId = catalog.Id("Base" + name + "Percent");
                long percent = Raw(source, percentId);
                if (percent == 0) continue;
                int baseId = catalog.Id("Base" + name);
                long reference = Raw(total, baseId);
                int id = catalog.Id(name);
                source[id] = Raw(source, id) + (long)(reference * (percent / 10000.0));
                source.Remove(percentId);
            }
        }

        /// <summary>Original <c>HandleNonBasePercentProps</c>.</summary>
        public static void HandleNonBasePercentProps(IReadOnlyDictionary<int, long> total,
            Dictionary<int, long> source, OfficialPropCatalog catalog)
        {
            foreach (string name in new[] { "MaxHp", "Attack", "Defence", "Speed" })
            {
                int percentId = catalog.Id(name + "Percent");
                long percent = Raw(source, percentId);
                if (percent == 0) continue;
                int id = catalog.Id(name);
                long reference = Raw(total, id);
                long value = Raw(source, id) + (long)(reference * (percent / 10000.0));
                source[id] = Math.Max(1L, value);
            }
        }

        /// <summary>Original <c>HandleSingleSystemDisplayProp</c>.</summary>
        public static Dictionary<int, long> SingleSystemDisplay(IReadOnlyDictionary<int, long> total,
            IReadOnlyDictionary<int, long> source, OfficialPropCatalog catalog)
        {
            var totalCopy = new Dictionary<int, long>(total);
            var result = source == null ? new Dictionary<int, long>() : new Dictionary<int, long>(source);
            HandleRawBaseRate(totalCopy, result, catalog);
            HandlePropRate(totalCopy, catalog);
            HandleSystemBasePercentProps(totalCopy, result, catalog);
            TurnBaseProps(result, catalog);
            HandleBaseProps(totalCopy, catalog);
            HandleNonBasePercentProps(totalCopy, result, catalog);
            return result;
        }

        /// <summary>Original <c>CalcCombatRatingByProps</c> (level-number factors omitted when absent).</summary>
        public static long CombatRatingByProps(int level, int subRank, IReadOnlyDictionary<int, long> props,
            OfficialPropCatalog catalog, IReadOnlyDictionary<int, long> baseProps = null,
            IReadOnlyList<int> propTypes = null, bool useCommonScore = true)
        {
            if (props == null) return 0L;
            long result = 0L;
            foreach (KeyValuePair<int, long> entry in props)
            {
                if (propTypes != null && !propTypes.Contains(entry.Key)) continue;
                OfficialPropDefinition definition = catalog.Get(entry.Key);
                if (definition == null) continue;
                double score = useCommonScore ? definition.Score : definition.PetScore;
                double prop = Value(props, entry.Key, catalog);
                long value = definition.ScoreBase == null || definition.ScoreBase.Length == 0
                    ? (long)(score * prop)
                    : (long)(score * prop * CombatRatingByProps(level, subRank, baseProps, catalog, null,
                        definition.ScoreBase, useCommonScore));
                // ScoreLevelGroupId factors require the level_number table; the pinned character
                // catalog only assigns it to element affinity properties, which are not part of
                // any offline chunk yet, so the untracted factor stays at 1.
                result += value;
            }
            return result;
        }

        /// <summary>Original <c>CalcCombatRatingCommon</c>.</summary>
        public static long CombatRatingCommon(int level, int subRank, IReadOnlyDictionary<int, long> props,
            OfficialPropCatalog catalog, CharacterPropBasicRef basicRef, bool useCommonScore = true)
        {
            if (props == null) return 0L;
            var selected = new Dictionary<int, long>();
            long result = 0L;
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.Base), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, null, null, useCommonScore);
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.NonBase), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, null, null, useCommonScore);
            if (basicRef == null) return result;
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.BaseRate), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, basicRef.PureBaseProps, null,
                useCommonScore);
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.BasePercent), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, basicRef.RateFormateBaseProps, null,
                useCommonScore);
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.Advanced), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, basicRef.RateFormateBaseProps, null,
                useCommonScore);
            RetrieveProps(props, catalog.ByCombatRatingSort(OfficialCombatRatingSort.NonBasePercent), selected);
            result += CombatRatingByProps(level, subRank, selected, catalog, basicRef.RefNonBaseProps, null,
                useCommonScore);
            return result;
        }

        /// <summary>Original <c>CalcCombatRatingRefProps</c>.</summary>
        public static bool CalcCombatRatingRefProps(int level, int subRank, OfficialPropCatalog catalog,
            Func<int, int, IReadOnlyDictionary<int, long>> levelProps, Dictionary<int, long> result, bool isBase)
        {
            result.Clear();
            int levelPropId = catalog.RolePropGroupLevelPropId(catalog.CalcCombatPropGroupId, subRank);
            if (levelPropId == 0) return false;
            IReadOnlyDictionary<int, long> source = levelProps(levelPropId, level);
            if (source == null || source.Count == 0) return false;
            RetrieveProps(source, catalog.ByCombatRatingSort(isBase
                ? OfficialCombatRatingSort.Base : OfficialCombatRatingSort.NonBase), result);
            return true;
        }
    }

    public sealed class CharacterPropSnapshot
    {
        public IReadOnlyDictionary<int, long> BaseBattleProps;
        public IReadOnlyDictionary<int, long> BattleProps;
        public IReadOnlyDictionary<int, long> OutOfFightBattleProps;
        public IReadOnlyDictionary<int, long> NonBattleProps;
        public long CombatRating;
        public IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> Chunks;
        public IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> DetailProps;
        public IReadOnlyList<int> DetailOrder;
    }

    /// <summary>
    /// Rebuilds the original <c>PlayerPropContext</c> chunk pipeline for the offline profile.
    /// Chunks owned by other features are supplied as <see cref="IOfflineCharacterPropSource"/>.
    /// </summary>
    public sealed class CharacterPropCalculator
    {
        private static readonly int[] DetailOrder =
        {
            CharacterPropDetailCategory.CharacterBase, CharacterPropDetailCategory.Profession,
            CharacterPropDetailCategory.Npc, CharacterPropDetailCategory.Equip,
            CharacterPropDetailCategory.Skill, CharacterPropDetailCategory.Treasure,
            CharacterPropDetailCategory.Gem, CharacterPropDetailCategory.Cooking,
            CharacterPropDetailCategory.Extra, CharacterPropDetailCategory.Pet,
            CharacterPropDetailCategory.Legion, CharacterPropDetailCategory.Appearance,
            CharacterPropDetailCategory.Astrological
        };

        private readonly OfficialCharacterCatalog catalog;
        private readonly OfficialPropCatalog props;
        private readonly List<IOfflineCharacterPropSource> sources = new List<IOfflineCharacterPropSource>();

        public CharacterPropCalculator(OfficialCharacterCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            props = catalog.Props;
            CharacterPropFormula.Configure(props);
        }

        public OfficialPropCatalog Props => props;

        public void AddSource(IOfflineCharacterPropSource source)
        {
            if (source == null) return;
            sources.RemoveAll(value => value.HandleType == source.HandleType);
            sources.Add(source);
        }

        public CharacterPropSnapshot Evaluate(OfflinePlayerProgress player, long nowUnixSeconds)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            int level = Math.Max(1, player.Level);
            int subRank = Math.Max(1, player.SubRank);
            var chunks = new Dictionary<int, IReadOnlyDictionary<int, long>>();
            Dictionary<int, long> characterBase = new Dictionary<int, long>(
                catalog.CalcCharacterBaseBattleProperties(level, subRank));
            Dictionary<int, long> profession = ProfessionFeatureProps(player);
            var basic = new Dictionary<int, long>(characterBase);
            CharacterPropFormula.Merge(basic, profession);
            chunks[CharacterPropHandleType.BattleBasic] = basic;
            Dictionary<int, long> appearance = AppearanceProps(player, nowUnixSeconds);
            chunks[CharacterPropHandleType.Appearance] = appearance;
            foreach (IOfflineCharacterPropSource source in sources)
                chunks[source.HandleType] = source.CalculateProps(player) ?? new Dictionary<int, long>();

            var scoreContext = new CharacterPropScoreContext
            {
                Level = level,
                SubRank = subRank,
                Props = props,
                BasicRef = CharacterPropBasicRef.Build(level, subRank, props, LevelProps)
            };
            long combatRating = 0L;
            var chunkRatings = new Dictionary<int, long>();
            foreach (KeyValuePair<int, IReadOnlyDictionary<int, long>> chunk in chunks)
            {
                long rating = CharacterPropFormula.CombatRatingCommon(level, subRank, chunk.Value, props,
                    scoreContext.BasicRef);
                IOfflineCharacterPropSource source = sources.FirstOrDefault(value => value.HandleType == chunk.Key);
                if (source != null)
                    rating = source.CalculateCombatRating(player, chunk.Value, scoreContext);
                chunkRatings[chunk.Key] = rating;
                combatRating += rating;
            }

            var rawBattle = new Dictionary<int, long>();
            var rawNonBattle = new Dictionary<int, long>();
            foreach (KeyValuePair<int, IReadOnlyDictionary<int, long>> chunk in chunks)
            {
                if (chunk.Key == CharacterPropHandleType.Skill) continue;
                foreach (KeyValuePair<int, long> entry in chunk.Value)
                {
                    if (props.IsBattleProp(entry.Key))
                        rawBattle[entry.Key] = rawBattle.TryGetValue(entry.Key, out long current)
                            ? current + entry.Value : entry.Value;
                    else
                        rawNonBattle[entry.Key] = rawNonBattle.TryGetValue(entry.Key, out long current)
                            ? current + entry.Value : entry.Value;
                }
            }
            Dictionary<int, long> outOfFight = CharacterPropFormula.ProcessBattleProps(rawBattle, props);
            IReadOnlyDictionary<int, long> totalBase = rawBattle;

            var details = new Dictionary<int, IReadOnlyDictionary<int, long>>();
            foreach (int category in DetailOrder)
            {
                Dictionary<int, long> source = DetailSource(chunks, category, characterBase, profession);
                if (source == null || source.Count == 0) continue;
                details[category] = CharacterPropFormula.SingleSystemDisplay(totalBase, source, props);
            }
            return new CharacterPropSnapshot
            {
                BaseBattleProps = basic,
                BattleProps = totalBase,
                OutOfFightBattleProps = outOfFight,
                NonBattleProps = rawNonBattle,
                CombatRating = combatRating,
                Chunks = chunks,
                DetailProps = details,
                DetailOrder = DetailOrder
            };
        }

        public IReadOnlyDictionary<int, long> LevelProps(int propClassId, int level)
        {
            return catalog.GetLevelProps(propClassId, level);
        }

        private Dictionary<int, long> AppearanceProps(OfflinePlayerProgress player, long nowUnixSeconds)
        {
            Dictionary<int, long> expiryTimes = player.Wardrobe.Expiries
                .ToDictionary(value => value.ItemId, value => value.ExpiresAtUnixSeconds);
            IReadOnlyDictionary<int, long> source = catalog.AppearanceProperties(
                player.Wardrobe.AppearanceLevel, player.Inventory, expiryTimes, nowUnixSeconds);
            return source == null ? new Dictionary<int, long>() : new Dictionary<int, long>(source);
        }

        private Dictionary<int, long> ProfessionFeatureProps(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            int profession = player.Wardrobe.Profession;
            if (profession <= 0) return result;
            List<Dictionary<string, object>> rows = catalog.Rows("profession_base")
                .Where(row => Integer(row, "Profession") == profession
                    && Integer(row, "Rank") <= Math.Max(1, player.MaxProfessionRank)).ToList();
            if (rows.Count == 0) return result;
            Dictionary<string, object> row = rows.OrderByDescending(value => Integer(value, "Rank")).First();
            if (row.TryGetValue("FeatureProps", out object feature) && feature is Dictionary<string, object> map)
                foreach (KeyValuePair<string, object> entry in map)
                {
                    int propType = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                    long amount = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                    result[propType] = result.TryGetValue(propType, out long current) ? current + amount : amount;
                }
            return result;
        }

        private static Dictionary<int, long> DetailSource(
            IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> chunks, int category,
            IReadOnlyDictionary<int, long> characterBase, IReadOnlyDictionary<int, long> profession)
        {
            switch (category)
            {
                case CharacterPropDetailCategory.CharacterBase:
                    return new Dictionary<int, long>(characterBase);
                case CharacterPropDetailCategory.Profession:
                    return profession == null || profession.Count == 0 ? null
                        : new Dictionary<int, long>(profession);
                case CharacterPropDetailCategory.Equip:
                    return MergeChunks(chunks, CharacterPropHandleType.Equip, CharacterPropHandleType.EquipResonance);
                case CharacterPropDetailCategory.Skill:
                    return MergeChunks(chunks, CharacterPropHandleType.Skill);
                case CharacterPropDetailCategory.Treasure:
                    return MergeChunks(chunks, CharacterPropHandleType.Treasure, CharacterPropHandleType.TreasureSuit);
                case CharacterPropDetailCategory.Gem:
                    return MergeChunks(chunks, CharacterPropHandleType.Gem);
                case CharacterPropDetailCategory.Cooking:
                    return MergeChunks(chunks, CharacterPropHandleType.Cooking);
                case CharacterPropDetailCategory.Extra:
                    return MergeChunks(chunks, CharacterPropHandleType.Extra);
                case CharacterPropDetailCategory.Npc:
                    return MergeChunks(chunks, CharacterPropHandleType.NpcFriendshipLevel,
                        CharacterPropHandleType.NpcTalentTree);
                case CharacterPropDetailCategory.Pet:
                    return MergeChunks(chunks, CharacterPropHandleType.Pet);
                case CharacterPropDetailCategory.Legion:
                    return MergeChunks(chunks, CharacterPropHandleType.Legion);
                case CharacterPropDetailCategory.Appearance:
                    return MergeChunks(chunks, CharacterPropHandleType.Appearance);
                case CharacterPropDetailCategory.Astrological:
                    return MergeChunks(chunks, CharacterPropHandleType.Astrological,
                        CharacterPropHandleType.EquipCabinet);
                default:
                    return null;
            }
        }

        private static Dictionary<int, long> MergeChunks(
            IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> chunks, params int[] handleTypes)
        {
            Dictionary<int, long> result = null;
            foreach (int handleType in handleTypes)
            {
                if (!chunks.TryGetValue(handleType, out IReadOnlyDictionary<int, long> source)
                    || source.Count == 0) continue;
                result = result ?? new Dictionary<int, long>();
                CharacterPropFormula.Merge(result, source);
            }
            return result;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }
    }
}

