// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
[Serializable] public sealed class EquipResonanceInfo
{
    public int Level, EquipCount, EquipLevel;
    public Dictionary<int,long> Effect;
    public ReadOnlyDictionary<int,long> TotalEffect;
    public EquipLevelCountCondition Condition;
}
public sealed class EquipLevelCountCondition { public int Level, Count; }

}
