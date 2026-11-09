using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Official.BattleSkillPlayables
{
    // These original client roles have no methods or consumed shared fields.
    public interface IFightSkillPassiveViewData { }

    public interface IFightPlayableOverrideStatusServices
    {
        // Original source -> MyFightStatusAgent -> ApplyStatusDict query.
        // Return null when that source has no status agent.
        FightHitSourceStatusAgent GetSourceStatusAgent(MyEntity source);
    }
}
