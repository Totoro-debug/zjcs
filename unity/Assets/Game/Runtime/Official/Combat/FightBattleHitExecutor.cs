using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightBattleHitResult
    {
        public FightDamageTypeData Type;
        public FightDamageFormulaResult Formula;
        public FightCureFormulaResult Cure;
        public FightDamageEvent Settlement;
        public long ShieldDamage;
        public bool IsCure;
        public List<int> TriggeredStatusSkills = new List<int>();
    }

    public sealed class FightBattleHitExecutor
    {
        private readonly FightDamageFormula formula;
        private readonly FightDamageSettlement settlement;

        public FightBattleHitExecutor(FightDamageFormula formula, FightDamageSettlement settlement)
        {
            this.formula = formula ?? throw new ArgumentNullException(nameof(formula));
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
        }

        public FightBattleHitResult Apply(FightRandomStream random, int damageId,
            FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            int stageType, bool isPvp, double elementConfrontation = 0.0,
            List<int> processedFalloffStatuses = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (actor.Hp <= 0 || target.Hp <= 0)
                throw new InvalidOperationException("Fight hit source and target must be alive.");

            Dictionary<int, long> sourceProps = Properties(actor.Properties);
            Dictionary<int, long> targetProps = Properties(target.Properties);
            if (skill != null) AddProperties(sourceProps, skill.Properties);
            FightEntityState sourceEntity = actor.EntityState();
            FightEntityState targetEntity = target.EntityState();
            var result = new FightBattleHitResult { Type = new FightDamageTypeData() };
            // DamageActionType is the action-mutex category. None has no HP settlement; other hit
            // actions resolve the configured damage type and attached status effects.
            if (hit.ActionType == 0) return result;
            long? custom = CustomDamage(actor, target, skill, hit, sourceProps, targetProps);

            if (hit.ActionType == 5 || (skill != null && skill.SkillType == 3))
            {
                result.IsCure = true;
                double reduction = FightBattleStatusDamageReducePerRuntime.Resolve(actor, target,
                    skill, result.Type, true, random);
                long? customCure = skill.HasCustomCure ? (long?)skill.CustomCure : custom;
                result.Cure = formula.CalculateCure(sourceProps, targetProps,
                    hit.DamageProperty, hit.FixedDamageProperty, customCure);
                if (reduction != 0.0)
                {
                    double scale = 1.0 / Math.Max(1.0 + reduction, 0.1);
                    result.Cure.Cure = (long)(result.Cure.Cure * scale);
                    result.Cure.AbsoluteCure = (long)(result.Cure.AbsoluteCure * scale);
                }
                result.Settlement = settlement.Apply(damageId, sourceEntity, targetEntity,
                    result.Cure.Cure);
            }
            else if (hit.DamageType == 0)
            {
                result.Formula = new FightDamageFormulaResult();
                result.Settlement = settlement.Apply(damageId, sourceEntity, targetEntity, 0);
            }
            else if (hit.DamageType == 8)
            {
                long direct = custom.HasValue ? custom.Value : 0;
                direct = direct == Int64.MinValue ? Int64.MinValue + 1 : -Math.Abs(direct);
                double reduction = FightBattleStatusDamageReducePerRuntime.Resolve(actor, target,
                    skill, result.Type, false, random);
                direct = (long)(direct / Math.Max(1.0 + reduction, 0.1));
                direct = FightDamageFalloff.Apply(random, actor, target, skill, hit,
                    direct, processedFalloffStatuses);
                long absorbed = FightBattleShieldRuntime.AbsorbDamage(random, target, hit,
                    direct, out result.ShieldDamage, result.TriggeredStatusSkills);
                result.Formula = new FightDamageFormulaResult { Damage = absorbed, AbsoluteDamage = direct };
                result.Settlement = settlement.Apply(damageId, sourceEntity, targetEntity, absorbed);
            }
            else
            {
                var request = new FightDamageFormulaRequest
                {
                    SourceProps = sourceProps,
                    TargetProps = targetProps,
                    SkillDamageBasicPropType = hit.BasicProperty,
                    SkillDamagePropType = hit.DamageProperty,
                    SkillDamageFixedPropType = hit.FixedDamageProperty,
                    SourceType = actor.CharacterType,
                    TargetType = target.CharacterType,
                    SourceMasterType = MasterType(actor, actor, target),
                    TargetMasterType = MasterType(target, actor, target),
                    SourceLevel = actor.Level,
                    TargetLevel = target.Level,
                    SourceSubRank = actor.SubRank,
                    TargetSubRank = target.SubRank,
                    SourceProfession = actor.Profession,
                    TargetProfession = target.Profession,
                    SourceElementType = skill == null ? 0 : skill.ElementType,
                    TargetMonsterClassId = target.MonsterId,
                    TargetMonsterType = target.MonsterType,
                    TargetHp = (int)Math.Min(Int32.MaxValue, target.Hp),
                    StageType = stageType,
                    IsPvp = isPvp
                };
                result.Type = formula.CalculateDamageType(random, sourceProps, targetProps, hit.DamageType);
                double reduction = FightBattleStatusDamageReducePerRuntime.Resolve(actor, target,
                    skill, result.Type, false, random);
                formula.AddPropertyValue(targetProps, "StatusDmgReducePer", reduction);
                Func<double, double> customProps = skill != null && skill.CostType == 2
                    && hit.DamageType == 6 ? null
                    : amount => ApplyDamageCustomProps(random, target, targetProps, skill, amount);
                result.Formula = formula.Calculate(request, result.Type, custom, elementConfrontation,
                    customProps);
                long damage = result.Formula.Damage;
                if (skill != null && skill.CostType == 2 && hit.DamageType == 6
                    && !result.Type.Blinding && !result.Type.Dodge && !result.Type.Immunity)
                {
                    damage = skill.SkillCost == Int64.MinValue ? Int64.MinValue + 1
                        : -Math.Abs(skill.SkillCost);
                    result.Formula.AbsoluteDamage = Math.Abs(damage);
                }
                if (hit.UseDamageRate)
                {
                    damage = (long)(damage * hit.DamageRate);
                    result.Formula.AbsoluteDamage = (long)(result.Formula.AbsoluteDamage * hit.DamageRate);
                }
                if (hit.DamageType != 0 && !result.Type.Blinding && !result.Type.Dodge && !result.Type.Immunity)
                    damage = FightDamageFalloff.Apply(random, actor, target, skill, hit, damage,
                        processedFalloffStatuses);
                damage = FightBattleShieldRuntime.AbsorbDamage(random, target, hit, damage,
                    out result.ShieldDamage, result.TriggeredStatusSkills);
                result.Formula.Damage = damage;
                result.Settlement = settlement.Apply(damageId, sourceEntity, targetEntity, damage);
            }

            target.Hp = targetEntity.Hp;
            return result;
        }

        private long? CustomDamage(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit,
            IDictionary<int, long> source, IDictionary<int, long> targetProps)
        {
            if (skill != null && skill.CostType == 2 && hit.DamageType == 6)
                return skill.SkillCost;
            if (skill != null && skill.HasCustomDamage) return skill.CustomDamage;
            if (hit.HasCustomDamage) return hit.CustomDamage;
            int prop = hit.DamageProperty;
            double coefficient = formula.ReadProperty(source, prop);
            switch (hit.DamageType)
            {
                case 3: return (long)((actor.MaxHp - actor.Hp) * coefficient);
                case 4: return (long)(actor.Hp * coefficient);
                case 5: return (long)(target.Hp * coefficient);
                case 6: return (long)((skill == null ? 0L : skill.SkillCost) * coefficient);
                case 7: return (long)((target.MaxHp - target.Hp) * coefficient);
                case 8: return hit.DamageParam;
                default: return null;
            }
        }

        private double ApplyDamageCustomProps(FightRandomStream random,
            FightBattleUnitState target, IDictionary<int, long> targetProperties,
            FightBattleSkillDefinition skill, double damage)
        {
            if (target.Statuses == null || target.Statuses.Count == 0) return damage;
            List<FightBattleStatusState> remove = null;
            foreach (FightBattleStatusState status in target.Statuses)
            {
                if (status == null || !status.HasDamageCustomProps
                    || status.DamageCustomPropsPhase != 1
                    || status.DamageCustomPropsMaxCount >= 0
                        && status.DamageCustomPropsTriggeredCount >= status.DamageCustomPropsMaxCount)
                    continue;
                float threshold = status.DamageCustomPropsDmgGreaterMaxHpPer;
                if (threshold >= 0f && damage <= target.MaxHp * (double)threshold) continue;
                if (status.DamageCustomPropsCheckSkillTrigger
                    && (skill == null || !skill.CanTriggerStatusEntityIds.Contains(status.ClassId)))
                    continue;
                float rate = Math.Max(0f, Math.Min(1f, status.DamageCustomPropsRate));
                if (rate < 1f && (random == null || random.Next() > rate)) continue;

                double reduction = formula.ReadProperty(targetProperties,
                    status.DamageCustomPropsPropertyId);
                damage /= Math.Max(1.0 + reduction, 0.1);
                status.DamageCustomPropsTriggeredCount++;
                if (status.DamageCustomPropsAutoRemove
                    && status.DamageCustomPropsMaxCount >= 0
                    && status.DamageCustomPropsTriggeredCount >= status.DamageCustomPropsMaxCount)
                {
                    if (remove == null) remove = new List<FightBattleStatusState>();
                    remove.Add(status);
                }
            }
            if (remove != null)
                foreach (FightBattleStatusState status in remove) target.Statuses.Remove(status);
            return damage;
        }

        private static int MasterType(FightBattleUnitState unit, FightBattleUnitState source,
            FightBattleUnitState target)
        {
            if (unit.MasterId == 0) return 0;
            if (unit.MasterId == source.Id) return source.CharacterType;
            if (unit.MasterId == target.Id) return target.CharacterType;
            return 0;
        }

        private static Dictionary<int, long> Properties(List<FightPropertyValue> properties)
        {
            var result = new Dictionary<int, long>();
            AddProperties(result, properties);
            return result;
        }

        private static void AddProperties(Dictionary<int, long> destination, List<FightPropertyValue> properties)
        {
            if (properties == null) return;
            for (int i = 0; i < properties.Count; i++)
            {
                FightPropertyValue property = properties[i];
                if (property == null || property.Id <= 0) continue;
                long previous;
                destination.TryGetValue(property.Id, out previous);
                destination[property.Id] = unchecked(previous + property.Value);
            }
        }

    }

    internal static class FightBattleUnitStateExtensions
    {
        public static FightEntityState EntityState(this FightBattleUnitState unit)
        {
            return new FightEntityState(unit.Id, unit.Hp, unit.MaxHp);
        }
    }

    public static class FightBattleShieldRuntime
    {
        public static void Initialize(FightBattleStatusState status,
            IList<FightPropertyValue> resolvedProperties, int shieldPropertyId)
        {
            if (status == null || status.ShieldKind == FightBattleShieldKind.None
                || status.ShieldInitialized) return;
            long value = Math.Max(0L, status.CustomShieldValue);
            if (!status.ShieldUsesCustomValue && resolvedProperties != null && shieldPropertyId > 0)
                foreach (FightPropertyValue property in resolvedProperties)
                    if (property != null && property.Id == shieldPropertyId)
                    {
                        value = Math.Max(0L, property.Value);
                        break;
                    }
            status.ShieldValue = value;
            status.ShieldRemainValue = value;
            status.ShieldRemainCount = status.ShieldInitialCount;
            status.ShieldInitialized = true;
        }

        public static long PersistentShieldTotal(FightBattleUnitState unit)
        {
            if (unit == null || unit.Statuses == null) return 0;
            long total = 0;
            foreach (FightBattleStatusState status in unit.Statuses)
            {
                if (status == null || status.ShieldKind != FightBattleShieldKind.Persistent) continue;
                long value = status.ShieldInitialized ? status.ShieldRemainValue : status.ShieldValue;
                if (value <= 0) continue;
                total = value > long.MaxValue - total ? long.MaxValue : total + value;
            }
            return total;
        }

        public static long ConsumeAllPersistentShields(FightBattleUnitState unit)
        {
            if (unit == null || unit.Statuses == null) return 0;
            long total = 0;
            foreach (FightBattleStatusState status in unit.Statuses)
            {
                if (status == null || status.ShieldKind != FightBattleShieldKind.Persistent) continue;
                long value = status.ShieldInitialized ? status.ShieldRemainValue : status.ShieldValue;
                if (value > 0) total = value > long.MaxValue - total ? long.MaxValue : total + value;
                status.ShieldRemainValue = 0;
                status.ShieldInitialized = true;
            }
            return total;
        }

        public static long AbsorbDamage(FightRandomStream random, FightBattleUnitState target,
            FightBattleHitDefinition hit, long damage, IList<int> destroyedSkills = null)
        {
            return AbsorbDamage(random, target, hit, damage, out _, destroyedSkills);
        }

        public static long AbsorbDamage(FightRandomStream random, FightBattleUnitState target,
            FightBattleHitDefinition hit, long damage, out long shieldDamage,
            IList<int> destroyedSkills = null)
        {
            shieldDamage = 0;
            if (damage >= 0 || target == null || hit == null || hit.IgnoreShield
                || target.Statuses == null) return damage;
            long remaining = damage == long.MinValue ? long.MaxValue : -damage;
            for (int i = 0; i < target.Statuses.Count && remaining > 0;)
            {
                FightBattleStatusState status = target.Statuses[i];
                if (status == null || status.ShieldKind == FightBattleShieldKind.None)
                {
                    i++;
                    continue;
                }
                long shieldValue = status.ShieldInitialized
                    ? status.ShieldRemainValue : status.ShieldValue;
                bool counted = status.ShieldKind == FightBattleShieldKind.Disposable
                    || status.ShieldKind == FightBattleShieldKind.Counted;
                if (status.ShieldKind == FightBattleShieldKind.Persistent && shieldValue <= 0
                    || counted && status.ShieldRemainCount == 0
                    || status.ShieldKind == FightBattleShieldKind.Invincible
                        && status.ShieldRemainCount == 0)
                {
                    i++;
                    continue;
                }
                if (status.ShieldDamageGreaterMaxHpPer >= 0f
                    && remaining <= target.MaxHp * (double)status.ShieldDamageGreaterMaxHpPer)
                {
                    i++;
                    continue;
                }
                float rate = Math.Max(0f, Math.Min(1f, status.ShieldRate));
                if (rate < 1f && (random == null || random.Next() > rate))
                {
                    i++;
                    continue;
                }

                long absorbed = status.ShieldKind == FightBattleShieldKind.Invincible
                    || status.ShieldIgnoreValue ? remaining : Math.Min(remaining, shieldValue);
                if (absorbed <= 0)
                {
                    i++;
                    continue;
                }
                shieldDamage = checked(shieldDamage + absorbed);
                remaining -= absorbed;
                bool destroyed = false;
                if (status.ShieldKind == FightBattleShieldKind.Persistent)
                {
                    status.ShieldRemainValue = Math.Max(0L, shieldValue - absorbed);
                    status.ShieldInitialized = true;
                    destroyed = status.ShieldRemainValue == 0;
                }
                else if (counted)
                {
                    if (status.ShieldRemainCount > 0) status.ShieldRemainCount--;
                    if (status.ShieldKeepRemainValue && !status.ShieldIgnoreValue)
                        status.ShieldRemainValue = Math.Max(0L, shieldValue - absorbed);
                    status.ShieldInitialized = true;
                    destroyed = status.ShieldRemainCount == 0;
                }
                else if (status.ShieldKind == FightBattleShieldKind.Invincible)
                {
                    if (status.ShieldRemainCount > 0) status.ShieldRemainCount--;
                    status.ShieldInitialized = true;
                    destroyed = status.ShieldRemainCount == 0;
                }

                if (destroyed)
                {
                    if (status.ShieldDestroyedSkillId > 0)
                        destroyedSkills?.Add(status.ShieldDestroyedSkillId);
                    target.Statuses.RemoveAt(i);
                }
                else i++;
            }
            return remaining == long.MaxValue && damage == long.MinValue
                ? long.MinValue + 1 : -remaining;
        }
    }
}
