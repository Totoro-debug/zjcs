// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
public interface INoBlessPet<TPet>
{
    int Level { get; set; }
    TPet CloneWrapForAnother();
}
public interface INoBlessPlayerInput<TPet> where TPet : INoBlessPet<TPet>
{
    Rank MyPlanesRank { get; }
    SubRank MySubRank { get; }
    int MyLevel { get; }
    Dictionary<int,int> MyEquipUpgradeLevel { get; }
    Dictionary<long,TPet> MyPetDatas { get; }
    Dictionary<int,Dictionary<int,int>> MySkillSlotLevelData { get; }
    uint MyActivedEquipResonanceLevel { get; }
}
public interface INoBlessConfiguration
{
    SystemLevelLimitInfoParser LevelLimits { get; }
    EquipResonanceInfoParser Resonances { get; }
    int GetSeasonByRank(Rank rank);
}

}
