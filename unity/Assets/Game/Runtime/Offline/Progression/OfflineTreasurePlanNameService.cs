using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.BattlePlanRules;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineTreasurePlanName
    {
        public int Index;
        public string Name;
        public static bool IsValid(List<OfflineTreasurePlanName> entries) => entries != null && entries.Count <= 32
            && entries.All(row => row != null && row.Index >= 0 && row.Index < 32)
            && entries.Select(row=>row.Index).Distinct().Count()==entries.Count;
    }

    // Names are metadata of the same local plans; slot snapshots retain ownership of equipment/training.
    public sealed class OfflineTreasurePlanNameService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly int maxPlans, characterMaxCount;
        public readonly GAction<BattlePlanType,int,string,string> OnNameChanged = new GAction<BattlePlanType,int,string,string>();
        public OfflineTreasurePlanNameService(OfflineMainlineProgressSession session,int maxPlans,int characterMaxCount)
        {
            this.session=session ?? throw new ArgumentNullException(nameof(session));
            if(maxPlans<1 || maxPlans>32 || characterMaxCount<1) throw new ArgumentOutOfRangeException(nameof(maxPlans));
            this.maxPlans=maxPlans;this.characterMaxCount=characterMaxCount;
        }
        public string GetName(int index)
        {
            session.Reload();
            return new View(session.Save.Player.CharacterProperties.TreasurePlanNames,maxPlans,null).GetPlanName(BattlePlanType.Treasure,index);
        }
        public int CheckName(string name,bool custom) => BattlePlanNames.CheckName(name,custom,characterMaxCount);
        public ProgressionResult ChangeName(int index,string name,bool custom,string receipt)
        {
            if(index<0 || index>=maxPlans || string.IsNullOrWhiteSpace(receipt) || receipt.Length>160)
                return Result(ProgressionResultStatus.InvalidState);
            int code=CheckName(name,custom);
            if(code!=0) return Result(ProgressionResultStatus.NotReady,code.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var notifications=new List<Action>();ProgressionResult result;
            try {
                session.Reload();
                if(session.Save.Player.AppliedInventoryEventIds.Contains(receipt)) return Result(ProgressionResultStatus.Unchanged);
                result=session.ApplyInventoryTransaction(receipt,next=>{
                    var view=new View(next.Player.CharacterProperties.TreasurePlanNames,maxPlans,(type,plan,oldName,newName)=>
                        notifications.Add(()=>OnNameChanged.SafeInvoke(type,plan,oldName,newName)));
                    if(!view.SetPlanName(BattlePlanType.Treasure,index,name)) throw new FormatException("Original plan name target is unavailable.");
                    next.Player.CharacterProperties.TreasurePlanNames=view.Capture();
                });
            }
            catch(IOException error){return Result(ProgressionResultStatus.InvalidState,error.Message);}
            catch(UnauthorizedAccessException error){return Result(ProgressionResultStatus.InvalidState,error.Message);}
            catch(FormatException error){return Result(ProgressionResultStatus.InvalidState,error.Message);}
            if(result.IsApplied) foreach(var notify in notifications) notify();
            return result;
        }
        private static ProgressionResult Result(ProgressionResultStatus status,string message=null)
            =>new ProgressionResult{Status=status,Message=message??string.Empty};
        private sealed class View : IBattlePlanNamePlayer
        {
            private readonly Dictionary<int,BattlePlanNameEntry> entries=new Dictionary<int,BattlePlanNameEntry>();
            private readonly Action<BattlePlanType,int,string,string> changed;
            public View(List<OfflineTreasurePlanName> names,int maximum,Action<BattlePlanType,int,string,string> changed)
            {
                if(!OfflineTreasurePlanName.IsValid(names) || names.Any(row=>row.Index>=maximum)) throw new FormatException("Local treasure plan name metadata is invalid.");
                this.changed=changed;
                for(int i=0;i<maximum;i++) entries.Add(i,new BattlePlanNameEntry());
                foreach(var name in names) entries[name.Index].Name=name.Name;
            }
            public bool TryGetPlanContent(BattlePlanType type,int index,out BattlePlanNameEntry entry)
            { entry=null;return type==BattlePlanType.Treasure && entries.TryGetValue(index,out entry); }
            public void FireOnBattlePlanNameChanged(BattlePlanType type,int index,string oldName,string newName)
                =>changed?.Invoke(type,index,oldName,newName);
            public List<OfflineTreasurePlanName> Capture()=>entries.Select(row=>new OfflineTreasurePlanName{Index=row.Key,Name=row.Value.Name}).ToList();
        }
    }
}
