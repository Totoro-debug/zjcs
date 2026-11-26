using System;
using Zjcs.Official.BattleSkillPlayables;

namespace Zjcs.Official.BattleStatusPlayables
{
    [Serializable]
    public sealed class StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter
        : FightViewArgsConverter<PlayPlayableWithSourceAndTargetData>
    {
        public override bool TryConvert(FightViewEventArgs args, out PlayPlayableWithSourceAndTargetData data)
        {
            data = default;
            if (!(args is StatusInvokedArgs status) || !status.StatusOwner) return false;
            var id = status.StatusOwner.Id;
            data = new PlayPlayableWithSourceAndTargetData { SourceId = id, TargetId = id };
            return true;
        }
    }
}
