using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.World;
namespace Zjcs.Official.EntityWorldStages
{
    // Consumed shared-component state/query surface. A formal local entity
    // must bind the original rules; this abstract type implements no rules.
    public abstract class ECWorldStageStateData
    {
        public abstract int State { get; }
        public abstract OfficialStageStateInfo GetCurStateInfo();
        public abstract int GetStateComponentIndex(StageComponentType type, int subStateIndex = 0);
    }
    public interface IECWorldStageReplacementSource
    {
        void AddReplaceReceiver(IMyEntityReplaceEventReceiver receiver);
        void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver receiver);
    }
}
