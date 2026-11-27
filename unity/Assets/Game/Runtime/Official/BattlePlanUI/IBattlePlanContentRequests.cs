using System;
using Zjcs.Official.BattlePlanRules;

namespace Zjcs.Official.BattlePlanUI
{
    public sealed class BattlePlanRequestOutcome
    {
        private readonly Action failure;
        private bool reported;
        public bool Success { get; }
        public BattlePlanRequestOutcome(bool success,Action failure=null)
        {Success=success;this.failure=failure;}
        public void ReportFailure(){if(!Success&&!reported){reported=true;failure?.Invoke();}}
    }
    public interface IBattlePlanContentRequests
    {
        void Apply(BattlePlanType type,int index,Action<BattlePlanRequestOutcome> complete);
        void Clear(BattlePlanType type,int index,Action<BattlePlanRequestOutcome> complete);
    }
}
