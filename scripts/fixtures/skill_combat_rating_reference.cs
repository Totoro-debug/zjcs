using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace SourceSkillRatingReference
{
/* ORIGINAL_ENUMS */
    public enum ItemType { ActiveSkill = 100, PassiveSkill = 101 }
    public sealed class SkillInfo { public int ProfessionRank; public Profession UnlockProfession; }
    public sealed class SkillInfoParser
    {
        public bool ExistSkill(int id) => Validation.SkillIds.Contains(id);
        public SkillInfo Get(int id) => new SkillInfo { UnlockProfession = Validation.UnlockProfessions[id] };
    }
    public sealed class ConfigManager
    {
        public T GetParser<T>() where T : new() => new T();
    }
    public class ParserBase { public readonly ConfigManager ConfigManager = new ConfigManager(); }
    public sealed class ProfessionBaseInfo
    {
        public Profession Profession, PrePro;
        public int Rank;
        public List<int> Skills, FightUsedSkills;
    }
    public sealed class ProfessionBaseInfoParser : ParserBase
    {
        public ProfessionBaseInfo Get(Profession profession) => Validation.Professions[profession];
/* ORIGINAL_AVAILABILITY */
    }
    public sealed class ItemInfo { public int ClassId; public SkillInfo PartInfo; }
    public sealed class SkillParam { public int Rank, EffectiveLevel; }
    public sealed class PlayerItemDataWrap
    {
        public ItemInfo Info;
        public SkillParam Param;
        public SkillParam GetItemParamSkill() => Param;
    }
    public sealed class Settings
    {
        public int SkillRankAttack, SkillScoreLevelFactor;
        public float SkillRankScore, SkillLevelScore;
        public Dictionary<int, float> SkillProfessionRankFactors;
        public List<PropType> SkillScoreBase;
    }
    public sealed class PropCfgInfo
    {
        public double Score, PetScore;
        public bool Float;
        public int ScoreLevelGroupId;
        public List<PropType> ScoreBase;
    }
    public sealed class PropCfgInfoParser
    {
        public PropCfgInfo Get(PropType type)
        {
            var row = Validation.Character.Props.Get((int)type);
            return row == null ? null : new PropCfgInfo { Score = row.Score, PetScore = row.PetScore,
                Float = row.Float, ScoreLevelGroupId = row.ScoreLevelGroupId,
                ScoreBase = row.ScoreBase?.Select(id => (PropType)id).ToList() };
        }
    }
    public sealed class ScoreLevelGroupInfo { public int LevelNumberID; }
    public sealed class ScoreLevelGroupInfoParser
    {
        public ScoreLevelGroupInfo Get(int group, SubRank subRank)
        {
            int id = Validation.Character.Props.ScoreLevelNumberId(group, (int)subRank);
            return id == 0 ? null : new ScoreLevelGroupInfo { LevelNumberID = id };
        }
    }
    public sealed class LevelNumberParser
    {
        public float GetNumber(int id, int level) => Validation.Character.LevelNumber(id, level);
        public float GetNumberLerp(int id, float level) => Validation.Character.LevelNumberLerp(id, level);
    }
    public sealed class Parsers
    {
        public static readonly Parsers Instance = new Parsers();
        public static Settings GameSettings;
        public readonly PropCfgInfoParser PropCfgInfoParser = new PropCfgInfoParser();
        public readonly ScoreLevelGroupInfoParser ScoreLevelGroupInfoParser = new ScoreLevelGroupInfoParser();
        public readonly LevelNumberParser LevelNumberParser = new LevelNumberParser();
    }
    public sealed class Formula
    {
        public static readonly Formula Instance = new Formula();
        public int CalcPlayerSkillSlotsCount(ItemType type) => Validation.SlotCounts[(int)type];
/* ORIGINAL_LONG_TO_DOUBLE */
    }
    public struct GLoggerErrorInterpolatedStringHandler
    {
        public GLoggerErrorInterpolatedStringHandler(int literalLength, int count, out bool enabled) { enabled = false; }
        public void AppendLiteral(string value) { }
        public void AppendFormatted<T>(T value, int alignment, string format, string name) { }
    }
    public static class Log
    {
        public static void Error(ref GLoggerErrorInterpolatedStringHandler message, object logger, string method, string file, int line)
            => throw new InvalidOperationException("Missing original reference input: " + method);
    }
    public static class Extensions
    {
        public static bool IsBattleProp(this PropType type) => Validation.Character.Props.IsBattleProp((int)type);
    }
    public sealed class Handler
    {
/* ORIGINAL_METHODS */
        // Adapters supply the existing character-table lookups, without handling any rate or score.
        public Dictionary<PropType, long> CalcCharacterBattleProps(Profession pro,
            IDictionary<Profession, int> professionLevels, int level, SubRank subRank)
        {
            var result = new Dictionary<int, long>(Validation.Character.CalcCharacterBaseBattleProperties(level, (int)subRank));
            CharacterPropFormula.Merge(result, Validation.Character.CalcProfessionLevelProperties(
                professionLevels.Select(pair => new OfflineProfessionLevel { Profession = (int)pair.Key, Level = pair.Value })));
            var row = Validation.Character.Find("profession_base", (int)pro, "Profession");
            if (row != null && row.TryGetValue("FeatureProps", out object raw) && raw is Dictionary<string, object> values)
                foreach (var pair in values)
                {
                    int id = int.Parse(pair.Key, CultureInfo.InvariantCulture);
                    long value = Convert.ToInt64(pair.Value, CultureInfo.InvariantCulture);
                    result[id] = result.TryGetValue(id, out long previous) ? previous + value : value;
                }
            return Validation.ConvertProps(result);
        }
        public void CalcCombatRatingRefProps(SubRank subRank, int level, Dictionary<PropType, long> result)
        {
            var props = Validation.Character.Props;
            int id = props.RolePropGroupLevelPropId(props.CalcCombatPropGroupId, (int)subRank);
            var allowed = props.ByCombatRatingSort(OfficialCombatRatingSort.Base);
            foreach (var pair in Validation.Character.GetLevelProps(id, level))
                if (allowed.Contains(pair.Key)) result[(PropType)pair.Key] = pair.Value;
        }
        public void MergePropsInPlace(Dictionary<PropType, long> target, Dictionary<PropType, long> addition)
        {
            foreach (var pair in addition)
                target[pair.Key] = target.TryGetValue(pair.Key, out long value) ? value + pair.Value : pair.Value;
        }
    }
    public static class Validation
    {
        public static OfficialCharacterCatalog Character;
        public static readonly HashSet<int> SkillIds = new HashSet<int>();
        public static readonly Dictionary<int, Profession> UnlockProfessions = new Dictionary<int, Profession>();
        public static readonly Dictionary<Profession, ProfessionBaseInfo> Professions = new Dictionary<Profession, ProfessionBaseInfo>();
        public static readonly Dictionary<int, int> SlotCounts = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> ProfessionRanks = new Dictionary<int, int>();
        public static Dictionary<PropType, long> ConvertProps(IEnumerable<KeyValuePair<int, long>> props)
            => props.ToDictionary(pair => (PropType)pair.Key, pair => pair.Value);
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);

        public static void RunAndBuild()
        {
            Run();
            Zjcs.Editor.Skills.OfficialSkillAssetTools.BuildWindowsValidation();
        }

        public static void Run()
        {
            Zjcs.Editor.ProjectTools.Validate();
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            Character = assets.Load();
            var rules = assets.LoadSkillRules();
            SkillIds.UnionWith(rules.Skills.Select(row => row.ClassId));
            var settings = Character.Rows("game_settings").First();
            var factors = (Dictionary<string, object>)settings["SkillProfessionRankFactors"];
            Parsers.GameSettings = new Settings { SkillRankAttack = Int(settings, "SkillRankAttack"),
                SkillScoreLevelFactor = Int(settings, "SkillScoreLevelFactor"),
                SkillRankScore = Convert.ToSingle(settings["SkillRankScore"], CultureInfo.InvariantCulture),
                SkillLevelScore = Convert.ToSingle(settings["SkillLevelScore"], CultureInfo.InvariantCulture),
                SkillProfessionRankFactors = factors.ToDictionary(pair => int.Parse(pair.Key), pair => Convert.ToSingle(pair.Value)),
                SkillScoreBase = ((List<object>)settings["SkillScoreBase"]).Select(value => (PropType)Convert.ToInt32(value)).ToList() };
            foreach (var pair in (Dictionary<string, object>)settings["SkillTypes"])
                SlotCounts[int.Parse(pair.Key)] = Convert.ToInt32(pair.Value);
            foreach (var profession in Character.Rows("profession_base"))
            {
                var professionId = (Profession)Int(profession, "Profession");
                Professions[professionId] = new ProfessionBaseInfo { Profession = professionId,
                    PrePro = (Profession)Int(profession, "PrePro"), Rank = Int(profession, "Rank"),
                    Skills = (profession["Skills"] as List<object>)?.Select(Convert.ToInt32).ToList() ?? new List<int>(),
                    FightUsedSkills = (profession["FightUsedSkills"] as List<object>)?.Select(Convert.ToInt32).ToList() };
                if (!(profession["Skills"] is List<object> ids) || ids.Count == 0) continue;
                foreach (object id in ids)
                {
                    ProfessionRanks[Convert.ToInt32(id)] = Int(profession, "Rank");
                    UnlockProfessions[Convert.ToInt32(id)] = professionId;
                }
                if (profession["FightUsedSkills"] is List<object> fight)
                    foreach (object id in fight) ProfessionRanks[Convert.ToInt32(id)] = Int(profession, "Rank");
            }
            var selected = Character.Rows("profession_base").Where(row => new[] { 1, 10, 2, 6, 11, 15 }.Contains(Int(row, "Profession")))
                .SelectMany(row => ((List<object>)row["Skills"]).Concat((List<object>)row["FightUsedSkills"]))
                .Select(Convert.ToInt32).Distinct().ToArray();
            Require(selected.Length == 104, "Original first-promotion item scope differs.");
            var source = new OfflineSkillPropSource(Character, rules);
            int comparisons = 0, positive = 0;
            foreach (int profession in new[] { 1, 10, 2, 6, 11, 15 })
            foreach (int level in new[] { 1, 50, 500 })
            foreach (int subRank in new[] { 1, 5, 40 })
            foreach (int rank in new[] { 1, 17, 34 })
            {
                var player = new OfflinePlayerProgress { Level = level, SubRank = subRank, SeasonRank = 17 };
                player.Wardrobe.Profession = profession;
                player.ProfessionLevels.Add(new OfflineProfessionLevel { Profession = profession, Level = 20 });
                player.Skills.ActiveSlotLevels = new[] { 2, 30, 9, 20 };
                player.Skills.PassiveSlotLevels = new[] { 22, 3, 13, 5 };
                player.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 18, ActiveSlotLevels = new[] { 99, 99, 99, 99 }, PassiveSlotLevels = new[] { 99, 99, 99, 99 } });
                player.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 17, ActiveSlotLevels = new[] { 1, 4, 0, 3 }, PassiveSlotLevels = new[] { 4, 0, 3, 1 } });
                foreach (int id in selected)
                {
                    player.Skills.Skills.Clear();
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = rank });
                    foreach (bool playerReference in new[] { true, false })
                    {
                        long expected = Expected(player, rules, playerReference);
                        Equal(source, player, playerReference, expected); comparisons++;
                        if (expected > 0) positive++;
                    }
                }
                player.Skills.Skills = selected.Select((id, index) => new OfflineOwnedSkill { ClassId = id, Rank = 1 + index % rank }).ToList();
                long mixed = Expected(player, rules, true);
                foreach (int plan in Enumerable.Range(0, 7))
                {
                    player.Skills.SelectedPlan = plan;
                    Equal(source, player, true, mixed); comparisons++;
                }
            }
            Require(positive > 0, "Production inputs contain no scored available skills.");
            // Probe original branches whose factors are zero in the current production score-base rows.
            // These are isolated fixture mutations, restored before leaving the validation method.
            var featureRow = Character.Find("profession_base", 1, "Profession");
            var features = (Dictionary<string, object>)featureRow["FeatureProps"];
            string rateId = Character.Props.Id("BaseAttackRate").ToString(CultureInfo.InvariantCulture);
            bool hadRate = features.TryGetValue(rateId, out object previousRate);
            var attackDefinition = Character.Props.Get(Character.Props.Id("BaseAttack"));
            int previousGroup = attackDefinition.ScoreLevelGroupId;
            object previousFactor = settings["SkillScoreLevelFactor"];
            int branchComparisons = 0;
            try
            {
                features[rateId] = 2500L;
                attackDefinition.ScoreLevelGroupId = Character.Rows("score_level_group")
                    .Where(row => Int(row, "SubRank") == 5).Select(row => Int(row, "ID")).First();
                var probe = new OfflinePlayerProgress { Level = 50, SubRank = 5 };
                probe.Wardrobe.Profession = 1;
                probe.Skills.ActiveSlotLevels = new[] { 2, 30, 9, 20 };
                probe.Skills.PassiveSlotLevels = new[] { 22, 3, 13, 5 };
                probe.Skills.Skills = selected.Select(id => new OfflineOwnedSkill { ClassId = id, Rank = 17 }).ToList();
                foreach (int factorId in new[] { 0, Parsers.GameSettings.SkillRankAttack })
                {
                    settings["SkillScoreLevelFactor"] = factorId;
                    Parsers.GameSettings.SkillScoreLevelFactor = factorId;
                    foreach (bool playerReference in new[] { true, false })
                    {
                        Equal(source, probe, playerReference, Expected(probe, rules, playerReference));
                        branchComparisons++;
                    }
                }
            }
            finally
            {
                if (hadRate) features[rateId] = previousRate; else features.Remove(rateId);
                attackDefinition.ScoreLevelGroupId = previousGroup;
                settings["SkillScoreLevelFactor"] = previousFactor;
                Parsers.GameSettings.SkillScoreLevelFactor = Convert.ToInt32(previousFactor);
            }
            Debug.Log("[Skill combat rating] Unchanged original methods passed " + comparisons + " production comparisons; " + positive + " single-skill scores were positive.");
            Debug.Log("[Skill combat rating] Nonzero base-rate, score-level and optional factor branches passed " + branchComparisons + " isolated comparisons.");
        }

        private static CharacterPropScoreContext Context(OfflinePlayerProgress player) => new CharacterPropScoreContext
        {
            Level = player.Level, SubRank = player.SubRank, Props = Character.Props,
            BasicRef = CharacterPropBasicRef.Build(player.Level, player.SubRank, Character.Props, Character.GetLevelProps)
        };
        private static void Equal(OfflineSkillPropSource source, OfflinePlayerProgress player, bool playerReference, long expected)
        {
            long actual = source.CalculateCombatRating(player, new Dictionary<int, long>(), Context(player), playerReference);
            Require(actual == expected, "Skill rating differs: profession=" + player.Wardrobe.Profession + " level=" + player.Level
                + " subRank=" + player.SubRank + " reference=" + playerReference + " actual=" + actual + " expected=" + expected);
        }
        private static long Expected(OfflinePlayerProgress player, OfflineSkillRules rules, bool playerReference)
        {
            var active = Wrap(player, rules, 100);
            var passive = Wrap(player, rules, 101);
            return new Handler().CalcCombatRatingSkill(player.Level, active, passive,
                ConvertProps(Context(player).BasicRef.RateFormateBaseProps), (SubRank)player.SubRank, playerReference,
                (Profession)player.Wardrobe.Profession, player.ProfessionLevels.ToDictionary(row => (Profession)row.Profession, row => row.Level));
        }
        private static List<PlayerItemDataWrap> Wrap(OfflinePlayerProgress player, OfflineSkillRules rules, int type)
        {
            var result = new List<PlayerItemDataWrap>();
            var levels = type == 100 ? player.Skills.ActiveSlotLevels : player.Skills.PassiveSlotLevels;
            var bless = player.Skills.BlessRecords.FirstOrDefault(row => row.SeasonId == player.SeasonRank);
            var extra = bless == null ? null : type == 100 ? bless.ActiveSlotLevels : bless.PassiveSlotLevels;
            int[] effective = levels.Select((level, index) => Math.Max(1, level + (extra == null ? 0 : extra[index]))).OrderByDescending(value => value).ToArray();
            foreach (var owned in player.Skills.Skills.OrderByDescending(row => row.Rank))
            {
                if (owned.Rank <= 0 || !new ProfessionBaseInfoParser().IsSkillAvailableForProfession(owned.ClassId,
                    (Profession)player.Wardrobe.Profession) || Character.ItemType(owned.ClassId) != type) continue;
                result.Add(new PlayerItemDataWrap { Info = new ItemInfo { ClassId = owned.ClassId,
                    PartInfo = new SkillInfo { ProfessionRank = ProfessionRanks.TryGetValue(owned.ClassId, out int rank) ? rank : 0 } },
                    Param = new SkillParam { Rank = owned.Rank, EffectiveLevel = effective[Math.Min(result.Count, effective.Length - 1)] } });
            }
            return result;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
