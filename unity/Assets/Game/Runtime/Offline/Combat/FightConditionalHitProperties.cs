using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // User-confirmed offline rule. One instance belongs to one invocation, including child casts.
    public sealed class FightConditionalHitProperties
    {
        private readonly FightBattleUnitState actor;
        private readonly FightBattleSkillDefinition skill;
        private readonly FightOfficialSkillItemCatalog items;
        private readonly IReadOnlyList<FightBattleUnitState> units;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly IReadOnlyList<FightBattleGridItemState> gridItems;
        private readonly HashSet<FightBattleStatusState> invoked = new HashSet<FightBattleStatusState>();
        private readonly HashSet<FightBattleStatusState> seen = new HashSet<FightBattleStatusState>();
        public FightConditionalHitProperties(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            FightOfficialSkillItemCatalog items = null, IReadOnlyList<FightBattleUnitState> units = null,
            FightOfficialCombatCatalog catalog = null, IReadOnlyList<FightBattleGridItemState> gridItems = null)
        { this.actor=actor; this.skill=skill; this.items=items; this.units=units; this.catalog=catalog; this.gridItems=gridItems; }

        public void Merge(FightBattleUnitState target, FightBattleHitDefinition hit,
            FightDamageTypeData type, bool cure, FightRandomStream random, Dictionary<int,long> properties)
        {
            if(actor?.Statuses==null || target==null) return;
            if(skill!=null && skill.GameplayTags==null && catalog?.ContainsEntity(skill.EntityId)==true)
                skill.GameplayTags=catalog.SkillGameplayTags(skill.EntityId);
            FightConditionalApplyProperties.Merge(actor,target,skill,type,cure,properties,units,gridItems);
            foreach(var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var c=status?.Components?.HitDmgAddPer;
                // All 29 records in the pinned official payload use Applicator as hit source.
                // This confirmed property-only correction is independent of CanTriggerChild.
                if(c==null || !c.DefinitionResolved || c.HitSourceType!=2 || c.HasUnsupportedStatusLogicTags
                    || status.ForbiddenType!=0
                    || c.DurationSkillCount==0
                    || c.IsApplyOneHit && seen.Contains(status)) continue;
                if(c.RoundResetCount>0 && (c.LastResetRound<0
                    || actor.SkillState.Round-c.LastResetRound>=c.RoundResetCount))
                { c.LastResetRound=actor.SkillState.Round;c.InvokeCount=0;c.ConditionProgress=0; }
                if(c.MaxInvokeCount>=0 && c.InvokeCount>=c.MaxInvokeCount) continue;
                if(cure ? !c.Cure : !c.Damage) continue;
                if(!cure && (type.Blinding || type.Dodge || type.Immunity)
                    || c.Block && !type.Block || c.Crit && !type.Critical
                    || c.IsTrueDamage && !hit.TrueDamage || !Matches(c.HitSkillElementTypes,skill?.ElementType ?? 0)
                    || !Matches(c.HitSkillEntityClassIds,skill?.EntityId ?? 0) || !SkillSortMatches(c.HitSkillTypes)) continue;
                if(c.TargetActionTypes.Count>0 && !target.Statuses.Exists(s=>s!=null
                    && s.ForbiddenType==0 && c.TargetActionTypes.Contains(s.ActionType))) continue;
                if(c.TargetHpPercent>=0f && Math.Sign((float)(target.Hp/(double)Math.Max(1,target.MaxHp))
                    -c.TargetHpPercent)!=Math.Sign(c.TargetHpComparison)) continue;
                double factor=StatusFactor(c.StatusFilter,status,target);
                if(c.StatusFilter?.IsCondition==true && c.StatusFilter.Target!=0 && factor<=0) continue;
                var stacks=c.ConsumeStatusId>0 ? actor.Statuses.Find(s=>s!=null && s.ClassId==c.ConsumeStatusId
                    && s.ForbiddenType==0 && s.IsOpenStack) : null;
                if(c.ConsumeStatusId>0 && (stacks==null || stacks.StackCount<c.ConsumeStackMinCount
                    || !Matches(c.ConsumeTriggerSkillIds,skill?.EntityId ?? 0))) continue;
                if(c.Rate<=0f || c.Rate<1f && (random==null || random.Next()>=c.Rate)) continue;
                if(++c.ConditionProgress<Math.Max(1,c.ConditionCount)) continue;
                c.ConditionProgress=0;c.InvokeCount++;seen.Add(status);invoked.Add(status);
                double scale=c.ScalePropByStatusTypeCount ? factor : 1.0;
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
                    properties.TryGetValue(property.Id,out long previous);
                    properties[property.Id]=previous+(long)(property.Value*scale);
                }
                if(c.AutoRemove && c.MaxInvokeCount>=0 && c.InvokeCount>=c.MaxInvokeCount)
                    FightStatusLifecycle.End(actor,status);
            }
        }

        public void EndCast()
        {
            foreach(var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var c=status?.Components?.HitDmgAddPer;
                if(c==null || !c.DefinitionResolved || c.HitSourceType!=2 || status.ForbiddenType!=0
                    || c.DurationSkillCount<0 || !Matches(c.OnlySkillActions,skill?.ActionType ?? 0)
                    || skill==null || skill.SkillType!=c.OnlySkillType
                    || c.UpdateDurationSkillCountIfTrigger && !invoked.Contains(status)) continue;
                if(c.DurationSkillCount>0) c.DurationSkillCount--;
                if(c.DurationSkillCount==0) FightStatusLifecycle.End(actor,status);
            }
        }

        private bool SkillSortMatches(List<int> filter)
        {
            if(filter==null || filter.Count==0) return true;
            FightOfficialSkillItem item;
            int id=skill?.UseItemId>0 ? skill.UseItemId : skill?.ItemId ?? 0;
            if(items==null || !items.TryGet(id,out item)) return false;
            foreach(int sort in item.SkillSortTypes) if(filter.Contains(sort)) return true;
            return false;
        }
        private double StatusFactor(FightBattleStatusTypeCountFilter filter,FightBattleStatusState source,
            FightBattleUnitState target)
        {
            if(filter==null || filter.Target==0) return 1;
            var unit=filter.Target==1 || filter.Target==2 ? actor
                : filter.Target==3 ? source.CreatorId==actor.Id ? actor : source.CreatorId==target.Id ? target : null
                : filter.Target==5 ? target : null;
            if(unit==null) return 0;
            int count=0;var ids=new HashSet<int>();
            foreach(var status in unit.Statuses)
            {
                if(status==null || status.ForbiddenType!=0 || !Matches(filter.AnyStatusTypes,status.StatusType)
                    || !Matches(filter.AnyActionTypes,status.ActionType)) continue;
                if(filter.CountType==2) {ids.Add(status.ClassId);count=ids.Count;}
                else if(filter.CountType==1) count+=filter.IsCalculateStack ? Math.Max(0,status.StackCount) : 1;
            }
            if(filter.MaxStatusCount>=0) count=Math.Min(count,filter.MaxStatusCount);
            if(filter.IsOpenZeroCheckWhenCalcStatus && count==0) return 0;
            return (filter.IsAdditionCount ? 1 : 0)+count*(double)filter.Unit;
        }
        private static bool Matches(List<int> values,int value) => values==null || values.Count==0 || values.Contains(value);
    }
}
