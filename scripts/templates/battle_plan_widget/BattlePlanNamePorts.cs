using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Official.BattlePlanRules
{
    /*ENUM*/
    public static class BattlePlanNameDiagnostics { public static Action<Exception> ReportObserverError; }
    /*SIGNAL*/
    public sealed class PlayerChangeBattlePlanNameRequest
    {
        public BattlePlanType PlanType;
        public int PlanIndex;
        public string Name;
        public bool Custom;
    }
    public sealed class BattlePlanNameEntry { public string Name; }
    public interface IBattlePlanNamePlayer
    {
        bool TryGetPlanContent(BattlePlanType type,int index,out BattlePlanNameEntry entry);
        void FireOnBattlePlanNameChanged(BattlePlanType type,int index,string oldName,string newName);
    }
    public static class BattlePlanNames
    {
        /*METHODS*/
        public static int CheckName(string name,bool custom,int characterMaxCount)
        {
            if(!custom) return string.IsNullOrEmpty(name) ? 0 : 62503;
            return Zjcs.Official.SkillRules.OfficialSkillPlanName.Check(name,characterMaxCount)==0 ? 0 : 62503;
        }
    }
    public static class BattlePlanLabels { /*LABELS*/ }
}
