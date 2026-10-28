using System.Text.RegularExpressions;

namespace Zjcs.Official.SkillRules
{
    // Common.FormulaHandler.CheckSkillPlanNameLegal and Formula.IllegalCharacterRegex.
    // The caller supplies GameSettings' limit for the selected region.
    public static class OfficialSkillPlanName
    {
        private static readonly Regex IllegalCharacterRegex = new Regex("[#<> \u3000]");

        public static int Check(string name, int characterMaxCount)
        {
            if (string.IsNullOrEmpty(name)) return 1204;
            if (Common.CJK.GetByteCount(name) > characterMaxCount) return 1149;
            if (IllegalCharacterRegex.IsMatch(name)) return 1150;
            return 0;
        }
    }
}
