using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // User-confirmed offline interpretation of the empty MyFightStatusHitApplyDamage handler.
    public static class FightConditionalApplyProperties
    {
        public static void Merge(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightDamageTypeData type, bool cure,
            Dictionary<int,long> properties, IReadOnlyList<FightBattleUnitState> units = null,
            IReadOnlyList<FightBattleGridItemState> gridItems = null)
        {
            if(actor?.Statuses==null || target==null || properties==null
                || !cure && (type.Blinding || type.Dodge || type.Immunity)) return;
            foreach(var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var c=status?.Components?.HitApplyDamage;
                // Confirmed offline rule: modifying this hit creates no child skill/status,
                // so CanTriggerChild and its child-status whitelist do not gate these properties.
                if(c==null || !c.DefinitionResolved || c.HasUnsupportedFilter
                    || c.HasEntityCountScale && c.EntityCountScale?.EntityClassId!=3051
                    || c.ScaleByHpType!=0 && c.ScaleByHpType!=5 || status.ForbiddenType!=0
                    || c.DamageType!=(cure ? 2 : 1)
                    || !Matches(c.TargetSkillEntityClassIds,skill?.EntityId ?? 0)
                    || c.IsCheckTargetShield && !HasShield(target)) continue;
                if(c.SourceSkillTagQuery.Count>0 && (skill?.GameplayTags==null || skill.GameplayTags.Length!=4
                    || c.SourceSkillTagQuery.Exists(clause=>!clause.Matches(skill.GameplayTags)))) continue;
                if(c.CharacterType!=0)
                {
                    var character=target;
                    if(c.ApplyTargetCharacterTypeIfSubstitute && target.IsVoodooDoll)
                    {
                        character=null;
                        if(units!=null) foreach(var unit in units)
                            if(unit.Id==target.VoodooDoll.TargetId) {character=unit;break;}
                    }
                    if(character==null || character.CharacterType!=c.CharacterType) continue;
                }
                var stacks=c.ConsumeStatusId>0 ? actor.Statuses.Find(s=>s!=null
                    && s.ClassId==c.ConsumeStatusId && s.ForbiddenType==0 && s.IsOpenStack) : null;
                if(c.ConsumeStatusId>0 && (stacks==null || stacks.StackCount<c.ConsumeStackMinCount
                    || !Matches(c.ConsumeTriggerSkillIds,skill?.EntityId ?? 0))) continue;
                double scale=1;
                if(c.HasEntityCountScale)
                {
                    scale=FightBattleEntityCountRuntime.Count(c.EntityCountScale,actor,gridItems);
                    if(scale<=0) continue;
                }
                if(c.ScaleByHpType==5)
                {
                    if(c.HpDecreaseUnit<=0f || target.MaxHp<=0) continue;
                    // Decimal conversion keeps the configured float's 15% unit exact at HP boundaries.
                    decimal loss=(target.MaxHp-Math.Min(target.Hp,target.MaxHp))/(decimal)target.MaxHp;
                    scale=(double)decimal.Floor(loss/(decimal)c.HpDecreaseUnit);
                    if(c.MaxHpScale>0f) scale=Math.Min(scale,c.MaxHpScale);
                    if(scale<=0) continue;
                }
                if(stacks!=null)
                {
                    int consumed=Math.Min(stacks.StackCount,Math.Max(0,c.ConsumeStackCount));
                    if(c.ConsumeScaleByStatusTypeCount) scale*=consumed;
                    FightStatusLifecycle.AddStack(stacks,-consumed);
                    if(stacks.StackCount==0) FightStatusLifecycle.End(actor,stacks);
                }
                foreach(var property in status.StatusProperties ?? status.ResolvedProperties ?? new List<FightPropertyValue>())
                {
                    if(property==null || property.Id<=0) continue;
                    int sign=(property.Id==c.AddPercentPropertyId ? 1 : 0)
                        -(property.Id==c.ReducePercentPropertyId ? 1 : 0);
                    if(sign==0) continue;
                    properties.TryGetValue(property.Id,out long previous);
                    properties[property.Id]=previous+(long)(property.Value*scale)*sign;
                }
            }
        }

        private static bool HasShield(FightBattleUnitState target)
        {
            foreach(var status in target.Statuses)
            {
                if(status==null || status.ForbiddenType!=0 || status.ShieldKind==FightBattleShieldKind.None) continue;
                long value=status.ShieldInitialized ? status.ShieldRemainValue : status.ShieldValue;
                int count=status.ShieldInitialized ? status.ShieldRemainCount : status.ShieldInitialCount;
                if(status.ShieldKind==FightBattleShieldKind.Persistent ? value>0
                    : count!=0 && (status.ShieldKind==FightBattleShieldKind.Invincible
                        || status.ShieldIgnoreValue || value>0)) return true;
            }
            return false;
        }
        private static bool Matches(List<int> values,int value) => values==null || values.Count==0 || values.Contains(value);
    }

    public static class FightBattleEntityCountRuntime
    {
        // Confirmed offline binding: count live grid entities around the status holder,
        // independently of the creating unit's faction. Clamp before adding ResultOffset.
        public static int Count(FightBattleEntityCountConfig rule, FightBattleUnitState owner,
            IReadOnlyList<FightBattleGridItemState> items)
        {
            if(rule==null || owner==null) return 0;
            int count=0;
            if(items!=null) foreach(var item in items)
                if(item!=null && !item.Ended && item.EntityClassId==rule.EntityClassId
                    && rule.Range.Contains(new FightGridPoint(item.X-owner.X,item.Y-owner.Y))) count++;
            if(rule.MaxCount>=0) count=Math.Min(count,rule.MaxCount);
            return Math.Max(0,count+rule.ResultOffset);
        }
    }
}
