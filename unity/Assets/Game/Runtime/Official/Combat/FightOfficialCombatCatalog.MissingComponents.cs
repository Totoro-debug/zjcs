using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusComponentState ParseStatusComponents(
            Dictionary<string, object> components)
        {
            var result = new FightBattleStatusComponentState();
            Dictionary<string, object> hitSkill = Component(components, "FightStatusHitSkillComponent");
            if (hitSkill != null)
            {
                result.HitSkill = new FightBattleStatusHitSkillComponentState
                {
                    HitSourceType = Int(hitSkill, "HitSourceType"),
                    Rate = Float(hitSkill, "Rate", 1f),
                    Block = Bool(hitSkill, "Block"),
                    Crit = Bool(hitSkill, "Crit"),
                    Damage = Bool(hitSkill, "Damage", true),
                    Cure = Bool(hitSkill, "Cure"),
                    IsTrueDamage = Bool(hitSkill, "IsTrueDamage"),
                    IsApplyOneHit = Bool(hitSkill, "IsApplyOneHit"),
                    HitSkillTypes = new List<int>(IntArray(hitSkill, "HitSkillTypes")),
                    TargetActionTypes = new List<int>(IntArray(hitSkill, "TargetActionTypes")),
                    HitSkillElementTypes = new List<int>(IntArray(hitSkill, "HitSkillElementTypes")),
                    HitSkillEntityClassIds = new List<int>(IntArray(hitSkill, "HitSkillEntityClassIds")),
                    ConditionCount = Int(hitSkill, "ConditionCount", 1),
                    MaxInvokeCount = Int(hitSkill, "MaxInvokeCount", -1),
                    AutoRemove = Bool(hitSkill, "AutoRemove"),
                    RoundResetCount = Int(hitSkill, "RoundResetCount", -1),
                    SkillId = Int(hitSkill, "SkillId"),
                    IsSkillTargetPos = Bool(hitSkill, "IsSkillTargetPos", true),
                    TargetStatusId = Int(hitSkill, "TargetStatusId"),
                    DisperseActionTypes = new List<int>(IntArray(hitSkill, "DisperseActionTypes")),
                    StatusId = Int(hitSkill, "StatusId"),
                    IsOverflow = Bool(hitSkill, "IsOverflow"),
                    StatusCustomParamByOverflow = Float(hitSkill, "StatusCustomParamByOverflow", 0f)
                };
                AddTriggerSkills(hitSkill, "SkillCfgs", result.HitSkill.SkillTriggers);
                AddTriggerStatuses(hitSkill, "StatusCfgs", result.HitSkill.StatusTriggers);
            }

            Dictionary<string, object> damageSkill = Component(components,
                "FightStatusDamageSkillComponent");
            if (damageSkill != null)
            {
                Dictionary<string, object> autoRemove = Component(damageSkill, "AutoRemove");
                result.DamageSkill = new FightBattleStatusDamageSkillComponentState
                {
                    Rate = Float(damageSkill, "Rate", 1f),
                    Block = Bool(damageSkill, "Block"),
                    Crit = Bool(damageSkill, "Crit"),
                    Damage = Bool(damageSkill, "Damage", true),
                    Cure = Bool(damageSkill, "Cure"),
                    RangeCondition = Int(damageSkill, "RangeCondition"),
                    SkillTargetType = Int(damageSkill, "SkillTargetType", 1),
                    ConditionCount = Int(damageSkill, "ConditionCount", 1),
                    MaxCount = Int(damageSkill, "MaxCount", -1),
                    AutoDestroy = Bool(damageSkill, "AutoDestory"),
                    GreaterThanMaxHpPer = Float(damageSkill, "GreaterThanMaxHpPer", -1f),
                    DamageMove = Int(damageSkill, "DamageMove"),
                    SkillId = Int(damageSkill, "SkillId"),
                    SourceStatusId = Int(damageSkill, "SourceStatusId"),
                    StatusId = Int(damageSkill, "StatusId"),
                    SkillCustomDamageByHpChangeValue = Float(damageSkill,
                        "SkillCustomDamageByHpChangeValue", 0f),
                    EachRoundMaxCount = Int(damageSkill, "EachRoundMaxCount", -1),
                    IsAttackerTarget = Bool(damageSkill, "IsAttackerTarget"),
                    ElementTypes = new List<int>(IntArray(damageSkill, "ElementTypes"))
                };
                if (autoRemove != null)
                    result.DamageSkill.AutoDestroy = result.DamageSkill.AutoDestroy
                        || Int(autoRemove, "MaxCount", -1) == 0;
                AddTriggerSkills(damageSkill, "SkillCfgs", result.DamageSkill.SkillTriggers);
                AddTriggerStatuses(damageSkill, "StatusCfgs", result.DamageSkill.StatusTriggers);
            }

            Dictionary<string, object> hitApply = Component(components,
                "FightStatusHitApplyDamageComponent");
            if (hitApply != null)
            {
                Dictionary<string, object> consume = Component(hitApply, "FightStatusConsumeStackCfg");
                result.HitApplyDamage = new FightBattleStatusHitApplyDamageComponentState
                {
                    DamageType = Int(hitApply, "DamageType"),
                    IsCheckTargetShield = Bool(hitApply, "IsCheckTargertShield"),
                    CharacterType = Int(hitApply, "CharacterType"),
                    TargetSkillEntityClassIds = new List<int>(IntArray(hitApply,
                        "TargetSkillEntityClassIds")),
                    ReducePercentPropertyId = Int(hitApply, "ReducePercentPropType"),
                    AddPercentPropertyId = Int(hitApply, "AddPercentPropType"),
                    ScaleByHpType = Int(hitApply, "ScaleByHpType"),
                    HpDecreaseUnit = Float(hitApply, "HpDecreaseUnit", 0f),
                    MaxHpScale = Float(hitApply, "MaxHpScale", 0f),
                    ApplyTargetCharacterTypeIfSubstitute = Bool(hitApply,
                        "ApplyTargetCharacterTypeIfSubstitute", true),
                    ConsumeStatusId = consume == null ? 0 : Int(consume,
                        "ConsumeStackStatusEntityClassId"),
                    ConsumeStackCount = consume == null ? 0 : Int(consume, "ConsumeStackCount"),
                    ConsumeStackMinCount = consume == null ? 0 : Int(consume,
                        "TriggerApplyStackMinCount"),
                    ConsumeScaleByStatusTypeCount = consume != null
                        && Bool(consume, "ScalePropByStatusTypeCount"),
                    RemoveFollowRootParent = Bool(hitApply, "IsRemoveFollowRootParent")
                };
            }

            Dictionary<string, object> hitDmgAdd = Component(components,
                "FightStatusHitDmgAddPerComponent");
            if (hitDmgAdd != null)
            {
                Dictionary<string, object> statusType = Component(hitDmgAdd, "StatusType");
                Dictionary<string, object> consume = Component(hitDmgAdd, "FightStatusConsumeStackCfg");
                result.HitDmgAddPer = new FightBattleStatusHitDmgAddPerComponentState
                {
                    HitSourceType = Int(hitDmgAdd, "HitSourceType"),
                    Rate = Float(hitDmgAdd, "Rate", 1f),
                    Block = Bool(hitDmgAdd, "Block"),
                    Crit = Bool(hitDmgAdd, "Crit"),
                    Damage = Bool(hitDmgAdd, "Damage", true),
                    Cure = Bool(hitDmgAdd, "Cure"),
                    IsTrueDamage = Bool(hitDmgAdd, "IsTrueDamage"),
                    ConditionCount = Int(hitDmgAdd, "ConditionCount", 1),
                    MaxInvokeCount = Int(hitDmgAdd, "MaxInvokeCount", -1),
                    AutoRemove = Bool(hitDmgAdd, "AutoRemove"),
                    RoundResetCount = Int(hitDmgAdd, "RoundResetCount", -1),
                    TargetHpPercent = Float(hitDmgAdd, "TargetHpPercent", -1f),
                    TargetHpComparison = Int(hitDmgAdd, "TargetHpComparison"),
                    DurationSkillCount = Int(hitDmgAdd, "DurationSkillCount", -1),
                    OnlySkillActions = new List<int>(IntArray(hitDmgAdd, "OnlySkillActions")),
                    OnlySkillType = Int(hitDmgAdd, "OnlySkillType"),
                    ScalePropByStatusTypeCount = Bool(hitDmgAdd, "ScalePropByStatusTypeCount"),
                    StatusTarget = statusType == null ? 0 : Int(statusType, "Target"),
                    StatusTypes = statusType == null ? new List<int>()
                        : new List<int>(IntArray(statusType, "AnyStatusTypes")),
                    ConsumeStatusId = consume == null ? 0 : Int(consume,
                        "ConsumeStackStatusEntityClassId"),
                    ConsumeStackCount = consume == null ? 0 : Int(consume, "ConsumeStackCount"),
                    ConsumeStackMinCount = consume == null ? 0 : Int(consume,
                        "TriggerApplyStackMinCount"),
                    ConsumeScaleByStatusTypeCount = consume != null
                        && Bool(consume, "ScalePropByStatusTypeCount")
                };
            }

            Dictionary<string, object> damageGetType = Component(components,
                "FightStatusDamageGetTypeComponent");
            if (damageGetType != null)
            {
                result.DamageGetType = new FightBattleStatusDamageGetTypeComponentState
                {
                    Rate = Float(damageGetType, "Rate", 1f),
                    DamageType = Int(damageGetType, "DamageType"),
                    Value = Bool(damageGetType, "Value"),
                    IsCheckSkillTrigger = Bool(damageGetType, "IsCheckSkillTrigger"),
                    MaxCount = Int(damageGetType, "MaxCount", -1),
                    FilterSkillIds = new List<int>(IntArray(damageGetType, "FilterSkillIds"))
                };
                foreach (object raw in FieldArray(damageGetType, "ElementInfos"))
                {
                    Dictionary<string, object> entry = Object(raw, "damage type element");
                    result.DamageGetType.Elements.Add(new FightBattleStatusDamageElementRule
                    {
                        ElementType = Int(entry, "ElementType"),
                        DamageType = Int(entry, "EDamageType"),
                        Value = Bool(entry, "Value")
                    });
                }
            }

            Dictionary<string, object> damageReduceAbsolute = Component(components,
                "FightStatusDamageReduceAbsoluteComponent");
            if (damageReduceAbsolute != null)
                result.DamageReduceAbsolute = new FightBattleStatusDamageReduceAbsoluteComponentState
                {
                    Rate = Float(damageReduceAbsolute, "Rate", 1f),
                    DamageGreaterMaxHpPer = Float(damageReduceAbsolute, "DmgGreaterMaxHpPer", -1f),
                    MaxCount = Int(damageReduceAbsolute, "Count", -1),
                    AutoRemove = Bool(damageReduceAbsolute, "AutoRemove"),
                    IsCheckSkillTrigger = Bool(damageReduceAbsolute, "IsCheckSkillTrigger")
                };

            Dictionary<string, object> hpIncrease = Component(components,
                "FightStatusHpIncreaseUnitComponent");
            if (hpIncrease != null)
            {
                result.HpIncreaseUnit = new FightBattleStatusHpUnitComponentState
                {
                    TargetType = Int(hpIncrease, "TargetType"),
                    UnitHpPer = Float(hpIncrease, "UnitHpPer", -1f),
                    MaxCount = Int(hpIncrease, "MaxCount", -1),
                    TryAtStart = Bool(hpIncrease, "TryAtStart"),
                    TriggerWhenHpLessThan0 = Bool(hpIncrease, "TriggerWhenHpLessThan0"),
                    InvokeMaxCountOnHpChange = Int(hpIncrease, "InvokeMaxCountOnHpChange", -1)
                };
                AddTriggerStatuses(hpIncrease, "StatusList", result.HpIncreaseUnit.Statuses);
                AddTriggerSkills(hpIncrease, "Skills", result.HpIncreaseUnit.Skills);
            }

            Dictionary<string, object> roundInterval = Component(components,
                "FightStatusRoundIntervalComponent");
            if (roundInterval != null)
            {
                result.RoundInterval = new FightBattleStatusRoundIntervalComponentState
                {
                    Rate = Float(roundInterval, "Rate", 1f),
                    RoundIntervalPercent = Float(roundInterval, "RoundIntervalPercent", 0f),
                    FastForward = Bool(roundInterval, "FastForward")
                };
                AddTriggerSkills(roundInterval, "StatusTriggerSkillCfgs",
                    result.RoundInterval.SkillTriggers);
            }

            Dictionary<string, object> start = Component(components, "FightStatusStartComponent");
            if (start != null)
            {
                result.Start = new FightBattleStatusStartComponentState();
                AddTriggerStatuses(start, "StatusList", result.Start.Statuses);
                Dictionary<string, object> forbidden = Component(start, "ForbiddenStatus");
                if (forbidden != null)
                {
                    result.Start.ForbiddenTarget = Int(forbidden, "Target");
                    result.Start.ForbiddenStatusId = Int(forbidden, "StatusEntityClassId");
                    result.Start.Forbidden = Bool(forbidden, "Forbidden");
                }
            }

            Dictionary<string, object> stackChange = Component(components,
                "FightStatusStackChangeComponent");
            if (stackChange != null)
            {
                result.StackChange = new FightBattleStatusStackChangeComponentState
                {
                    MaxCount = Int(stackChange, "MaxCount", -1)
                };
                foreach (object raw in FieldArray(stackChange, "ChangeStatusIdAndChangeValue"))
                {
                    Dictionary<string, object> entry = Object(raw, "stack change entry");
                    result.StackChange.Changes.Add(new FightBattleStatusStackChangeRule
                    {
                        StatusId = Int(entry, "StatusId"),
                        ChangeValue = Int(entry, "ChangeValue")
                    });
                }
            }

            Dictionary<string, object> hitSummon = Component(components,
                "FightStatusHitSummonComponent");
            if (hitSummon != null)
                result.HitSummon = new FightBattleStatusHitSummonComponentState
                {
                    Rate = Float(hitSummon, "Rate", 1f),
                    SummonStatusId = Int(hitSummon, "SummonStatusId"),
                    SkillId = Int(hitSummon, "SkillId"),
                    Create = Bool(hitSummon, "Create"),
                    Remove = Bool(hitSummon, "Remove"),
                    MaxCount = Int(hitSummon, "MaxCount", -1),
                    CheckSummonLifeDuration = Bool(hitSummon, "CheckSummonLifeDuration"),
                    FilterSubstitute = Bool(hitSummon, "FilterSubstitute")
                };
            return result;
        }

        private static void AddTriggerSkills(Dictionary<string, object> component, string field,
            List<FightBattleStatusStateTrigger> output)
        {
            if (component == null || output == null || !component.TryGetValue(field, out object raw)
                || raw == null) return;
            foreach (object item in Array(raw, field))
            {
                Dictionary<string, object> row = Object(item, field + " skill");
                int id = Int(row, "SkillId");
                if (id <= 0) continue;
                output.Add(new FightBattleStatusStateTrigger
                {
                    EntityId = id,
                    BasePercent = Float(row, "BasePercent", 1f),
                    AffectedByProp = Bool(row, "AffectedByProp"),
                    Source = Int(row, "Source"),
                    Target = Int(row, "Target"),
                    UseAI = Bool(row, "UseAI"),
                    TargetPos = Int(row, "TargetPos"),
                    StackedCountToDamageFactor = Bool(row, "StackedCountToDamageFactor"),
                    PropTarget = ReadTriggerPropTarget(row),
                    TargetWithoutStatusIds = IntArray(row, "TargetWithoutStatusIds"),
                    SourceSkillItemId = Int(row, "SkillItemId"),
                    StatusReplacementProperties = ReadReplacementProperties(row,
                        "StatusReplacePropertyValues")
                });
            }
        }

        private static void AddTriggerStatuses(Dictionary<string, object> component, string field,
            List<FightBattleStatusStateTrigger> output)
        {
            if (component == null || output == null || !component.TryGetValue(field, out object raw)
                || raw == null) return;
            foreach (object item in Array(raw, field))
            {
                Dictionary<string, object> row = Object(item, field + " status");
                int id = Int(row, "StatusId");
                if (id <= 0) continue;
                output.Add(new FightBattleStatusStateTrigger
                {
                    EntityId = id,
                    BasePercent = Float(row, "BasePercent", 1f),
                    AffectedByProp = Bool(row, "AffectedByProp"),
                    ApplyTarget = Int(row, "ApplyTarget", 2),
                    PropTarget = ReadTriggerPropTarget(row),
                    StatusReplacementProperties = ReadReplacementProperties(row,
                        "StatusReplacePropertyValues")
                });
            }
        }

        private static FightBattleHitTriggerSkillState ParseHitTriggerSkill(
            Dictionary<string, object> components)
        {
            Dictionary<string, object> component = Component(components,
                "FightHitTriggerSkillComponent");
            if (component == null) return null;
            var result = new FightBattleHitTriggerSkillState
            {
                UseSkillAsNewSkillParent = Bool(component, "UseSkillAsNewSkillParent")
            };
            Dictionary<string, object> resolver = Component(component, "HitTargetPosResolver");
            result.UseTargetPosition = resolver != null
                && StringField(resolver, "_type") != "SelfTargetPosResolver";
            foreach (object raw in FieldArray(component, "SkillList"))
            {
                Dictionary<string, object> skill = Object(raw, "hit trigger skill");
                int id = Int(skill, "SkillId");
                if (id > 0) result.SkillIds.Add(id);
            }
            return result.SkillIds.Count == 0 ? null : result;
        }

        private static void ParseHitExtraComponents(Dictionary<string, object> components,
            FightBattleHitEntity output)
        {
            if (output == null) return;
            output.TriggerSkill = ParseHitTriggerSkill(components);
            Dictionary<string, object> extra = Component(components,
                "FightHitExtApplyDamageComponent");
            if (extra != null)
            {
                output.ExtApplyDamageTargetHpLessThan = Float(extra, "TargetHpLessThenPer", -1f);
                output.ExtApplyDamageScale = 1f + Float(extra, "ChangeScale", 0f);
            }
        }

        private static List<object> FieldArray(Dictionary<string, object> component, string field)
        {
            if (component == null || !component.TryGetValue(field, out object raw) || raw == null)
                return new List<object>();
            return Array(raw, field);
        }
    }
}
