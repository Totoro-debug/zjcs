using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHangUpWindowsUI
{
    public interface IHomeBlessingRuleServices
    {
        IHomeHangUpWindowServices Window { get; }
        IProfessionRuleTipData RuleTips { get; }
        int FurnitureLevelUpLimit(int blessingLevel);
    }
}
