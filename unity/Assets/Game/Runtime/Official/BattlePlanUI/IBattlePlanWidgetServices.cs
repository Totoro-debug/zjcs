using System;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattlePlanUI
{
    public interface IBattlePlanWidgetServices
    {
        event Action<BattlePlanType,int,string,string> OnBattlePlanNameChanged;
        int GetActivePlanIndex(BattlePlanType type);
        string GetPlanName(BattlePlanType type,int index);
        string GetBattlePlanName(BattlePlanType type,int index,string customName);
        bool IsBattlePlanContentUsable(BattlePlanType type,int index);
        int GetPlanMaxCount(BattlePlanType type);
        bool IsPlanLocked();
        bool IsPlanFunctionDisabled(BattlePlanType type);
        int CheckSkillPlanNameLegal(string name);
        int GetCharacterMaxCount();
        void ChangePlanName(PlayerChangeBattlePlanNameRequest request,Action<bool> complete);
        void Confirm(IWindowOperator owner,string key,Action accepted);
        void ShowError(int code,params object[] args);
        void SystemMessage(string key,params object[] args);
    }
}
