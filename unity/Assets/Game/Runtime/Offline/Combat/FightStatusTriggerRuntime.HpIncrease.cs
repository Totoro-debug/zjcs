using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static partial class FightStatusTriggerRuntime
    {
        // Offline rule confirmed by the user. Client classes expose the configuration only.
        // TryAtStart initializes this zero balance; it never synthesizes a healing event.
        public static int ApplyHpIncrease(FightBattleUnitState changed, long actualIncrease,
            FightOfficialCombatCatalog catalog, IReadOnlyList<FightBattleUnitState> units,
            FightRandomStream random = null, FightBattleSkillDefinition sourceSkill = null,
            Action<List<FightBattleStatusSkillInvocation>> executeSkills = null)
        {
            if (changed == null || actualIncrease <= 0 || changed.MaxHp <= 0 || catalog == null) return 0;
            var listeners = new List<KeyValuePair<FightBattleUnitState, FightBattleStatusState>>();
            foreach (var owner in units ?? new[] {changed})
                if (owner?.Statuses != null)
                    for (int index=owner.Statuses.Count-1;index>=0;index--)
                        listeners.Add(new KeyValuePair<FightBattleUnitState, FightBattleStatusState>(owner,owner.Statuses[index]));
            int applied=0;
            foreach (var listener in listeners)
            {
                var owner=listener.Key;var source=listener.Value;
                var component=source?.Components?.HpIncreaseUnit;
                if (component == null || owner.Ended || source.ForbiddenType != 0
                    || !owner.Statuses.Contains(source) || !FightSkillTriggerPolicy.AllowsStatus(sourceSkill,source)
                    || FightStatusLifecycle.ResolveTargetUnitId(source,component.TargetType,units) != changed.Id
                    || changed.Hp <= 0 && !component.TriggerWhenHpLessThan0
                    || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount
                    || component.UnitHpPer <= 0f || float.IsNaN(component.UnitHpPer)
                    || float.IsInfinity(component.UnitHpPer)) continue;
                // Decimal conversion preserves the configured percentage (e.g. 0.15f), avoiding
                // a spurious 151-HP threshold for a 1,000-HP unit. Fractional HP stays in the balance.
                decimal threshold=changed.MaxHp*(decimal)component.UnitHpPer;
                decimal available=(decimal)component.AccumulatedHp+actualIncrease;
                int count=(int)Math.Min(int.MaxValue,decimal.Floor(available/threshold));
                if(component.InvokeMaxCountOnHpChange>=0) count=Math.Min(count,component.InvokeMaxCountOnHpChange);
                if(component.MaxCount>=0) count=Math.Min(count,component.MaxCount-component.TriggeredCount);
                component.AccumulatedHp=(double)available;
                for(int index=0;index<count && owner.Statuses.Contains(source) && source.ForbiddenType==0;index++)
                {
                    component.AccumulatedHp=(double)((decimal)component.AccumulatedHp-threshold);
                    component.TriggeredCount++;
                    foreach(var trigger in component.Statuses)
                        ApplyThresholdStatus(owner,source,trigger,catalog,units,random,changed,true);
                    var skills=new List<FightBattleStatusSkillInvocation>();
                    foreach(var trigger in component.Skills)
                        AppendThresholdSkill(owner,source,trigger,random,skills,null,units,changed,true);
                    if(skills.Count>0) executeSkills?.Invoke(skills);
                    applied++;
                }
            }
            return applied;
        }
    }
}
