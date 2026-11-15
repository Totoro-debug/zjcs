// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
[Serializable] public sealed class SystemLevelLimitInfo
{
    public SubRank SubRank;
    public int PlayerLevel;
    public int PlayerBlessLevel;
    public int SkillLevel;
    public int SkillBlessLevel;
    public int EquipLevel;
    public int EquipBlessLevel;
    public int PetLevel;
    public int PetBlessLevel;
}

}
