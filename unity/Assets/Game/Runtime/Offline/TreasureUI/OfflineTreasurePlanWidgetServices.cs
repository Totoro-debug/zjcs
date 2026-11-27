using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;
using Zjcs.Official.ProfessionUI;
using S = Zjcs.Official.TreasureSlots;

namespace Zjcs.Offline.TreasureUI
{
    [Serializable]
    public sealed class OfflineBattlePlanUiCatalog
    {
        [Serializable] public sealed class RegionLimit { public int Region, Count; }
        [Serializable] public sealed class TextEntry { public string Key, Value; }
        public int NameCharacterDefaultMaxCount, SkillPlanMaxCount, EquipPlanMaxCount, TreasurePlanMaxCount;
        public RegionLimit[] NameCharacterMaxCountDict;
        public TextEntry[] Texts;
        private Dictionary<string,string> texts;
        public static OfflineBattlePlanUiCatalog LoadDefault()
        {
            var asset=Resources.Load<TextAsset>("Treasure/NativePlanWidget/battle-plan-ui");
            if(asset==null) throw new InvalidOperationException("Original shared plan UI configuration is unavailable.");
            var result=JsonUtility.FromJson<OfflineBattlePlanUiCatalog>(asset.text);
            if(result.NameCharacterDefaultMaxCount<1 || result.NameCharacterMaxCountDict==null || result.Texts==null
                || result.NameCharacterMaxCountDict.Any(row=>row==null || row.Count<1)
                || result.Texts.Any(row=>row==null || row.Key==null || row.Value==null)) throw new FormatException("Original plan UI configuration is incomplete.");
            result.texts=result.Texts.ToDictionary(row=>row.Key,row=>row.Value);
            return result;
        }
        public int NameMaximum(int region)
            =>NameCharacterMaxCountDict.FirstOrDefault(row=>row.Region==region)?.Count ?? NameCharacterDefaultMaxCount;
        public string Text(string key)=>texts.TryGetValue(key,out string text) ? text : key;
        public int Maximum(BattlePlanType type)
        {
            int count=type==BattlePlanType.Skill ? SkillPlanMaxCount : type==BattlePlanType.Equip ? EquipPlanMaxCount : type==BattlePlanType.Treasure ? TreasurePlanMaxCount : 1;
            return count<=0 ? 1 : count;
        }
    }

    // A window-owned adapter. Function availability, confirmation windows and broadcasts remain explicit enclosing-host dependencies.
    public sealed class OfflineTreasurePlanWidgetServices : IBattlePlanWidgetServices
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflineTreasureSlotService slots;
        private readonly OfflineTreasurePlanNameService names;
        private readonly OfflineBattlePlanUiCatalog catalog;
        private readonly int region;
        private readonly Func<bool> locked,disabled;
        private readonly Action<IWindowOperator,string,Action> confirm;
        private readonly Action<int,object[]> error;
        private readonly Action<string> message;
        private readonly Action<ProgressionResult> failed;
        public OfflineTreasurePlanWidgetServices(OfflineMainlineProgressSession session,OfflineTreasureSlotService slots,
            OfflineTreasurePlanNameService names,OfflineBattlePlanUiCatalog catalog,int region,Func<bool> locked,Func<bool> disabled,
            Action<IWindowOperator,string,Action> confirm,Action<int,object[]> error,Action<string> message,Action<ProgressionResult> failed)
        {
            this.session=session??throw new ArgumentNullException(nameof(session));this.slots=slots??throw new ArgumentNullException(nameof(slots));
            this.names=names??throw new ArgumentNullException(nameof(names));this.catalog=catalog??throw new ArgumentNullException(nameof(catalog));this.region=region;
            this.locked=locked??throw new ArgumentNullException(nameof(locked));this.disabled=disabled??throw new ArgumentNullException(nameof(disabled));
            this.confirm=confirm??throw new ArgumentNullException(nameof(confirm));this.error=error??throw new ArgumentNullException(nameof(error));
            this.message=message??throw new ArgumentNullException(nameof(message));this.failed=failed??throw new ArgumentNullException(nameof(failed));
        }
        public event Action<BattlePlanType,int,string,string> OnBattlePlanNameChanged
        { add { _ = names.OnNameChanged + value; } remove { _ = names.OnNameChanged - value; } }
        private static void RequireTreasure(BattlePlanType type)
        { if(type!=BattlePlanType.Treasure) throw new ArgumentException("Bind this plan type to its own local content owner.",nameof(type)); }
        public int GetActivePlanIndex(BattlePlanType type)
        { RequireTreasure(type);session.Reload();return slots.Snapshot().ActivePlanIndex; }
        public string GetPlanName(BattlePlanType type,int index) { RequireTreasure(type);return names.GetName(index); }
        public string GetBattlePlanName(BattlePlanType type,int index,string customName)
            =>BattlePlanLabels.GetBattlePlanName(type,index,customName,catalog.Text);
        public bool IsBattlePlanContentUsable(BattlePlanType type,int index)
        {
            RequireTreasure(type);session.Reload();var view=slots.Snapshot();
            return view.Plans.TryGetValue(index,out var plan) && S.PlayerTreasurePlanExt.HasTreasurePlanSlot(plan.Data.TreasureSlots);
        }
        public int GetPlanMaxCount(BattlePlanType type)=>catalog.Maximum(type);
        public bool IsPlanLocked()=>locked();
        public bool IsPlanFunctionDisabled(BattlePlanType type) { RequireTreasure(type);return disabled(); }
        public int GetCharacterMaxCount()=>catalog.NameMaximum(region);
        public int CheckSkillPlanNameLegal(string name)=>Zjcs.Official.SkillRules.OfficialSkillPlanName.Check(name,GetCharacterMaxCount());
        public void ChangePlanName(PlayerChangeBattlePlanNameRequest request,Action<bool> complete)
        {
            RequireTreasure(request.PlanType);
            var result=names.ChangeName(request.PlanIndex,request.Name,request.Custom,"treasure-plan-name-"+Guid.NewGuid().ToString("N"));
            bool success=result.IsApplied || result.Status==ProgressionResultStatus.Unchanged;
            if(!success) failed(result);
            complete(success);
        }
        public void Confirm(IWindowOperator owner,string key,Action accepted)=>confirm(owner,key,accepted);
        public void ShowError(int code,params object[] args)=>error(code,args);
        public void SystemMessage(string key,params object[] args)=>message(args.Length==0 ? catalog.Text(key) : string.Format(catalog.Text(key),args));
    }
}
