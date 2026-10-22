using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightDamagePropertySchema
    {
        public List<FightPropertyName> Names = new List<FightPropertyName>();
        public List<int> FloatPropertyIds = new List<int>();

        public int Id(string name)
        {
            for (int i = 0; i < Names.Count; i++)
                if (Names[i].Name == name) return Names[i].Id;
            return 0;
        }

        public bool IsFloat(int id)
        {
            for (int i = 0; i < FloatPropertyIds.Count; i++)
                if (FloatPropertyIds[i] == id) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class FightPropertyName
    {
        public string Name;
        public int Id;
    }

    [Serializable]
    public sealed class FightFormulaTableRow
    {
        public int Key;
        public int Value;
        public int SecondaryValue;
    }

    [Serializable]
    public sealed class FightDamageFormulaConfig
    {
        public int FixedDamageLimitPercent;
        public double MinCritPowerPercent;
        public double MinBlockValuePercent;
        public int MaxLevelOffsetDamageScale;
        public double CureAddSoftCapThreshold;
        public double CureAddOverflowScale;
        public List<FightFormulaTableRow> LevelOffsetDamage = new List<FightFormulaTableRow>();
        public List<FightFormulaTableRow> RankOffsetDamage = new List<FightFormulaTableRow>();
        public List<FightFormulaTableRow> DemonRankOffsetDamage = new List<FightFormulaTableRow>();
        public List<FightFormulaTableRow> MonsterSpecialProps = new List<FightFormulaTableRow>();
    }

    public sealed class FightDamageFormulaRequest
    {
        public FightDamagePropertySchema Schema;
        public IDictionary<int, long> SourceProps;
        public IDictionary<int, long> TargetProps;
        public int SkillDamageBasicPropType;
        public int SkillDamagePropType;
        public int SkillDamageFixedPropType;
        public int SourceType;
        public int TargetType;
        public int SourceMasterType;
        public int TargetMasterType;
        public int SourceLevel;
        public int TargetLevel;
        public int SourceSubRank;
        public int TargetSubRank;
        public int SourceProfession;
        public int TargetProfession;
        public int SourceElementType;
        public int TargetMonsterClassId;
        public int TargetMonsterType;
        public int TargetHp;
        public int StageType;
        public bool IsPvp;
        public double CombatRatingSuppression;
        public int Distance;
    }

    public struct FightDamageTypeData
    {
        public bool Blinding;
        public bool Dodge;
        public bool Block;
        public bool Critical;
        public bool Immunity;
    }

    public sealed class FightDamageFormulaResult
    {
        public long Damage;
        public long AbsoluteDamage;
        public bool ElementAffected;
    }

    public sealed class FightCureFormulaResult
    {
        public long Cure;
        public long AbsoluteCure;
    }

    public sealed class FightDamageFormula
    {
        private const int Player = 1;
        private const int Monster = 2;
        private const int Summon = 3;
        private const int Npc = 4;
        private const int Pet = 5;
        private const int PetEntity = 7;
        private readonly FightDamagePropertySchema schema;
        private readonly FightDamageFormulaConfig config;

        public FightDamageFormula(FightDamagePropertySchema schema, FightDamageFormulaConfig config)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public double ReadProperty(IDictionary<int, long> values, int propertyId)
        {
            return Prop(values, propertyId);
        }

        public int CalculateMoveDist(IDictionary<int, long> values)
        {
            return (int)((1.0 + Prop(values, "MoveDistPercent")) * Prop(values, "MoveDist"));
        }

        public FightDamageTypeData CalculateDamageType(FightRandomStream random,
            IDictionary<int, long> source, IDictionary<int, long> target, int hitType)
        {
            var result = new FightDamageTypeData();
            if (hitType != 1 && hitType != 2 && hitType != 6) return result;

            result.Blinding = (float)random.Next() <= Prop(source, "BlindingPercent");
            result.Dodge = (float)random.Next() <= Prop(target, "DodgePercent");
            double block = Prop(target, "BlockPercent")
                + Divide(Prop(target, "BlockPercentValue"), Prop(target, "BaseBlockPercentValue"))
                - Prop(source, "BlockAvoidPercent")
                - Divide(Prop(source, "BlockAvoidPercentValue"), Prop(source, "BaseBlockAvoidPercentValue"));
            result.Block = (float)random.Next() <= block;
            if (!result.Block)
            {
                double critical = 0.05000000074505806
                    + Prop(source, "CritRatePercent")
                    + Divide(Prop(source, "CritRatePercentValue"), Prop(source, "BaseCritRatePercentValue"))
                    - Prop(target, "CritAvoidPercent")
                    - Divide(Prop(target, "CritAvoidPercentValue"), Prop(target, "BaseCritAvoidPercentValue"));
                result.Critical = (float)random.Next() <= critical;
            }
            return result;
        }

        public FightDamageFormulaResult Calculate(FightDamageFormulaRequest request,
            FightDamageTypeData type, long? customDamage = null, double elementConfrontation = 0)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var result = new FightDamageFormulaResult();
            if (type.Blinding || type.Dodge || type.Immunity) return result;

            IDictionary<int, long> source = request.SourceProps ?? EmptyProps;
            IDictionary<int, long> target = request.TargetProps ?? EmptyProps;
            bool sourceCharacter = IsCharacter(request.SourceType, request.SourceMasterType);
            bool targetCharacter = IsCharacter(request.TargetType, request.TargetMasterType);
            double attack = Prop(source, "Attack");
            double defence = Math.Max(0, Prop(target, "Defence")
                - Prop(target, "Defence") * Prop(source, "StatusIgnoreDefence")
                - Prop(source, "FixedStatusIgnoreDefence"));
            double amount = 0;
            double absolute = 0;

            if (attack == 0)
            {
                amount = customDamage.GetValueOrDefault();
            }
            else
            {
                double basic = Prop(source, request.SkillDamageBasicPropType);
                double coefficient = Prop(source, request.SkillDamagePropType);
                amount = customDamage.HasValue ? customDamage.Value : basic * coefficient;
                absolute = amount;
                if (sourceCharacter) amount /= OneAdd(target, "PlayerSkillDmgReduceScale", 1.0);
                if (request.SourceType == Pet || request.SourceType == PetEntity)
                    amount /= OneAdd(target, "PetSkillDmgReduceScale", 1.0);
                double fixedDamage = Prop(source, request.SkillDamageFixedPropType);
                amount += fixedDamage;
                absolute += fixedDamage;
                amount *= attack / (attack + defence);

                // This assignment order matches the pinned BattleFormulaHandler.Damage method.
                absolute = basic;
                double add = Prop(source, "DmgAdd") + Prop(source, "FixedStatusDmgAdd")
                    + Prop(target, "FixedDmgVulnerable") + Prop(target, "FixedstatusDmgVulnerable")
                    - Prop(target, "DmgReduce") - Prop(target, "FixedStatusDmgReduce");
                if (type.Critical) add += Prop(source, "CritPower");
                if (type.Block) add -= Prop(target, "BlockValue");
                add *= coefficient;
                if (sourceCharacter) add /= OneAdd(target, "PlayerSkillDmgReduceScale", 1.0);
                if (request.SourceType == Pet || request.SourceType == PetEntity)
                    add /= OneAdd(target, "PetSkillDmgReduceScale", 1.0);
                add = Math.Max(-config.FixedDamageLimitPercent / 10000.0 * amount, add);
                amount += add;
                if (sourceCharacter) amount /= OneAdd(target, "ProSkillDmgReduceScale", 1.0);
            }

            string element = ElementName(request.SourceElementType);
            double baseElementAdd = Prop(source, "BaseElementAdd");
            double baseElementReduce = Prop(target, "BaseElementReduce");
            double elementAdd = 0;
            double elementReduce = 0;
            double scaledElementAdd = 0;
            double scaledElementReduce = 0;
            if (baseElementAdd != 0 && baseElementReduce != 0)
            {
                if (request.SourceElementType == 0)
                {
                    foreach (string name in Elements)
                    {
                        elementAdd += Prop(source, name + "DamageAdd");
                        elementReduce += Prop(target, name + "DamageReduce");
                    }
                    elementAdd /= 5.0 * baseElementAdd;
                    elementReduce /= 5.0 * baseElementReduce;
                }
                else if (element != null)
                {
                    elementAdd = Prop(source, element + "DamageAdd") / baseElementAdd;
                    elementReduce = Prop(target, element + "DamageReduce") / baseElementReduce;
                }
                scaledElementAdd = elementAdd * Math.Pow(baseElementAdd / baseElementReduce, 1.0);
                scaledElementReduce = elementReduce * Math.Pow(baseElementReduce / baseElementAdd, 1.0);
            }

            string mastery = request.SourceElementType == 0 ? "KongFu" : "Element";
            double baseMastery = Prop(source, "Base" + mastery + "Master");
            double baseResistance = Prop(target, "Base" + mastery + "Resistance");
            double masteryValue = 0;
            double resistanceValue = 0;
            if (baseMastery != 0 && baseResistance != 0)
            {
                masteryValue = Math.Max(0, Prop(source, mastery + "Master") / baseMastery
                    * Math.Pow(baseMastery / baseResistance, 1.0));
                resistanceValue = Math.Max(0, Prop(target, mastery + "Resistance") / baseResistance
                    * Math.Pow(baseResistance / baseMastery, 1.0));
            }
            amount *= (1.0 + scaledElementAdd + masteryValue) / (1.0 + scaledElementReduce + resistanceValue);
            absolute *= 1.0 + scaledElementAdd + masteryValue;

            double increase = Prop(source, "DmgAddPercent") + Prop(target, "DmgVulnerable");
            double kindAdd = targetCharacter ? Prop(source, "PlayerDmgAddScale")
                : request.TargetType == Monster ? Prop(source, "MonsterDmgAddScale") : 0;
            double kindReduce = sourceCharacter ? Prop(target, "PlayerDmgReduceScale")
                : request.SourceType == Monster ? Prop(target, "MonsterDmgReduceScale") : 0;
            double professionAdd = ProfessionScale(source, request.TargetProfession, "DmgAddScale");
            double professionReduce = ProfessionScale(target, request.SourceProfession, "DmgReduceScale");
            int specialProperty = FindMonsterSpecial(request.TargetMonsterClassId);
            double specialAdd = specialProperty == 0 ? 0 : Prop(source, specialProperty);
            amount = amount * (1.0 + increase + kindAdd + professionAdd + specialAdd)
                / Math.Max(1.0 + Prop(target, "DmgReducePercent") + kindReduce + professionReduce, 0.1);
            absolute *= 1.0 + elementAdd + kindAdd + professionAdd + specialAdd;

            double finalScale = Prop(source, "FinalDamageScale");
            if (finalScale != 0)
            {
                amount *= 1.0 + finalScale;
                absolute *= 1.0 + finalScale;
            }
            if (type.Critical)
            {
                double avoid = Prop(target, "CritAvoidPercent")
                    + Divide(Prop(target, "CritAvoidPercentValue"), Prop(target, "BaseCritAvoidPercentValue"));
                amount *= Math.Max(config.MinCritPowerPercent, 1.0 + Prop(source, "CritPowerPercent") - avoid);
                absolute *= Prop(source, "CritPowerPercent");
            }
            if (type.Block)
            {
                double avoid = Prop(source, "BlockAvoidPercent")
                    + Divide(Prop(source, "BlockAvoidPercentValue"), Prop(source, "BaseBlockAvoidPercentValue"));
                amount /= Math.Max(config.MinBlockValuePercent, 1.0 + Prop(target, "BlockValuePercent") - avoid);
            }

            if (!request.IsPvp)
            {
                int delta = (int)(float)((float)request.SourceLevel - (float)request.TargetLevel);
                int rowIndex = Math.Abs(delta);
                int scale = FindValue(config.LevelOffsetDamage, rowIndex, config.MaxLevelOffsetDamageScale);
                double factor = 1.0 + scale / 10000.0;
                if (delta > 0) { amount *= factor; absolute *= factor; }
                else { amount /= factor; absolute /= factor; }
            }
            if (SuppressionEffective(request))
            {
                if (request.CombatRatingSuppression > 0)
                {
                    amount *= 1.0 + request.CombatRatingSuppression;
                    absolute *= 1.0 + request.CombatRatingSuppression;
                }
                else if (request.CombatRatingSuppression < 0)
                    amount /= 1.0 - request.CombatRatingSuppression;
            }

            double cookAdd = Prop(source, "CookFinalDmgAddPercent");
            double cookReduce = Prop(target, "CookFinalDmgReducePercent");
            if (element != null)
            {
                cookAdd += Prop(source, "CookFinal" + element + "DmgAddPercent")
                    + Prop(target, "CookFinal" + element + "DmgVulnerablePercent");
                cookReduce += Prop(target, "CookFinal" + element + "DmgReducePercent");
            }
            amount = amount * (1.0 + cookAdd) / (1.0 + cookReduce);
            if (request.TargetType == Player || request.TargetType == Npc)
                amount *= 1.0 + Prop(source, "FinalCharacterDamageScale");
            else if (request.TargetType == Summon)
                amount *= 1.0 + Prop(source, "FinalSummonDamageScale");
            else if (request.TargetType == PetEntity)
                amount *= 1.0 + Prop(source, "FinalPetEntityDamageScale");

            double targetMaxHp = Prop(target, "MaxHp");
            double hpStep = Prop(source, "SkillTargetReduceHpPer");
            if (hpStep != 0)
            {
                int steps = (int)((targetMaxHp - request.TargetHp) / targetMaxHp / hpStep);
                double value = Math.Min(Prop(source, "SkillDmgMaxAddPer"), Prop(source, "SkillDmgUnitAddPer") * steps);
                amount *= 1.0 + value;
                absolute *= 1.0 + value;
            }
            double hpAdd = Prop(source, "SkillDmgAddPerByTargetHp");
            amount += hpAdd * targetMaxHp;
            absolute += hpAdd * targetMaxHp;
            amount += (Prop(source, "DistanceAddDmgPercent") * amount + Prop(source, "DistanceAddDmg")) * request.Distance;
            if (request.TargetMonsterType == 2 || request.TargetMonsterType == 3 || request.TargetMonsterType == 4)
            {
                amount *= 1.0 + Prop(source, "SkillDmgAddPerByLargeTarget");
                absolute *= 1.0 + hpAdd;
            }
            amount *= 1.0 + Prop(source, "StatusDmgAddPer") + Prop(target, "StatusDmgVulnerablePer");
            amount /= OneAdd(target, "StatusDmgReducePer", 0.1);

            if (!request.IsPvp)
            {
                double factor = RankFactor(config.RankOffsetDamage, request.SourceSubRank, request.TargetSubRank);
                amount *= factor;
                absolute *= factor;
            }
            if (request.StageType == 3)
            {
                double factor = RankFactor(config.DemonRankOffsetDamage, request.SourceSubRank, request.TargetSubRank);
                amount *= factor;
                absolute *= factor;
            }

            float confrontation = (float)elementConfrontation;
            if (Math.Abs(confrontation) >= Math.Max(0.000001 * Math.Abs(confrontation), 8.0 * 1.401298464324817e-45))
            {
                amount *= (float)(1.0 - confrontation);
                result.ElementAffected = true;
            }
            result.Damage = -(long)Math.Max(0, amount);
            result.AbsoluteDamage = -(long)Math.Max(0, absolute);
            return result;
        }

        public FightCureFormulaResult CalculateCure(IDictionary<int, long> sourceProps,
            IDictionary<int, long> targetProps, int skillCureProperty, int fixedCureProperty,
            long? customCure = null)
        {
            IDictionary<int, long> source = sourceProps ?? EmptyProps;
            IDictionary<int, long> target = targetProps ?? EmptyProps;
            double value = customCure ?? 0L;
            int hpCure = schema.Id("SkillCureByHp");
            int attackCure = schema.Id("SkillCureByAttack");
            int targetHpCure = schema.Id("SkillCureByTargetHp");
            if (skillCureProperty == hpCure)
                value += Prop(source, "MaxHp") * Prop(source, skillCureProperty);
            else if (skillCureProperty == attackCure)
                value += Prop(source, "Attack") * Prop(source, skillCureProperty);
            else if (skillCureProperty == targetHpCure)
                value += Prop(target, "MaxHp") * Prop(source, skillCureProperty);

            value += Prop(source, fixedCureProperty);
            double cureAdd = Prop(source, "CureAddPercent") + Prop(target, "BeCureAddPercent")
                + Divide(Prop(source, "CureAdd"), Prop(source, "BaseCureAdd"))
                + Divide(Prop(target, "BeCureAdd"), Prop(target, "BaseBeCureAdd"));
            if (cureAdd > config.CureAddSoftCapThreshold)
                cureAdd = config.CureAddSoftCapThreshold
                    + Divide((cureAdd - config.CureAddSoftCapThreshold) * config.CureAddOverflowScale, cureAdd);
            value *= 1.0 + cureAdd;
            value += Prop(source, "FinalCureScale") * value;
            long absolute = (long)value;
            return new FightCureFormulaResult { Cure = Math.Max(0L, absolute), AbsoluteCure = absolute };
        }

        private static readonly IDictionary<int, long> EmptyProps = new Dictionary<int, long>();
        private static readonly string[] Elements = { "Water", "Fire", "Wind", "Light", "Dark" };

        private double Prop(IDictionary<int, long> values, string name)
        {
            return Prop(values, schema.Id(name));
        }

        private double Prop(IDictionary<int, long> values, int id)
        {
            if (id == 0 || values == null) return 0;
            long value;
            if (!values.TryGetValue(id, out value)) return 0;
            return schema.IsFloat(id) ? value / 10000.0 : value;
        }

        private double OneAdd(IDictionary<int, long> values, string name, double minimum)
        {
            return Math.Max(1.0 + Prop(values, name), minimum);
        }

        private int FindMonsterSpecial(int monsterId)
        {
            for (int i = 0; i < config.MonsterSpecialProps.Count; i++)
                if (config.MonsterSpecialProps[i].Key == monsterId)
                    return config.MonsterSpecialProps[i].Value;
            return 0;
        }

        private static int FindValue(List<FightFormulaTableRow> rows, int key, int fallback)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Key == key) return rows[i].Value;
            return fallback;
        }

        private static double RankFactor(List<FightFormulaTableRow> rows, int sourceSubRank, int targetSubRank)
        {
            int sourceRank = Rank(sourceSubRank);
            int targetRank = Rank(targetSubRank);
            int sourceScale = FindValue(rows, sourceRank, Int32.MinValue);
            int targetScale = FindValue(rows, targetRank, Int32.MinValue);
            if (sourceScale == Int32.MinValue || targetScale == Int32.MinValue) return 1.0;
            double factor = 1.0 + Math.Abs((sourceScale - targetScale) / 10000.0);
            return sourceRank > targetRank ? factor : 1.0 / factor;
        }

        private static int Rank(int subRank)
        {
            if (subRank < 0 || subRank > 85) return 0;
            return subRank < 2 ? subRank : 2 + (subRank - 2) / 3;
        }

        private static bool IsCharacter(int type, int masterType)
        {
            return type == Player || type == Npc || masterType == Player || masterType == Npc;
        }

        private static bool SuppressionEffective(FightDamageFormulaRequest request)
        {
            if (request.IsPvp) return true;
            int sourceRank = Rank(request.SourceSubRank);
            int targetRank = Rank(request.TargetSubRank);
            bool sourceCharacter = IsCharacter(request.SourceType, request.SourceMasterType);
            bool targetCharacter = IsCharacter(request.TargetType, request.TargetMasterType);
            bool sourceMonster = request.SourceType == Monster || request.SourceMasterType == Monster;
            bool targetMonster = request.TargetType == Monster || request.TargetMasterType == Monster;
            return !((sourceCharacter && targetMonster && sourceRank > targetRank)
                || (sourceMonster && targetCharacter && targetRank > sourceRank));
        }

        private double ProfessionScale(IDictionary<int, long> values, int profession, string suffix)
        {
            string first = null;
            string second = null;
            switch (profession)
            {
                case 1: first = "Zhanshi"; break;
                case 10: first = "Fashi"; break;
                case 2: first = "Zhanshi"; second = "Huwei"; break;
                case 6: first = "Zhanshi"; second = "Doushi"; break;
                case 11: first = "Fashi"; second = "Shushi"; break;
                case 15: first = "Fashi"; second = "Xianzhe"; break;
            }
            if (first == null) return 0;
            return Prop(values, first + suffix) + (second == null ? 0 : Prop(values, second + suffix));
        }

        private static string ElementName(int type)
        {
            switch (type)
            {
                case 100: return "Wind";
                case 101: return "Water";
                case 102: return "Fire";
                case 103: return "Light";
                case 104: return "Dark";
                default: return null;
            }
        }

        private static double Divide(double left, double right)
        {
            if (right != 0) return left / right;
            if (left == 0) return double.NaN;
            return left > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        }
    }
}
