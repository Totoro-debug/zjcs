using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleBgm
{
    // The original selector consumes these four fields. Audio overrides and
    // presentation paths retain their separate, original resource bindings.
    [Serializable] public sealed class FightThemeInfo
    {
        public int FightThemeId, ConditionId, TriggerPercent;
        public List<int> StageTypes;
    }
}
