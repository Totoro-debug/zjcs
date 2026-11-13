using System;
using System.Collections.Generic;

namespace Zjcs.Official.BattleBgm
{
    // Original selection fields and the NPC path consumed by its HUD module.
    [Serializable] public sealed class FightThemeInfo
    {
        public int FightThemeId, ConditionId, TriggerPercent;
        public List<int> StageTypes;
        public string UIPrefabPath;
    }
}
