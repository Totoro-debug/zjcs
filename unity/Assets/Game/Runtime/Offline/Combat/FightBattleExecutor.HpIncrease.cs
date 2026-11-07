using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class HpIncreaseNotice
        {
            public FightBattleUnitState Unit;
            public long Amount;
            public FightBattleSkillDefinition Skill;
            public float Time;
        }
        private readonly Queue<HpIncreaseNotice> hpIncreaseNotices=new Queue<HpIncreaseNotice>();
        private bool applyingHpIncrease;
        private void OnHpIncreased(FightBattleUnitState unit,long amount) => ApplyHpIncreaseTriggers(unit,amount,null);
        private void ApplyHpIncreaseTriggers(FightBattleUnitState unit,long amount,FightBattleSkillDefinition skill)
        {
            if(movementStartContext==null || amount<=0 || statusCatalog==null) return;
            hpIncreaseNotices.Enqueue(new HpIncreaseNotice {Unit=unit,Amount=amount,Skill=skill,
                Time=movementStartContext.StatusStartTime});
            if(applyingHpIncrease) return;
            applyingHpIncrease=true;
            float previousTime=movementStartContext.StatusStartTime;
            try
            {
                // Derived effects may restore HP again. Drain each actual change once, preserving
                // its own per-change quota instead of recursively consuming another event's balance.
                while(hpIncreaseNotices.Count>0)
                {
                    var notice=hpIncreaseNotices.Dequeue();
                    movementStartContext.StatusStartTime=notice.Time;
                    FightStatusTriggerRuntime.ApplyHpIncrease(notice.Unit,notice.Amount,statusCatalog,
                        units,random,notice.Skill,ExecuteHpStartupSkills);
                }
            }
            finally
            {
                movementStartContext.StatusStartTime=previousTime;
                applyingHpIncrease=false;hpIncreaseNotices.Clear();
            }
        }
    }
}
