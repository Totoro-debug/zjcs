using System.Collections.Generic;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionRuleTipData
    {
        string GetTitleKey(string key);
        IReadOnlyList<string> GetTipsKey(string key);
    }

    // Local equivalent of the original LanguageUtils.TextKeyArgWrap value only.
    public readonly struct ProfessionRuleTextKeyArg
    {
        public string Key { get; }
        public object[] Args { get; }
        public ProfessionRuleTextKeyArg(string key, params object[] args)
        {
            Key = key;
            Args = args;
        }
    }
}
